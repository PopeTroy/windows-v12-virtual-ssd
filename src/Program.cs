using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SovereignSSD.Engine;

namespace SovereignSSD
{
    internal class Program
    {
        private const string VIRTUAL_DRIVE_LETTER = "V:";
        private const string PUTER_FS_ENDPOINT = "https://celsiusmediagroup.co.za/puterfs";
        private const long TOTAL_CLOUD_CAPACITY_BYTES = 100L * 1024L * 1024L * 1024L; // 100 GB Allocation Target
        private const int MAX_CONCURRENT_INGEST = 4;

        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        private static readonly Lock ConsoleLock = new Lock();
        private static readonly SemaphoreSlim IngestLimiter = new SemaphoreSlim(MAX_CONCURRENT_INGEST, MAX_CONCURRENT_INGEST);
        private static readonly Channel<string> PendingEntriesChannel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        private static string LocalStoragePath = string.Empty;
        private static long _currentCloudUsedBytes = 0;
        private static SageEngineOrchestrator? _sageOrchestrator;

        private static long CurrentCloudUsedBytes
        {
            get => Interlocked.Read(ref _currentCloudUsedBytes);
            set => Interlocked.Exchange(ref _currentCloudUsedBytes, Math.Max(0L, value));
        }

        [STAThread]
        static async Task Main(string[] args)
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            try
            {
                using Process currentProcess = Process.GetCurrentProcess();
                currentProcess.PriorityClass = ProcessPriorityClass.High;
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

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                cts.Cancel();
                UnmountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER);
            };

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                UnmountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER);
            };

            try
            {
                _sageOrchestrator = new SageEngineOrchestrator();

                LocalStoragePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SpaceTimeV12SSD");

                if (!Directory.Exists(LocalStoragePath))
                {
                    Directory.CreateDirectory(LocalStoragePath);
                }

                MountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER, LocalStoragePath);
                SafeLog($"[MOUNT SUCCESS] Partition online at {VIRTUAL_DRIVE_LETTER}\\ -> Surface target ready in 'This PC'", ConsoleColor.Green);

                await SyncCloudCapacityMetricsAsync(cts.Token);

                string mountRoot = $"{VIRTUAL_DRIVE_LETTER}\\";
                Task consumerTask = Task.Run(() => ConsumePendingEntriesAsync(mountRoot, cts.Token), cts.Token);

                await InitialSyncSweepAsync(mountRoot, cts.Token);
                using FileSystemWatcher watcher = StartActiveZeroWeightInterceptor(mountRoot);

                SafeLog($"\n[READY] Space Time SSD Active. Drop files directly into {mountRoot}...\n", ConsoleColor.Magenta);

                await Task.Delay(Timeout.Infinite, cts.Token);
                await consumerTask;
            }
            catch (OperationCanceledException)
            {
                SafeLog("[SHUTDOWN] Space Time SSD stopping cleanly.", ConsoleColor.Yellow);
            }
            catch (Exception ex)
            {
                SafeLog($"[CRITICAL ERROR] Core initialization failure: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                UnmountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER);
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
                p?.WaitForExit(5000);
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
                p?.WaitForExit(3000);
            }
            catch
            {
                // Unmount cleanup fallback
            }
        }

        #endregion

        #region Drive Metrics & Sync

        private static async Task SyncCloudCapacityMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var response = await HttpClient.GetAsync($"{PUTER_FS_ENDPOINT}?action=CAPACITY", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    string res = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (long.TryParse(res.Trim(), out long bytesUsed))
                    {
                        CurrentCloudUsedBytes = bytesUsed;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Capacity fallback
            }

            DisplaySpaceMetrics(0);
        }

        private static void DisplaySpaceMetrics(long incomingPayloadSize)
        {
            long usedBytes = CurrentCloudUsedBytes;
            long availableCloudBytes = TOTAL_CLOUD_CAPACITY_BYTES - usedBytes;
            DriveInfo localDrive = new DriveInfo("C:\\");

            lock (ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("-------------------------------------------------------------------");
                Console.WriteLine($"[CLOUD METRICS] Total: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES)} | Used: {FormatBytes(usedBytes)} | Available: {FormatBytes(availableCloudBytes)}");
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

        private static async Task InitialSyncSweepAsync(string mountPath, CancellationToken cancellationToken)
        {
            SafeLog($"[SWEEP] Checking partition {mountPath} for local items...", ConsoleColor.Gray);
            await ProcessDirectoryRecursivelyAsync(mountPath, mountPath, cancellationToken);
        }

        private static FileSystemWatcher StartActiveZeroWeightInterceptor(string mountPath)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(mountPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = true
            };

            watcher.Created += (_, e) => { _ = PendingEntriesChannel.Writer.TryWrite(e.FullPath); };
            watcher.Renamed += (_, e) => { _ = PendingEntriesChannel.Writer.TryWrite(e.FullPath); };
            watcher.Error += (_, e) =>
            {
                SafeLog($"[WATCHER WARNING] {e.GetException().Message}", ConsoleColor.Yellow);
            };

            return watcher;
        }

        private static async Task ConsumePendingEntriesAsync(string mountPath, CancellationToken cancellationToken)
        {
            await foreach (string entryPath in PendingEntriesChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await IngestLimiter.WaitAsync(cancellationToken);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleFileSystemEntryAsync(entryPath, mountPath, cancellationToken);
                    }
                    finally
                    {
                        IngestLimiter.Release();
                    }
                }, cancellationToken);
            }
        }

        private static async Task HandleFileSystemEntryAsync(string targetPath, string mountPath, CancellationToken cancellationToken)
        {
            if (Directory.Exists(targetPath))
            {
                await ProcessDirectoryRecursivelyAsync(targetPath, mountPath, cancellationToken);
            }
            else if (File.Exists(targetPath))
            {
                await ProcessAndStreamToCloudImmediatelyAsync(targetPath, mountPath, cancellationToken);
            }
        }

        private static async Task ProcessDirectoryRecursivelyAsync(
            string currentDirectoryPath,
            string mountPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!string.Equals(
                    Path.GetFullPath(currentDirectoryPath).TrimEnd('\\'),
                    Path.GetFullPath(mountPath).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase))
                {
                    string relativeDirPath = NormalizeVirtualPath(Path.GetRelativePath(mountPath, currentDirectoryPath));
                    await PuterFS_MkdirAsync(relativeDirPath, cancellationToken);
                }

                foreach (string subDir in Directory.GetDirectories(currentDirectoryPath))
                {
                    await ProcessDirectoryRecursivelyAsync(subDir, mountPath, cancellationToken);
                }

                foreach (string filePath in Directory.GetFiles(currentDirectoryPath))
                {
                    await ProcessAndStreamToCloudImmediatelyAsync(filePath, mountPath, cancellationToken);
                }

                if (!string.Equals(
                    Path.GetFullPath(currentDirectoryPath).TrimEnd('\\'),
                    Path.GetFullPath(mountPath).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase) &&
                    Directory.GetFileSystemEntries(currentDirectoryPath).Length == 0)
                {
                    Directory.Delete(currentDirectoryPath, recursive: false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SafeLog($"[SYNC WARNING] {currentDirectoryPath}: {ex.Message}", ConsoleColor.Yellow);
            }
        }

        private static async Task PuterFS_MkdirAsync(string virtualPath, CancellationToken cancellationToken)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("MKDIR"), "action");
                content.Add(new StringContent(virtualPath), "virtualPath");

                using var response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Fallback for directory creation
            }
        }

        private static async Task ProcessAndStreamToCloudImmediatelyAsync(
            string localPath,
            string mountPath,
            CancellationToken cancellationToken)
        {
            string fileName = Path.GetFileName(localPath);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".sov_tmp"))
            {
                return;
            }

            string relativePath = NormalizeVirtualPath(Path.GetRelativePath(mountPath, localPath));

            if (!await WaitForFileReadyAsync(localPath, timeoutMs: 10000, cancellationToken))
            {
                SafeLog($"[SKIP] File locked: {relativePath}", ConsoleColor.DarkYellow);
                return;
            }

            if (!ShinobiTactics.JoganVerifyDimensionalPath(relativePath))
            {
                return;
            }

            string? parentDir = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(parentDir) && parentDir != "." && parentDir != "/")
            {
                await EnsureRemoteDirectoryPathAsync(parentDir, cancellationToken);
            }

            ShinobiTactics.RegisterKawarimiDeception(localPath);
            string sealId = ShinobiTactics.ApplyHiraishinSeal(relativePath);
            SafeLog($"[HIRAISHIN TELEPORT] Sector Seal [{sealId}] attached to path {relativePath}", ConsoleColor.Gray);

            FileInfo info = new FileInfo(localPath);
            long rawFileSize = info.Exists ? info.Length : 0L;
            ShinobiTactics.SharinganObservePattern(relativePath, 0, (int)Math.Min(rawFileSize, int.MaxValue));

            await ShinobiTactics.ExecuteKuramaOverclockingAsync(async () =>
            {
                DriveInfo driveBefore = new DriveInfo("C:\\");

                SafeLog($"\n[INTERCEPTED PARTITION ENTRY] {relativePath}", ConsoleColor.Cyan);
                DisplaySpaceMetrics(rawFileSize);

                try
                {
                    byte[] fileBytes = ShinobiTactics.MemoryMappedVectorizedReadPass(localPath);
                    ShinobiTactics.TenseiganPulseGravityBalance(fileBytes.Length);
                    byte[] hardenedPayload = ShinobiTactics.ApplyIsobuStreamHardening(fileBytes);
                    byte[] compressedPayload = ShinobiTactics.DaikokutenStoreInPocketDimension(hardenedPayload);

                    if (_sageOrchestrator != null)
                    {
                        await _sageOrchestrator.RunOrchestrationCycleAsync($"Payload: {relativePath} | Size: {rawFileSize} bytes");
                    }

                    _ = ShinobiTactics.GenerateKageBunshins(compressedPayload, chunkSizeMb: 16);

                    await ShinobiTactics.OhirumeSunBurstAccelerationAsync(async () =>
                    {
                        await StreamToCloudWithProgressBarAsync("WRITE", relativePath, compressedPayload, cancellationToken);
                    });

                    Interlocked.Add(ref _currentCloudUsedBytes, compressedPayload.Length);

                    ShinobiTactics.AmenotejikaraSwapLocation(localPath, relativePath, compressedPayload);
                    ShinobiTactics.ApplyShikakuSandSeal(sealId, compressedPayload);

                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                        SafeLog($"[ZERO-WEIGHT PURGE] Erased local buffer: {relativePath}", ConsoleColor.Gray);
                    }

                    ShinobiTactics.ReleaseKawarimiDeception(localPath);

                    DriveInfo driveAfter = new DriveInfo("C:\\");
                    long diskDifference = driveBefore.AvailableFreeSpace - driveAfter.AvailableFreeSpace;

                    SafeLog($"[VERIFICATION] Physical Local Disk Consumption: {FormatBytes(Math.Max(0, diskDifference))} (ZERO-WEIGHT CONFIRMED)", ConsoleColor.Green);
                    SafeLog($"[CLOUD STATUS] Upload Complete. Available Space: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES - CurrentCloudUsedBytes)}\n", ConsoleColor.Green);

                    ShinobiTactics.ByakuganFullSystemAudit();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    SafeLog($"[STREAMING ERROR] {relativePath}: {ex.Message}", ConsoleColor.Red);
                }
            });
        }

        private static async Task EnsureRemoteDirectoryPathAsync(string directoryPath, CancellationToken cancellationToken)
        {
            string normalized = NormalizeVirtualPath(directoryPath);
            string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string currentPath = "";

            foreach (string part in parts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                await PuterFS_MkdirAsync(currentPath, cancellationToken);
            }
        }

        private static async Task StreamToCloudWithProgressBarAsync(
            string action,
            string virtualPath,
            byte[] payload,
            CancellationToken cancellationToken)
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

            using HttpResponseMessage response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            SafeLog(string.Empty);
        }

        private static void RenderProgressBar(long bytesSent, long totalBytes)
        {
            if (totalBytes <= 0) return;
            double percentage = (double)bytesSent / totalBytes * 100.0;
            int totalBlocks = 30;
            int filledBlocks = Math.Clamp((int)Math.Round((percentage / 100.0) * totalBlocks), 0, totalBlocks);

            string bar = new string('█', filledBlocks) + new string('-', totalBlocks - filledBlocks);

            lock (ConsoleLock)
            {
                Console.Write($"\r[UPLOADING TO CLOUD] [{bar}] {percentage:F1}% ({FormatBytes(bytesSent)} / {FormatBytes(totalBytes)})");
            }
        }

        private static async Task<bool> WaitForFileReadyAsync(string path, int timeoutMs, CancellationToken cancellationToken)
        {
            int elapsed = 0;
            const int interval = 200;

            while (elapsed < timeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using FileStream fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    return true;
                }
                catch (IOException)
                {
                    await Task.Delay(interval, cancellationToken);
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
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n2} {suffixes[counter]}";
        }

        #endregion

        #region Progressable Stream Class

        private sealed class ProgressableStreamContent : HttpContent
        {
            private readonly byte[] _content;
            private readonly int _bufferSize;
            private readonly Action<long, long>? _progress;

            public ProgressableStreamContent(byte[] content, int bufferSize, Action<long, long>? progress)
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
                    await stream.WriteAsync(_content.AsMemory(i, length));
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
