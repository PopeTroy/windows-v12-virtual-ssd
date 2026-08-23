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
#endif

// Fixed-Point Q16.16 Conversions
#define FLOAT_TO_Q16(x) (static_cast<int32_t>((x) * 65536.0f))
#define Q16_TO_FLOAT(x) (static_cast<float>(x) / 65536.0f)
#define MULT_Q16(a, b)  (static_cast<int32_t>((static_cast<int64_t>(a) * (b)) >> 16))

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

int main() {
    SharedMemoryManager shm;
    PidSharedMemory* ptr = shm.get();
    if (!ptr) return -1;

    FixedPointPID pid;
    int32_t setpoint = FLOAT_TO_Q16(250.0f);
    int32_t process_var = FLOAT_TO_Q16(20.0f);
    int32_t dt = FLOAT_TO_Q16(0.01f); // 10ms loop time

    std::cout << "[C++ CORE] Real-Time Control Loop Active with Lock-Free Telemetry Ring Buffer..." << std::endl;

    for (int step = 0; step < 1000; ++step) {
        // Read dynamic gains updated by Python ONNX/TensorRT process
        int32_t Kp = ptr->Kp_q16.load(std::memory_order_relaxed);
        int32_t Ki = ptr->Ki_q16.load(std::memory_order_relaxed);
        int32_t Kd = ptr->Kd_q16.load(std::memory_order_relaxed);

        int32_t error = setpoint - process_var;
        int32_t output = pid.compute(Kp, Ki, Kd, setpoint, process_var, dt);

        uint64_t timestamp = std::chrono::duration_cast<std::chrono::microseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()).count();

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
