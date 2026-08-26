#include <iostream>
#include <cmath>
#include <algorithm>
#include <cstdint>
#include <chrono>
#include <thread>

// ============================================================================
// FIXED-POINT Q16.16 HELPERS
// ============================================================================
#define FLOAT_TO_Q16(x) (static_cast<int32_t>((x) * 65536.0f))
#define Q16_TO_FLOAT(x) (static_cast<float>(x) / 65536.0f)

#define ADD_Q16(a, b) ((a) + (b))
#define SUB_Q16(a, b) ((a) - (b))

// Safe Q16.16 Multiplication (prevents 64-bit precision truncation)
inline int32_t MULT_Q16(int32_t a, int32_t b) {
    int64_t temp = static_cast<int64_t>(a) * static_cast<int64_t>(b);
    return static_cast<int32_t>(temp >> 16);
}

// Safe Q16.16 Division
inline int32_t DIV_Q16(int32_t numerator, int32_t denominator) {
    if (denominator == 0) return 0; // Guard against division by zero
    int64_t temp = static_cast<int64_t>(numerator) << 16;
    return static_cast<int32_t>(temp / denominator);
}

// ============================================================================
// ADVANCED QUANTUM-INSPIRED ADAPTIVE PID CONTROLLER (Q16.16)
// ============================================================================
class QuantumInspiredPID {
private:
    // Core Gains (Q16.16 format)
    int32_t Kp, Ki, Kd;

    // Adaptive Factors (1.0 = Normal gain)
    float Kp_adaptation_factor;
    float Ki_adaptation_factor;
    float Kd_adaptation_factor;

    // Internal State Variables
    int32_t integral;
    int32_t prev_error;
    int32_t out_min, out_max;

    // Predictive Control Parameters
    int32_t lookahead_dt_q16;
    float prediction_weight; // Blend weight (0.0 to 1.0)

    // System Monitoring & Diagnostics
    uint32_t consecutive_overshoot_count;

public:
    QuantumInspiredPID(float kp, float ki, float kd, float min_out, float max_out,
                       float lookahead_time = 0.001f, float pred_weight = 0.5f)
        : Kp(FLOAT_TO_Q16(kp)), Ki(FLOAT_TO_Q16(ki)), Kd(FLOAT_TO_Q16(kd)),
          Kp_adaptation_factor(1.0f), Ki_adaptation_factor(1.0f), Kd_adaptation_factor(1.0f),
          integral(0), prev_error(0),
          out_min(FLOAT_TO_Q16(min_out)), out_max(FLOAT_TO_Q16(max_out)),
          lookahead_dt_q16(FLOAT_TO_Q16(lookahead_time)),
          prediction_weight(pred_weight),
          consecutive_overshoot_count(0) {}

    // Update base PID Gains dynamically
    void updateGains(int32_t new_kp, int32_t new_ki, int32_t new_kd) {
        Kp = new_kp;
        Ki = new_ki;
        Kd = new_kd;
        integral = 0; // Reset integral on fundamental gain shift to prevent windup
    }

    // Adaptive Gain Scheduling Strategy
    void adaptGains(int32_t setpoint, int32_t process_variable, int32_t control_output) {
        int32_t error = SUB_Q16(setpoint, process_variable);
        int32_t abs_error = std::abs(error);

        // Scenario 1: Target approaching setpoint -> scale down Kp to prevent overshoot
        if (abs_error < FLOAT_TO_Q16(2.0f)) {
            Kp_adaptation_factor = 0.85f;
            Ki_adaptation_factor = 1.20f; // Boost integral for zero steady-state error
        } 
        else if (abs_error > FLOAT_TO_Q16(10.0f)) {
            // Far from target -> boost proportional action for aggressive response
            Kp_adaptation_factor = 1.25f;
            Ki_adaptation_factor = 0.80f;
        } 
        else {
            Kp_adaptation_factor = 1.00f;
            Ki_adaptation_factor = 1.00f;
        }

        // Detect saturation or ringing oscillation
        if (control_output >= out_max || control_output <= out_min) {
            Kd_adaptation_factor = 0.75f; // Dampen derivative during active output saturation
        } else {
            Kd_adaptation_factor = 1.00f;
        }
    }

