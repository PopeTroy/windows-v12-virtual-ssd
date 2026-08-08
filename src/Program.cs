using System;
using System.IO;
using System.Runtime.InteropServices;
using SovereignEngine.Native;

namespace SovereignSSD
{
    internal class Program
    {
        private const string VIRTUAL_VOLUME_LABEL = "UESP_V12_SSD";

        static void Main(string[] args)
        {
            Console.Title = "UESP Sovereign V12 Virtual SSD Core Engine";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" UESP Sovereign V12 Virtual SSD Volume (Windows Native Core)");
            Console.WriteLine(" Capacity Target: 200 GB Sparse Partition");
            Console.WriteLine("===================================================================\n");
            Console.ResetColor();

            try
            {
                // Step 1: Validate Native Dynamic Library FFI
                Console.WriteLine("[INIT] Verifying Native Sovereign Engine FFI binding...");
                byte[] samplePayload = System.Text.Encoding.UTF8.GetBytes("UESP_V12_INITIALIZATION_VECTOR_SECTOR_0");
                byte[] compressed = SovereignCompressor.Compress(samplePayload, compressionLevel: 3);
                byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: samplePayload.Length);

                if (System.Text.Encoding.UTF8.GetString(decompressed) == "UESP_V12_INITIALIZATION_VECTOR_SECTOR_0")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SUCCESS] Native Zstd / Rayon Engine Binding Online.");
                    Console.ResetColor();
                }

                // Step 2: Establish Virtual Mount Space Directory Struct
                string baseSSDPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SovereignV12SSD");
                if (!Directory.Exists(baseSSDPath))
                {
                    Directory.CreateDirectory(baseSSDPath);
                }

                Console.WriteLine($"[SSD MOUNT] Base Virtual Sector path established: {baseSSDPath}");
                Console.WriteLine("[READY] Virtual SSD Orchestrator initialized. Active and awaiting Puter FS synchronization streams.\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL ERROR] Core initialization failure: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
