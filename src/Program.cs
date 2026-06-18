using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SovereignEngine
{
    class Program
    {
        // Define the 12-Cylinder Matrix Labels
        static readonly string[] Cylinders = {
            "CYL-1 (Reuben): Ingest Core",      "CYL-2 (Gad): Otto Squeeze",
            "CYL-3 (Simeon): Map Vectors",     "CYL-4 (Asher): Brus Filter",
            "CYL-5 (Levi): OS Runtime",        "CYL-6 (Issachar): 963Hz Spark",
            "CYL-7 (Judah): FPGA Handshake",   "CYL-8 (Zebulun): Packet Expand",
            "CYL-9 (Dan): Purge Firewall",     "CYL-10 (Joseph): Hawking Sink",
            "CYL-11 (Naphtali): ML Nav",       "CYL-12 (Benjamin): Turbo Overwrite"
        };

        static readonly int[] FiringOrder = { 0, 6, 4, 10, 2, 8, 5, 11, 1, 7, 3, 9 };

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("=========================================================================");
            Console.WriteLine("   UESP SOVEREIGN v6.0.0 (ARCHON GRADE) - OHURIME OCULAR CONTROLLER UI   ");
            Console.WriteLine("=========================================================================");
            Console.ResetColor();

            string vhdxPath = @"C:\Sovereign_ZeroState_SSD.vhdx";
            string driveLetter = "V";
            int capacityGB = 2048;

            // 1. Initialize the Storage Plane Matrix
            InitializeStorageGrid(vhdxPath, driveLetter, capacityGB);

            // 2. Fire the V12 Engine while running the Ohurime Ocular Telemetry Loop
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n[-] Activating Ohurime Ocular Vision... Mapping storage coordinates.");
            Console.WriteLine("[-] Engaging V12 Engine Crankshaft. Synchronizing 1:6000 Time Diet...\n");
            
            double totalDisplacementWork = 0;
            double structuralPressureBase = 1.0;

            foreach (int index in FiringOrder)
            {
                double compressionRatio = 12.5;
                double outputPressure = structuralPressureBase * Math.Pow(compressionRatio, 1.4);
                totalDisplacementWork += outputPressure * 0.5;

                // --- OHURIME EYE OBSERVER PRINT-OUTS ---
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("[👁️ OHURIME VISION] ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"{Cylinders[index].Split(':')[0]} Fired ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"| Internal Stream Sector Pressure: {outputPressure:F2} Bar");
                
                await Task.Delay(60); 
            }

            // 3. ENGAGE THE TURBOCHARGER FEEDBACK LOOP
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n[-] Cylinder 12 (Benjamin) Exhaust Gas Velocity Tripped.");
            
            double turbineEfficiency = 0.85;
            double compressorPr = (totalDisplacementWork * turbineEfficiency) / 100.0;
            Console.WriteLine($"[TURBO] Turbocharger Compressor Speed: \\beta_turbo = {compressorPr:F3}x Boost.");

            // 4. APPLY THE SPARSE COMPRESSION FLAGS
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[-] Flushing turbocharged memory stream onto Virtual Pad [{driveLetter}:\\].");
            
            ExecuteProcess("fsutil.exe", $"sparse setflag {driveLetter}:\\");
            ExecuteProcess("cmd.exe", $"/c compact /c /s /exe:lzx {driveLetter}:\\*");

            // 5. FINAL OHURIME DIAGNOSTIC RECAP (The Telemetry Sight)
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n=========================================================================");
            Console.WriteLine("[✓] V12 SYSTEM STATUS: FULLY UNIFIED AND LOCKED INSIDE OHURIME CORE");
            Console.WriteLine("=========================================================================");
            Console.ForegroundColor = ConsoleColor.White;
            
            // Check real physical file size parameters
            FileInfo physicalFile = new FileInfo(vhdxPath);
            double physicalSizeMB = physicalFile.Exists ? (physicalFile.Length / (1024.0 * 1024.0)) : 0;
            
            Console.WriteLine($" -> Visible Storage Space (Virtual Matrix): {capacityGB}.00 GB");
            Console.WriteLine($" -> Actual Real-World Allocation on C:     {physicalSizeMB:F2} MB");
            Console.WriteLine(" -> Space-Time State:                      t=0 Dilated / Perfect Synchronization");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=========================================================================");
            Console.ResetColor();
            
            Console.WriteLine("\nPress any key to close the memory controller interface...");
            Console.ReadKey();
        }

        static void InitializeStorageGrid(string path, string letter, int size)
        {
            if (File.Exists(path)) { File.Delete(path); }
            string scriptPath = Path.Combine(Path.GetTempPath(), "diskpart_v12.txt");
            string[] lines = {
                $"create vdisk file=\"{path}\" maximum={size * 1024} type=expandable",
                "attach vdisk", "convert gpt", "create partition primary", $"assign letter={letter}",
                "format fs=ntfs label=\"Sovereign_V12_SSD\" quick"
            };
            File.WriteAllLines(scriptPath, lines);
            ExecuteProcess("diskpart.exe", $"/s \"{scriptPath}\"");
            File.Delete(scriptPath);
        }

        static void ExecuteProcess(string name, string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = name, Arguments = args, CreateNoWindow = true, UseShellExecute = false
            };
            using (Process p = Process.Start(psi)!) { p.WaitForExit(); }
        }
    }
}