    // Main Compute Loop
    int32_t compute(int32_t setpoint, int32_t process_variable, int32_t dt_q16) {
        int32_t error = SUB_Q16(setpoint, process_variable);

        // 1. Quantum-inspired Predictive Lookahead
        int32_t error_change = SUB_Q16(error, prev_error);
        int32_t error_rate = (dt_q16 > 0) ? DIV_Q16(error_change, dt_q16) : 0;
        int32_t predicted_future_error = ADD_Q16(error, MULT_Q16(error_rate, lookahead_dt_q16));

        // Blend instant error with precognition trajectory
        int32_t effective_error = static_cast<int32_t>(
            (1.0f - prediction_weight) * error + prediction_weight * predicted_future_error
        );

        // Apply dynamic adaptation factors to base gains
        int32_t current_kp = static_cast<int32_t>(Kp * Kp_adaptation_factor);
        int32_t current_ki = static_cast<int32_t>(Ki * Ki_adaptation_factor);
        int32_t current_kd = static_cast<int32_t>(Kd * Kd_adaptation_factor);

        // 2. Proportional Term
        int32_t p_term = MULT_Q16(current_kp, effective_error);

        // 3. Integral Term with Clamping (Anti-Windup)
        integral = ADD_Q16(integral, MULT_Q16(error, dt_q16));
        int32_t i_term = MULT_Q16(current_ki, integral);

        // Clamp Integral contribution boundaries
        if (i_term > out_max) {
            i_term = out_max;
            integral = DIV_Q16(out_max, current_ki);
        } else if (i_term < out_min) {
            i_term = out_min;
            integral = DIV_Q16(out_min, current_ki);
        }

        // 4. Derivative Term
        int32_t d_term = MULT_Q16(current_kd, error_rate);

        // 5. Calculate Total Output
        int32_t output = ADD_Q16(p_term, ADD_Q16(i_term, d_term));

        // 6. Output Saturation Guard
        output = std::clamp(output, out_min, out_max);

        // Store past states for next iteration
        prev_error = error;

        // Perform gain adaptation pass for next loop iteration
        adaptGains(setpoint, process_variable, output);

        return output;
    }
};

// ============================================================================
// SIMULATION ENTRY POINT
// ============================================================================
int main() {
    // Controller Configuration
    QuantumInspiredPID pid(2.0f, 0.2f, 0.05f, -100.0f, 100.0f, 0.005f, 0.4f);

    int32_t setpoint_q16 = FLOAT_TO_Q16(250.0f); // Target Temperature
    int32_t pv_q16 = FLOAT_TO_Q16(20.0f);        // Initial Ambient PV
    int32_t dt_q16 = FLOAT_TO_Q16(0.01f);        // 10ms control step

    std::cout << "Starting Quantum-Inspired PID Simulation Loops...\n";
    std::cout << "Target PV Setpoint: " << Q16_TO_FLOAT(setpoint_q16) << " C\n\n";

    float process_gain = 0.05f;

    for (int step = 0; step < 100; ++step) {
        // Compute Control Output
        int32_t output_q16 = pid.compute(setpoint_q16, pv_q16, dt_q16);
        float output_f = Q16_TO_FLOAT(output_q16);

        // Simulate Plant Thermal Response (PV += Output * ProcessGain)
        pv_q16 += MULT_Q16(output_q16, FLOAT_TO_Q16(process_gain));

        std::cout << "Step [" << step << "] "
                  << "PV: " << Q16_TO_FLOAT(pv_q16) << " C | "
                  << "Control Output: " << output_f << "\n";

        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }

    return 0;
}
