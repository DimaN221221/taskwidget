using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace TaskWidget;

/// <summary>
/// Installs a new version of the widget from GitHub Releases.
/// The page finds the release and asks for it (see "update" in wwwroot/index.html); here the exe is
/// downloaded to %TEMP%, checked against its .sha256 and swapped in: the running exe is renamed to
/// TaskWidget.exe.old (Windows allows renaming a running exe), the new one takes its place and is
/// started with --wait-pid, so it opens only after this process has saved everything and quit.
/// </summary>
internal static class Updater
{
    private const string DownloadPrefix = "https://github.com/" + AppInfo.Repo + "/releases/download/";

    private static readonly string StageDir = Path.Combine(Path.GetTempPath(), "taskwidget-update");
    private static readonly HttpClient Http = CreateClient();

    private static string ExePath => Environment.ProcessPath!;
    private static string OldPath => ExePath + ".old";

    /// <summary>Download and verify the new exe, then put it in place of the running one.</summary>
    /// <returns>Path of the installed exe, ready to start.</returns>
    public static async Task<string> DownloadAndSwapAsync(string exeUrl, string shaUrl, IProgress<int> progress)
    {
        if (!exeUrl.StartsWith(DownloadPrefix, StringComparison.Ordinal) || !shaUrl.StartsWith(DownloadPrefix, StringComparison.Ordinal))
            throw new UpdateException("source");
#if DEBUG
        throw new UpdateException("debug"); // only the published dist\TaskWidget.exe updates itself
#else
        Directory.CreateDirectory(StageDir);
        string staged = Path.Combine(StageDir, "TaskWidget.exe");

        string expected = (await Http.GetStringAsync(shaUrl)).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];

        using (var response = await Http.GetAsync(exeUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? 0;
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = File.Create(staged);
            var buffer = new byte[81920];
            long done = 0;
            int read, lastPct = -1;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                int pct = total > 0 ? (int)(done * 100 / total) : 0;
                if (pct != lastPct) progress.Report(lastPct = pct);
            }
        }

        string actual;
        await using (var file = File.OpenRead(staged))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(staged);
            throw new UpdateException("checksum");
        }

        if (File.Exists(OldPath)) File.Delete(OldPath);
        File.Move(ExePath, OldPath);
        try
        {
            File.Move(staged, ExePath);
        }
        catch
        {
            File.Move(OldPath, ExePath); // put the working exe back
            throw;
        }
        return ExePath;
#endif
    }

    /// <summary>Start the freshly installed exe; it waits for this process to exit before opening.</summary>
    public static void StartNew(string exe)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        psi.ArgumentList.Add("--wait-pid");
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(psi);
    }

    /// <summary>
    /// At startup: with --wait-pid, wait until the previous version has quit (it holds the
    /// single-instance lock and is still writing tasks.json); then remove what the update left behind.
    /// </summary>
    public static void FinishPrevious(string[] args)
    {
        int i = Array.IndexOf(args, "--wait-pid");
        if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int pid))
        {
            try
            {
                using var old = Process.GetProcessById(pid);
                old.WaitForExit(30_000);
            }
            catch (ArgumentException)
            {
                // already gone
            }
        }

        // The old exe may stay locked for a moment after its process ends (antivirus, Explorer).
        Task.Run(async () =>
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    if (File.Exists(OldPath)) File.Delete(OldPath);
                    if (Directory.Exists(StageDir)) Directory.Delete(StageDir, recursive: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    await Task.Delay(500);
                }
            }
        });
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"TaskWidget/{AppInfo.Version}");
        return client;
    }
}

/// <summary>A known update failure; the page shows <see cref="Code"/> in its own language ("source", "checksum", "debug").</summary>
internal sealed class UpdateException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
