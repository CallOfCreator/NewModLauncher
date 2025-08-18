using Avalonia.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NewModLauncher.ViewModels
{
    public class ModViewModel : INotifyPropertyChanged
    {

        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string Description { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string RepositoryUrl { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public string IconText { get; set; } = "";

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(InstallButtonText));
                    OnPropertyChanged(nameof(HasUpdate));
                }
            }
        }


        private string _supportVersion = "";
        public string SupportVersion
        {
            get => _supportVersion;
            set
            {
                if (_supportVersion != value)
                {
                    _supportVersion = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCompatible));
                }
            }
        }

        private bool _isCompatible;
        public bool IsCompatible
        {
            get => _isCompatible;
            set { _isCompatible = value; OnPropertyChanged(); OnPropertyChanged(nameof(InstallButtonText)); }
        }

        private Bitmap? _iconImage;
        public Bitmap? IconImage
        {
            get => _iconImage;
            set { _iconImage = value; OnPropertyChanged(); }
        }

        public string CompatibilityText => IsCompatible ?
            $"Compatible with v{SupportVersion}" :
            $"Incompatible with current game version";


        public bool HasUpdate
        {
            get
            {
                if (!IsInstalled || string.IsNullOrEmpty(LatestVersion)) return false;
                return VersionUtils.Compare(LatestVersion, Version) > 0;
            }
        }

        public void NotifyUpdateChange()
        {
            OnPropertyChanged(nameof(HasUpdate));
        }

        public string InstallButtonText =>
            IsInstalled ? (HasUpdate ? "Update" : "Installed") : (IsCompatible ? "Install" : "Incompatible");

        public async Task RefreshIconAsync()
        {
            if (string.IsNullOrEmpty(IconUrl)) { IconImage = null; return; }
            try
            {
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(IconUrl);
                using var ms = new MemoryStream(bytes);
                IconImage = new Bitmap(ms);
            }
            catch { IconImage = null; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
