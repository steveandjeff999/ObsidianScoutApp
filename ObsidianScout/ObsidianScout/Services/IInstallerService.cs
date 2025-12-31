namespace ObsidianScout.Services;

public interface IInstallerService
{
    Task<bool> DownloadAndInstallApkAsync(string url);
    Task<bool> IsFileProviderAvailableAsync();
}
