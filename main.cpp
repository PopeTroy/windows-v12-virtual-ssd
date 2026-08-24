#include <iostream>
#include <cstdint>
#include <chrono>
#include <thread>
#include <cstring>
#include "SharedData.h"

#if defined(_WIN32) || defined(_WIN64)
    #include <windows.h>
#else
    #include <sys/mman.h>
    #include <sys/stat.h>
    #include <fcntl.h>
    #include <unistd.h>
    #include <sched.h>
#endif

// Real-Time System Scheduler & Memory Locking Configuration
void configure_realtime_scheduler() {
#if defined(__linux__)
    // 1. Lock process memory pages into physical RAM to eliminate OS swap page-fault latency
    if (mlockall(MCL_CURRENT | MCL_FUTURE) != 0) {
        std::cerr << "[WARNING] Failed to lock memory in RAM. Run executable with sudo privileges!" << std::endl;
    }

    // 2. Configure POSIX First-In, First-Out (SCHED_FIFO) real-time policy
    struct sched_param param;
    param.sched_priority = 80; // High real-time priority execution (Range: 1-99)

    if (sched_setscheduler(0, SCHED_FIFO, &param) != 0) {
        std::cerr << "[WARNING] Failed to set SCHED_FIFO real-time priority. Running on default OS scheduler." << std::endl;
    } else {
        std::cout << "[REAL-TIME] Process successfully assigned SCHED_FIFO priority 80." << std::endl;
    }
#endif
}

// Fixed-Point Q16.16 Conversions
#define FLOAT_TO_Q16(x) (static_cast<int32_t>((x) * 65536.0f))
#define Q16_TO_FLOAT(x) (static_cast<float>(x) / 65536.0f)
#define MULT_Q16(a, b)  (static_cast<int32_t>((static_cast<int64_t>(a) * (b)) >> 16))

// Safety Threshold: Fall back if Python doesn't update within 50ms (50,000 us)
constexpr uint64_t SAFETY_TIMEOUT_US = 50000;

// Cross-Platform Shared Memory Manager
class SharedMemoryManager {
private:
    const char* shm_name = "/pid_onnx_shm";
    PidSharedMemory* shared_data = nullptr;

#if defined(_WIN32) || defined(_WIN64)
    HANDLE hMapFile = NULL;
#else
    int shm_fd = -1;
#endif

public:
    SharedMemoryManager() {
#if defined(_WIN32) || defined(_WIN64)
        hMapFile = CreateFileMappingA(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, sizeof(PidSharedMemory), "Global\\pid_onnx_shm");
        if (hMapFile == NULL) {
            std::cerr << "Windows Shared Memory Creation Failed!" << std::endl;
            return;
        }
        shared_data = (PidSharedMemory*)MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(PidSharedMemory));
#else
        shm_fd = shm_open(shm_name, O_CREAT | O_RDWR, 0666);
        ftruncate(shm_fd, sizeof(PidSharedMemory));
        shared_data = (PidSharedMemory*)mmap(0, sizeof(PidSharedMemory), PROT_READ | PROT_WRITE, MAP_SHARED, shm_fd, 0);
#endif
        if (shared_data) {
            std::memset(shared_data, 0, sizeof(PidSharedMemory));
            // Initialize dynamic default conservative gains (Kp=1.5, Ki=0.1, Kd=0.05)
            shared_data->Kp_q16.store(FLOAT_TO_Q16(1.5f), std::memory_order_relaxed);
            shared_data->Ki_q16.store(FLOAT_TO_Q16(0.1f), std::memory_order_relaxed);
            shared_data->Kd_q16.store(FLOAT_TO_Q16(0.05f), std::memory_order_relaxed);

            shared_data->ring_head.store(0, std::memory_order_relaxed);
            shared_data->ring_tail.store(0, std::memory_order_relaxed);
            shared_data->last_python_update_us.store(0, std::memory_order_relaxed);
        }
    }

    ~SharedMemoryManager() {
#if defined(_WIN32) || defined(_WIN64)
        if (shared_data) UnmapViewOfFile(shared_data);
        if (hMapFile) CloseHandle(hMapFile);
#else
        if (shared_data) munmap(shared_data, sizeof(PidSharedMemory));
        if (shm_fd != -1) {
            close(shm_fd);
            shm_unlink(shm_name);
        }
#endif
    }

    PidSharedMemory* get() { return shared_data; }
};

