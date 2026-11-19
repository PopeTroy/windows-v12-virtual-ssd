import time
import numpy as np
import onnxruntime as ort


class CudaTensorRTPidTuner:
    def __init__(self, model_path: str, input_dim: int = 4):
        providers = [
            (
                "TensorrtExecutionProvider",
                {
                    "device_id": 0,
                    "trt_max_workspace_size": 2147483648,
                    "trt_fp16_enable": True,
                },
            ),
            (
                "CUDAExecutionProvider",
                {
                    "device_id": 0,
                    "arena_extend_strategy": "kNextPowerOfTwo",
                    "gpu_mem_limit": 2 * 1024 * 1024 * 1024,
                    "cudnn_conv_algo_search": "EXHAUSTIVE",
                },
            ),
            "CPUExecutionProvider",
        ]

        opts = ort.SessionOptions()
        opts.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_ALL

        print("[PYTHON ML] Loading ONNX Session with Hardware Acceleration...")
        self.session = ort.InferenceSession(model_path, sess_options=opts, providers=providers)
        active_providers = self.session.get_providers()
        print(f"[PYTHON ML] Active Execution Provider: {active_providers[0]}")

        self.input_name = self.session.get_inputs()[0].name
        self.output_name = self.session.get_outputs()[0].name

        # Pre-warm execution provider off critical path to avoid cold-start latency spikes
        warmup_batch = np.zeros((1, input_dim), dtype=np.float32)
        _ = self.session.run([self.output_name], {self.input_name: warmup_batch})

    def infer_gains(self, state_batch: np.ndarray) -> np.ndarray:
        batch = np.asarray(state_batch, dtype=np.float32)
        outputs = self.session.run([self.output_name], {self.input_name: batch})
        gains = np.asarray(outputs[0], dtype=np.float32)
        gains = np.nan_to_num(gains, nan=0.0, posinf=20.0, neginf=0.0)
        return np.clip(gains, 0.0, 20.0)


if __name__ == "__main__":
    tuner = CudaTensorRTPidTuner("pid_tuner.onnx", input_dim=4)
    dummy_state = np.random.randn(1, 4).astype(np.float32)

    start_time = time.perf_counter()
    predicted_gains = tuner.infer_gains(dummy_state)
    latency_us = (time.perf_counter() - start_time) * 1e6

    print(f"[BENCHMARK] Inference Time: {latency_us:.2f} microseconds")
    print(f"[BENCHMARK] Predicted Gains [Kp, Ki, Kd]: {predicted_gains[0]}")
