#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <thread>
#include "SharedData.h"

#if defined(_WIN32) || defined(_WIN64)
    #ifndef NOMINMAX
    #define NOMINMAX
    #endif
    #include <windows.h>
#else
    #include <fcntl.h>
    #include <pthread.h>
    #include <sched.h>
    #include <sys/mman.h>
    #include <sys/stat.h>
    #include <time.h>
    #include <unistd.h>
#endif

// Fixed-Point Q16.16 Conversions
constexpr int32_t Q16_ONE_VAL = 65536;
constexpr int32_t Q16_MAX_VAL = INT32_MAX;
constexpr int32_t Q16_MIN_VAL = INT32_MIN;

inline int32_t SAT_Q16(int64_t val) noexcept {
    if (val > static_cast<int64_t>(Q16_MAX_VAL)) return Q16_MAX_VAL;
    if (val < static_cast<int64_t>(Q16_MIN_VAL)) return Q16_MIN_VAL;
    return static_cast<int32_t>(val);
}

inline int32_t FLOAT_TO_Q16(float x) noexcept {
    const double scaled = static_cast<double>(x) * 65536.0;
    if (scaled >= static_cast<double>(Q16_MAX_VAL)) return Q16_MAX_VAL;
    if (scaled <= static_cast<double>(Q16_MIN_VAL)) return Q16_MIN_VAL;
    return static_cast<int32_t>(scaled);
}

inline float Q16_TO_FLOAT(int32_t x) noexcept {
    return static_cast<float>(x) / 65536.0f;
}

inline int32_t MULT_Q16(int32_t a, int32_t b) noexcept {
    return SAT_Q16((static_cast<int64_t>(a) * static_cast<int64_t>(b)) >> 16);
}

// Safety Threshold: Fall back if Python doesn't update within 50ms (50,000 us)
constexpr uint64_t SAFETY_TIMEOUT_US = 50000ULL;

// Real-Time System Scheduler & Memory Locking Configuration
void configure_realtime_scheduler() {
#if defined(_WIN32) || defined(_WIN64)
    HeapSetInformation(nullptr, HeapEnableTerminationOnCorruption, nullptr, 0);
    if (!SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS)) {
        std::cerr << "[WARNING] Could not elevate Windows process priority class." << std::endl;
    }
#elif defined(__linux__)
    if (mlockall(MCL_CURRENT | MCL_FUTURE) != 0) {
        std::cerr << "[WARNING] Failed to lock memory in RAM. Run executable with sudo privileges!" << std::endl;
    }

    struct sched_param param {};
    param.sched_priority = 80; // Range: 1-99

    if (sched_setscheduler(0, SCHED_FIFO, &param) != 0) {
        std::cerr << "[WARNING] Failed to set SCHED_FIFO real-time priority. Running on default OS scheduler." << std::endl;
    } else {
        std::cout << "[REAL-TIME] Process successfully assigned SCHED_FIFO priority 80." << std::endl;
    }
#endif
}

// Cross-Platform Shared Memory Manager
class SharedMemoryManager {
private:
    const char* shm_name = "/pid_onnx_shm";
    PidSharedMemory* shared_data = nullptr;

#if defined(_WIN32) || defined(_WIN64)
    HANDLE hMapFile = nullptr;
#else
    int shm_fd = -1;
#endif

public:
    SharedMemoryManager() {
#if defined(_WIN32) || defined(_WIN64)
        hMapFile = CreateFileMappingA(
            INVALID_HANDLE_VALUE,
            nullptr,
            PAGE_READWRITE,
            0,
            static_cast<DWORD>(sizeof(PidSharedMemory)),
            "Global\\pid_onnx_shm");

        if (hMapFile == nullptr) {
            std::cerr << "Windows Shared Memory Creation Failed!" << std::endl;
            return;
        }

        shared_data = static_cast<PidSharedMemory*>(
            MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(PidSharedMemory)));
#else
        shm_fd = shm_open(shm_name, O_CREAT | O_RDWR, 0666);
        if (shm_fd != -1) {
            if (ftruncate(shm_fd, static_cast<off_t>(sizeof(PidSharedMemory))) == 0) {
                void* mapped = mmap(
                    nullptr,
                    sizeof(PidSharedMemory),
                    PROT_READ | PROT_WRITE,
                    MAP_SHARED,
                    shm_fd,
                    0);
                if (mapped != MAP_FAILED) {
                    shared_data = static_cast<PidSharedMemory*>(mapped);
                }
            }
        }
#endif
        if (shared_data) {
            new (shared_data) PidSharedMemory();
            shared_data->publish_gains(
                FLOAT_TO_Q16(1.5f),
                FLOAT_TO_Q16(0.1f),
                FLOAT_TO_Q16(0.05f),
                0ULL);
        }
    }

    ~SharedMemoryManager() {
#if defined(_WIN32) || defined(_WIN64)
        if (shared_data) UnmapViewOfFile(shared_data);
        if (hMapFile) CloseHandle(hMapFile);
#else
        if (shared_data && shared_data != MAP_FAILED) {
            munmap(shared_data, sizeof(PidSharedMemory));
        }
        if (shm_fd != -1) {
            close(shm_fd);
            shm_unlink(shm_name);
        }
#endif
    }

    PidSharedMemory* get() noexcept { return shared_data; }
};

