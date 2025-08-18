using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Diagnostics;
using NewModLauncher.ViewModels;

namespace NewModLauncher.Views
{
    public class BoolToInstallStateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isInstalled)
            {
                return isInstalled ? "Installed" : "Not Installed";
            }
            return "Unknown";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isInstalled)
            {
                return isInstalled ? Brushes.LimeGreen : Brushes.Orange;
            }
            return Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CompatibilityColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isCompatible)
            {
                return isCompatible ? Brushes.SeaGreen : Brushes.DarkRed;
            }
            return Brushes.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToInstallButtonTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isInstalled)
            {
                return isInstalled ? "Update" : "Install";
            }
            return "Install";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToVisibilityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count == 0;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class ModsView : UserControl
    {
        private ModsViewModel _viewModel;
        private DispatcherTimer? _refreshDotsTimer;
        private string _baseRefreshText = "Refreshing";
        private int _dotCount = 0;
        private object? _refreshOriginalContent;
        public ModsView()
        {
            InitializeComponent();
            _viewModel = new ModsViewModel();
            DataContext = _viewModel;

            if (this.GetVisualRoot() is MainWindow mainWindow && !string.IsNullOrEmpty(mainWindow._amongUsPath))
            {
                mainWindow._gamePathService.GetAmongUsVersion(mainWindow._amongUsPath);
                _ = _viewModel.CheckForModUpdatesAsync();
            }
        }
        public void StartRefreshVisual()
        {
            _refreshOriginalContent = RefreshButton.Content;
            RefreshButton.IsEnabled = false;
            RefreshButton.Content = _baseRefreshText;

            _dotCount = 0;
            _refreshDotsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
            _refreshDotsTimer.Tick += (_, __) =>
            {
                _dotCount = (_dotCount + 1) % 4;
                RefreshButton.Content = _baseRefreshText + new string('.', _dotCount);
            };
            _refreshDotsTimer.Start();
        }

        public void StopRefreshVisual()
        {
            _refreshDotsTimer?.Stop();
            _refreshDotsTimer = null;
            RefreshButton.IsEnabled = true;
            RefreshButton.Content = _refreshOriginalContent ?? "Refresh";
        }
        public async Task RefreshModsAsync()
        {
            if (this.GetVisualRoot() is MainWindow window)
            {
                var gameVersion = window._gamePathService.GetAmongUsVersion(window._settingsService.AmongUsPath);
                _viewModel.CurrentGameVersion = gameVersion;

                await _viewModel.CheckForModUpdatesAsync();

                string pluginsPath = Path.Combine(window._amongUsPath, "BepInEx", "plugins");
                var files = Directory.GetFiles(pluginsPath, "*.dll").Select(Path.GetFileNameWithoutExtension).ToArray();
                _viewModel.CheckModInstallStates(files);

                foreach (var m in _viewModel.Mods)
                {
                    if (!string.IsNullOrWhiteSpace(m.LatestVersion))
                    {
                        m.Version = m.LatestVersion;
                        m.NotifyUpdateChange();
                    }
                }
                _viewModel.UpdateModCompatibility();
            }
        }
        public async void OnRefreshModClick(object sender, RoutedEventArgs e)
        {
            StartRefreshVisual();
            try
            {
                await RefreshModsAsync();
            }
            finally
            {
                StopRefreshVisual();
            }
        }
        private async void OnInstallModClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string modName)
            {
                if (this.GetVisualRoot() is not MainWindow mainWindow) return;

                try
                {

                    ModViewModel? mod = null;
                    foreach (var m in _viewModel.Mods)
                    {
                        if (m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
                        {
                            mod = m;
                            break;
                        }
                    }

                    if (mod == null)
                    {
                        await mainWindow.ShowMessageAsync("Error", $"Cannot find mod information for {modName}", false);
                        return;
                    }

                    if (!mod.IsCompatible)
                    {
                        var result = await mainWindow.ShowDialogAsync("Compatibility Warning",
                            $"{mod.Name} is designed for Among Us version {mod.SupportVersion}, but your current version is {_viewModel.CurrentGameVersion}.\n\nInstalling incompatible mods may cause crashes or errors.\n\nDo you want to install it anyway?",
                            false,
                            showConfirmButton: true,
                            customBrush: Brushes.Orange);

                        if (result != "confirm")
                        {
                            return;
                        }


                        await mainWindow.ShowMessageAsync("Installation Proceeding",
                            $"Installing {mod.Name} despite version mismatch. You may need to update the game or wait for a mod update if issues occur.",
                            false);
                    }


                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        mainWindow.DownloadProgressBar.IsVisible = true;
                        mainWindow.DownloadProgressBar.Value = 0;
                        mainWindow.DownloadProgressBar.Maximum = 100;
                        mainWindow.DownloadProgressBar.ShowDownloadProgressText = true;
                        mainWindow.DownloadProgressBar.DownloadItemName = $"Downloading {mod.Name}";
                        mainWindow.DownloadProgressBar.Margin = new Thickness(0, 0, 0, 35);
                        mainWindow.DownloadProgressBar.Height = 25;
                        mainWindow.DownloadProgressBar.MaxWidth = 600;
                        mainWindow.DownloadProgressBar.MinWidth = 300;
                        mainWindow.DownloadProgressBar.MinHeight = 25;
                        mainWindow.DownloadProgressBar.InvalidateVisual();
                    });

                    await Task.Delay(200);

                    string pluginsPath = Path.Combine(mainWindow._amongUsPath, "BepInEx", "plugins");
                    if (!Directory.Exists(pluginsPath))
                    {
                        Directory.CreateDirectory(pluginsPath);
                    }

                    string modDllPath = Path.Combine(pluginsPath, $"{mod.Name}.dll");

                    await mainWindow._modInstallerService.DownloadFileAsyncWrapper(mod.DownloadUrl, modDllPath, progress =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            mainWindow.DownloadProgressBar.Value = progress;
                            mainWindow.DownloadProgressBar.InvalidateVisual();
                        });
                    });

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        mainWindow.DownloadProgressBar.IsVisible = false;
                        mainWindow.DownloadProgressBar.ShowDownloadProgressText = false;
                        mainWindow.DownloadProgressBar.DownloadItemName = "";
                        mainWindow.DownloadProgressBar.Value = 0;
                        mod.IsInstalled = true;

                        var installedMods = mainWindow._settingsService.InstalledMods.ToList();
                        if (!installedMods.Contains(mod.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            installedMods.Add(mod.Name);
                            mainWindow._settingsService.InstalledMods = installedMods.ToArray();
                        }
                    });

                    await mainWindow.ShowMessageAsync("Success", $"{mod.Name} has been installed successfully!", true);
                }
                catch (Exception ex)
                {
                    await mainWindow.ShowMessageAsync("Error", $"Failed to install {modName}: {ex.Message}", false);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        mainWindow.DownloadProgressBar.IsVisible = false;
                        mainWindow.DownloadProgressBar.ShowDownloadProgressText = false;
                    });
                }
            }
        }
        private async void OnUninstallModClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string modName)
            {
                if (this.GetVisualRoot() is not MainWindow mainWindow) return;

                try
                {

                    ModViewModel? mod = null;
                    foreach (var m in _viewModel.Mods)
                    {
                        if (m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
                        {
                            mod = m;
                            break;
                        }
                    }

                    if (mod == null)
                    {
                        await mainWindow.ShowMessageAsync("Error", $"Cannot find mod information for {modName}", false);
                        return;
                    }


                    var result = await mainWindow.ShowDialogAsync("Confirm Uninstall",
                        $"Are you sure you want to uninstall {mod.Name}?",
                        false,
                        showConfirmButton: true);

                    if (result != "confirm")
                        return;

                    string pluginsPath = Path.Combine(mainWindow._amongUsPath, "BepInEx", "plugins");
                    string modDllPath = Path.Combine(pluginsPath, $"{mod.Name}.dll");
                    string backupPath = Path.Combine(pluginsPath, $"{mod.Name}.dll.old");

                    if (File.Exists(modDllPath))
                    {

                        if (File.Exists(backupPath))
                            File.Delete(backupPath);

                        File.Move(modDllPath, backupPath);

                        mod.IsInstalled = false;

                        var installedMods = mainWindow._settingsService.InstalledMods.ToList();
                        installedMods.RemoveAll(m => m.Equals(mod.Name, StringComparison.OrdinalIgnoreCase));
                        mainWindow._settingsService.InstalledMods = installedMods.ToArray();

                        await mainWindow.ShowMessageAsync("Success", $"{mod.Name} has been uninstalled successfully! A backup copy has been saved as {mod.Name}.dll.old", true);
                    }
                    else
                    {
                        await mainWindow.ShowMessageAsync("Error", $"Could not find {mod.Name}.dll in the plugins folder.", false);
                    }
                }
                catch (Exception ex)
                {
                    await mainWindow.ShowMessageAsync("Error", $"Failed to uninstall {modName}: {ex.Message}", false);
                }
            }
        }

        private void OnViewModRepositoryClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string modName)
            {

                string repoUrl = "";
                foreach (var mod in _viewModel.Mods)
                {
                    if (mod.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))
                    {
                        repoUrl = mod.RepositoryUrl;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(repoUrl))
                {
                    repoUrl = string.Empty;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = repoUrl,
                    UseShellExecute = true
                };

                Process.Start(psi);
            }
        }
    }
}