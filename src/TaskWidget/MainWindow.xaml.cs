using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace TaskWidget;

public partial class MainWindow : Window
{
    // The page is served from embedded resources under this made-up origin.
    private const string Origin = "https://taskwidget.local/";

    public MainWindow()
    {
        InitializeComponent();
        Storage.RestoreGeometry(this);
        Loaded += async (_, _) => await InitWebViewAsync();
        Closing += (_, _) => Storage.SaveGeometry(this);
    }

    private async Task InitWebViewAsync()
    {
        var env = await CoreWebView2Environment.CreateAsync(null, Storage.WebViewDataDir);
        await Web.EnsureCoreWebView2Async(env);
        var core = Web.CoreWebView2;

        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false; // no F5 / Ctrl+P etc.; clipboard keys still work
#if DEBUG
        core.Settings.AreDevToolsEnabled = true;
#else
        core.Settings.AreDevToolsEnabled = false;
#endif

#if !DEBUG
        // Right-click menu only where it is useful: copy/paste in text fields.
        // (Debug builds keep the full menu, which has "Inspect" for DevTools.)
        core.ContextMenuRequested += (_, e) => e.Handled = !e.ContextMenuTarget.IsEditable;
#endif

        // Links (if any are ever added) open in the default browser, not in the widget.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
        };

        core.AddWebResourceRequestedFilter(Origin + "*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += ServeEmbeddedFile;

        // Hand the saved tasks (and what the page needs for updates) to the page before its script runs.
        var host = new JsonObject
        {
            ["version"] = AppInfo.Version,
            ["repo"] = AppInfo.Repo,
            ["asset"] = "TaskWidget.exe",
        };
        await core.AddScriptToExecuteOnDocumentCreatedAsync(
            $"window.taskWidgetHost = {host.ToJsonString()}; window.taskWidgetHost.initialState = {Storage.LoadStateJson()};");

        core.WebMessageReceived += OnWebMessage;
        core.Navigate(Origin + "index.html");
    }

    private void ServeEmbeddedFile(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var path = new Uri(e.Request.Uri).AbsolutePath.TrimStart('/');
        if (path.Length == 0) path = "index.html";

        var env = Web.CoreWebView2.Environment;
        var stream = EmbeddedContent.Open(path);
        e.Response = stream is null
            ? env.CreateWebResourceResponse(null, 404, "Not Found", "")
            : env.CreateWebResourceResponse(stream, 200, "OK", $"Content-Type: {EmbeddedContent.MimeType(path)}");
    }

    // Messages from the page: { type: "...", ... } — see the script in wwwroot/index.html.
    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (JsonNode.Parse(e.WebMessageAsJson) is not JsonObject msg) return;

        switch ((string?)msg["type"])
        {
            case "save":
                if (msg["data"] is JsonObject data) Storage.SaveState(data.ToJsonString(Json));
                break;
            case "drag":
                NativeMethods.BeginWindowDrag(new WindowInteropHelper(this).Handle);
                break;
            case "onTop":
                Topmost = (bool?)msg["value"] ?? false;
                break;
            case "minimize":
                WindowState = WindowState.Minimized;
                break;
            case "close":
                Close();
                break;
            case "theme":
                Background = ToBrush((string?)msg["bg"], Background);
                BorderBrush = ToBrush((string?)msg["line"], BorderBrush);
                break;
            case "title": // the window's name on the taskbar, in the page's language
                Title = (string?)msg["value"] ?? Title;
                break;
            case "update":
                _ = InstallUpdateAsync((string?)msg["url"] ?? "", (string?)msg["sha"] ?? "");
                break;
        }
    }

    private bool _updating;

    // Download, swap the exe and restart; progress and errors go back to window.taskWidgetUpdate in the page.
    private async Task InstallUpdateAsync(string url, string sha)
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var progress = new Progress<int>(pct => ReportUpdate(new JsonObject { ["state"] = "progress", ["pct"] = pct }));
            string exe = await Updater.DownloadAndSwapAsync(url, sha, progress);
            ReportUpdate(new JsonObject { ["state"] = "restarting" });
            Updater.StartNew(exe);
            Close();
        }
        catch (Exception ex)
        {
            _updating = false;
            ReportUpdate(ex is UpdateException known
                ? new JsonObject { ["state"] = "error", ["code"] = known.Code }
                : new JsonObject { ["state"] = "error", ["message"] = ex.Message });
        }
    }

    private void ReportUpdate(JsonObject status) =>
        _ = Web.CoreWebView2.ExecuteScriptAsync($"window.taskWidgetUpdate && window.taskWidgetUpdate({status.ToJsonString()})");

    // keeps Cyrillic readable in tasks.json
    private static readonly System.Text.Json.JsonSerializerOptions Json = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
    };

    private static Brush ToBrush(string? css, Brush fallback)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(css));
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}
