using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SovereignEngine.Native;

namespace SovereignSSD
{
    public partial class MainWindow : Window
    {
        private const string PUTER_FS_ENDPOINT = "https://celsiusmediagroup.co.za/puterfs";
        private const long TOTAL_CLOUD_CAPACITY_BYTES = 100L * 1024L * 1024L * 1024L; // 100 GB Virtual Limit
        
        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        private readonly string _baseSSDPath;
        private long _cloudUsedBytes = 0;
        private long _totalBytesSavedLocally = 0;

        public MainWindow()
        {
            InitializeComponent();

            _baseSSDPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SovereignV12SSD");
            if (!Directory.Exists(_baseSSDPath))
            {
                Directory.CreateDirectory(_baseSSDPath);
            }

            VerifyNativeBinding();
            UpdateMetricsDisplay();
            StartFileSystemWatcher();
        }

        private void VerifyNativeBinding()
        {
            try
            {
                byte[] samplePayload = Encoding.UTF8.GetBytes("UESP_V12_INITIALIZATION_VECTOR_SECTOR_0");
                byte[] compressed = SovereignCompressor.Compress(samplePayload, compressionLevel: 3);
                byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: samplePayload.Length);

                if (Encoding.UTF8.GetString(decompressed) != "UESP_V12_INITIALIZATION_VECTOR_SECTOR_0")
                {
                    TxtStatus.Text = "Warning: SovereignCompressor native binding verification mismatch.";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Native Binding Error: {ex.Message}";
            }
        }

        private void UpdateMetricsDisplay()
        {
            DriveInfo localDrive = new DriveInfo(Path.GetPathRoot(_baseSSDPath) ?? "C:\\");
            
            TxtLocalFree.Text = FormatBytes(localDrive.AvailableFreeSpace);
            TxtCloudSpace.Text = FormatBytes(Math.Max(0, TOTAL_CLOUD_CAPACITY_BYTES - _cloudUsedBytes));
            TxtDifferential.Text = FormatBytes(_totalBytesSavedLocally);
        }

        private async void BtnForceSync_Click(object sender, RoutedEventArgs e)
        {
            BtnForceSync.IsEnabled = false;
            TxtStatus.Text = "Executing manual force sync sweep on Virtual SSD V:\\...";
            ProgressSync.Value = 0;

            await ProcessDirectoryRecursivelyAsync(_baseSSDPath, _baseSSDPath);

            TxtStatus.Text = "Force sync complete. All objects purged locally and committed to Puter FS.";
            ProgressSync.Value = 100;
            BtnForceSync.IsEnabled = true;

            UpdateMetricsDisplay();
        }

        private void StartFileSystemWatcher()
        {
            FileSystemWatcher watcher = new FileSystemWatcher(_baseSSDPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += async (s, e) => { await Dispatcher.InvokeAsync(async () => await HandleEntryAsync(e.FullPath)); };
            watcher.Renamed += async (s, e) => { await Dispatcher.InvokeAsync(async () => await HandleEntryAsync(e.FullPath)); };
        }

        private async Task HandleEntryAsync(string targetPath)
        {
            if (Directory.Exists(targetPath))
            {
                await ProcessDirectoryRecursivelyAsync(targetPath, _baseSSDPath);
            }
            else if (File.Exists(targetPath))
            {
                await ProcessAndStreamFileAsync(targetPath, _baseSSDPath);
            }
        }

        private async Task ProcessDirectoryRecursivelyAsync(string currentDir, string mountPath)
        {
            try
            {
                if (currentDir != mountPath)
                {
                    string relativeDirPath = Path.GetRelativePath(mountPath, currentDir).Replace('\\', '/');
                    await PuterFS_MkdirAsync(relativeDirPath);
                }

                foreach (string subDir in Directory.GetDirectories(currentDir))
                {
                    await ProcessDirectoryRecursivelyAsync(subDir, mountPath);
                }

                foreach (string file in Directory.GetFiles(currentDir))
                {
                    await ProcessAndStreamFileAsync(file, mountPath);
                }

                if (currentDir != mountPath && Directory.GetFileSystemEntries(currentDir).Length == 0)
                {
                    Directory.Delete(currentDir);
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Directory Sync Warning: {ex.Message}";
            }
        }

        private async Task ProcessAndStreamFileAsync(string localFile, string mountPath)
        {
            string fileName = Path.GetFileName(localFile);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".sov_tmp"))
            {
                return;
            }

            string relativePath = Path.GetRelativePath(mountPath, localFile).Replace('\\', '/');
            TxtStatus.Text = $"Compressing & Uploading: {relativePath}...";

            try
            {
                FileInfo info = new FileInfo(localFile);
                long originalSize = info.Length;

                DriveInfo driveBefore = new DriveInfo(Path.GetPathRoot(_baseSSDPath) ?? "C:\\");

                byte[] rawBytes = await File.ReadAllBytesAsync(localFile);
                byte[] compressed = SovereignCompressor.Compress(rawBytes, compressionLevel: 3);

                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("WRITE"), "action");
                content.Add(new StringContent(relativePath), "virtualPath");
                content.Add(new ByteArrayContent(compressed), "payload", fileName + ".sov");

                var response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
                response.EnsureSuccessStatusCode();

                if (File.Exists(localFile))
                {
                    File.Delete(localFile);
                }

                DriveInfo driveAfter = new DriveInfo(Path.GetPathRoot(_baseSSDPath) ?? "C:\\");
                long actualDiskConsumed = driveBefore.AvailableFreeSpace - driveAfter.AvailableFreeSpace;

                _cloudUsedBytes += compressed.Length;
                _totalBytesSavedLocally += originalSize;

                UpdateMetricsDisplay();
                TxtStatus.Text = $"Synced: {relativePath} | Cloud: +{FormatBytes(compressed.Length)} | Local Consumption: {FormatBytes(Math.Max(0, actualDiskConsumed))}";
                ProgressSync.Value = 100;
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Sync Error ({relativePath}): {ex.Message}";
            }
        }

        private async Task PuterFS_MkdirAsync(string virtualDirPath)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("MKDIR"), "action");
                content.Add(new StringContent(virtualDirPath), "virtualPath");

                await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content);
            }
            catch
            {
                // Directory creation fallback handler
            }
        }

        private string FormatBytes(long bytes)
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
    }
}
