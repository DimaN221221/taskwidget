using System.IO;
using System.Text.Json;
using System.Windows;

namespace TaskWidget;

/// <summary>Where the widget keeps its files (%APPDATA%\TaskWidget), and the window position.</summary>
internal static class Storage
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskWidget");

    private static readonly string StateFile = Path.Combine(DataDir, "tasks.json");
    private static readonly string WindowFile = Path.Combine(DataDir, "window.json");

    // Browser cache/profile (also keeps the sync sign-in); kept out of the exe's folder so the exe can live anywhere.
    public static readonly string WebViewDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskWidget", "WebView2");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Saved state as a JSON literal, or "null" when there is none or it is corrupt.</summary>
    public static string LoadStateJson()
    {
        try
        {
            var text = File.ReadAllText(StateFile);
            using var _ = JsonDocument.Parse(text);
            return text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return "null";
        }
    }

    /// <summary>The page owns the data (and merges cloud changes itself); the file just holds its latest copy.</summary>
    public static void SaveState(string json) => WriteAtomic(StateFile, json);

    public static void RestoreGeometry(Window window)
    {
        WindowGeometry? g;
        try
        {
            g = JsonSerializer.Deserialize<WindowGeometry>(File.ReadAllText(WindowFile), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            g = null;
        }

        if (g is null)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        if (g.Width is > 0) window.Width = g.Width.Value;
        if (g.Height is > 0) window.Height = g.Height.Value;

        // Only reuse the position if the window would still be visible (a monitor may be gone).
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                              SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        if (g.X is { } x && g.Y is { } y && screen.IntersectsWith(new Rect(x, y, window.Width, 40)))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = x;
            window.Top = y;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public static void SaveGeometry(Window window)
    {
        if (window.WindowState != WindowState.Normal) return;
        var g = new WindowGeometry(window.Left, window.Top, window.Width, window.Height);
        WriteAtomic(WindowFile, JsonSerializer.Serialize(g));
    }

    public static void WriteAtomic(string path, string text) =>
        WriteAtomic(path, System.Text.Encoding.UTF8.GetBytes(text));

    public static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record WindowGeometry(double? X, double? Y, double? Width, double? Height);
}
