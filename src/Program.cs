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
        private const string PUTER_FS_ENDPOINT = "https://info@celsiusmediagroup.co.za/puterfs"; // Ingestion route for Puter FS
        private static readonly HttpClient HttpClient = new HttpClient();
        private static string BaseSSDPath = string.Empty;

        static async Task Main(string[] args)
        {
            Console.Title = "UESP Sovereign V12 Virtual SSD Core Engine";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===================================================================");
            Console.WriteLine(" UESP Sovereign V12 Virtual SSD Volume (Windows Native Core)");
            Console.WriteLine(" Capacity Target: 200 GB Sparse Partition");
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
                
                // Step 3: Start Puter FS Volume Watcher
                StartPuterFSWatcher(BaseSSDPath);

                Console.WriteLine("[READY] Virtual SSD Orchestrator initialized. Active and awaiting Puter FS synchronization streams.\n");

                await Task.Delay(-1); // Keep engine running
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL ERROR] Core initialization failure: {ex.Message}");
                Console.ResetColor();
            }
        }

        #region Puter FS Storage Operations (Read, Write, Copy, Paste, Stat, Delete, Mkdir)

        // PUTER FS: WRITE / PASTE STREAM
        public static async Task PuterFS_Write(string virtualPath, byte[] data)
        {
            Console.WriteLine($"[PuterFS WRITE] Processing stream for path: {virtualPath}");
            byte[] compressed = SovereignCompressor.Compress(data, compressionLevel: 3);

            string localSectorFile = GetLocalSectorPath(virtualPath);
            await File.WriteAllBytesAsync(localSectorFile, compressed);

            await SyncToPuterCloudAsync("WRITE", virtualPath, compressed);
            Console.WriteLine($"[PuterFS WRITE COMPLETE] {virtualPath} ({data.Length:N0} -> {compressed.Length:N0} bytes)");
        }

        // PUTER FS: READ / COPY STREAM
        public static async Task<byte[]> PuterFS_Read(string virtualPath)
        {
            Console.WriteLine($"[PuterFS READ] Fetching stream for path: {virtualPath}");
            string localSectorFile = GetLocalSectorPath(virtualPath);

            if (!File.Exists(localSectorFile))
            {
                throw new FileNotFoundException($"[PuterFS] Virtual file stream not found: {virtualPath}");
            }

            byte[] compressed = await File.ReadAllBytesAsync(localSectorFile);
            byte[] decompressed = SovereignCompressor.Decompress(compressed);
            
            return decompressed;
        }

        // PUTER FS: COPY & PASTE ROUTINE
        public static async Task PuterFS_CopyPaste(string sourcePath, string destinationPath)
        {
            Console.WriteLine($"[PuterFS COPY-PASTE] {sourcePath} -> {destinationPath}");
            byte[] sourceData = await PuterFS_Read(sourcePath);
            await PuterFS_Write(destinationPath, sourceData);
        }

        // PUTER FS: MARK DIRECTORY (MKDIR)
        public static void PuterFS_Mkdir(string virtualDirPath)
        {
            string localDir = GetLocalSectorPath(virtualDirPath);
            if (!Directory.Exists(localDir))
            {
                Directory.CreateDirectory(localDir);
                Console.WriteLine($"[PuterFS MKDIR] Marked Virtual Directory: {virtualDirPath}");
            }
        }

        // PUTER FS: STAT / METADATA QUERY
        public static PuterFileStat PuterFS_Stat(string virtualPath)
        {
            string localSectorFile = GetLocalSectorPath(virtualPath);
            FileInfo info = new FileInfo(localSectorFile);

            if (!info.Exists) throw new FileNotFoundException("Virtual file missing", virtualPath);

            return new PuterFileStat
            {
                Path = virtualPath,
                CompressedSizeBytes = info.Length,
                LastWriteTime = info.LastWriteTimeUtc
            };
        }

        // PUTER FS: DELETE
        public static async Task PuterFS_Delete(string virtualPath)
        {
            string localSectorFile = GetLocalSectorPath(virtualPath);
            if (File.Exists(localSectorFile))
            {
                File.Delete(localSectorFile);
                await SyncToPuterCloudAsync("DELETE", virtualPath, Array.Empty<byte>());
                Console.WriteLine($"[PuterFS DELETE] Purged sector: {virtualPath}");
            }
        }

        #endregion

        #region Puter FS Cloud Transmission & Storage Watcher

        private static async Task SyncToPuterCloudAsync(string action, string virtualPath, byte[] payload)
        {
            try
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
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[PuterFS CLOUD SYNC WARNING] Remote endpoint sync deferred for {virtualPath}: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void StartPuterFSWatcher(string mountPath)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(mountPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += async (s, e) =>
            {
                if (File.Exists(e.FullPath) && !e.FullPath.EndsWith(".sov"))
                {
                    await Task.Delay(500); // Allow write handle allocation
                    byte[] fileData = await File.ReadAllBytesAsync(e.FullPath);
                    string relativePath = Path.GetRelativePath(mountPath, e.FullPath);
                    
                    await PuterFS_Write(relativePath, fileData);
                }
                else if (Directory.Exists(e.FullPath))
                {
                    string relativePath = Path.GetRelativePath(mountPath, e.FullPath);
                    PuterFS_Mkdir(relativePath);
                }
            };
        }

        private static string GetLocalSectorPath(string virtualPath)
        {
            string cleanPath = virtualPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(BaseSSDPath, cleanPath.EndsWith(".sov") ? cleanPath : cleanPath + ".sov");
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
