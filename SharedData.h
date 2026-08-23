#ifndef SHARED_DATA_H
#define SHARED_DATA_H

#include <cstdint>

#pragma pack(push, 1)
struct PidSharedMemory {
    // Machine Learning Inputs -> C++ PID Engine
    int32_t Kp_q16;         // Fixed-point Q16.16
    int32_t Ki_q16;         // Fixed-point Q16.16
    int32_t Kd_q16;         // Fixed-point Q16.16
    
    // C++ PID Engine Outputs -> Python ML Engine
    int32_t setpoint_q16;   // Current target value
    int32_t process_var_q16;// Measured sensor reading
    int32_t error_q16;      // Instantaneous error e(t)
    int32_t output_q16;     // Computed control output u(t)
    uint64_t timestamp_us;  // Microsecond tick
};
#pragma pack(pop)

#endif // SHARED_DATA_H
