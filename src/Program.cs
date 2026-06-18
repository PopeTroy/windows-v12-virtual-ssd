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
            Console.WriteLine("     UESP SOVEREIGN v6.0.0 (ARCHON GRADE) - WINDOWS SSD INITIALIZER      ");
            Console.WriteLine("=========================================================================");
            Console.ResetColor();

            string vhdxPath = @"C:\Sovereign_Virtual_SSD.vhdx";
            string driveLetter = "V";
            int capacityGB = 200; // Change this number to alter your Virtual SSD capacity limit

            try
            {
                if (File.Exists(vhdxPath))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[!] Overlapping storage metric detected. Purging target container file...");
                    File.Delete(vhdxPath);
                }

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"[-] Slicing a {capacityGB}GB dynamic virtual SSD allocation space...");

                string command = $"New-VHD -Path '{vhdxPath}' -SizeBytes {capacityGB}GB -Dynamic | Mount-VHD -Passthru | Initialize-Disk -PartitionStyle GPT -Passthru | New-Partition -DriveLetter {driveLetter} -UseMaximumSize | Format-Volume -FileSystem NTFS -NewFileSystemLabel 'Sovereign_V12_SSD' -Confirm:$false";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
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
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\n[SUCCESS] Virtual SSD active and mapped cleanly to Drive [{driveLetter}:\\].");
                    }
                    else
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception(error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[FATAL ERROR] System Matrix Failure: {ex.Message}");
            }

            Console.ResetColor();
            Console.WriteLine("\nPress any key to close the execution terminal...");
            Console.ReadKey();
        }
    }
}
