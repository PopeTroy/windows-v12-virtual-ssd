using System;
using System.Diagnostics;
using System.IO;

namespace SovereignEngine
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=========================================================================");
            Console.WriteLine("     UESP SOVEREIGN v6.0.0 (ARCHON GRADE) - DUAL-ENGINE SSD INITIALIZER ");
            Console.WriteLine("=========================================================================");
            Console.ResetColor();

            string vhdxPath = @"C:\Sovereign_Virtual_SSD.vhdx";
            string driveLetter = "V";
            int capacityGB = 200;

            // Step 1: Clear any collision anomalies
            try
            {
                if (File.Exists(vhdxPath))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[!] Overlapping storage metric detected. Purging target container file...");
                    File.Delete(vhdxPath);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Failed to clear existing volume file: {ex.Message}");
            }

            // Step 2: Attempt Primary Engine Allocation (PowerShell Hyper-V Module)
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[-] [ENGINE BANK 1] Attempting high-performance VHDX slicing ({capacityGB}GB)...");

            bool primaryEngineSuccess = false;
            try
            {
                string psCommand = $"New-VHD -Path '{vhdxPath}' -SizeBytes {capacityGB}GB -Dynamic | Mount-VHD -Passthru | Initialize-Disk -PartitionStyle GPT -Passthru | New-Partition -DriveLetter {driveLetter} -UseMaximumSize | Format-Volume -FileSystem NTFS -NewFileSystemLabel 'Sovereign_V12_SSD' -Confirm:$false";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi)!)
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        primaryEngineSuccess = true;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\n[SUCCESS] Primary VHDX Matrix active and mapped cleanly to Drive [{driveLetter}:\\].");
                    }
                    else
                    {
                        // Throw exception to trigger the failover catch block below
                        string errMessage = process.StandardError.ReadToEnd();
                        throw new Exception(errMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[!] Primary Engine Alert: Native Hyper-V / New-VHD architecture is unavailable.");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"(Technical Root: {ex.Message.Trim()})");
            }

            // Step 3: Failover Protection Matrix (Universal DiskPart Subsystem)
            if (!primaryEngineSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[-] [ENGINE BANK 2] Engaging secondary failover protocol (Universal DiskPart)...");
                Console.ForegroundColor = ConsoleColor.White;

                try
                {
                    string scriptPath = Path.Combine(Path.GetTempPath(), "diskpart_failover.txt");
                    string[] diskpartLines = {
                        $"create vdisk file=\"{vhdxPath}\" maximum={capacityGB * 1024} type=expandable",
                        "attach vdisk",
                        "convert gpt",
                        "create partition primary",
                        $"assign letter={driveLetter}",
                        "format fs=ntfs label=\"Sovereign_V12_SSD\" quick"
                    };
                    File.WriteAllLines(scriptPath, diskpartLines);

                    ProcessStartInfo psiFallback = new ProcessStartInfo
                    {
                        FileName = "diskpart.exe",
                        Arguments = $"/s \"{scriptPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process processFallback = Process.Start(psiFallback)!)
                    {
                        processFallback.WaitForExit();
                        File.Delete(scriptPath); // Clean temporary runtime instructions

                        if (processFallback.ExitCode == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\n[SUCCESS] Failover complete. Universal Virtual SSD initialized on Drive [{driveLetter}:\\].");
                        }
                        else
                        {
                            string finalError = processFallback.StandardError.ReadToEnd();
                            throw new Exception(finalError);
                        }
                    }
                }
                catch (Exception finalEx)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[FATAL ERROR] Total System Failure. Both storage engines failed: {finalEx.Message}");
                }
            }

            Console.ResetColor();
            Console.WriteLine("\nPress any key to close the memory controller interface...");
            Console.ReadKey();
        }
    }
}
