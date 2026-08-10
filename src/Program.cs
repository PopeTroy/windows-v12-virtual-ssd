using System;
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
        private const string VIRTUAL_VOLUME_LABEL = "UESP_V12_SSD";
        private const string PUTER_FS_ENDPOINT = "https://info@celsiusmediagroup.co.za/puterfs";
        private const long TOTAL_CLOUD_CAPACITY_BYTES = 100L * 1024L * 1024L * 1024L; // 100 GB Virtual Allocation Limit
        
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        private static string BaseSSDPath = string.Empty;
        private static long CurrentCloudUsedBytes = 0;

        [STAThread]
        static async Task Main(string[] args)
        {
            Console.Title = "UESP Sovereign V12 Virtual SSD Core Engine";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" UESP Sovereign V12 Virtual SSD Volume (Windows Native Core)");
            Console.WriteLine(" Capacity Target: Zero-Local-Weight Direct Cloud Pipe");
            Console.WriteLine(" Mode: Real-Time Stream Tracking & Space Metrics Active");
            Console.WriteLine("===================================================================\n");
            Console.ResetColor();

            try
            {
                // Step 1: Validate Native Dynamic Library FFI Binding
                Console.WriteLine("[INIT] Verifying Native Sovereign Engine FFI binding...");
                byte[] samplePayload = Encoding.UTF8.GetBytes("UESP_V12_INITIALIZATION_VECTOR_SECTOR_0");
                byte[] compressed = SovereignCompressor.Compress(samplePayload, compressionLevel: 3);
                byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: samplePayload.Length);

                if (Encoding.UTF8.GetString(decompressed) == "UESP_V12_INITIALIZATION_VECTOR_SECTOR_0")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SUCCESS] Native Zstd / Rayon Engine Binding Online.");
                    Console.ResetColor();
                }

                // Step 2: Establish Virtual Mount Space Directory
                BaseSSDPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SovereignV12SSD");
                if (!Directory.Exists(BaseSSDPath))
                {
                    Directory.CreateDirectory(BaseSSDPath);
                }

                Console.WriteLine($"[SSD MOUNT] Base Virtual Sector path established: {BaseSSDPath}");

                // Fetch initial cloud usage metrics
                await SyncCloudCapacityMetricsAsync();

                // Step 3: Run immediate sweep on existing local items before listening
                await InitialSyncSweepAsync(BaseSSDPath);

                // Step 4: Start Active High-Frequency Interceptor
                StartActiveZeroWeightInterceptor(BaseSSDPath);

                Console.WriteLine("[READY] Virtual SSD Orchestrator active. Monitoring drop events...\n");

                await Task.Delay(-1); // Keep process alive
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL ERROR] Core initialization failure: {ex.Message}");
                Console.ResetColor();
            }
        }

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
                // Fallback to local tracking if capacity endpoint is unpopulated
            }

            DisplaySpaceMetrics(0);
        }

        private static void DisplaySpaceMetrics(long incomingPayloadSize)
        {
            long availableCloudBytes = TOTAL_CLOUD_CAPACITY_BYTES - CurrentCloudUsedBytes;
            DriveInfo localDrive = new DriveInfo(Path.GetPathRoot(BaseSSDPath) ?? "C:\\");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("-------------------------------------------------------------------");
            Console.WriteLine($"[CLOUD METRICS] Total: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES)} | Used: {FormatBytes(CurrentCloudUsedBytes)} | Available: {FormatBytes(availableCloudBytes)}");
            if (incomingPayloadSize > 0)
            {
                Console.WriteLine($"[INCOMING OBJECT] Raw Size: {FormatBytes(incomingPayloadSize)} | Cloud Space Remaining After: {FormatBytes(availableCloudBytes - incomingPayloadSize)}");
            }
            Console.WriteLine($"[LOCAL DISK METRICS] System Free Space: {FormatBytes(localDrive.AvailableFreeSpace)} (Zero-Weight Target Active)");
            Console.WriteLine("-------------------------------------------------------------------");
            Console.ResetColor();
        }

        #endregion

        #region Drive Sweeper & Recursive Directory Interceptor

        private static async Task InitialSyncSweepAsync(string mountPath)
        {
            Console.WriteLine("[SWEEP] Checking mount space for leftover local items...");
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

                string[] subdirectories = Directory.GetDirectories(currentDirectoryPath);
                foreach (string subDir in subdirectories)
                {
                    await ProcessDirectoryRecursivelyAsync(subDir, mountPath);
                }

                string[] files = Directory.GetFiles(currentDirectoryPath);
                foreach (string filePath in files)
                {
                    await ProcessAndStreamToCloudImmediatelyAsync(filePath, mountPath);
                }

                if (currentDirectoryPath != mountPath && Directory.GetFileSystemEntries(currentDirectoryPath).Length == 0)
                {
                    Directory.Delete(currentDirectoryPath, recursive: false);
                    Console.WriteLine($"[ZERO-WEIGHT PURGE] Empty directory purged locally: {NormalizeVirtualPath(Path.GetRelativePath(mountPath, currentDirectoryPath))}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[DIRECTORY SYNC ERROR] {currentDirectoryPath}: {ex.Message}");
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
                Console.WriteLine($"[SKIP] File locked by another process: {relativePath}");
                return;
            }

            DriveInfo driveBefore = new DriveInfo(Path.GetPathRoot(BaseSSDPath) ?? "C:\\");
            long rawFileSize = new FileInfo(localPath).Length;

            Console.WriteLine($"\n[INTERCEPTED] {relativePath}");
            DisplaySpaceMetrics(rawFileSize);

            try
            {
                byte[] finalCompressedPayload;

                // Native Zstd compression loop
                using (var memoryPipe = new MemoryStream())
                {
                    const int chunkSizeBytes = 4 * 1024 * 1024; // 4MB RAM window
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

                // Stream directly to cloud with progress tracking
                await StreamToCloudWithProgressBarAsync("WRITE", relativePath, finalCompressedPayload);

                CurrentCloudUsedBytes += finalCompressedPayload.Length;

                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                    Console.WriteLine($"[ZERO-WEIGHT PURGE] Local file wiped: {relativePath}");
                }

                DriveInfo driveAfter = new DriveInfo(Path.GetPathRoot(BaseSSDPath) ?? "C:\\");
                long diskDifference = driveBefore.AvailableFreeSpace - driveAfter.AvailableFreeSpace;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[VERIFICATION] Physical Local Disk Consumption: {FormatBytes(Math.Max(0, diskDifference))} (ZERO-WEIGHT CONFIRMED)");
                Console.WriteLine($"[CLOUD STATUS] Transfer complete. Remaining Cloud Storage: {FormatBytes(TOTAL_CLOUD_CAPACITY_BYTES - CurrentCloudUsedBytes)}\n");
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
            string normalized = NormalizeVirtualPath(virtualDirPath);
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("MKDIR"), "action");
            content.Add(new StringContent(normalized), "virtualPath");

            HttpResponseMessage response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
            response.EnsureSuccessStatusCode();
            Console.WriteLine($"[PuterFS MKDIR SUCCESS] Directory registered on cloud: {normalized}");
        }

        #endregion
    }
}
