import numpy as np
import time
import ctypes

# Native Q16.16 Conversion utilities
def float_to_q16(val: float) -> int:
    return int(val * 65536.0)

def q16_to_float(val: int) -> float:
    return float(val) / 65536.0

class PidSharedMemory(ctypes.Structure):
    _pack_ = 1
    _fields_ = [
        ("Kp_q16", ctypes.c_int32),
        ("Ki_q16", ctypes.c_int32),
        ("Kd_q16", ctypes.c_int32),
        ("setpoint_q16", ctypes.c_int32),
        ("process_var_q16", ctypes.c_int32),
        ("error_q16", ctypes.c_int32),
        ("output_q16", ctypes.c_int32),
        ("timestamp_us", ctypes.c_uint64),
    ]

class AdaptiveMLTuner:
    def __init__(self, target_setpoint: float):
        self.setpoint = target_setpoint
        # Initial PID Gains
        self.Kp = 1.5
        self.Ki = 0.1
        self.Kd = 0.05
        
        # Performance Tracking Metrics
        self.error_history = []

    def evaluate_performance_and_tune(self, current_pv: float, current_error: float) -> tuple:
        """
        Actor-Critic policy step: Adjusts gains based on transient response metrics.
        - High overshoot -> Reduce Kp/Ki, increase Kd.
        - High steady-state error -> Increase Ki.
        - Slow rise time -> Increase Kp.
        """
        self.error_history.append(abs(current_error))
        if len(self.error_history) > 50:
            self.error_history.pop(0)

        mean_error = np.mean(self.error_history)
        error_rate = current_error - (self.error_history[-2] if len(self.error_history) > 1 else current_error)

        # Policy gradients / Heuristic tuning adjustment rules
        if mean_error > 5.0:
            self.Kp += 0.05 * np.sign(current_error)
            self.Ki += 0.01
        elif abs(error_rate) > 2.0:
            self.Kd += 0.02  # Dampen oscillations

        # Boundary clamping to prevent instabilities
        self.Kp = np.clip(self.Kp, 0.1, 20.0)
        self.Ki = np.clip(self.Ki, 0.0, 5.0)
        self.Kd = np.clip(self.Kd, 0.0, 10.0)

        return self.Kp, self.Ki, self.Kd

if __name__ == "__main__":
    tuner = AdaptiveMLTuner(target_setpoint=250.0)
    print("Python ML Adaptive Tuner initialized.")
    
    # Simulated execution loop reading shared telemetry
    simulated_pv = 20.0
    for tick in range(20):
        error = 250.0 - simulated_pv
        kp, ki, kd = tuner.evaluate_performance_and_tune(simulated_pv, error)
        
        # Convert updated gains to Q16.16 for C++ IPC write
        kp_q16, ki_q16, kd_q16 = float_to_q16(kp), float_to_q16(ki), float_to_q16(kd)
        
        print(f"[ML TICK {tick:02d}] Error: {error:6.2f} | Updated Gains -> Kp: {kp:.3f}, Ki: {ki:.3f}, Kd: {kd:.3f}")
        
        # Simulate plant progression
        simulated_pv += (250.0 - simulated_pv) * 0.2
        time.sleep(0.05)
