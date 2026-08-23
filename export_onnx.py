import torch
import torch.nn as nn

class PIDPolicyNetwork(nn.Module):
    """
    Inputs:  [error, integral_error, derivative_error, process_variable]
    Outputs: [Kp, Ki, Kd]
    """
    def __init__(self):
        super(PIDPolicyNetwork, self).__init__()
        self.net = nn.Sequential(
            nn.Linear(4, 32),
            nn.ReLU(),
            nn.Linear(32, 32),
            nn.ReLU(),
            nn.Linear(32, 3),
            nn.Softplus() # Guarantees non-negative PID gains
        )

    def forward(self, x):
        return self.net(x)

if __name__ == "__main__":
    model = PIDPolicyNetwork()
    model.eval()

    # Dummy input representing state: [error, integral_error, d_error, PV]
    dummy_input = torch.randn(1, 4, dtype=torch.float32)

    # Export model to ONNX runtime format
    torch.onnx.export(
        model,
        dummy_input,
        "pid_tuner.onnx",
        export_params=True,
        opset_version=14,
        do_constant_folding=True,
        input_names=['state'],
        output_names=['gains'],
        dynamic_axes={'state': {0: 'batch_size'}, 'gains': {0: 'batch_size'}}
    )
    print("Successfully exported 'pid_tuner.onnx' for fast runtime inference!")
