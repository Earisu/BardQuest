using System.IO.Compression;

using BardQuest.Updater.Core.Patching;

namespace BardQuest.Updater.Core.Releases;

// Downloads a release zip and locates the folder holding the three mod DLLs.
public static class ReleaseDownloader
{
    public static string? ValidateExtracted(string dir) => ContainsAllModDlls(dir)
        ? dir
        : Directory.GetDirectories(dir).FirstOrDefault(ContainsAllModDlls);

    public static async Task<string> DownloadAndExtractAsync(
        HttpClient http, string assetUrl, string destDir, CancellationToken ct = default)
    {
        if (Directory.Exists(destDir))
        {
            Directory.Delete(destDir, recursive: true);
        }

        _ = Directory.CreateDirectory(destDir);

        string zipPath = Path.Combine(destDir, "release.zip");
        await using (Stream src = await http.GetStreamAsync(assetUrl, ct))
        await using (FileStream dst = File.Create(zipPath))
        {
            await src.CopyToAsync(dst, ct);
        }

        await ZipFile.ExtractToDirectoryAsync(zipPath, destDir, ct);
        File.Delete(zipPath);
        return destDir;
    }

    private static bool ContainsAllModDlls(string dir) =>
        ModDeployer.ModDllNames.All(name => File.Exists(Path.Combine(dir, name)));
}