class FixedPointPID {
private:
    int32_t integral = 0;
    int32_t prev_error = 0;
    int32_t out_min = FLOAT_TO_Q16(0.0f);
    int32_t out_max = FLOAT_TO_Q16(100.0f);

public:
    int32_t compute(int32_t Kp, int32_t Ki, int32_t Kd, int32_t setpoint, int32_t process_variable, int32_t dt_q16) {
        int32_t error = setpoint - process_variable;

        // Proportional
        int32_t p_term = MULT_Q16(Kp, error);

        // Integral with anti-windup clamping
        int32_t error_dt = MULT_Q16(error, dt_q16);
        integral += error_dt;
        int32_t i_term = MULT_Q16(Ki, integral);
        if (i_term > out_max || i_term < out_min) {
            integral -= error_dt; // Clamp integration
        }

        // Derivative
        int32_t d_error = error - prev_error;
        int32_t rate = (dt_q16 > 0) ? (static_cast<int64_t>(d_error) << 16) / dt_q16 : 0;
        int32_t d_term = MULT_Q16(Kd, rate);

        int32_t output = p_term + i_term + d_term;
        if (output > out_max) output = out_max;
        if (output < out_min) output = out_min;

        prev_error = error;
        return output;
    }
};

// Lock-Free Telemetry Push Handler
inline bool push_telemetry(PidSharedMemory* shm, int32_t sp, int32_t pv, int32_t err, int32_t out, uint64_t ts) {
    uint32_t head = shm->ring_head.load(std::memory_order_relaxed);
    uint32_t tail = shm->ring_tail.load(std::memory_order_acquire);

    // Buffer overflow check
    if ((head - tail) >= TELEMETRY_RING_CAPACITY) {
        return false; // Drop sample to prevent blocking real-time control
    }

    uint32_t index = head & (TELEMETRY_RING_CAPACITY - 1);
    
    // Direct write to shared ring buffer
    shm->ring_buffer[index] = {sp, pv, err, out, ts};

    // Update head pointer atomically
    shm->ring_head.store(head + 1, std::memory_order_release);
    return true;
}

#ifndef _WINDLL
int main() {
    // Elevate process priority to real-time execution prior to initializing control logic
    configure_realtime_scheduler();

    SharedMemoryManager shm;
    PidSharedMemory* ptr = shm.get();
    if (!ptr) return -1;

    FixedPointPID pid;
    int32_t setpoint = FLOAT_TO_Q16(250.0f);
    int32_t process_var = FLOAT_TO_Q16(20.0f);
    int32_t dt = FLOAT_TO_Q16(0.01f); // 10ms loop time

    // Fallback Conservative Gains
    const int32_t SAFE_KP = FLOAT_TO_Q16(1.5f);
    const int32_t SAFE_KI = FLOAT_TO_Q16(0.1f);
    const int32_t SAFE_KD = FLOAT_TO_Q16(0.05f);

    std::cout << "[C++ CORE] Real-Time Control Loop Active with Lock-Free Telemetry & Safety Fallback..." << std::endl;

    for (int step = 0; step < 1000; ++step) {
        uint64_t timestamp = std::chrono::duration_cast<std::chrono::microseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();

        // Check Python heartbeat timestamp
        uint64_t last_python_ts = ptr->last_python_update_us.load(std::memory_order_relaxed);

        int32_t Kp, Ki, Kd;

        if ((timestamp - last_python_ts) > SAFETY_TIMEOUT_US) {
            // Safety Timeout Triggered: Revert to conservative gains
            Kp = SAFE_KP;
            Ki = SAFE_KI;
            Kd = SAFE_KD;
            
            if (step % 50 == 0) {
                std::cout << "[C++ SAFETY] Python process heartbeat timeout! Using conservative fallback gains." << std::endl;
            }
        } else {
            // Read dynamic gains updated by Python ONNX/TensorRT process
            Kp = ptr->Kp_q16.load(std::memory_order_relaxed);
            Ki = ptr->Ki_q16.load(std::memory_order_relaxed);
            Kd = ptr->Kd_q16.load(std::memory_order_relaxed);
        }

        int32_t error = setpoint - process_var;
        int32_t output = pid.compute(Kp, Ki, Kd, setpoint, process_var, dt);

        // 1. Update immediate state registers
        ptr->setpoint_q16.store(setpoint, std::memory_order_relaxed);
        ptr->process_var_q16.store(process_var, std::memory_order_relaxed);
        ptr->error_q16.store(error, std::memory_order_relaxed);
        ptr->output_q16.store(output, std::memory_order_relaxed);
        ptr->timestamp_us.store(timestamp, std::memory_order_relaxed);

        // 2. Push zero-copy telemetry into SPSC Ring Buffer for DDPG replay buffer logging
        push_telemetry(ptr, setpoint, process_var, error, output, timestamp);

        // Simulate thermal physical response
        process_var += MULT_Q16(output, FLOAT_TO_Q16(0.05f));

        if (step % 50 == 0) {
            std::cout << "[C++ STEP " << step << "] PV: " << Q16_TO_FLOAT(process_var)
                      << " | Gains (Kp, Ki, Kd): (" << Q16_TO_FLOAT(Kp) << ", " 
                      << Q16_TO_FLOAT(Ki) << ", " << Q16_TO_FLOAT(Kd) << ")" << std::endl;
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }

    return 0;
}
#endif
