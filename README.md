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

                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.
      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      patent license to make, have made, use, offer to sell, sell, import,
      and otherwise transfer the Work.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an "AS IS" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law or agreed to in writing, shall
      any Contributor be liable to You for damages, including any direct,
      indirect, special, incidental, or consequential damages of any
      character arising as a result of this License or out of the use or
      inability to use the Work.

   Copyright 2026 Celsius Technology & Media Group © 
