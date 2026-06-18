# UESP Sovereign V12 Virtual SSD (Windows Core)

This repository hosts a standalone C# application that virtualizes a high-performance **200 GB Virtual SSD Volume** mapped natively to your local workstation environment out of your primary storage layer.

## How It Works
* **Dynamic Overlay Execution:** Uses Windows VHDX sparse allocation matrices to consume physical space only as files are written.
* **Administrative Cleared Hub:** Leverages secure hardware partitioning parameters to deploy an isolated volume layout (`V:\`).

## How to Download and Run (.exe)
1. Navigate to the **Releases** pipeline module on the right sidebar of this repository.
2. Download the pre-compiled standalone executable target: `SovereignVirtualSSD.exe`.
3. Right-click the `.exe` file and choose **Run as Administrator** (Required to interact with physical drive partitioning tracks).
4. Check your Windows File Explorer—a brand new **Sovereign_V12_SSD (V:)** disk drive will be initialized!
