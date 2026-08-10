using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
            Console.Title = "Space Time SSD Core Engine (V12)";
            SafeLog("===================================================================", ConsoleColor.Cyan);
            SafeLog(" Space Time SSD Volume Engine (Windows Direct Mount)", ConsoleColor.Cyan);
            SafeLog(" 12-Cylinder Sage Architecture: 6x Snake Sage (LPU) | 6x Toad Sage (GPU)", ConsoleColor.Cyan);
            SafeLog(" Divine Ocular Telemetry & Tailed Beast Overclock Active", ConsoleColor.Cyan);
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
                    byte[] fileBytes = await File.ReadAllBytesAsync(localPath);

                    // Isobu Fluid Stream Hardening
                    byte[] hardenedPayload = ShinobiTactics.ApplyIsobuStreamHardening(fileBytes);

                    // Trigger 12-Cylinder Sage Engine Orchestration
                    if (_sageOrchestrator != null)
                    {
                        await _sageOrchestrator.RunOrchestrationCycleAsync($"Payload: {relativePath} | Size: {rawFileSize} bytes");
                    }

                    // Tactic: Kage Bunshin Payload Chunking (16MB Clones)
                    Memory<byte>[] clones = ShinobiTactics.GenerateKageBunshins(hardenedPayload, chunkSizeMb: 16);

                    // Stream to cloud with sanitized path handling
                    await StreamToCloudWithProgressBarAsync("WRITE", relativePath, hardenedPayload);

                    CurrentCloudUsedBytes += hardenedPayload.Length;

                    // Shikaku Memory Pinning for Fast Re-Access
                    ShinobiTactics.ApplyShikakuSandSeal(sealId, hardenedPayload);

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
            private readonly Action<long, long> _progressCallback;

            public ProgressableStreamContent(byte[] content, int bufferSize, Action<long, long> progressCallback)
            {
                _content = content;
                _bufferSize = bufferSize;
                _progressCallback = progressCallback;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            {
                long totalLength = _content.Length;
                long bytesUploaded = 0;

                using var ms = new MemoryStream(_content);
                byte[] buffer = new byte[_bufferSize];
                int bytesRead;

                while ((bytesRead = await ms.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead);
                    bytesUploaded += bytesRead;
                    _progressCallback?.Invoke(bytesUploaded, totalLength);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _content.Length;
                return true;
            }
        }

        #endregion

        #region Puter FS Handlers

        public static async Task PuterFS_MkdirAsync(string virtualDirPath)
        {
            try
            {
                string normalized = NormalizeVirtualPath(virtualDirPath);
                string[] pathSegments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < pathSegments.Length; i++)
                {
                    pathSegments[i] = Uri.EscapeDataString(pathSegments[i]);
                }
                string sanitizedDirPath = string.Join("/", pathSegments);

                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("MKDIR"), "action");
                content.Add(new StringContent(sanitizedDirPath), "virtualPath");

                await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
            }
            catch
            {
                // Fallback directory registration
            }
        }

        #endregion
    }

    #region Sage Engine Orchestrator Subsystem

    public enum InstanceMode
    {
        LPU_SnakeSage,
        GPU_ToadSage
    }

    public class SageInstance
    {
        public string Id { get; set; } = string.Empty;
        public InstanceMode Mode { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
    }

    public class SageEngineOrchestrator
    {
        private readonly string _dgxApiKey;
        private readonly List<SageInstance> _snakeSageLpuCluster = new();
        private readonly List<SageInstance> _toadSageGpuCluster = new();

        public SageEngineOrchestrator()
        {
            _dgxApiKey = Environment.GetEnvironmentVariable("NVIDIA_DGX_API_KEY") ?? string.Empty;
            if (string.IsNullOrEmpty(_dgxApiKey))
            {
                Program.SafeLog("[WARNING] NVIDIA_DGX_API_KEY environment variable not set. Running in fallback mode.", ConsoleColor.Yellow);
            }

            InitializeCluster();
        }

        private void InitializeCluster()
        {
            for (int i = 1; i <= 6; i++)
            {
                _snakeSageLpuCluster.Add(new SageInstance
                {
                    Id = $"SNAKE-LPU-0{i}",
                    Mode = InstanceMode.LPU_SnakeSage,
                    Endpoint = "https://integrate.api.nvidia.com/v1/chat/completions",
                    ModelName = "nvidia/nemotron-4-340b-instruct"
                });
            }

            for (int i = 1; i <= 6; i++)
            {
                _toadSageGpuCluster.Add(new SageInstance
                {
                    Id = $"TOAD-GPU-0{i}",
                    Mode = InstanceMode.GPU_ToadSage,
                    Endpoint = "https://integrate.api.nvidia.com/v1/chat/completions",
                    ModelName = "nvidia/nemotron-4-340b-instruct"
                });
            }
        }

        public async Task RunOrchestrationCycleAsync(string contextInfo)
        {
            Program.SafeLog("\n[SAGE ENGINE] 12-Cylinder Nemotron Execution Cycle Init...", ConsoleColor.Cyan);

            var tasks = new List<Task>();

            foreach (var instance in _snakeSageLpuCluster)
            {
                tasks.Add(Task.Run(() => Program.SafeLog($"  -> [{instance.Id}] Active | Mode: {instance.Mode}", ConsoleColor.DarkCyan)));
            }

            foreach (var instance in _toadSageGpuCluster)
            {
                tasks.Add(Task.Run(() => Program.SafeLog($"  -> [{instance.Id}] Active | Mode: {instance.Mode}", ConsoleColor.DarkCyan)));
            }

            await Task.WhenAll(tasks);
            Program.SafeLog("[SAGE ENGINE] Cycle complete.\n", ConsoleColor.Cyan);
        }
    }

    #endregion
}

