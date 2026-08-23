#!/usr/bin/env bash

# Terminate execution if any setup step fails
set -e

SHM_NAME="/dev/shm/pid_onnx_shm"
BUILD_DIR="build"

echo "=========================================================="
echo " Starting Real-Time Control & Dynamic Tuning Pipeline"
echo "=========================================================="

# 1. Clean up lingering POSIX shared memory from previous crashes
if [ -f "$SHM_NAME" ]; then
    echo "[SETUP] Removing orphaned shared memory segment..."
    rm -f "$SHM_NAME"
fi

# 2. Build C++ Engine via CMake
echo "[BUILD] Compiling C++ PID Control Engine..."
mkdir -p $BUILD_DIR
cd $BUILD_DIR
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)
cd ..

# 3. Setup Process Cleanup Handler on Exit / Ctrl+C
cleanup() {
    echo ""
    echo "[SHUTDOWN] Terminating processes..."
    kill -TERM "$CPP_PID" 2>/dev/null || true
    kill -TERM "$PYTHON_PID" 2>/dev/null || true
    
    # Remove shared memory block
    if [ -f "$SHM_NAME" ]; then
        rm -f "$SHM_NAME"
    fi
    echo "[SHUTDOWN] System offline."
    exit 0
}

trap cleanup SIGINT SIGTERM EXIT

# 4. Launch C++ Control Node with sudo for real-time priority access
echo "[SYSTEM] Launching C++ Control Loop..."
sudo ./$BUILD_DIR/pid_control_node &
CPP_PID=$!

# Brief pause to allow C++ to initialize and allocate shared memory struct
sleep 0.5

# 5. Launch Python RL / ONNX Dynamic Tuner Process
echo "[SYSTEM] Launching Python Dynamic Tuning Engine..."
python3 python_tuner.py &
PYTHON_PID=$!

# Keep master script running to monitor child processes
wait $CPP_PID $PYTHON_PID
