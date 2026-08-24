#ifndef SHARED_DATA_H
#define SHARED_DATA_H

#include <cstdint>
#include <atomic>

// ============================================================================
// ARCHITECTURAL DESIGN & COMMUNICATION STRATEGY
// ============================================================================
//
// 1. CONTROL PARADIGMS:
//    - Model Predictive Control (MPC) Lite: For systems with predictable dynamics,
//      an ML model can be trained to predict future system states. This prediction
//      can then be used to optimize control actions, acting as a form of advanced MPC.
//    - Reinforcement Learning (RL): An RL agent could be trained to directly 
//      optimize PID parameters or even learn a control policy that surpasses 
//      traditional PID. The RL agent's policy (once trained) would be converted 
//      to an ONNX model for inference.
//
// 2. COMMUNICATION STRATEGY:
//    - Real-time C++ <-> Host Python: For systems where the ML model is run on a 
//      separate host (e.g., a Linux board running alongside a microcontroller), 
//      communication can be via:
//        * Network Sockets (TCP/UDP): For higher bandwidth, but introduces latency.
//        * Message Queues (e.g., ZeroMQ): Efficient for distributed systems.
//        * Shared Memory: Fastest option if both processes are on the same machine.
//    - Edge ML (ONNX Runtime on Embedded): If the target microcontroller has 
//      sufficient resources or an ML accelerator, ONNX Runtime can be compiled 
//      and run directly on the embedded device, eliminating the need for external 
//      communication for inference. The ML model would be loaded into the C++ application.
// ============================================================================

// Ring buffer capacity (Must be a power of 2 for fast bitwise masking)
constexpr size_t TELEMETRY_RING_CAPACITY = 1024;

#pragma pack(push, 1)
// Single telemetry frame captured on every PID control step
struct TelemetrySample {
    int32_t setpoint_q16;
    int32_t process_var_q16;
    int32_t error_q16;
    int32_t output_q16;
    uint64_t timestamp_us;
};

struct PidSharedMemory {
    // Dynamic PID Gains (Written atomically by Python DDPG / ONNX Engine)
    std::atomic<int32_t> Kp_q16;
    std::atomic<int32_t> Ki_q16;
    std::atomic<int32_t> Kd_q16;

    // Latest state telemetry (Direct low-latency reads)
    std::atomic<int32_t> setpoint_q16;
    std::atomic<int32_t> process_var_q16;
    std::atomic<int32_t> error_q16;
    std::atomic<int32_t> output_q16;
    std::atomic<uint64_t> timestamp_us;

    // Heartbeat timestamp (Written by Python on every update for C++ Safety Timeout)
    std::atomic<uint64_t> last_python_update_us;

    // Lock-Free Single-Producer Single-Consumer (SPSC) Ring Buffer Indices
    std::atomic<uint32_t> ring_head{0}; // Incremented by C++ Control Loop (Producer)
    std::atomic<uint32_t> ring_tail{0}; // Incremented by Python RL Loop (Consumer)

    // Zero-Copy Telemetry Buffer
    TelemetrySample ring_buffer[TELEMETRY_RING_CAPACITY];
};
#pragma pack(pop)

#endif // SHARED_DATA_H
