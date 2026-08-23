import mmap
import struct
import time
import numpy as np
import torch

# Ring buffer size matching SharedData.h
RING_CAPACITY = 1024
TELEMETRY_STRUCT_SIZE = 24 # 4x int32 (16 bytes) + 1x uint64 (8 bytes)

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
    def __init__(self, shm_name="/pid_onnx_shm"):
        # Access shared memory segment
        with open(f"/dev/shm{shm_name}", "r+b") as f:
            self.shm = mmap.mmap(f.fileno(), 0)

        self.replay_buffer = DDPGReplayBuffer()
        self.last_state = None

    def read_ring_buffer(self) -> int:
        """
        Drains all available samples from C++ ring buffer lock-free.
        """
        # Header Offsets: Kp(0), Ki(4), Kd(8), SP(12), PV(16), Err(20), Out(24), TS(28)
        # Head index is at offset 36, Tail at offset 40
        head = struct.unpack("I", self.shm[36:40])[0]
        tail = struct.unpack("I", self.shm[40:44])[0]

        samples_read = 0
        buffer_start_offset = 44 # Start of ring_buffer array

        while tail < head:
            index = tail & (RING_CAPACITY - 1)
            offset = buffer_start_offset + (index * TELEMETRY_STRUCT_SIZE)

            # Unpack struct TelemetrySample: int32 sp, pv, err, out; uint64 ts
            sp_q16, pv_q16, err_q16, out_q16, ts = struct.unpack(
                "iiiiQ", self.shm[offset:offset + TELEMETRY_STRUCT_SIZE]
            )

            # Convert Q16.16 to float
            process_var = pv_q16 / 65536.0
            error = err_q16 / 65536.0
            current_state = np.array([process_var, error], dtype=np.float32)

            if self.last_state is not None:
                # Compute step reward (minimizing error)
                reward = -abs(error)
                # Dummy action gains for buffer tuple
                action = np.array([1.5, 0.1, 0.05], dtype=np.float32) 
                
                # Ingest transition tuple into PyTorch Replay Buffer
                self.replay_buffer.add(self.last_state, action, reward, current_state)

            self.last_state = current_state
            tail += 1
            samples_read += 1

        # Atomically advance tail pointer in shared memory
        self.shm[40:44] = struct.pack("I", tail)
        return samples_read

    def update_heartbeat_and_gains(self, kp: float, ki: float, kd: float):
        """
        Sends updated gains and updates the heartbeat timestamp for C++ safety.
        """
        kp_q16 = int(kp * 65536)
        ki_q16 = int(ki * 65536)
        kd_q16 = int(kd * 65536)
        now_us = int(time.time() * 1e6)

        # Write Gains and Heartbeat Timestamp
        self.shm[0:4] = struct.pack("i", kp_q16)
        self.shm[4:8] = struct.pack("i", ki_q16)
        self.shm[8:12] = struct.pack("i", kd_q16)
        # Offset for last_python_update_us
        self.shm[48:56] = struct.pack("Q", now_us)

if __name__ == "__main__":
    consumer = SharedMemoryTelemetryConsumer()
    print("[PYTHON] Listening to C++ Ring Buffer and feeding PyTorch DDPG Buffer...")

    while True:
        count = consumer.read_ring_buffer()
        if count > 0:
            print(f"[PYTHON] Read {count} samples | Total Replay Buffer Size: {consumer.replay_buffer.size}")
        
        # Send heartbeat & gains to keep C++ loop off fallback mode
        consumer.update_heartbeat_and_gains(1.8, 0.12, 0.06)
        time.sleep(0.02) # 50Hz ingestion cycle
