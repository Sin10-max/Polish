using System.IO;
using System.Runtime.InteropServices;

namespace Polisher.Services;

public class CleanupService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public string TempPath => Path.GetTempPath();
    public string WindowsTempPath => @"C:\Windows\Temp";
    public string CrashDumpPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
    public string UpdateCachePath => @"C:\Windows\SoftwareDistribution\Download";
    public string ExplorerCachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"Microsoft\Windows\Explorer");

    /// Deletes everything inside a folder it can, leaving locked files alone.
    /// Returns bytes actually freed.
    public long CleanFolder(string path)
    {
        long freed = 0;
        if (!Directory.Exists(path)) return 0;

        foreach (var file in SafeEnumerate(path))
        {
            try
            {
                var info = new FileInfo(file);
                long size = info.Length;
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                freed += size;
            }
            catch
            {
                // File in use / permission denied — skip it, don't crash the run.
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* skip locked/non-empty-after-failed-cleanup dirs */ }
        }

        return freed;
    }

    private static IEnumerable<string> SafeEnumerate(string path)
    {
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly); }
        catch { yield break; }
        foreach (var f in files) yield return f;
    }

    public Task<long> CleanTempFilesAsync() => Task.Run(() =>
        CleanFolder(TempPath) + CleanFolder(WindowsTempPath));

    public Task<long> CleanCachesAsync() => Task.Run(() => CleanFolder(ExplorerCachePath));

    public Task<long> CleanOldUpdatesAsync() => Task.Run(() => CleanFolder(UpdateCachePath));

    public Task<long> CleanCrashDumpsAsync() => Task.Run(() => CleanFolder(CrashDumpPath));

    public Task EmptyRecycleBinAsync() => Task.Run(() =>
        SHEmptyRecycleBinW(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND));

    /// Finds files larger than thresholdMb under the user profile (one level deep scan for speed).
    public Task<List<(string Path, long Size)>> FindLargeFilesAsync(long thresholdMb = 500)
    {
        return Task.Run(() =>
        {
            var results = new List<(string, long)>();
            var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length > thresholdMb * 1024L * 1024L)
                            results.Add((file, info.Length));
                    }
                    catch { }
                }
            }
            catch { }
            return results.OrderByDescending(r => r.Item2).Take(25).ToList();
        });
    }
}
