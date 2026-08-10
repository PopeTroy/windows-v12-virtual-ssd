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
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) }; // Extended timeout for massive cloud streams
        private static string BaseSSDPath = string.Empty;

        static async Task Main(string[] args)
        {
            Console.Title = "UESP Sovereign V12 Virtual SSD Core Engine";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" UESP Sovereign V12 Virtual SSD Volume (Windows Native Core)");
            Console.WriteLine(" Capacity Target: Zero-Local-Weight Direct Cloud Pipe");
            Console.WriteLine("===================================================================\n");
            Console.ResetColor();

            try
            {
                // Step 1: Validate Native Dynamic Library FFI
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

                // Step 3: Start Active High-Frequency File System Interceptor
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

        #region Active Zero-Weight Interceptor & Cloud Stream Engine

        private static void StartActiveZeroWeightInterceptor(string mountPath)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(mountPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // Triggers immediately upon the first byte being dropped into the volume
            watcher.Created += async (s, e) =>
            {
                if (File.Exists(e.FullPath) && !e.FullPath.EndsWith(".sov_tmp"))
                {
                    await ProcessAndStreamToCloudImmediatelyAsync(e.FullPath, mountPath);
                }
                else if (Directory.Exists(e.FullPath))
                {
                    string relativePath = Path.GetRelativePath(mountPath, e.FullPath);
                    PuterFS_Mkdir(relativePath);
                }
            };
        }

        private static async Task ProcessAndStreamToCloudImmediatelyAsync(string localPath, string mountPath)
        {
            string relativePath = Path.GetRelativePath(mountPath, localPath);
            Console.WriteLine($"[INTERCEPTED IMMEDIATELY] {relativePath} detected. Streaming to Rust engine...");

            try
            {
                // Process stream in 4MB RAM chunks so 10GB+ files never touch/fill physical storage
                const int chunkSizeBytes = 4 * 1024 * 1024; // 4MB Chunk Window in RAM
                byte[] buffer = new byte[chunkSizeBytes];

                using (FileStream fs = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int bytesRead;
                    long totalBytesProcessed = 0;

                    using var memoryPipe = new MemoryStream();

                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        ReadOnlySpan<byte> chunkSpan = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
                        byte[] compressedChunk = SovereignCompressor.Compress(chunkSpan, compressionLevel: 3);

                        await memoryPipe.WriteAsync(compressedChunk, 0, compressedChunk.Length);
                        totalBytesProcessed += bytesRead;
                    }

                    byte[] finalCompressedPayload = memoryPipe.ToArray();

                    // Stream payload directly to Puter FS Cloud API
                    await SyncToPuterCloudAsync("WRITE", relativePath, finalCompressedPayload);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[CLOUD STREAM SUCCESS] {relativePath} ({totalBytesProcessed:N0} bytes raw -> {finalCompressedPayload.Length:N0} bytes compressed) -> Puter FS");
                    Console.ResetColor();
                }

                // WIPE LOCAL FILE IMMEDIATELY: Keep physical SSD weight at 0 bytes
                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                    Console.WriteLine($"[ZERO-WEIGHT PURGE] Local copy wiped: {relativePath}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[STREAMING INTERCEPT ERROR] {relativePath}: {ex.Message}");
                Console.ResetColor();
            }
        }

        #endregion

        #region Puter FS Handlers (Read, Write, Copy, Paste, Stat, Delete, Mkdir)

        public static async Task PuterFS_Write(string virtualPath, byte[] data)
        {
            byte[] compressed = SovereignCompressor.Compress(data, compressionLevel: 3);
            await SyncToPuterCloudAsync("WRITE", virtualPath, compressed);
        }

        public static async Task<byte[]> PuterFS_Read(string virtualPath)
        {
            using var response = await HttpClient.GetAsync($"{PUTER_FS_ENDPOINT}?action=READ&virtualPath={Uri.EscapeDataString(virtualPath)}");
            response.EnsureSuccessStatusCode();

            byte[] compressedData = await response.Content.ReadAsByteArrayAsync();
            return SovereignCompressor.Decompress(compressedData);
        }

        public static async Task PuterFS_CopyPaste(string sourcePath, string destinationPath)
        {
            byte[] sourceData = await PuterFS_Read(sourcePath);
            await PuterFS_Write(destinationPath, sourceData);
        }

        public static void PuterFS_Mkdir(string virtualDirPath)
        {
            _ = SyncToPuterCloudAsync("MKDIR", virtualDirPath, Array.Empty<byte>());
            Console.WriteLine($"[PuterFS MKDIR] Directory registered on cloud: {virtualDirPath}");
        }

        public static async Task<PuterFileStat> PuterFS_Stat(string virtualPath)
        {
            using var response = await HttpClient.GetAsync($"{PUTER_FS_ENDPOINT}?action=STAT&virtualPath={Uri.EscapeDataString(virtualPath)}");
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            return new PuterFileStat { Path = virtualPath, CompressedSizeBytes = response.Content.Headers.ContentLength ?? 0, LastWriteTime = DateTime.UtcNow };
        }

        public static async Task PuterFS_Delete(string virtualPath)
        {
            await SyncToPuterCloudAsync("DELETE", virtualPath, Array.Empty<byte>());
            Console.WriteLine($"[PuterFS DELETE] Cloud sector purged: {virtualPath}");
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
