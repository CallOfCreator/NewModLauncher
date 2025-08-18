using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NewModLauncher;

namespace NewModLauncher.Services
{
    public class ModUpdateService
    {
        private const string ModReleasesUrl = "https://api.github.com/repos/CallOfCreator/NewMod/releases/latest";
        private readonly SettingsService _settings;
        public ModUpdateService(SettingsService settings)
        {
            _settings = settings;
        }

        public async Task<bool> IsModUpdateAvailableAsync()
        {
            var current = _settings.ModVersion;
            if (string.IsNullOrEmpty(current))
                return false;
            var latest = (await FetchLatestModVersionAsync())?.TrimStart('v', 'V').Trim();
            if (string.IsNullOrEmpty(latest))
                return false;
            return VersionUtils.Compare(latest, current) > 0;
        }

        public async Task<string> FetchLatestModVersionAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NewModLauncher");
            var json = await client.GetStringAsync(ModReleasesUrl);
            using var document = JsonDocument.Parse(json);
            var tag = document.RootElement.GetProperty("tag_name").GetString() ?? "";
            return tag;
        }

        public async Task<(bool hasZip, string zipUrl, string dllUrl)> GetDownloadUrlsAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NewModLauncher");
            var json = await client.GetStringAsync(ModReleasesUrl);
            using var document = JsonDocument.Parse(json);

            string zipUrl = "";
            string dllUrl = "";

            if (document.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameElement) &&
                        asset.TryGetProperty("browser_download_url", out var urlElement))
                    {
                        string name = nameElement.GetString() ?? "";
                        string url = urlElement.GetString() ?? "";

                        if ((name.Contains("NewMod") || name.Contains("NewMod-MS")) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            zipUrl = url;
                        }
                        else if (name.Equals("NewMod.dll", StringComparison.OrdinalIgnoreCase))
                        {
                            dllUrl = url;
                            Logger.Log("MESSAGE", $"{dllUrl}");
                        }
                    }
                }
            }

            return (!string.IsNullOrEmpty(zipUrl), zipUrl, dllUrl);
        }
    }
}
