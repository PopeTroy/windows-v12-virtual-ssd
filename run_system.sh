#!/usr/bin/env bash
set -e

SHM_NAME="/dev/shm/pid_onnx_shm"
BUILD_DIR="build"
CONFIG_FILE="vssdhx_dlss5_config.ini"

echo "=========================================================="
echo " Starting Multi-Engine Real-Time Control & Tuning Stack"
echo "=========================================================="

# 1. Purge orphaned shared memory (Linux environments)
if [ -f "$SHM_NAME" ]; then
    rm -f "$SHM_NAME"
fi

# 2. Check and generate default DLSS 5 configuration file if missing
if [ ! -f "$CONFIG_FILE" ]; then
    echo "[CONFIG] Generating default vssdhx_dlss5_config.ini..."
    cat << 'EOF' > "$CONFIG_FILE"
[DLSS5]
Enabled = true
ResolutionScale = 2.0
Sharpness = 0.8

[ReShade]
ActiveShaders = CAS.fx, SMAA.fx, NeuralSharpen.fx, RenoDX_HDR.fx
EOF
fi

# 3. Compile C++ Core Engine
mkdir -p $BUILD_DIR
cd $BUILD_DIR
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j$(nproc 2>/dev/null || echo 4)
cd ..

# 4. Cleanup handler for all spawned background processes
cleanup() {
    echo ""
    echo "[SHUTDOWN] Terminating all parallel control engines..."
    kill -TERM "$CPP_PID" "$CUDA_PID" "$ONNX_PID" "$ML_PID" "$DDPG_PID" 2>/dev/null || true
    if [ -f "$SHM_NAME" ]; then
        rm -f "$SHM_NAME"
    fi
    echo "[SHUTDOWN] System offline."
    exit 0
}
trap cleanup SIGINT SIGTERM EXIT

# 5. Launch C++ Real-Time Core (With OS cross-compatibility detection)
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    ./$BUILD_DIR/Release/pid_control_node.exe &
else
    sudo ./$BUILD_DIR/pid_control_node &
fi
CPP_PID=$!
sleep 0.5

# 6. Launch All Hybrid AI & Ingestion Tuners Simultaneously
echo "[SYSTEM] Launching CUDA/TensorRT Engine..."
python3 onnx_cuda_tuner.py &
CUDA_PID=$!

echo "[SYSTEM] Launching ONNX/PyTorch Engine..."
python3 onnx_tuner.py &
ONNX_PID=$!

echo "[SYSTEM] Launching ML PID Tuner..."
python3 ml_tuner.py &
ML_PID=$!

echo "[SYSTEM] Launching DDPG Replay Ingestion Pipeline..."
python3 python_ddpg_ingest.py &
DDPG_PID=$!

# 7. Execute C# Virtual SSD Host Process
dotnet run --configuration Release --project SovereignSSD.csproj

wait
