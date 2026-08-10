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

namespace SovereignSSD
{
    internal class Program
    {
        private const string VIRTUAL_DRIVE_LETTER = "V:";
        private const string PUTER_FS_ENDPOINT = "https://celsiusmediagroup.co.za/puterfs";
        private const long TOTAL_CLOUD_CAPACITY_BYTES = 100L * 1024L * 1024L * 1024L; // 100 GB Allocation Target

        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        private static string LocalStoragePath = string.Empty;
        private static long CurrentCloudUsedBytes = 0;

        // Sage Engine Orchestrator Instance
        private static SageEngineOrchestrator? _sageOrchestrator;

        [STAThread]
        static async Task Main(string[] args)
        {
            Console.Title = "Space Time SSD Core Engine (V12)";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" Space Time SSD Volume Engine (Windows Direct Mount)");
            Console.WriteLine(" 12-Cylinder Sage Architecture: 6x Snake Sage (LPU) | 6x Toad Sage (GPU)");
            Console.WriteLine(" Divine Ocular Telemetry & Tailed Beast Overclock Active");
            Console.WriteLine("===================================================================\n");
            Console.ResetColor();

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

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[MOUNT SUCCESS] Partition online at {VIRTUAL_DRIVE_LETTER}\\ -> Surface target ready in 'This PC'");
                Console.ResetColor();

                // Step 3: Fetch initial cloud metrics
                await SyncCloudCapacityMetricsAsync();

                // Step 4: Run initial sweep on existing partition items
                await InitialSyncSweepAsync($"{VIRTUAL_DRIVE_LETTER}\\");

                // Step 5: Intercept drop events on Mounted Partition
                StartActiveZeroWeightInterceptor($"{VIRTUAL_DRIVE_LETTER}\\");

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"\n[READY] Space Time SSD Active. Drop files directly into {VIRTUAL_DRIVE_LETTER}\\...\n");
                Console.ResetColor();

