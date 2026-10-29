using System.Reflection;

namespace TaskWidget;

/// <summary>Version and the GitHub repository that updates come from.</summary>
internal static class AppInfo
{
    /// <summary>"owner/name"; every release there carries TaskWidget.exe and DayTasks.apk (each with a .sha256).</summary>
    public const string Repo = "DimaN221221/taskwidget";

    /// <summary>"1.3.0" — from &lt;Version&gt; in TaskWidget.csproj (the Android app reads the same line).</summary>
    public static readonly string Version = ReadVersion();

    private static string ReadVersion()
    {
        var info = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // .NET appends "+<commit>" to the informational version; the release tag has only the numbers.
        return info?.Split('+')[0] ?? "0.0.0";
    }
}