class FixedPointPID {
private:
    int32_t integral = 0;
    int32_t prev_error = 0;
    int32_t filtered_deriv = 0;
    int32_t deriv_alpha_q16 = FLOAT_TO_Q16(0.25f);
    int32_t back_calc_gain_q16 = FLOAT_TO_Q16(0.50f);
    int32_t out_min = FLOAT_TO_Q16(0.0f);
    int32_t out_max = FLOAT_TO_Q16(100.0f);

public:
    int32_t compute(
        int32_t Kp,
        int32_t Ki,
        int32_t Kd,
        int32_t setpoint,
        int32_t process_variable,
        int32_t dt_q16) noexcept {
        const int32_t error = SAT_Q16(static_cast<int64_t>(setpoint) - static_cast<int64_t>(process_variable));

        // Proportional
        const int32_t p_term = MULT_Q16(Kp, error);

        // Integral accumulation
        const int32_t error_dt = MULT_Q16(error, dt_q16);
        integral = SAT_Q16(static_cast<int64_t>(integral) + static_cast<int64_t>(error_dt));
        const int32_t i_term = MULT_Q16(Ki, integral);

        // Filtered Derivative (low-pass smoothing to curb quantization noise amplification)
        const int32_t d_error = SAT_Q16(static_cast<int64_t>(error) - static_cast<int64_t>(prev_error));
        const int32_t raw_rate = (dt_q16 > 0)
            ? SAT_Q16((static_cast<int64_t>(d_error) << 16) / dt_q16)
            : 0;
        const int32_t raw_d_term = MULT_Q16(Kd, raw_rate);
        const int32_t d_diff = SAT_Q16(static_cast<int64_t>(raw_d_term) - static_cast<int64_t>(filtered_deriv));
        filtered_deriv = SAT_Q16(static_cast<int64_t>(filtered_deriv) + static_cast<int64_t>(MULT_Q16(deriv_alpha_q16, d_diff)));

        // Output clamping + back-calculation anti-windup
        const int32_t unsaturated = SAT_Q16(
            static_cast<int64_t>(p_term) + static_cast<int64_t>(i_term) + static_cast<int64_t>(filtered_deriv));
        const int32_t clamped = std::clamp(unsaturated, out_min, out_max);
        const int32_t sat_delta = SAT_Q16(static_cast<int64_t>(clamped) - static_cast<int64_t>(unsaturated));
        if (sat_delta != 0) {
            integral = SAT_Q16(static_cast<int64_t>(integral) + static_cast<int64_t>(MULT_Q16(back_calc_gain_q16, sat_delta)));
        }

        prev_error = error;
        return clamped;
    }
};

// Lock-Free Telemetry Push Handler
inline bool push_telemetry(
    PidSharedMemory* shm,
    int32_t sp,
    int32_t pv,
    int32_t err,
    int32_t out,
    uint64_t ts) noexcept {
    if (!shm) return false;
    TelemetrySample sample{sp, pv, err, out, ts};
    return shm->try_push_telemetry(sample);
}

#ifndef _WINDLL
int main() {
    configure_realtime_scheduler();

    SharedMemoryManager shm;
    PidSharedMemory* ptr = shm.get();
    if (!ptr) return -1;

    FixedPointPID pid;
    const int32_t setpoint = FLOAT_TO_Q16(250.0f);
    int32_t process_var = FLOAT_TO_Q16(20.0f);
    const int32_t dt = FLOAT_TO_Q16(0.01f); // 10ms loop time

    const int32_t SAFE_KP = FLOAT_TO_Q16(1.5f);
    const int32_t SAFE_KI = FLOAT_TO_Q16(0.1f);
    const int32_t SAFE_KD = FLOAT_TO_Q16(0.05f);

    std::cout << "[C++ CORE] Real-Time Control Loop Active with Lock-Free Telemetry & Safety Fallback..." << std::endl;

    auto next_tick = std::chrono::steady_clock::now();

    for (int step = 0; step < 1000; ++step) {
        next_tick += std::chrono::milliseconds(10);

        const uint64_t timestamp = static_cast<uint64_t>(
            std::chrono::duration_cast<std::chrono::microseconds>(
                std::chrono::steady_clock::now().time_since_epoch()).count());

        int32_t Kp = SAFE_KP;
        int32_t Ki = SAFE_KI;
        int32_t Kd = SAFE_KD;

        const bool fresh_gains = ptr->try_read_fresh_gains(timestamp, SAFETY_TIMEOUT_US, Kp, Ki, Kd);
        if (!fresh_gains && (step % 50 == 0)) {
            std::cout << "[C++ SAFETY] Python process heartbeat timeout! Using conservative fallback gains." << std::endl;
        }

        const int32_t error = SAT_Q16(static_cast<int64_t>(setpoint) - static_cast<int64_t>(process_var));
        const int32_t output = pid.compute(Kp, Ki, Kd, setpoint, process_var, dt);

        ptr->setpoint_q16.store(setpoint, std::memory_order_relaxed);
        ptr->process_var_q16.store(process_var, std::memory_order_relaxed);
        ptr->error_q16.store(error, std::memory_order_relaxed);
        ptr->output_q16.store(output, std::memory_order_relaxed);
        ptr->timestamp_us.store(timestamp, std::memory_order_release);

        push_telemetry(ptr, setpoint, process_var, error, output, timestamp);

        process_var = SAT_Q16(static_cast<int64_t>(process_var) + static_cast<int64_t>(MULT_Q16(output, FLOAT_TO_Q16(0.05f))));

        if (step % 50 == 0) {
            std::cout << "[C++ STEP " << step << "] PV: " << Q16_TO_FLOAT(process_var)
                      << " | Gains (Kp, Ki, Kd): (" << Q16_TO_FLOAT(Kp) << ", "
                      << Q16_TO_FLOAT(Ki) << ", " << Q16_TO_FLOAT(Kd) << ")" << std::endl;
        }

        std::this_thread::sleep_until(next_tick);
    }

    return 0;
}
#endif
