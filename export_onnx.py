import io
import sys
import torch
import torch.nn as nn

if hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
if hasattr(sys.stderr, "buffer"):
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8")


class PIDPolicyNetwork(nn.Module):
    """
    Inputs:  [error, integral_error, derivative_error, process_variable]
    Outputs: [Kp, Ki, Kd]
    """

    def __init__(self):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(4, 32),
            nn.ReLU(),
            nn.Linear(32, 32),
            nn.ReLU(),
            nn.Linear(32, 3),
            nn.Softplus(),
        )

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return self.net(x)


if __name__ == "__main__":
    model = PIDPolicyNetwork()
    model.eval()

    dummy_input = torch.randn(1, 4, dtype=torch.float32)

    torch.onnx.export(
        model,
        dummy_input,
        "pid_tuner.onnx",
        export_params=True,
        opset_version=18,
        do_constant_folding=True,
        input_names=["state"],
        output_names=["gains"],
        dynamic_axes={"state": {0: "batch_size"}, "gains": {0: "batch_size"}},
        dynamo=False,
    )
    print("Successfully exported 'pid_tuner.onnx' using PyTorch CPU runtime!")
