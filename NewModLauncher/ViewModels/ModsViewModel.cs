using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace NewModLauncher.ViewModels
{
    public class ModsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ModViewModel> Mods { get; } = new();
        public bool _isRefreshing;
        public bool IsRefreshing { get => _isRefreshing; private set { _isRefreshing = value; OnPropertyChanged(); OnPropertyChanged(nameof(RefreshText)); } }
        public string RefreshText => IsRefreshing ? "Refreshing mods…" : "";
        private string _currentGameVersion = "";
        public string CurrentGameVersion
        {
            get => _currentGameVersion;
            set
            {
                if (_currentGameVersion != value)
                {
                    _currentGameVersion = value;
                    OnPropertyChanged();
                    UpdateModCompatibility();
                }
            }
        }
        public ModsViewModel()
        {
            Mods.Add(new ModViewModel
            {
                Name = "yanplaRoles",
                IconText = "YR",
                Description = "yanplaRoles is a mod for the game Among Us that introduces new roles and modifiers.",
                DownloadUrl = "https://github.com/yanpla/yanplaRoles/releases/latest/download/yanplaRoles.dll",
                RepositoryUrl = "https://github.com/yanpla/yanplaRoles",
                SupportVersion = "2024.10.29"
            });
            Mods.Add(new ModViewModel
            {
                Name = "Submerged",
                IconUrl = "https://raw.githubusercontent.com/SubmergedAmongUs/Submerged/main/Submerged/Resources/Images/OptionsIcon.png",
                Description = "Submerged is a mod for Among Us which adds a new map into the game.",
                DownloadUrl = "https://github.com/SubmergedAmongUs/Submerged/releases/latest/download/Submerged.dll",
                RepositoryUrl = "https://github.com/SubmergedAmongUs/Submerged",
                SupportVersion = "2024.11.26"
            });
            Mods.Add(new ModViewModel
            {
                Name = "Launchpad Reloaded",
                IconUrl = "https://allofus.dev/static/images/launchpad_no_shadow.png",
                Description = "A vanilla oriented fun and unique Among Us client mod.",
                DownloadUrl = "https://github.com/All-Of-Us-Mods/LaunchpadReloaded/releases/download/latest/LaunchpadReloaded.dll",
                RepositoryUrl = "https://github.com/All-Of-Us-Mods/LaunchpadReloaded",
                SupportVersion = "2025.6.10"
            });

            foreach (var mod in Mods)
            {
                _ = mod.RefreshIconAsync();
            }
        }

        public void CheckModInstallStates(string[] installedMods)
        {
            foreach (var mod in Mods)
                mod.IsInstalled = installedMods.Contains(mod.Name, System.StringComparer.OrdinalIgnoreCase);
        }

        public void UpdateModCompatibility()
        {
            foreach (var mod in Mods)
                mod.IsCompatible = VersionUtils.Compare(CurrentGameVersion, mod.SupportVersion) <= 0;
        }
        public async Task CheckForModUpdatesAsync()
        {
            IsRefreshing = true;
            try
            {
                foreach (var mod in Mods)
                {
                    var info = await GetLatestReleaseInfo(mod.RepositoryUrl);
                    if (info != null)
                    {
                        if (!string.IsNullOrWhiteSpace(info.Tag))
                            mod.Version = info.Tag;

                        if (string.IsNullOrWhiteSpace(mod.DownloadUrl) && !string.IsNullOrWhiteSpace(info.DllAssetUrl))
                            mod.DownloadUrl = info.DllAssetUrl;

                        mod.NotifyUpdateChange();
                    }
                }
            }
            finally { IsRefreshing = false; }
        }
        private async Task<ReleaseInfo> GetLatestReleaseInfo(string repoUrl)
        {
            var userRepo = repoUrl.Replace("https://github.com/", "").TrimEnd('/');
            var apiUrl = $"https://api.github.com/repos/{userRepo}/releases/latest";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NewModLauncher");

            var json = await client.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            string tag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";

            string dllUrl = "";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                dllUrl = assets.EnumerateArray()
                    .Select(a => new
                    {
                        Name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : ""
                    })
                    .Where(x => x.Name.EndsWith(".dll", System.StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Name.Length)
                    .Select(x => x.Url)
                    .FirstOrDefault() ?? "";
            }

            return new ReleaseInfo { Tag = tag, DllAssetUrl = dllUrl };
        }

        public sealed class ReleaseInfo
        {
            public string Tag { get; init; } = "";
            public string DllAssetUrl { get; init; } = "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