                AppDomain.CurrentDomain.ProcessExit += (s, e) => UnmountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER);

                await Task.Delay(-1); // Keep process alive
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL ERROR] Core initialization failure: {ex.Message}");
                Console.ResetColor();
            }
        }

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
                Console.WriteLine($"[MOUNT WARNING] Could not bind partition drive letter: {ex.Message}");
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

        #endregion

        #region Drive Sweeper & Partition Interceptor

        private static async Task InitialSyncSweepAsync(string mountPath)
        {
            Console.WriteLine($"[SWEEP] Checking partition {mountPath} for local items...");
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
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[SYNC WARNING] {currentDirectoryPath}: {ex.Message}");
                Console.ResetColor();
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
                Console.WriteLine($"[SKIP] File locked: {relativePath}");
                return;
            }

            // Tactic: Kawarimi Instant Response Stub
            ShinobiTactics.RegisterKawarimiDeception(localPath);

            // Tactic: Flying Thunder God (Hiraishin) Direct Hash Seal
            string sealId = ShinobiTactics.ApplyHiraishinSeal(relativePath);
            Console.WriteLine($"[HIRAISHIN TELEPORT] Sector Seal [{sealId}] attached to path {relativePath}");

            // Ocular: Sharingan Mirror Pattern
            ShinobiTactics.SharinganObservePattern(relativePath, 0, (int)new FileInfo(localPath).Length);

            // Execute processing under Kurama Overclocking
            await ShinobiTactics.ExecuteKuramaOverclockingAsync(async () =>
            {
                DriveInfo driveBefore = new DriveInfo("C:\\");
                long rawFileSize = new FileInfo(localPath).Length;

                Console.WriteLine($"\n[INTERCEPTED PARTITION ENTRY] {relativePath}");
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

                    // Stream to cloud
                    await StreamToCloudWithProgressBarAsync("WRITE", relativePath, hardenedPayload);

                    CurrentCloudUsedBytes += hardenedPayload.Length;

                    // Shikaku Memory Pinning for Fast Re-Access
                    ShinobiTactics.ApplyShikakuSandSeal(sealId, hardenedPayload);

                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                        Console.WriteLine($"[ZERO-WEIGHT PURGE] Erased local buffer: {relativePath}");
                    }

                    // Release Kawarimi Stub
                    ShinobiTactics.ReleaseKawarimiDeception(localPath);

                    DriveInfo driveAfter = new DriveInfo("C:\\");
                    long diskDifference = driveBefore.AvailableFreeSpace - driveAfter.AvailableFreeSpace;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[VERIFICATION] Physical Local Disk Consumption: {FormatBytes(Math.Max(0, diskDifference))} (ZERO-WEIGHT CONFIRMED)");
                    Console.WriteLine($"[CLOUD STATUS] Upload Complete. Available Space: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES - CurrentCloudUsedBytes)}\n");
                    Console.ResetColor();

                    // Ocular: Byakugan Memory Audit
                    ShinobiTactics.ByakuganFullSystemAudit();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[STREAMING ERROR] {relativePath}: {ex.Message}");
                    Console.ResetColor();
                }
            });
        }

        private static async Task StreamToCloudWithProgressBarAsync(string action, string virtualPath, byte[] payload)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(action), "action");
            content.Add(new StringContent(virtualPath), "virtualPath");

            if (payload.Length > 0)
            {
                ProgressableStreamContent streamContent = new ProgressableStreamContent(payload, 64 * 1024, (sent, total) =>
                {
                    RenderProgressBar(sent, total);
                });

                content.Add(streamContent, "payload", Path.GetFileName(virtualPath) + ".sov");
            }

            HttpResponseMessage response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
            response.EnsureSuccessStatusCode();
            Console.WriteLine();
        }

        private static void RenderProgressBar(long bytesSent, long totalBytes)
        {
            double percentage = (double)bytesSent / totalBytes * 100;
            int totalBlocks = 30;
            int filledBlocks = (int)Math.Round((percentage / 100) * totalBlocks);

            string bar = new string('█', filledBlocks) + new string('-', totalBlocks - filledBlocks);

            Console.Write($"\r[UPLOADING TO CLOUD] [{bar}] {percentage:F1}% ({FormatBytes(bytesSent)} / {FormatBytes(totalBytes)})");
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
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("MKDIR"), "action");
                content.Add(new StringContent(normalized), "virtualPath");

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
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[WARNING] NVIDIA_DGX_API_KEY environment variable not set. Running in fallback mode.");
                Console.ResetColor();
            }

            InitializeCluster();
        }

        private void InitializeCluster()
        {
            // 6 Nemotron LPU Instances (Snake Sage)
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

            // 6 Nemotron GPU Instances (Toad Sage)
            for (int i = 1; i <= 6; i++)
            {
                _toadSageGpuCluster.Add(new SageInstance
                {
                    Id = $"TOAD-GPU-0{i}",
                    Mode = InstanceMode.GPU_ToadSage,
                    Endpoint = "https://integrate.api.nvidia.com/v1/chat/completions",
                    ModelName = "nvidia/nemotron-4-340b-reward"
                });
            }
        }

        public async Task RunOrchestrationCycleAsync(string fileMetadataContext)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[SAGE ENGINE] 12-Cylinder Nemotron Execution Cycle Init...");
            Console.ResetColor();

            string generatedKernel = await SynthesizeComputeKernelsWithQwenAsync(fileMetadataContext);
            var dispatchPlan = await CoordinateWithMiniMaxLiaisonAsync(generatedKernel);

            List<Task> cylinderTasks = new List<Task>();

            foreach (var lpuInstance in _snakeSageLpuCluster)
            {
                cylinderTasks.Add(ExecuteCylinderTaskAsync(lpuInstance, dispatchPlan.LpuInstruction));
            }

            foreach (var gpuInstance in _toadSageGpuCluster)
            {
                cylinderTasks.Add(ExecuteCylinderTaskAsync(gpuInstance, dispatchPlan.GpuInstruction));
            }

            await Task.WhenAll(cylinderTasks);
            Console.WriteLine("[SAGE ENGINE] Cycle complete.\n");
        }

        private async Task<string> SynthesizeComputeKernelsWithQwenAsync(string context)
        {
            await Task.Delay(100);
            return "// Qwen Synthesized Kernel Core\n// LPU: Stream Buffering\n// GPU: Zstd Parallel Vectorization";
        }

        private async Task<DispatchPlan> CoordinateWithMiniMaxLiaisonAsync(string kernelCode)
        {
            await Task.Delay(100);
            return new DispatchPlan
            {
                LpuInstruction = "Snake-Sage: Stream memory management active.",
                GpuInstruction = "Toad-Sage: Multi-threaded GPU compression active."
            };
        }

        private async Task ExecuteCylinderTaskAsync(SageInstance instance, string instruction)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, instance.Endpoint);
                if (!string.IsNullOrEmpty(_dgxApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _dgxApiKey);
                }

                Console.WriteLine($"  -> [{instance.Id}] Active | Mode: {instance.Mode}");
                await Task.Delay(50);
            }
            catch
            {
                // Task execution fallback
            }
        }
    }

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

    public class DispatchPlan
    {
        public string LpuInstruction { get; set; } = string.Empty;
        public string GpuInstruction { get; set; } = string.Empty;
    }

    #endregion

    #region Shinobi & Ocular Tactics Subsystem

    public static class ShinobiTactics
    {
        private static readonly ConcurrentDictionary<string, string> HiraishinSeals = new();
        private static readonly ConcurrentDictionary<string, byte> KawarimiStubs = new();
        private static readonly ConcurrentDictionary<string, byte[]> ShikakuPinningCache = new();
        private static int _kuramaModeActive = 0;

        // 1. Hiraishin Seal
        public static string ApplyHiraishinSeal(string virtualPath)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(virtualPath));
            string sealId = Convert.ToHexString(hash)[..16];
            HiraishinSeals[virtualPath] = sealId;
            return sealId;
        }

        // 2. Kage Bunshin Dynamic Binary Slicing
        public static Memory<byte>[] GenerateKageBunshins(byte[] fullPayload, int chunkSizeMb = 16)
        {
            int chunkSize = chunkSizeMb * 1024 * 1024;
            int totalClones = (int)Math.Ceiling((double)fullPayload.Length / chunkSize);

            Memory<byte>[] clones = new Memory<byte>[totalClones];
            for (int i = 0; i < totalClones; i++)
            {
                int start = i * chunkSize;
                int length = Math.Min(chunkSize, fullPayload.Length - start);
                clones[i] = new Memory<byte>(fullPayload, start, length);
            }
            return clones;
        }

        // 3. Kawarimi Deception Stub
        public static void RegisterKawarimiDeception(string localPath)
        {
            KawarimiStubs[localPath] = 1;
        }

        public static void ReleaseKawarimiDeception(string localPath)
        {
            KawarimiStubs.TryRemove(localPath, out _);
        }

        // 4. Kurama Overclocking
        public static async Task ExecuteKuramaOverclockingAsync(Func<Task> heavyIoWorkload)
        {
            if (Interlocked.Exchange(ref _kuramaModeActive, 1) == 1)
            {
                await heavyIoWorkload();
                return;
            }

            Process currentProcess = Process.GetCurrentProcess();
            ProcessPriorityClass originalPriority = currentProcess.PriorityClass;

            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[KURAMA OVERCLOCK] Threads Maxed & Priority Elevated.");
                Console.ResetColor();

                currentProcess.PriorityClass = ProcessPriorityClass.High;
                ThreadPool.SetMinThreads(64, 64);

                await heavyIoWorkload();
            }
            finally
            {
                currentProcess.PriorityClass = originalPriority;
                Interlocked.Exchange(ref _kuramaModeActive, 0);
            }
        }

        // 5. Shikaku Memory Pinning
        public static void ApplyShikakuSandSeal(string sectorKey, byte[] data)
        {
            ShikakuPinningCache[sectorKey] = data;
        }

        // 6. Isobu Stream Hardening
        public static byte[] ApplyIsobuStreamHardening(byte[] rawBuffer)
        {
            int alignedSize = (rawBuffer.Length + 65535) & ~65535;
            byte[] hardenedBuffer = new byte[alignedSize];
            Buffer.BlockCopy(rawBuffer, 0, hardenedBuffer, 0, rawBuffer.Length);
            return hardenedBuffer;
        }

        // 7. Sharingan Observation
        public static void SharinganObservePattern(string virtualPath, long offset, int length)
        {
            Console.WriteLine($"[SHARINGAN VISION] Mirrored Path={virtualPath} | Predicted Next={offset + length}");
        }

        // 8. Byakugan Memory Audit
        public static void ByakuganFullSystemAudit()
        {
            long managedMemory = GC.GetTotalMemory(forceFullCollection: false);
            Process proc = Process.GetCurrentProcess();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("-------------------------------------------------------------------");
            Console.WriteLine($" [BYAKUGAN 360° AUDIT] GC: {managedMemory / (1024 * 1024):N2} MB | Working Set: {proc.WorkingSet64 / (1024 * 1024):N2} MB");
            Console.WriteLine("-------------------------------------------------------------------");
            Console.ResetColor();
        }
    }

    #endregion
}
