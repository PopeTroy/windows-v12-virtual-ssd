using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SovereignEngine.Native;

namespace SovereignSSD
{
    internal class Program
    {
        private const string VIRTUAL_VOLUME_LABEL = "UESP_V12_SSD";
        private const string PUTER_FS_ENDPOINT = "https://info@celsiusmediagroup.co.za/puterfs";
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        private static string BaseSSDPath = string.Empty;

        static async Task Main(string[] args)
        {
            Console.Title = "UESP Sovereign V12 Virtual SSD Core Engine";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" UESP Sovereign V12 Virtual SSD Volume (Windows Native Core)");
            Console.WriteLine(" Capacity Target: Zero-Local-Weight Direct Cloud Pipe");
            Console.WriteLine(" Mode: Strict Full-Tree Directory Interceptor Active");
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

                // Step 2: Establish Virtual Mount Space Directory Struct
                BaseSSDPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SovereignV12SSD");
                if (!Directory.Exists(BaseSSDPath))
                {
                    Directory.CreateDirectory(BaseSSDPath);
                }

                Console.WriteLine($"[SSD MOUNT] Base Virtual Sector path established: {BaseSSDPath}");

                // Step 3: Run immediate sweep on existing local items before listening
                await InitialSyncSweepAsync(BaseSSDPath);

                // Step 4: Start Active High-Frequency File System Interceptor
                StartActiveZeroWeightInterceptor(BaseSSDPath);

                Console.WriteLine("[READY] Virtual SSD Orchestrator active. Intercepting writes directly to Puter FS cloud...\n");

