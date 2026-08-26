"""
============================================================================
JUBI TEN-TAILS DDPG INGESTION ENGINE
============================================================================
Integrates 10-tailpiece state momentum vectors into the PyTorch DDPG ingestion
pipeline with state energy signature analysis and quantum-inspired gain modulation.
============================================================================
"""

import sys
import mmap
import struct
import time
import numpy as np
import torch

# Cross-platform IPC imports
if sys.platform == "win32":
    import ctypes
else:
    import posix_ipc

# Ring buffer size matching SharedData.h
RING_CAPACITY = 1024
TELEMETRY_STRUCT_SIZE = 24  # 4x int32 (16 bytes) + 1x uint64 (8 bytes)

class TenTailsMomentumEngine:
    """Tracks 10 distinct tailpiece state vectors to calculate Jubi momentum."""
    def __init__(self):
        self.tail_vectors = np.zeros(10, dtype=np.float32)
        self.tail_weights = np.array([0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0], dtype=np.float32)

    def accumulate_tail_energy(self, current_error: float) -> float:
        self.tail_vectors = np.roll(self.tail_vectors, 1)
        self.tail_vectors[0] = current_error
        jubi_energy = np.dot(self.tail_vectors, self.tail_weights)
        return float(jubi_energy)

    def analyze_state_energy_relationship(self, error: float, jubi_energy: float) -> float:
        """
        Analogous to Brus Equation analysis.
        Analyzes how the current system state (error) and its momentum signature (jubi_energy)
        contribute to the overall "system energy" which dictates reward and control gain adjustments.
        Relates control error and accumulated momentum to a "control bandgap" penalty/bonus.
        """
        control_energy_signature = (
            1.0 * abs(error) +           # Primary energy contribution from current error
            0.5 * abs(jubi_energy)       # Secondary contribution from momentum state
        )
        return float(control_energy_signature)

class DDPGReplayBuffer:
    def __init__(self, state_dim=2, action_dim=3, max_size=100000):
        self.max_size = max_size
        self.ptr = 0
        self.size = 0

        self.state = np.zeros((max_size, state_dim), dtype=np.float32)
        self.action = np.zeros((max_size, action_dim), dtype=np.float32)
        self.reward = np.zeros((max_size, 1), dtype=np.float32)
        self.next_state = np.zeros((max_size, state_dim), dtype=np.float32)

    def add(self, state, action, reward, next_state):
        self.state[self.ptr] = state
        self.action[self.ptr] = action
        self.reward[self.ptr] = reward
        self.next_state[self.ptr] = next_state

        self.ptr = (self.ptr + 1) % self.max_size
        self.size = min(self.size + 1, self.max_size)

class SharedMemoryTelemetryConsumer:
    def __init__(self, shm_name="pid_onnx_shm"):
        if sys.platform == "win32":
            self.shm = mmap.mmap(-1, 24624, f"Global\\{shm_name}")
        else:
            clean_shm_name = shm_name.lstrip("/")
            with open(f"/dev/shm/{clean_shm_name}", "r+b") as f:
                self.shm = mmap.mmap(f.fileno(), 0)

        self.replay_buffer = DDPGReplayBuffer()
        self.jubi_engine = TenTailsMomentumEngine()
        self.last_state = None

    def read_ring_buffer(self) -> int:
        head = struct.unpack("I", self.shm[36:40])[0]
        tail = struct.unpack("I", self.shm[40:44])[0]

        samples_read = 0
        buffer_start_offset = 44

        while tail < head:
            index = tail & (RING_CAPACITY - 1)
            offset = buffer_start_offset + (index * TELEMETRY_STRUCT_SIZE)

            sp_q16, pv_q16, err_q16, out_q16, ts = struct.unpack(
                "iiiiQ", self.shm[offset:offset + TELEMETRY_STRUCT_SIZE]
            )

            process_var = pv_q16 / 65536.0
            error = err_q16 / 65536.0
            current_state = np.array([process_var, error], dtype=np.float32)

            # Energy analysis sequence
            jubi_energy = self.jubi_engine.accumulate_tail_energy(error)
            control_energy_signature = self.jubi_engine.analyze_state_energy_relationship(error, jubi_energy)

            if self.last_state is not None:
                # Reward shaping using energy signature and jubi_energy
                reward = -abs(error) - (0.05 * abs(jubi_energy)) - (0.02 * control_energy_signature)

                # Control gain modulation based on energy state
                base_kp_adjustment = (0.01 * np.sign(jubi_energy)) + (0.005 * np.sign(control_energy_signature))
                base_kp = 1.5 + base_kp_adjustment

                # Action space projection
                action = np.array([base_kp, 0.1, 0.05], dtype=np.float32) 
                
                self.replay_buffer.add(self.last_state, action, reward, current_state)

            self.last_state = current_state
            tail += 1
            samples_read += 1

        self.shm[40:44] = struct.pack("I", tail)
        return samples_read

    def update_heartbeat_and_gains(self, kp: float, ki: float, kd: float):
        kp_q16 = int(kp * 65536)
        ki_q16 = int(ki * 65536)
        kd_q16 = int(kd * 65536)
        now_us = int(time.time() * 1e6)

        self.shm[0:4] = struct.pack("i", kp_q16)
        self.shm[4:8] = struct.pack("i", ki_q16)
        self.shm[8:12] = struct.pack("i", kd_q16)
        self.shm[28:36] = struct.pack("Q", now_us)

if __name__ == "__main__":
    consumer = SharedMemoryTelemetryConsumer()
    print("[JUBI 10-TAILS ENGINE] Listening to C++ Ring Buffer and feeding PyTorch DDPG Buffer...")

    count = consumer.read_ring_buffer()
    print(f"[JUBI DDPG] Ingested {count} samples | Total Replay Buffer Size: {consumer.replay_buffer.size}")
    consumer.update_heartbeat_and_gains(1.8, 0.12, 0.06)
