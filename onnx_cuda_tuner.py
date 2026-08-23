import time
import numpy as np
import onnxruntime as ort

class CudaTensorRTPidTuner:
    def __init__(self, model_path: str):
        # Configure Providers: Prioritize TensorRT -> CUDA -> CPU Fallback
        providers = [
            (
                'TensorrtExecutionProvider', {
                    'device_id': 0,
                    'trt_max_workspace_size': 2147483648, # 2 GB
                    'trt_fp16_enable': True, # FP16 Precision for maximum speed
                }
            ),
            (
                'CUDAExecutionProvider', {
                    'device_id': 0,
                    'arena_extend_strategy': 'kNextPowerOfTwo',
                    'gpu_mem_limit': 2 * 1024 * 1024 * 1024,
                    'cudnn_conv_algo_search': 'EXHAUSTIVE',
                }
            ),
            'CPUExecutionProvider'
        ]

        print("[PYTHON ML] Loading ONNX Session with Hardware Acceleration...")
        self.session = ort.InferenceSession(model_path, providers=providers)
        
        active_providers = self.session.get_providers()
        print(f"[PYTHON ML] Active Execution Provider: {active_providers[0]}")

        self.input_name = self.session.get_inputs()[0].name
        self.output_name = self.session.get_outputs()[0].name

    def infer_gains(self, state_batch: np.ndarray) -> np.ndarray:
        """
        Executes high-speed batch inference on CUDA/TensorRT cores.
        """
        outputs = self.session.run([self.output_name], {self.input_name: state_batch})
        return outputs[0]

if __name__ == "__main__":
    # Test initialization
    tuner = CudaTensorRTPidTuner("pid_tuner.onnx")

    # Sample batch representing high-frequency state telemetry
    dummy_state = np.random.randn(1, 4).astype(np.float32)

    # Benchmark Latency
    start_time = time.perf_counter()
    predicted_gains = tuner.infer_gains(dummy_state)
    latency_us = (time.perf_counter() - start_time) * 1e6

    print(f"[BENCHMARK] Inference Time: {latency_us:.2f} microseconds")
    print(f"[BENCHMARK] Predicted Gains [Kp, Ki, Kd]: {predicted_gains[0]}")
