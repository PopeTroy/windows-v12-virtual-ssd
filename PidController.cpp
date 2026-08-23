#include <iostream>
#include <cmath>
#include <cstdint>
#include <chrono>
#include <thread>
#include "SharedData.h"

// Fixed-Point Q16.16 Helpers
#define FLOAT_TO_Q16(x) (static_cast<int32_t>((x) * 65536.0f))
#define Q16_TO_FLOAT(x) (static_cast<float>(x) / 65536.0f)
#define MULT_Q16(a, b)  (static_cast<int32_t>((static_cast<int64_t>(a) * (b)) >> 16))

class FixedPointPID {
private:
    int32_t Kp, Ki, Kd;
    int32_t integral;
    int32_t prev_error;
    int32_t out_min, out_max;

public:
    FixedPointPID(float kp, float ki, float kd, float min_out, float max_out)
        : Kp(FLOAT_TO_Q16(kp)), Ki(FLOAT_TO_Q16(ki)), Kd(FLOAT_TO_Q16(kd)),
          integral(0), prev_error(0),
          out_min(FLOAT_TO_Q16(min_out)), out_max(FLOAT_TO_Q16(max_out)) {}

    void updateGains(int32_t new_kp, int32_t new_ki, int32_t new_kd) {
        Kp = new_kp;
        Ki = new_ki;
        Kd = new_kd;
    }

    int32_t compute(int32_t setpoint, int32_t process_variable, int32_t dt_q16) {
        // e(t) = Setpoint - PV
        int32_t error = setpoint - process_variable;

        // Proportional term = Kp * e(t)
        int32_t p_term = MULT_Q16(Kp, error);

        // Integral term = Ki * integral(e(t) * dt)
        int32_t error_dt = MULT_Q16(error, dt_q16);
        integral += error_dt;
        int32_t i_term = MULT_Q16(Ki, integral);

        // Anti-windup clamping on integral term
        if (i_term > out_max) {
            i_term = out_max;
            integral -= error_dt;
        } else if (i_term < out_min) {
            i_term = out_min;
            integral -= error_dt;
        }

        // Derivative term = Kd * (e(t) - e(t-1)) / dt
        int32_t derivative = 0;
        if (dt_q16 > 0) {
            int32_t d_error = error - prev_error;
            int32_t rate = (static_cast<int64_t>(d_error) << 16) / dt_q16;
            derivative = MULT_Q16(Kd, rate);
        }

        // u(t) = P + I + D
        int32_t output = p_term + i_term + derivative;

        // Output clamping
        if (output > out_max) output = out_max;
        if (output < out_min) output = out_min;

        prev_error = error;
        return output;
    }
};

int main() {
    // Initial conservative gains
    FixedPointPID pid(1.5f, 0.1f, 0.05f, 0.0f, 100.0f);

    int32_t setpoint = FLOAT_TO_Q16(250.0f); // e.g., Target temperature in °C
    int32_t pv = FLOAT_TO_Q16(20.0f);       // Initial temperature
    int32_t dt = FLOAT_TO_Q16(0.01f);       // 10ms execution loop

    std::cout << "Starting C++ Real-Time PID Loop (Q16.16 Fixed-Point)..." << std::endl;

    for (int step = 0; step < 100; ++step) {
        int32_t output = pid.compute(setpoint, pv, dt);
        
        // Simulated process thermal response: PV += (Output * 0.05)
        pv += MULT_Q16(output, FLOAT_TO_Q16(0.05f));

        std::cout << "Step " << step 
                  << " | Target: " << Q16_TO_FLOAT(setpoint)
                  << " | PV: " << Q16_TO_FLOAT(pv)
                  << " | Control u(t): " << Q16_TO_FLOAT(output) << std::endl;

        std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }

    return 0;
}
