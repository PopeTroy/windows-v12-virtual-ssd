import os
import sys
import time
import mmap
import struct

# Shared Memory Segment Name & Ring Buffer Capacity (Must match SharedData.h)
SHM_NAME = "/pid_onnx_shm"
TELEMETRY_RING_CAPACITY = 1024

# Struct Binary Unpacking Format Strings (Q16.16 Fixed-Point Architecture)
# PidSharedMemory Layout:
# - Kp_q16, Ki_q16, Kd_q16: int32 (i)
# - setpoint_q16, process_var_q16, error_q16, output_q16: int32 (i)
# - timestamp_us: uint64 (Q)
# - last_python_update_us: uint64 (Q)
# - ring_head, ring_tail: uint32 (I)
HEADER_FORMAT = "=iii iiii Q Q II"
HEADER_SIZE = struct.calcsize(HEADER_FORMAT)

# TelemetrySample Layout:
# - setpoint_q16, process_var_q16, error_q16, output_q16: int32 (i)
# - timestamp_us: uint64 (Q)
SAMPLE_FORMAT = "=iiii Q"
SAMPLE_SIZE = struct.calcsize(SAMPLE_FORMAT)


def float_to_q16(val: float) -> int:
    """Convert floating-point value to Q16.16 fixed-point integer."""
    return int(val * 65536.0)


def q16_to_float(val: int) -> float:
    """Convert Q16.16 fixed-point integer to floating-point value."""
    return float(val) / 65536.0


def main():
    shm_path = f"/dev/shm{SHM_NAME}" if os.name == "posix" else SHM_NAME

    # Wait for C++ engine to create and initialize the shared memory block
    print("[PYTHON] Waiting for C++ shared memory segment to initialize...")
    while not os.path.exists(shm_path):
        time.sleep(0.1)

    # Calculate total size of PidSharedMemory struct
    total_size = HEADER_SIZE + (TELEMETRY_RING_CAPACITY * SAMPLE_SIZE)

    try:
        # Open shared memory file descriptor and map into memory
        fd = os.open(shm_path, os.O_RDWR)
        shm = mmap.mmap(fd, total_size, mmap.MAP_SHARED, mmap.PROT_READ | mmap.PROT_WRITE)
        os.close(fd)
        print("[PYTHON] Successfully mapped shared memory segment.")
    except Exception as e:
        print(f"[PYTHON ERROR] Failed to map shared memory: {e}")
        sys.exit(1)

    # Dynamic PID Tuning Loop State
    current_kp = 2.0
    current_ki = 0.15
    current_kd = 0.08

    print("[PYTHON] Real-time ONNX/RL Tuning Engine Active...")

    try:
        while True:
            # 1. Read current shared memory state header
            shm.seek(0)
            header_bytes = shm.read(HEADER_SIZE)
            (
                kp_q16, ki_q16, kd_q16,
                sp_q16, pv_q16, err_q16, out_q16,
                ts_us, last_py_us,
                ring_head, ring_tail
            ) = struct.unpack(HEADER_FORMAT, header_bytes)

            # 2. Consume unread telemetry samples from the SPSC Ring Buffer
            samples_consumed = 0
            while ring_tail < ring_head:
                idx = ring_tail & (TELEMETRY_RING_CAPACITY - 1)
                offset = HEADER_SIZE + (idx * SAMPLE_SIZE)

                shm.seek(offset)
                sample_bytes = shm.read(SAMPLE_SIZE)
                s_sp, s_pv, s_err, s_out, s_ts = struct.unpack(SAMPLE_FORMAT, sample_bytes)

                # Process sample data (e.g., append to RL Replay Buffer or ONNX state vector)
                # s_pv_float = q16_to_float(s_pv)
                # s_err_float = q16_to_float(s_err)

                ring_tail += 1
                samples_consumed += 1

            # Update ring_tail in shared memory if samples were processed
            if samples_consumed > 0:
                shm.seek(40)  # Byte offset of ring_tail in header
                shm.write(struct.pack("=I", ring_tail))

            # 3. Simulate dynamic RL inference/tuning update
            # (Example: dynamically adjusting gains based on error state)
            current_pv = q16_to_float(pv_q16)
            current_sp = q16_to_float(sp_q16)

            if abs(current_sp - current_pv) > 50.0:
                current_kp = 2.8  # Aggressive response for large setpoint deltas
                current_kd = 0.12
            else:
                current_kp = 1.8  # Smooth response near setpoint
                current_kd = 0.06

            # 4. Write updated PID gains in Q16.16 format back to shared memory
            shm.seek(0)
            shm.write(struct.pack("=iii", 
                float_to_q16(current_kp),
                float_to_q16(current_ki),
                float_to_q16(current_kd)
            ))

            # 5. Write Python Heartbeat Timestamp (Microseconds)
            now_us = int(time.time_ns() / 1000)
            shm.seek(32)  # Byte offset of last_python_update_us
            shm.write(struct.pack("=Q", now_us))

            # Loop sleep interval (e.g., 20ms ONNX inference pass)
            time.sleep(0.02)

    except KeyboardInterrupt:
        print("\n[PYTHON] Detaching from shared memory and shutting down.")
    finally:
        shm.close()


if __name__ == "__main__":
    main()
