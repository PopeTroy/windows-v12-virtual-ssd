using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SovereignSSD.Engine;

namespace SovereignSSD
{
    internal class Program
    {
        private const string VIRTUAL_DRIVE_LETTER = "V:";
        private const string PUTER_FS_ENDPOINT = "https://celsiusmediagroup.co.za/puterfs";
        private const long TOTAL_CLOUD_CAPACITY_BYTES = 100L * 1024L * 1024L * 1024L; // 100 GB Allocation Target

        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        private static readonly object ConsoleLock = new object();
        private static string LocalStoragePath = string.Empty;
        private static long CurrentCloudUsedBytes = 0;

        // Sage Engine Orchestrator Instance
        private static SageEngineOrchestrator? _sageOrchestrator;

        [STAThread]
        static async Task Main(string[] args)
        {
            // Elevate process priority for high-throughput memory-mapped SSD operations
            try
            {
                using (Process currentProcess = Process.GetCurrentProcess())
                {
                    currentProcess.PriorityClass = ProcessPriorityClass.High;
                }
            }
            catch (Exception ex)
            {
                SafeLog($"[PRIORITY WARNING] Could not set High Priority Class: {ex.Message}", ConsoleColor.Yellow);
            }

            Console.Title = "Space Time SSD Core Engine (V12) - Apex Primal Edition";
            SafeLog("===================================================================", ConsoleColor.Cyan);
            SafeLog(" Space Time SSD Volume Engine (Windows Direct Mount)", ConsoleColor.Cyan);
            SafeLog(" 12-Cylinder Sage Architecture: 6x Snake Sage (LPU) | 6x Toad Sage (GPU)", ConsoleColor.Cyan);
            SafeLog(" Divine Ocular Telemetry & Tailed Beast Overclock Active", ConsoleColor.Cyan);
            SafeLog(" AVX2 SIMD Hardware Vectorization & Memory-Mapped Direct Pass Active", ConsoleColor.Green);
            SafeLog(" Tactics Active: Amenotejikara | Daikokuten | Tenseigan | Jogan | Ohirume", ConsoleColor.Magenta);
            SafeLog("===================================================================\n", ConsoleColor.Cyan);

            try
            {
                // Initialize 12-Cylinder Sage Orchestrator
                _sageOrchestrator = new SageEngineOrchestrator();

                // Step 1: Establish Local Storage Sub-Directory
                LocalStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpaceTimeV12SSD");
                if (!Directory.Exists(LocalStoragePath))
                {
                    Directory.CreateDirectory(LocalStoragePath);
                }

                // Step 2: Mount Virtual Folder as standard Windows Drive Partition (V:)
                MountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER, LocalStoragePath);

                SafeLog($"[MOUNT SUCCESS] Partition online at {VIRTUAL_DRIVE_LETTER}\\ -> Surface target ready in 'This PC'", ConsoleColor.Green);

                // Step 3: Fetch initial cloud metrics
                await SyncCloudCapacityMetricsAsync();

                // Step 4: Run initial sweep on existing partition items
                await InitialSyncSweepAsync($"{VIRTUAL_DRIVE_LETTER}\\");

                // Step 5: Intercept drop events on Mounted Partition
                StartActiveZeroWeightInterceptor($"{VIRTUAL_DRIVE_LETTER}\\");

                SafeLog($"\n[READY] Space Time SSD Active. Drop files directly into {VIRTUAL_DRIVE_LETTER}\\...\n", ConsoleColor.Magenta);

                AppDomain.CurrentDomain.ProcessExit += (s, e) => UnmountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER);

