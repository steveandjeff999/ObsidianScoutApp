#if ANDROID
using Android.Content;
using Android.OS;
using Java.IO;
using ObsidianScout.Services;
using System.Net.Http;
using System.Threading.Tasks;

namespace ObsidianScout.Platforms.Android
{
    public class InstallerServiceAndroid : IInstallerService
    {
        public async Task<bool> DownloadAndInstallApkAsync(string url)
        {
            try
            {
                var context = global::Android.App.Application.Context;

                // Download with a short timeout and stream to file to avoid large memory allocations
                using var handler = new global::System.Net.Http.HttpClientHandler();
                using var http = new global::System.Net.Http.HttpClient(handler) { Timeout = global::System.TimeSpan.FromSeconds(20) };
                using var resp = await http.GetAsync(url, global::System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                if (!resp.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[InstallerServiceAndroid] Download failed: {resp.StatusCode}");
                    return false;
                }

                // Save to cache
                var cacheDir = context.CacheDir.AbsolutePath;
                var filePath = global::System.IO.Path.Combine(cacheDir, "update.apk");
                using (var fs = global::System.IO.File.Create(filePath))
                {
                    await resp.Content.CopyToAsync(fs);
                }

                global::Android.Net.Uri uri;
                var fileJava = new global::Java.IO.File(filePath);
                try
                {
                    // Try FileProvider first (recommended)
                    uri = global::AndroidX.Core.Content.FileProvider.GetUriForFile(context, context.PackageName + ".fileprovider", fileJava);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InstallerServiceAndroid] FileProvider not available or failed: {ex.Message}. Falling back to file:// URI.");
                    try
                    {
                        uri = global::Android.Net.Uri.FromFile(fileJava);
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[InstallerServiceAndroid] Fallback Uri.FromFile failed: {ex2.Message}");
                        return false;
                    }
                }

                // Launch installer intent using the resolved URI (FileProvider content:// or file://)
                try
                {
                    var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionInstallPackage);
                    intent.SetData(uri);
                    if (uri.Scheme == "content")
                    {
                        intent.SetFlags(global::Android.Content.ActivityFlags.GrantReadUriPermission | global::Android.Content.ActivityFlags.NewTask);
                    }
                    else
                    {
                        intent.SetFlags(global::Android.Content.ActivityFlags.NewTask);
                    }

                    context.StartActivity(intent);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[InstallerServiceAndroid] Intent install failed: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InstallerServiceAndroid] Error: {ex.Message}");
                return false;
            }
        }

        public Task<bool> IsFileProviderAvailableAsync()
        {
            try
            {
                var context = global::Android.App.Application.Context;
                var provider = context.PackageManager.ResolveContentProvider(context.PackageName + ".fileprovider", 0);
                return Task.FromResult(provider != null);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
#endif
