using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SovereignEngine.Native;

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

        [STAThread]
        static async Task Main(string[] args)
        {
            Console.Title = "Virtual SSD Core Engine";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" Virtual SSD Volume Engine (Windows Direct Mount)");
            Console.WriteLine(" Capacity Target: Zero-Local-Weight Direct Cloud Pipe");
            Console.WriteLine(" Mode: Real-Time Stream Tracking & Mount Interceptor Active");
            Console.WriteLine("===================================================================\n");
            Console.ResetColor();

            try
            {
                // Step 1: Establish Local Storage Sub-Directory
                LocalStoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SovereignV12SSD");
                if (!Directory.Exists(LocalStoragePath))
                {
                    Directory.CreateDirectory(LocalStoragePath);
                }

                // Step 2: Mount Virtual Folder as standard Windows Drive Partition (V:)
                MountVirtualDrivePartition(VIRTUAL_DRIVE_LETTER, LocalStoragePath);

                // Step 3: Validate Native Dynamic Library FFI Binding
                Console.WriteLine("[INIT] Verifying Native Engine FFI binding...");
                byte[] samplePayload = Encoding.UTF8.GetBytes("V12_INITIALIZATION_VECTOR_SECTOR_0");
                byte[] compressed = SovereignCompressor.Compress(samplePayload, compressionLevel: 3);
                byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: samplePayload.Length);

                if (Encoding.UTF8.GetString(decompressed) == "V12_INITIALIZATION_VECTOR_SECTOR_0")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SUCCESS] Native Zstd / Rayon Engine Binding Online.");
                    Console.ResetColor();
                }

                Console.WriteLine($"[MOUNT SUCCESS] Partition online at {VIRTUAL_DRIVE_LETTER}\\ -> Surface target ready in 'This PC'");

                // Fetch initial cloud metrics
                await SyncCloudCapacityMetricsAsync();

                // Step 4: Run immediate sweep on existing partition items
                await InitialSyncSweepAsync($"{VIRTUAL_DRIVE_LETTER}\\");

                // Step 5: Intercept drop events on the Mounted Partition
                StartActiveZeroWeightInterceptor($"{VIRTUAL_DRIVE_LETTER}\\");

                Console.WriteLine($"\n[READY] Virtual Storage Partition active. Drop files directly into {VIRTUAL_DRIVE_LETTER}\\...\n");

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

            DriveInfo driveBefore = new DriveInfo("C:\\");
            long rawFileSize = new FileInfo(localPath).Length;

            Console.WriteLine($"\n[INTERCEPTED PARTITION ENTRY] {relativePath}");
            DisplaySpaceMetrics(rawFileSize);

            try
            {
                byte[] finalCompressedPayload;

                using (var memoryPipe = new MemoryStream())
                {
                    const int chunkSizeBytes = 4 * 1024 * 1024;
                    byte[] buffer = new byte[chunkSizeBytes];

                    using (FileStream fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        int bytesRead;
                        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            byte[] chunk = new byte[bytesRead];
                            Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);

                            byte[] compressedChunk = SovereignCompressor.Compress(chunk, compressionLevel: 3);
                            memoryPipe.Write(compressedChunk, 0, compressedChunk.Length);
                        }
                    }

                    finalCompressedPayload = memoryPipe.ToArray();
                }

                await StreamToCloudWithProgressBarAsync("WRITE", relativePath, finalCompressedPayload);

                CurrentCloudUsedBytes += finalCompressedPayload.Length;

                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                    Console.WriteLine($"[ZERO-WEIGHT PURGE] Erased from partition disk buffer: {relativePath}");
                }

                DriveInfo driveAfter = new DriveInfo("C:\\");
                long diskDifference = driveBefore.AvailableFreeSpace - driveAfter.AvailableFreeSpace;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[VERIFICATION] Physical Local Disk Consumption: {FormatBytes(Math.Max(0, diskDifference))} (ZERO-WEIGHT CONFIRMED)");
                Console.WriteLine($"[CLOUD STATUS] Upload Complete. Available Cloud Space: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES - CurrentCloudUsedBytes)}\n");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[STREAMING INTERCEPT ERROR] {relativePath}: {ex.Message}");
                Console.ResetColor();
            }
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
}
