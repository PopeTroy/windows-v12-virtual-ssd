using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using IniParser;
using IniParser.Model;
using SovereignEngine.Native;

namespace SovereignSSD
{
    public partial class MainWindow : Window
    {
        private const string PUTER_FS_ENDPOINT = "https://celsiusmediagroup.co.za/puterfs";
        private const long TOTAL_CLOUD_CAPACITY_BYTES = 100L * 1024L * 1024L * 1024L; // 100 GB Virtual Limit
        private const string CONFIG_FILE_NAME = "vssdhx_dlss5_config.ini";
        private const string SHINOBI_SHADER_FILE_NAME = "Shinobi_HPL3_DIP_Enhancer.fx";

        private static readonly HttpClient HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120),
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        private readonly string _baseSSDPath;
        private readonly string _configPath;
        private readonly string _shaderPath;
        private readonly Channel<string> _watcherQueue = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        private readonly CancellationTokenSource _lifetimeCts = new CancellationTokenSource();

        private FileSystemWatcher? _watcher;
        private bool _isInitializingUi;
        private long _cloudUsedBytes;
        private long _totalBytesSavedLocally;

        public MainWindow()
        {
            InitializeComponent();

            _baseSSDPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SovereignV12SSD");

            Directory.CreateDirectory(_baseSSDPath);

            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE_NAME);
            _shaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SHINOBI_SHADER_FILE_NAME);

            UpdateMetricsDisplay();
            Loaded += OnWindowLoaded;
            Closed += OnWindowClosed;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() =>
                {
                    VerifyNativeBindingBackground();
                    EnsureEmbeddedOrDiskAsset(_configPath, "SovereignSSD.Config.vssdhx_dlss5_config.ini");
                    EnsureEmbeddedOrDiskAsset(_shaderPath, "SovereignSSD.Shaders.Shinobi_HPL3_DIP_Enhancer.fx");
                    DeployAssetsToGameInjectionDirectory();
                }, _lifetimeCts.Token);

                LoadIniConfiguration();
                UpdateMetricsDisplay();
                StartFileSystemWatcher();
                _ = Task.Run(() => ProcessWatcherQueueAsync(_lifetimeCts.Token), _lifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Window closing
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Initialization Warning: {ex.Message}";
            }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            _lifetimeCts.Cancel();
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            _lifetimeCts.Dispose();
        }

        private static void EnsureEmbeddedOrDiskAsset(string diskPath, string manifestResourceName)
        {
            if (File.Exists(diskPath))
                return;

            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? resourceStream = asm.GetManifestResourceStream(manifestResourceName);
            if (resourceStream is null)
                return;

            string? dir = Path.GetDirectoryName(diskPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using FileStream fs = new FileStream(diskPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            resourceStream.CopyTo(fs);
        }

        private void DeployAssetsToGameInjectionDirectory()
        {
            try
            {
                string targetInjectionDir = _baseSSDPath;

                if (File.Exists(_configPath))
                {
                    File.Copy(_configPath, Path.Combine(targetInjectionDir, CONFIG_FILE_NAME), overwrite: true);
                }

                if (File.Exists(_shaderPath))
                {
                    File.Copy(_shaderPath, Path.Combine(targetInjectionDir, SHINOBI_SHADER_FILE_NAME), overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    TxtStatus.Text = $"Asset Injection Deployment Warning: {ex.Message}";
                });
            }
        }

        private void VerifyNativeBindingBackground()
        {
            try
            {
                byte[] samplePayload = Encoding.UTF8.GetBytes("UESP_V12_INITIALIZATION_VECTOR_SECTOR_0");
                byte[] compressed = SovereignCompressor.Compress(samplePayload, compressionLevel: 3);
                byte[] decompressed = SovereignCompressor.Decompress(compressed, expectedUncompressedSize: samplePayload.Length);

                if (Encoding.UTF8.GetString(decompressed) != "UESP_V12_INITIALIZATION_VECTOR_SECTOR_0")
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        TxtStatus.Text = "Warning: SovereignCompressor native binding verification mismatch.";
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    TxtStatus.Text = $"Native Binding Error: {ex.Message}";
                });
            }
        }

        private void LoadIniConfiguration()
        {
            _isInitializingUi = true;
            try
            {
                if (File.Exists(_configPath))
                {
                    var parser = new FileIniDataParser();
                    IniData data = parser.ReadFile(_configPath);

                    string enabled = data["DLSS5"]["Enabled"] ?? "true";
                    string scale = data["DLSS5"]["ResolutionScale"] ?? "2.0";
                    string shaders = data["ReShade"]["ActiveShaders"] ?? "CAS.fx, SMAA.fx, NeuralSharpen.fx, Shinobi_HPL3_DIP_Enhancer.fx";

                    ChkDlssEnable.IsChecked = enabled.Equals("true", StringComparison.OrdinalIgnoreCase);
                    TxtDlssStatus.Text = $"Scale Multiplier: {scale}x | Shaders: {shaders}";
                }
                else
                {
                    TxtDlssStatus.Text = "Config file vssdhx_dlss5_config.ini not found. Using runtime defaults.";
                }
            }
            catch (Exception ex)
            {
                TxtDlssStatus.Text = $"INI Config Read Warning: {ex.Message}";
            }
            finally
            {
                _isInitializingUi = false;
            }
        }

        private void ChkDlssEnable_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializingUi)
                return;

            try
            {
                if (File.Exists(_configPath))
                {
                    var parser = new FileIniDataParser();
                    IniData data = parser.ReadFile(_configPath);
                    data["DLSS5"]["Enabled"] = (ChkDlssEnable.IsChecked == true).ToString().ToLowerInvariant();
                    parser.WriteFile(_configPath, data);
                    DeployAssetsToGameInjectionDirectory();
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Failed to update INI state: {ex.Message}";
            }
        }

        private void BtnReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadIniConfiguration();
            DeployAssetsToGameInjectionDirectory();
            TxtStatus.Text = "Configuration reloaded and assets deployed to game injection directory.";
        }

        private void UpdateMetricsDisplay()
        {
            DriveInfo localDrive = new DriveInfo(Path.GetPathRoot(_baseSSDPath) ?? "C:\\");
            long usedCloud = Interlocked.Read(ref _cloudUsedBytes);
            long savedLocally = Interlocked.Read(ref _totalBytesSavedLocally);

            TxtLocalFree.Text = FormatBytes(localDrive.AvailableFreeSpace);
            TxtCloudSpace.Text = FormatBytes(Math.Max(0, TOTAL_CLOUD_CAPACITY_BYTES - usedCloud));
            TxtDifferential.Text = FormatBytes(savedLocally);
        }

        private async void BtnForceSync_Click(object sender, RoutedEventArgs e)
        {
            BtnForceSync.IsEnabled = false;
            TxtStatus.Text = "Executing manual force sync sweep on Virtual SSD V:\\...";
            ProgressSync.Value = 0;

            try
            {
                await ProcessDirectoryRecursivelyAsync(_baseSSDPath, _baseSSDPath, _lifetimeCts.Token);
                TxtStatus.Text = "Force sync complete. All objects purged locally and committed to Puter FS.";
                ProgressSync.Value = 100;
            }
            catch (OperationCanceledException)
            {
                TxtStatus.Text = "Sync cancelled.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Force sync error: {ex.Message}";
            }
            finally
            {
                BtnForceSync.IsEnabled = true;
                UpdateMetricsDisplay();
            }
        }

        private void StartFileSystemWatcher()
        {
            _watcher = new FileSystemWatcher(_baseSSDPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                EnableRaisingEvents = true
            };

            _watcher.Created += (_, e) => _ = _watcherQueue.Writer.TryWrite(e.FullPath);
            _watcher.Renamed += (_, e) => _ = _watcherQueue.Writer.TryWrite(e.FullPath);
            _watcher.Error += (_, e) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    TxtStatus.Text = $"Watcher Warning: {e.GetException().Message}";
                });
            };
        }

        private async Task ProcessWatcherQueueAsync(CancellationToken cancellationToken)
        {
            await foreach (string fullPath in _watcherQueue.Reader.ReadAllAsync(cancellationToken))
            {
                await HandleEntryAsync(fullPath, cancellationToken);
            }
        }

        private async Task HandleEntryAsync(string targetPath, CancellationToken cancellationToken)
        {
            if (Directory.Exists(targetPath))
            {
                await ProcessDirectoryRecursivelyAsync(targetPath, _baseSSDPath, cancellationToken);
            }
            else if (File.Exists(targetPath))
            {
                await ProcessAndStreamFileAsync(targetPath, _baseSSDPath, cancellationToken);
            }
        }

        private async Task ProcessDirectoryRecursivelyAsync(string currentDir, string mountPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!string.Equals(
                    Path.GetFullPath(currentDir).TrimEnd('\\'),
                    Path.GetFullPath(mountPath).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase))
                {
                    string relativeDirPath = Path.GetRelativePath(mountPath, currentDir).Replace('\\', '/');
                    await PuterFS_MkdirAsync(relativeDirPath, cancellationToken);
                }

                foreach (string subDir in Directory.GetDirectories(currentDir))
                {
                    await ProcessDirectoryRecursivelyAsync(subDir, mountPath, cancellationToken);
                }

                foreach (string file in Directory.GetFiles(currentDir))
                {
                    await ProcessAndStreamFileAsync(file, mountPath, cancellationToken);
                }

                if (!string.Equals(
                    Path.GetFullPath(currentDir).TrimEnd('\\'),
                    Path.GetFullPath(mountPath).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase) &&
                    Directory.GetFileSystemEntries(currentDir).Length == 0)
                {
                    Directory.Delete(currentDir);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    TxtStatus.Text = $"Directory Sync Warning: {ex.Message}";
                });
            }
        }

        private async Task ProcessAndStreamFileAsync(string localFile, string mountPath, CancellationToken cancellationToken)
        {
            string fileName = Path.GetFileName(localFile);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(CONFIG_FILE_NAME, StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals(SHINOBI_SHADER_FILE_NAME, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".sov_tmp", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string relativePath = Path.GetRelativePath(mountPath, localFile).Replace('\\', '/');

            await Dispatcher.InvokeAsync(() =>
            {
                TxtStatus.Text = $"Compressing & Uploading: {relativePath}...";
            });

            try
            {
                FileInfo info = new FileInfo(localFile);
                if (!info.Exists)
                    return;

                long originalSize = info.Length;
                DriveInfo driveBefore = new DriveInfo(Path.GetPathRoot(_baseSSDPath) ?? "C:\\");

                byte[] rawBytes = await File.ReadAllBytesAsync(localFile, cancellationToken);
                byte[] compressed = SovereignCompressor.Compress(rawBytes, compressionLevel: 3);

                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("WRITE"), "action");
                content.Add(new StringContent(relativePath), "virtualPath");
                content.Add(new ByteArrayContent(compressed), "payload", fileName + ".sov");

                using HttpResponseMessage response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                if (File.Exists(localFile))
                {
                    File.Delete(localFile);
                }

                DriveInfo driveAfter = new DriveInfo(Path.GetPathRoot(_baseSSDPath) ?? "C:\\");
                long actualDiskConsumed = driveBefore.AvailableFreeSpace - driveAfter.AvailableFreeSpace;

                Interlocked.Add(ref _cloudUsedBytes, compressed.Length);
                Interlocked.Add(ref _totalBytesSavedLocally, originalSize);

                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateMetricsDisplay();
                    TxtStatus.Text = $"Synced: {relativePath} | Cloud: +{FormatBytes(compressed.Length)} | Local Consumption: {FormatBytes(Math.Max(0, actualDiskConsumed))}";
                    ProgressSync.Value = 100;
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    TxtStatus.Text = $"Sync Error ({relativePath}): {ex.Message}";
                });
            }
        }

        private async Task PuterFS_MkdirAsync(string virtualDirPath, CancellationToken cancellationToken)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent("MKDIR"), "action");
                content.Add(new StringContent(virtualDirPath), "virtualPath");

                using var response = await HttpClient.PostAsync(PUTER_FS_ENDPOINT, content, cancellationToken);
                response.EnsureSuccessStatusCode();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Directory creation fallback handler
            }
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
    }
}