                await Task.Delay(-1); // Keep process alive
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL ERROR] Core initialization failure: {ex.Message}");
                Console.ResetColor();
            }
        }

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

            // Intercepts any file or folder created, pasted, or moved into the volume
            watcher.Created += async (s, e) =>
            {
                await HandleFileSystemEntryAsync(e.FullPath, mountPath);
            };

            watcher.Renamed += async (s, e) =>
            {
                await HandleFileSystemEntryAsync(e.FullPath, mountPath);
            };
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
                // Register directory on Puter FS if not root
                if (currentDirectoryPath != mountPath)
                {
                    string relativeDirPath = NormalizeVirtualPath(Path.GetRelativePath(mountPath, currentDirectoryPath));
                    await PuterFS_MkdirAsync(relativeDirPath);
                }

                // 1. Process all subdirectories first
                string[] subdirectories = Directory.GetDirectories(currentDirectoryPath);
                foreach (string subDir in subdirectories)
                {
                    await ProcessDirectoryRecursivelyAsync(subDir, mountPath);
                }

                // 2. Process all files inside this directory
                string[] files = Directory.GetFiles(currentDirectoryPath);
                foreach (string filePath in files)
                {
                    await ProcessAndStreamToCloudImmediatelyAsync(filePath, mountPath);
                }

                // 3. Purge directory locally if empty and not the base mount path
                if (currentDirectoryPath != mountPath && Directory.GetFileSystemEntries(currentDirectoryPath).Length == 0)
                {
                    Directory.Delete(currentDirectoryPath, recursive: false);
                    Console.WriteLine($"[ZERO-WEIGHT PURGE] Empty directory removed: {NormalizeVirtualPath(Path.GetRelativePath(mountPath, currentDirectoryPath))}");
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
            // Skip system or temporary operational locks
            string fileName = Path.GetFileName(localPath);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".sov_tmp"))
            {
                return;
            }

            string relativePath = NormalizeVirtualPath(Path.GetRelativePath(mountPath, localPath));
            Console.WriteLine($"[INTERCEPTED IMMEDIATELY] {relativePath} detected. Streaming to Rust engine...");

            // Wait for Windows file lock release if file is still being written/copied
            if (!WaitForFileReady(localPath, timeoutMs: 10000))
            {
                Console.WriteLine($"[SKIP] File locked by another process: {relativePath}");
                return;
            }

            try
            {
                byte[] finalCompressedPayload;
                long totalBytesProcessed = 0;

                // Synchronous stream read and native Zstd compression loop (compatible with C# 12)
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
                            totalBytesProcessed += bytesRead;
                        }
                    }

                    finalCompressedPayload = memoryPipe.ToArray();
                }

                // Upload directly to Puter FS endpoint
                await SyncToPuterCloudAsync("WRITE", relativePath, finalCompressedPayload);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[CLOUD STREAM SUCCESS] {relativePath} ({totalBytesProcessed:N0} bytes raw -> {finalCompressedPayload.Length:N0} bytes compressed) -> Puter FS");
                Console.ResetColor();

                // Delete local copy immediately
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                    Console.WriteLine($"[ZERO-WEIGHT PURGE] Local file wiped: {relativePath}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[STREAMING INTERCEPT ERROR] {relativePath}: {ex.Message}");
                Console.ResetColor();
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
                    System.Threading.Thread.Sleep(interval);
                    elapsed += interval;
                }
            }
            return false;
        }

        private static string NormalizeVirtualPath(string path)
        {
            return path.Replace('\\', '/');
        }

        #endregion

        #region Puter FS Handlers (Read, Write, Copy, Paste, Stat, Delete, Mkdir)

        public static async Task PuterFS_Write(string virtualPath, byte[] data)
        {
            byte[] compressed = SovereignCompressor.Compress(data, compressionLevel: 3);
            await SyncToPuterCloudAsync("WRITE", NormalizeVirtualPath(virtualPath), compressed);
        }

        public static async Task<byte[]> PuterFS_Read(string virtualPath)
        {
            string normalized = NormalizeVirtualPath(virtualPath);
            using var response = await HttpClient.GetAsync($"{PUTER_FS_ENDPOINT}?action=READ&virtualPath={Uri.EscapeDataString(normalized)}");
            response.EnsureSuccessStatusCode();

            byte[] compressedData = await response.Content.ReadAsByteArrayAsync();
            return SovereignCompressor.Decompress(compressedData);
        }

        public static async Task PuterFS_CopyPaste(string sourcePath, string destinationPath)
        {
            byte[] sourceData = await PuterFS_Read(sourcePath);
            await PuterFS_Write(destinationPath, sourceData);
        }

        public static async Task PuterFS_MkdirAsync(string virtualDirPath)
        {
            string normalized = NormalizeVirtualPath(virtualDirPath);
            await SyncToPuterCloudAsync("MKDIR", normalized, Array.Empty<byte>());
            Console.WriteLine($"[PuterFS MKDIR SUCCESS] Directory registered on cloud: {normalized}");
        }

        public static async Task<PuterFileStat> PuterFS_Stat(string virtualPath)
        {
            string normalized = NormalizeVirtualPath(virtualPath);
            using var response = await HttpClient.GetAsync($"{PUTER_FS_ENDPOINT}?action=STAT&virtualPath={Uri.EscapeDataString(normalized)}");
            response.EnsureSuccessStatusCode();

            return new PuterFileStat { Path = normalized, CompressedSizeBytes = response.Content.Headers.ContentLength ?? 0, LastWriteTime = DateTime.UtcNow };
        }

        public static async Task PuterFS_Delete(string virtualPath)
        {
            string normalized = NormalizeVirtualPath(virtualPath);
            await SyncToPuterCloudAsync("DELETE", normalized, Array.Empty<byte>());
            Console.WriteLine($"[PuterFS DELETE] Cloud sector purged: {normalized}");
        }

        #endregion

        #region Cloud Transmission Gateway

        private static async Task SyncToPuterCloudAsync(string action, string virtualPath, byte[] payload)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(action), "action");
            content.Add(new StringContent(virtualPath), "virtualPath");

            if (payload.Length > 0)
            {
                content.Add(new ByteArrayContent(payload), "payload", Path.GetFileName(virtualPath) + ".sov");
            }

            HttpResponseMessage response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
            response.EnsureSuccessStatusCode();
        }

        #endregion
    }

    public class PuterFileStat
    {
        public string Path { get; set; } = string.Empty;
        public long CompressedSizeBytes { get; set; }
        public DateTime LastWriteTime { get; set; }
    }
}
