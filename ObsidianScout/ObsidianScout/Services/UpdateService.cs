using System.Net.Http;
using System.Text.Json;

namespace ObsidianScout.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> GetLatestApkAsync(bool useAlternatePackage = false);
}

public class UpdateService : IUpdateService
{
    private readonly HttpClient _http;

    public UpdateService(HttpClient http)
    {
        _http = http;
    }

    private const string ReleasesApiUrl = "https://api.github.com/repos/steveandjeff999/ObsidianScoutApp/releases/latest";
    private const string RepoOwner = "steveandjeff999";
    private const string RepoName = "ObsidianScoutApp";

    public async Task<UpdateInfo?> GetLatestApkAsync(bool useAlternatePackage = false)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            req.Headers.UserAgent.ParseAdd("ObsidianScoutAppUpdateChecker/1.0");
            req.Headers.Accept.ParseAdd("application/vnd.github.v3+json");
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Requesting latest release: {ReleasesApiUrl}");

            // Use a dedicated HttpClient instance without default headers to ensure
            // no Authorization header from the shared HttpClient is sent.
            using var client = new HttpClient() { Timeout = _http.Timeout };
            var resp = await client.SendAsync(req);
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Releases response: {(int)resp.StatusCode} {resp.ReasonPhrase}");

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[UpdateService] Releases error body: {err}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Releases json length: {json?.Length}");

            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release == null)
            {
                System.Diagnostics.Debug.WriteLine("[UpdateService] Failed to deserialize release");
                return null;
            }

            // Get version from tag_name (e.g., "1.2.8" or "v1.2.8")
            var version = release.TagName ?? string.Empty;
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Found release: {version}");

            // Determine the APK filename to look for
            var preferredApk = useAlternatePackage ? "com.herodcorp.obsidianscout.apk" : "com.obsidian.obsidianscout.apk";
            var fallbackApk = useAlternatePackage ? "com.obsidian.obsidianscout.apk" : "com.herodcorp.obsidianscout.apk";

            // Try to find the APK in the release assets
            GitHubReleaseAsset? asset = null;
            string? downloadUrl = null;
            string? fileName = null;

            if (release.Assets != null && release.Assets.Count > 0)
            {
                // Look for preferred APK first
                asset = release.Assets.FirstOrDefault(a =>
                    a.Name.Equals(preferredApk, StringComparison.OrdinalIgnoreCase));

                // Fallback to alternate APK
                if (asset == null)
                {
                    asset = release.Assets.FirstOrDefault(a =>
                        a.Name.Equals(fallbackApk, StringComparison.OrdinalIgnoreCase));
                }

                // Fallback to any .apk file
                if (asset == null)
                {
                    asset = release.Assets.FirstOrDefault(a =>
                        a.Name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
                }

                if (asset != null)
                {
                    downloadUrl = asset.BrowserDownloadUrl;
                    fileName = asset.Name;
                    System.Diagnostics.Debug.WriteLine($"[UpdateService] Found asset: {fileName} -> {downloadUrl}");
                }
            }

            // If no asset found, construct the expected download URL
            // Format: https://github.com/{owner}/{repo}/releases/download/{tag}/{filename}
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                var tagForUrl = version;
                downloadUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{tagForUrl}/{preferredApk}";
                fileName = preferredApk;
                System.Diagnostics.Debug.WriteLine($"[UpdateService] Constructed download URL: {downloadUrl}");
            }

            var info = new UpdateInfo
            {
                Version = version,
                FileName = fileName,
                DownloadUrl = downloadUrl
            };

            return info;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Error checking updates: {ex.Message}");
            return null;
        }
    }

    private class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("published_at")]
        public string PublishedAt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public List<GitHubReleaseAsset>? Assets { get; set; }
    }

    private class GitHubReleaseAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("size")]
        public long Size { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("content_type")]
        public string ContentType { get; set; } = string.Empty;
    }
}