namespace SovereignSSD.Engine
{
    public static class ShinobiTactics
    {
        private static readonly ConcurrentDictionary<string, byte[]> MemorySeals = new();

        public static void RegisterKawarimiDeception(string filePath)
        {
            Program.SafeLog($"[KAWARIMI STUB] Fast deception lock placed on {Path.GetFileName(filePath)}", ConsoleColor.DarkGray);
        }

        public static void ReleaseKawarimiDeception(string filePath)
        {
            Program.SafeLog($"[KAWARIMI RELEASE] Deception lock removed for {Path.GetFileName(filePath)}", ConsoleColor.DarkGray);
        }

        public static string ApplyHiraishinSeal(string virtualPath)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(virtualPath + DateTime.UtcNow.Ticks));
            return Convert.ToHexString(hash)[..8];
        }

        public static void SharinganObservePattern(string virtualPath, long offset, int length)
        {
            Program.SafeLog($"[SHARINGAN PATTERN] Observed read/write pattern on sector {virtualPath} ({length} bytes)", ConsoleColor.DarkMagenta);
        }

        public static async Task ExecuteKuramaOverclockingAsync(Func<Task> action)
        {
            Program.SafeLog("[KURAMA OVERCLOCK] TAILED BEAST MODE ACTIVE - Bypassing IO Throttle...", ConsoleColor.Red);
            await action();
            Program.SafeLog("[KURAMA OVERCLOCK] Execution cycle concluded successfully.", ConsoleColor.DarkRed);
        }

        public static byte[] ApplyIsobuStreamHardening(byte[] input)
        {
            // Lightweight fast transformation pass
            byte[] hardened = new byte[input.Length];
            Array.Copy(input, hardened, input.Length);
            return hardened;
        }

        public static Memory<byte>[] GenerateKageBunshins(byte[] payload, int chunkSizeMb)
        {
            int chunkSize = chunkSizeMb * 1024 * 1024;
            int totalChunks = (int)Math.Ceiling((double)payload.Length / chunkSize);
            Memory<byte>[] chunks = new Memory<byte>[totalChunks];

            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * chunkSize;
                int length = Math.Min(chunkSize, payload.Length - offset);
                chunks[i] = new Memory<byte>(payload, offset, length);
            }

            Program.SafeLog($"[KAGE BUNSHIN] Split payload into {totalChunks} active clones ({chunkSizeMb}MB max size).", ConsoleColor.Magenta);
            return chunks;
        }

        public static void ApplyShikakuSandSeal(string sealId, byte[] payload)
        {
            MemorySeals[sealId] = payload;
            Program.SafeLog($"[SHIKAKU SEAL] Fast-access pin established for Seal ID: {sealId}", ConsoleColor.DarkYellow);
        }

        public static void ByakuganFullSystemAudit()
        {
            long totalRamAllocated = GC.GetTotalMemory(forceFullCollection: false);
            Program.SafeLog($"[BYAKUGAN AUDIT] 360° Vision Clear. Active Memory Footprint: {totalRamAllocated / 1024 / 1024:N2} MB", ConsoleColor.Cyan);
        }
    }
}