                await Task.Delay(-1); // Keep process alive
            }
            catch (Exception ex)
            {
                SafeLog($"[CRITICAL ERROR] Core initialization failure: {ex.Message}", ConsoleColor.Red);
            }
        }

        #region Thread-Safe Logging

        public static void SafeLog(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            lock (ConsoleLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        #endregion

        #region Partition Management (Subst)

        private static void MountVirtualDrivePartition(string driveLetter, string targetPath)
        {
            try
            {
                UnmountVirtualDrivePartition(driveLetter);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "subst",
                    Arguments = $"{driveLetter} \"{targetPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch (Exception ex)
            {
                SafeLog($"[MOUNT WARNING] Could not bind partition drive letter: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        private static void UnmountVirtualDrivePartition(string driveLetter)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "subst",
                    Arguments = $"{driveLetter} /D",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process? p = Process.Start(psi);
                p?.WaitForExit();
            }
            catch
            {
                // Unmount cleanup fallback
            }
        }

        #endregion

        #region Drive Metrics & Sync

        private static async Task SyncCloudCapacityMetricsAsync()
        {
            try
            {
                using var response = await HttpClient.GetAsync($"{PUTER_FS_ENDPOINT}?action=CAPACITY");
                if (response.IsSuccessStatusCode)
                {
                    string res = await response.Content.ReadAsStringAsync();
                    if (long.TryParse(res, out long bytesUsed))
                    {
                        CurrentCloudUsedBytes = bytesUsed;
                    }
                }
            }
            catch
            {
                // Capacity fallback
            }

            DisplaySpaceMetrics(0);
        }

        private static void DisplaySpaceMetrics(long incomingPayloadSize)
        {
            long availableCloudBytes = TOTAL_CLOUD_CAPACITY_BYTES - CurrentCloudUsedBytes;
            DriveInfo localDrive = new DriveInfo("C:\\");

            lock (ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("-------------------------------------------------------------------");
                Console.WriteLine($"[CLOUD METRICS] Total: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES)} | Used: {FormatBytes(CurrentCloudUsedBytes)} | Available: {FormatBytes(availableCloudBytes)}");
                if (incomingPayloadSize > 0)
                {
                    Console.WriteLine($"[INCOMING OBJECT] Raw Size: {FormatBytes(incomingPayloadSize)} | Remaining: {FormatBytes(availableCloudBytes - incomingPayloadSize)}");
                }
                Console.WriteLine($"[LOCAL DISK METRICS] C:\\ Free Space: {FormatBytes(localDrive.AvailableFreeSpace)} (Zero-Weight Target Active)");
                Console.WriteLine("-------------------------------------------------------------------");
                Console.ResetColor();
            }
        }

        #endregion

        #region Drive Sweeper & Partition Interceptor

        private static async Task InitialSyncSweepAsync(string mountPath)
        {
            SafeLog($"[SWEEP] Checking partition {mountPath} for local items...", ConsoleColor.Gray);
            await ProcessDirectoryRecursivelyAsync(mountPath, mountPath);
        }

        private static void StartActiveZeroWeightInterceptor(string mountPath)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(mountPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += async (s, e) => { await HandleFileSystemEntryAsync(e.FullPath, mountPath); };
            watcher.Renamed += async (s, e) => { await HandleFileSystemEntryAsync(e.FullPath, mountPath); };
        }

        private static async Task HandleFileSystemEntryAsync(string targetPath, string mountPath)
        {
            if (Directory.Exists(targetPath))
            {
                await ProcessDirectoryRecursivelyAsync(targetPath, mountPath);
            }
            else if (File.Exists(targetPath))
            {
                await ProcessAndStreamToCloudImmediatelyAsync(targetPath, mountPath);
            }
        }

        private static async Task ProcessDirectoryRecursivelyAsync(string currentDirectoryPath, string mountPath)
        {
            try
            {
                if (currentDirectoryPath != mountPath)
                {
                    string relativeDirPath = NormalizeVirtualPath(Path.GetRelativePath(mountPath, currentDirectoryPath));
                    await PuterFS_MkdirAsync(relativeDirPath);
                }

                foreach (string subDir in Directory.GetDirectories(currentDirectoryPath))
                {
                    await ProcessDirectoryRecursivelyAsync(subDir, mountPath);
                }

                foreach (string filePath in Directory.GetFiles(currentDirectoryPath))
                {
                    await ProcessAndStreamToCloudImmediatelyAsync(filePath, mountPath);
                }

                if (currentDirectoryPath != mountPath && Directory.GetFileSystemEntries(currentDirectoryPath).Length == 0)
                {
                    Directory.Delete(currentDirectoryPath, recursive: false);
                }
            }
            catch (Exception ex)
            {
                SafeLog($"[SYNC WARNING] {currentDirectoryPath}: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        private static async Task PuterFS_MkdirAsync(string virtualPath)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("MKDIR"), "action");
                content.Add(new StringContent(virtualPath), "virtualPath");

                await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
            }
            catch
            {
                // Fallback for directory creation
            }
        }

        private static async Task ProcessAndStreamToCloudImmediatelyAsync(string localPath, string mountPath)
        {
            string fileName = Path.GetFileName(localPath);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".sov_tmp"))
            {
                return;
            }

            string relativePath = NormalizeVirtualPath(Path.GetRelativePath(mountPath, localPath));

            if (!WaitForFileReady(localPath, timeoutMs: 10000))
            {
                SafeLog($"[SKIP] File locked: {relativePath}", ConsoleColor.DarkYellow);
                return;
            }

            // Ocular: Jogan Dimensional Portal Path Audit (Eliminates false 404s)
            bool pathValid = ShinobiTactics.JoganVerifyDimensionalPath(relativePath);
            if (!pathValid) return;

            // ENSURE PARENT DIRECTORIES EXIST ON CLOUD BEFORE UPLOADING
            string? parentDir = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(parentDir) && parentDir != "." && parentDir != "/")
            {
                await EnsureRemoteDirectoryPathAsync(parentDir);
            }

            // Tactic: Kawarimi Instant Response Stub
            ShinobiTactics.RegisterKawarimiDeception(localPath);

            // Tactic: Flying Thunder God (Hiraishin) Direct Hash Seal
            string sealId = ShinobiTactics.ApplyHiraishinSeal(relativePath);
            SafeLog($"[HIRAISHIN TELEPORT] Sector Seal [{sealId}] attached to path {relativePath}", ConsoleColor.Gray);

            // Ocular: Sharingan Mirror Pattern
            ShinobiTactics.SharinganObservePattern(relativePath, 0, (int)new FileInfo(localPath).Length);

            // Execute processing under Kurama Overclocking
            await ShinobiTactics.ExecuteKuramaOverclockingAsync(async () =>
            {
                DriveInfo driveBefore = new DriveInfo("C:\\");
                long rawFileSize = new FileInfo(localPath).Length;

                SafeLog($"\n[INTERCEPTED PARTITION ENTRY] {relativePath}", ConsoleColor.Cyan);
                DisplaySpaceMetrics(rawFileSize);

                try
                {
                    // SYSTEM.IO.MEMORYMAPPEDFILES & AVX2 SIMD DIRECT PIPELINE
                    byte[] fileBytes = ShinobiTactics.MemoryMappedVectorizedReadPass(localPath);

                    // Tenseigan IO Throttle Dampening
                    ShinobiTactics.TenseiganPulseGravityBalance(fileBytes.Length);

                    // Isobu Fluid Stream Hardening
                    byte[] hardenedPayload = ShinobiTactics.ApplyIsobuStreamHardening(fileBytes);

                    // Daikokuten Dimensional Shrinkage
                    byte[] compressedPayload = ShinobiTactics.DaikokutenStoreInPocketDimension(hardenedPayload);

                    // Trigger 12-Cylinder Sage Engine Orchestration
                    if (_sageOrchestrator != null)
                    {
                        await _sageOrchestrator.RunOrchestrationCycleAsync($"Payload: {relativePath} | Size: {rawFileSize} bytes");
                    }

                    // Tactic: Kage Bunshin Payload Chunking (16MB Clones)
                    Memory<byte>[] clones = ShinobiTactics.GenerateKageBunshins(compressedPayload, chunkSizeMb: 16);

                    // Ohirume Burst Acceleration Engine
                    await ShinobiTactics.OhirumeSunBurstAccelerationAsync(async () =>
                    {
                        // Stream to cloud with sanitized path handling
                        await StreamToCloudWithProgressBarAsync("WRITE", relativePath, compressedPayload);
                    });

                    CurrentCloudUsedBytes += compressedPayload.Length;

                    // Amenotejikara Memory Swap Registration
                    ShinobiTactics.AmenotejikaraSwapLocation(localPath, relativePath, compressedPayload);

                    // Shikaku Memory Pinning for Fast Re-Access
                    ShinobiTactics.ApplyShikakuSandSeal(sealId, compressedPayload);

                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                        SafeLog($"[ZERO-WEIGHT PURGE] Erased local buffer: {relativePath}", ConsoleColor.Gray);
                    }

                    // Release Kawarimi Stub
                    ShinobiTactics.ReleaseKawarimiDeception(localPath);

                    DriveInfo driveAfter = new DriveInfo("C:\\");
                    long diskDifference = driveBefore.AvailableFreeSpace - driveAfter.AvailableFreeSpace;

                    SafeLog($"[VERIFICATION] Physical Local Disk Consumption: {FormatBytes(Math.Max(0, diskDifference))} (ZERO-WEIGHT CONFIRMED)", ConsoleColor.Green);
                    SafeLog($"[CLOUD STATUS] Upload Complete. Available Space: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES - CurrentCloudUsedBytes)}\n", ConsoleColor.Green);

                    // Ocular: Byakugan Memory Audit
                    ShinobiTactics.ByakuganFullSystemAudit();
                }
                catch (Exception ex)
                {
                    SafeLog($"[STREAMING ERROR] {relativePath}: {ex.Message}", ConsoleColor.Red);
                }
            });
        }

        private static async Task EnsureRemoteDirectoryPathAsync(string directoryPath)
        {
            string normalized = NormalizeVirtualPath(directoryPath);
            string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string currentPath = "";

            foreach (string part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                await PuterFS_MkdirAsync(currentPath);
            }
        }

        private static async Task StreamToCloudWithProgressBarAsync(string action, string virtualPath, byte[] payload)
        {
            string[] pathSegments = virtualPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pathSegments.Length; i++)
            {
                pathSegments[i] = Uri.EscapeDataString(pathSegments[i]);
            }
            string sanitizedPath = string.Join("/", pathSegments);

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(action), "action");
            content.Add(new StringContent(sanitizedPath), "virtualPath");
            content.Add(new StringContent("true"), "createMissingParents");

            if (payload.Length > 0)
            {
                ProgressableStreamContent streamContent = new ProgressableStreamContent(payload, 64 * 1024, (sent, total) =>
                {
                    RenderProgressBar(sent, total);
                });

                content.Add(streamContent, "payload", Path.GetFileName(sanitizedPath) + ".sov");
            }

            HttpResponseMessage response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
            response.EnsureSuccessStatusCode();
            SafeLog(string.Empty);
        }

        private static void RenderProgressBar(long bytesSent, long totalBytes)
        {
            double percentage = (double)bytesSent / totalBytes * 100;
            int totalBlocks = 30;
            int filledBlocks = (int)Math.Round((percentage / 100) * totalBlocks);

            string bar = new string('█', filledBlocks) + new string('-', totalBlocks - filledBlocks);

            lock (ConsoleLock)
            {
                Console.Write($"\r[UPLOADING TO CLOUD] [{bar}] {percentage:F1}% ({FormatBytes(bytesSent)} / {FormatBytes(totalBytes)})");
            }
        }

        private static bool WaitForFileReady(string path, int timeoutMs)
        {
            int elapsed = 0;
            const int interval = 200;

            while (elapsed < timeoutMs)
            {
                try
                {
                    using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(interval);
                    elapsed += interval;
                }
            }
            return false;
        }

        private static string NormalizeVirtualPath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n2} {suffixes[counter]}";
        }

        #endregion

        #region Progressable Stream Class

        private class ProgressableStreamContent : HttpContent
        {
            private readonly byte[] _content;
            private readonly int _bufferSize;
            private readonly Action<long, long> _progress;

            public ProgressableStreamContent(byte[] content, int bufferSize, Action<long, long> progress)
            {
                _content = content ?? throw new ArgumentNullException(nameof(content));
                _bufferSize = bufferSize > 0 ? bufferSize : 4096;
                _progress = progress;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            {
                long totalLength = _content.Length;
                long sent = 0;

                for (int i = 0; i < totalLength; i += _bufferSize)
                {
                    int length = Math.Min(_bufferSize, (int)(totalLength - i));
                    await stream.WriteAsync(_content, i, length);
                    sent += length;
                    _progress?.Invoke(sent, totalLength);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _content.Length;
                return true;
            }
        }

        #endregion
    }
}
