import sys
import time
import ctypes
import numpy as np
import onnxruntime as ort

# Multi-platform shared memory import
if sys.platform == "win32":
    import mmap
else:
    import posix_ipc
    import mmap

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

class OnnxPidTuner:
    def __init__(self, model_path: str):
        # Initialize ONNX Runtime session with CPU / OpenVINO / CUDA execution providers
        self.session = ort.InferenceSession(model_path, providers=['CPUExecutionProvider'])
        self.input_name = self.session.get_inputs()[0].name
        self.output_name = self.session.get_outputs()[0].name
        
        self.integral_error = 0.0
        self.prev_error = 0.0

    def predict_gains(self, error: float, pv: float) -> tuple:
        self.integral_error += error * 0.01
        d_error = (error - self.prev_error) / 0.01
        self.prev_error = error

        # Build feature vector [4]
        state_input = np.array([[error, self.integral_error, d_error, pv]], dtype=np.float32)

        # ONNX inference execution
        outputs = self.session.run([self.output_name], {self.input_name: state_input})
        gains = outputs[0][0]

        # Apply gain constraints
        kp = float(np.clip(gains[0], 0.1, 20.0))
        ki = float(np.clip(gains[1], 0.0, 5.0))
        kd = float(np.clip(gains[2], 0.0, 10.0))

        return kp, ki, kd

def main():
    print("[PYTHON ONNX] Initializing ONNX Runtime PID Tuner Engine...")
    tuner = OnnxPidTuner("pid_tuner.onnx")

    # Connect to Cross-Platform Shared Memory
    shm_size = ctypes.sizeof(PidSharedMemory)
    if sys.platform == "win32":
        shm_map = mmap.mmap(-1, shm_size, "Global\\pid_onnx_shm")
    else:
        memory = posix_ipc.SharedMemory("/pid_onnx_shm")
        shm_map = mmap.mmap(memory.fd, shm_size)

    shm_struct = PidSharedMemory.from_buffer(shm_map)

    print("[PYTHON ONNX] Attached to C++ Shared Memory. Tuning Active...")

    try:
        while True:
            # Read state exported by C++ PID Loop
            error = q16_to_float(shm_struct.error_q16)
            pv = q16_to_float(shm_struct.process_var_q16)

            # Fast ONNX inference call
            kp, ki, kd = tuner.predict_gains(error, pv)

            # Write optimized gains back to Shared Memory for C++ execution
            shm_struct.Kp_q16 = float_to_q16(kp)
            shm_struct.Ki_q16 = float_to_q16(ki)
            shm_struct.Kd_q16 = float_to_q16(kd)

            time.sleep(0.01) # 100Hz tuning loop rate
    except KeyboardInterrupt:
        print("[PYTHON ONNX] Tuning stopped gracefully.")

if __name__ == "__main__":
    main()
