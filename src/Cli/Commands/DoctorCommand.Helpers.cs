#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

internal sealed partial class DoctorChecker
{
    private static string Combine(string root, string relative) => Path.Combine(relative.Split('/').Prepend(root).ToArray());

    private static bool TryReadJson(string path, out JsonDocument document)
    {
        document = null!;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            return true;
        }
        catch (Exception) when (!Debugger.IsAttached)
        {
            return false;
        }
    }

    private static bool TryReadXml(string path, out XDocument document)
    {
        document = null!;
        try
        {
            document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            return true;
        }
        catch (Exception) when (!Debugger.IsAttached)
        {
            return false;
        }
    }

    private static bool IsSdkVersion(string? version) => version is not null && Regex.IsMatch(version, @"^10\.0\.3\d\d$");

    private static bool IsAtLeast(string? value, Version minimum) => Version.TryParse(value, out var version) && version >= minimum;

    private static bool HasTrackedContent(string root, string relativePath)
    {
        if (!Directory.Exists(Path.Combine(root, ".git")))
        {
            return true;
        }

        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = root,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add($"safe.directory={root.Replace('\\', '/')}");
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(relativePath.Replace('\\', '/'));

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return true;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode != 0 || !string.IsNullOrWhiteSpace(output);
    }

    private sealed class PackageVersionPin
    {
        internal PackageVersionPin(string id, string version)
        {
            Id = id;
            Version = version;
        }

        internal string Id { get; }

        internal string Version { get; }
    }
}
