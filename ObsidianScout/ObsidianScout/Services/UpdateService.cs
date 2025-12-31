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

    private const string BaseContentsUrl = "https://api.github.com/repos/steveandjeff999/ObsidianScoutApp/contents/ObsidianScout/apks";

    public async Task<UpdateInfo?> GetLatestApkAsync(bool useAlternatePackage = false)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, BaseContentsUrl);
            req.Headers.UserAgent.ParseAdd("ObsidianScoutAppUpdateChecker/1.0");
            req.Headers.Accept.ParseAdd("application/vnd.github.v3+json");
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Requesting contents list: {BaseContentsUrl}");
            // Use a dedicated HttpClient instance without default headers to ensure
            // no Authorization header from the shared HttpClient is sent.
            using var client = new HttpClient() { Timeout = _http.Timeout };
            var resp = await client.SendAsync(req);
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Contents list response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[UpdateService] Contents list error body: {err}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Contents list json length: {json?.Length}");
            var items = JsonSerializer.Deserialize<List<GitHubContentEntry>>(json);
            if (items == null || items.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[UpdateService] No items found in contents response");
                return null;
            }

            // Choose the folder representing the latest version.
            // Try to parse semantic version-like folder names (e.g., "1.2.1" or "v1.2.1").
            var dirs = items.Where(i => i.Type == "dir").ToList();
            if (dirs.Count == 0) return null;

            GitHubContentEntry? best = null;
            Version? bestVer = null;

            foreach (var d in dirs)
            {
                var name = d.Name.Trim();
                if (name.StartsWith("v", StringComparison.OrdinalIgnoreCase)) name = name.Substring(1);
                if (Version.TryParse(name, out var v))
                {
                    if (bestVer == null || v > bestVer)
                    {
                        bestVer = v;
                        best = d;
                    }
                }
            }

            // Fallback to lexicographic latest if no semantic versions found
            if (best == null)
            {
                best = dirs.OrderByDescending(d => d.Name).FirstOrDefault();
            }
            if (best == null) return null;
            if (best == null) return null;

            System.Diagnostics.Debug.WriteLine($"[UpdateService] Selected version folder: {best.Name} - URL: {best.Url}");
            var folderReq = new HttpRequestMessage(HttpMethod.Get, best.Url);
            folderReq.Headers.UserAgent.ParseAdd("ObsidianScoutAppUpdateChecker/1.0");
            folderReq.Headers.Accept.ParseAdd("application/vnd.github.v3+json");
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Requesting folder contents: {best.Url}");
            var folderResp = await client.SendAsync(folderReq);
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Folder contents response: {(int)folderResp.StatusCode} {folderResp.ReasonPhrase}");
            if (!folderResp.IsSuccessStatusCode)
            {
                var err2 = await folderResp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[UpdateService] Folder list error body: {err2}");
                return null;
            }
            var folderJson = await folderResp.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[UpdateService] Folder json length: {folderJson?.Length}");
            var files = JsonSerializer.Deserialize<List<GitHubContentEntry>>(folderJson);
            if (files == null)
            {
                System.Diagnostics.Debug.WriteLine("[UpdateService] No files found in folder response");
                return null;
            }
            var want = useAlternatePackage ? "com.obsidian.obsidianscout.apk" : "com.herodcorp.obsidianscout.apk";
            var file = files.FirstOrDefault(f => f.Type == "file" && f.Name.Equals(want, StringComparison.OrdinalIgnoreCase))
                       ?? files.FirstOrDefault(f => f.Type == "file");

            // Return info with version name and optional download url
            // Ensure we provide a usable download URL. GitHub API sometimes omits "download_url"
            // for certain entries (e.g., symlinks). Fall back to constructing a raw.githubusercontent URL
            // using the known repo and branch.
            string? downloadUrl = file?.DownloadUrl;
            if (string.IsNullOrWhiteSpace(downloadUrl) && file != null && !string.IsNullOrWhiteSpace(file.Path))
            {
                try
                {
                    // Repo and branch are known for this project
                    var raw = $"https://raw.githubusercontent.com/steveandjeff999/ObsidianScoutApp/master/{file.Path}";
                    downloadUrl = raw;
                    System.Diagnostics.Debug.WriteLine($"[UpdateService] Fallback download URL constructed: {downloadUrl}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateService] Failed to construct fallback download URL: {ex.Message}");
                }
            }

            var info = new UpdateInfo
            {
                Version = best.Name,
                FileName = file?.Name,
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

    private class GitHubContentEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("size")]
        public int Size { get; set; }

        // API url for this content (folder/file) - use this to list folder contents
        [System.Text.Json.Serialization.JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string Html_Url { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("git_url")]
        public string Git_Url { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("download_url")]
        public string DownloadUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    // UpdateInfo now defined in UpdateInfo.cs
}
