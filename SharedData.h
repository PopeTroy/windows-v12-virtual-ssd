#ifndef SHARED_DATA_H
#define SHARED_DATA_H

#include <atomic>
#include <cstddef>
#include <cstdint>

// ============================================================================
// ARCHITECTURAL DESIGN & COMMUNICATION STRATEGY
// ============================================================================
//
// 1. CONTROL PARADIGMS:
//    - Model Predictive Control (MPC) Lite: Predicts future system states to
//      guide control actions over short horizons.
//    - Reinforcement Learning (RL) / DDPG: Dynamically tunes PID gains or
//      policies via an external Python / ONNX inference loop.
//
// 2. COMMUNICATION STRATEGY:
//    - Shared Memory IPC: Zero-copy cross-process memory with cache-line
//      aligned atomics and acquire/release ordering for low-latency telemetry
//      and gain updates.
// ============================================================================

constexpr size_t SHARED_CACHE_LINE_SIZE = 64;
constexpr size_t TELEMETRY_RING_CAPACITY = 1024; // Must be a power of 2
static_assert((TELEMETRY_RING_CAPACITY & (TELEMETRY_RING_CAPACITY - 1)) == 0,
              "TELEMETRY_RING_CAPACITY must be a power of 2.");

struct TelemetrySample {
    int32_t setpoint_q16;
    int32_t process_var_q16;
    int32_t error_q16;
    int32_t output_q16;
    uint64_t timestamp_us;
};

struct alignas(SHARED_CACHE_LINE_SIZE) PidSharedMemory {
    // Dynamic PID Gains (Written atomically by Python DDPG / ONNX Engine)
    alignas(SHARED_CACHE_LINE_SIZE) std::atomic<int32_t> Kp_q16;
    std::atomic<int32_t> Ki_q16;
    std::atomic<int32_t> Kd_q16;

    // Latest state telemetry (Direct low-latency reads)
    alignas(SHARED_CACHE_LINE_SIZE) std::atomic<int32_t> setpoint_q16;
    std::atomic<int32_t> process_var_q16;
    std::atomic<int32_t> error_q16;
    std::atomic<int32_t> output_q16;
    std::atomic<uint64_t> timestamp_us;

    // Heartbeat timestamp (Written by Python on every update for C++ Safety Timeout)
    alignas(SHARED_CACHE_LINE_SIZE) std::atomic<uint64_t> last_python_update_us;

    // Lock-Free SPSC Ring Buffer Indices (Monotonic 64-bit counters; masked on access)
    alignas(SHARED_CACHE_LINE_SIZE) std::atomic<uint64_t> ring_head; // Producer (C++ Control Loop)
    alignas(SHARED_CACHE_LINE_SIZE) std::atomic<uint64_t> ring_tail; // Consumer (Python RL Loop)

    // Zero-Copy Telemetry Buffer
    alignas(SHARED_CACHE_LINE_SIZE) TelemetrySample ring_buffer[TELEMETRY_RING_CAPACITY];

    PidSharedMemory() noexcept
        : Kp_q16(0),
          Ki_q16(0),
          Kd_q16(0),
          setpoint_q16(0),
          process_var_q16(0),
          error_q16(0),
          output_q16(0),
          timestamp_us(0),
          last_python_update_us(0),
          ring_head(0),
          ring_tail(0),
          ring_buffer{} {}

    // Producer (C++): Pushes a telemetry sample if capacity is available.
    // Returns false when the ring buffer is full.
    bool try_push_telemetry(const TelemetrySample& sample) noexcept {
        const uint64_t head = ring_head.load(std::memory_order_relaxed);
        const uint64_t tail = ring_tail.load(std::memory_order_acquire);

        if ((head - tail) >= static_cast<uint64_t>(TELEMETRY_RING_CAPACITY)) {
            return false;
        }

        const size_t idx = static_cast<size_t>(head & static_cast<uint64_t>(TELEMETRY_RING_CAPACITY - 1));
        ring_buffer[idx] = sample;
        ring_head.store(head + 1ULL, std::memory_order_release);
        return true;
    }

    // Consumer (Python / Reader): Pops the oldest unread telemetry sample.
    // Returns false when the ring buffer is empty.
    bool try_pop_telemetry(TelemetrySample& out_sample) noexcept {
        const uint64_t tail = ring_tail.load(std::memory_order_relaxed);
        const uint64_t head = ring_head.load(std::memory_order_acquire);

        if (tail >= head) {
            return false;
        }

        const size_t idx = static_cast<size_t>(tail & static_cast<uint64_t>(TELEMETRY_RING_CAPACITY - 1));
        out_sample = ring_buffer[idx];
        ring_tail.store(tail + 1ULL, std::memory_order_release);
        return true;
    }

    // Python side: Publishes updated gains and refreshes heartbeat timestamp.
    void publish_gains(int32_t kp, int32_t ki, int32_t kd, uint64_t now_us) noexcept {
        Kp_q16.store(kp, std::memory_order_relaxed);
        Ki_q16.store(ki, std::memory_order_relaxed);
        Kd_q16.store(kd, std::memory_order_relaxed);
        last_python_update_us.store(now_us, std::memory_order_release);
    }

    // C++ side: Reads gains if heartbeat is within timeout_us; otherwise returns false.
    bool try_read_fresh_gains(
        uint64_t now_us,
        uint64_t timeout_us,
        int32_t& out_kp,
        int32_t& out_ki,
        int32_t& out_kd) const noexcept {
        const uint64_t last_hb = last_python_update_us.load(std::memory_order_acquire);
        if (last_hb == 0ULL || now_us < last_hb || (now_us - last_hb) > timeout_us) {
            return false;
        }

        out_kp = Kp_q16.load(std::memory_order_relaxed);
        out_ki = Ki_q16.load(std::memory_order_relaxed);
        out_kd = Kd_q16.load(std::memory_order_relaxed);
        return true;
    }
};

#endif // SHARED_DATA_H
