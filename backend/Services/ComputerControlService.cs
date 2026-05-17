using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NexusBackend.Services;

public record ComputerActionResult(bool Success, string Message, string? Target = null);

public class ComputerControlService
{
    private readonly Dictionary<string, string> _knownApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "chrome.exe",
        ["google chrome"] = "chrome.exe",
        ["google chorme"] = "chrome.exe",
        ["chorme"] = "chrome.exe",
        ["edge"] = "msedge.exe",
        ["microsoft edge"] = "msedge.exe",
        ["navegador"] = "msedge.exe",
        ["explorer"] = "explorer.exe",
        ["notepad"] = "notepad.exe",
        ["bloco de notas"] = "notepad.exe",
        ["calculadora"] = "calc.exe",
        ["calculator"] = "calc.exe",
        ["powershell"] = "powershell.exe",
        ["terminal"] = "wt.exe",
        ["vscode"] = "code",
        ["vs code"] = "code",
        ["visual studio code"] = "code",
        ["obsidian"] = "obsidian.exe",
        ["word"] = "winword.exe",
        ["excel"] = "excel.exe",
        ["powerpoint"] = "powerpnt.exe",
        ["outlook"] = "outlook.exe",
        ["whatsapp"] = "store:whatsapp",
        ["whats"] = "store:whatsapp",
        ["zap"] = "store:whatsapp",
        ["teams"] = "ms-teams:",
        ["spotify"] = "spotify.exe",
        ["discord"] = "discord.exe"
    };

    public ComputerActionResult OpenApp(string appName)
    {
        var target = CleanAppName(appName);

        if (string.IsNullOrWhiteSpace(target))
            return new ComputerActionResult(false, "Informe qual programa devo abrir.");

        if (!_knownApps.TryGetValue(target, out var executable))
        {
            var discovered = TryFindInstalledApp(target);

            if (discovered is null)
                return new ComputerActionResult(false, $"Não encontrei esse programa instalado ou na lista segura: {appName}");

            executable = discovered;
        }

        if (executable.StartsWith("store:", StringComparison.OrdinalIgnoreCase))
            return StartStoreApp(executable["store:".Length..], $"Abrindo programa: {appName}");

        return Start(executable, $"Abrindo programa: {appName}", executable);
    }

    public ComputerActionResult OpenFolder(string folder)
    {
        var path = ResolveFolder(folder);

        if (path is null)
            return new ComputerActionResult(false, $"Não reconheci essa pasta: {folder}");

        Directory.CreateDirectory(path);
        return Start(path, $"Abrindo pasta: {path}", path);
    }

    public ComputerActionResult OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new ComputerActionResult(false, "Informe a URL que devo abrir.");

        var target = url.Trim();

        if (!target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            target = "https://" + target;

        return Start(target, $"Abrindo URL: {target}", target);
    }

    public ComputerActionResult SearchWeb(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new ComputerActionResult(false, "Informe o que devo pesquisar.");

        var url = "https://www.google.com/search?q=" + Uri.EscapeDataString(query.Trim());
        return Start(url, $"Pesquisando no navegador: {query.Trim()}", url);
    }

    public ComputerActionResult PasteText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ComputerActionResult(false, "Informe o texto que devo digitar.");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new ComputerActionResult(false, "Digitação automática está disponível apenas no Windows.");

        var script = $$"""
        Start-Sleep -Milliseconds 700
        Set-Clipboard -Value @'
        {{text}}
        '@
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.SendKeys]::SendWait('^v')
        """;

        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(startInfo);
        return new ComputerActionResult(true, "Vou colar o texto na janela ativa.", text);
    }

    public object GetCapabilities()
    {
        return new
        {
            canOpenApps = true,
            canOpenFolders = true,
            canOpenUrls = true,
            canSearchWeb = true,
            canPasteIntoActiveWindow = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            safeApps = _knownApps.Keys.OrderBy(x => x).ToArray(),
            discoveredApps = DiscoverInstalledApps().Take(60).ToArray(),
            knownFolders = new[] { "downloads", "documentos", "desktop", "vault", "projeto", "frontend", "backend" }
        };
    }

    public IEnumerable<string> SearchInstalledApps(string query)
    {
        var clean = Normalize(query);

        return DiscoverInstalledApps()
            .Where(name => Normalize(name).Contains(clean, StringComparison.OrdinalIgnoreCase))
            .Take(20);
    }

    private static ComputerActionResult Start(string target, string successMessage, string? resultTarget)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return new ComputerActionResult(true, successMessage, resultTarget);
        }
        catch (Exception exc)
        {
            return new ComputerActionResult(false, $"Não consegui abrir: {target}. Detalhe: {exc.Message}", target);
        }
    }

    private static string? ResolveFolder(string folder)
    {
        var target = Normalize(folder);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
        var vault = Environment.GetEnvironmentVariable("VAULT_PATH");

        return target switch
        {
            "downloads" or "download" => Path.Combine(userProfile, "Downloads"),
            "documentos" or "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "desktop" or "area de trabalho" or "área de trabalho" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "vault" or "obsidian" => string.IsNullOrWhiteSpace(vault) ? Path.Combine(projectRoot, "vault") : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), vault)),
            "projeto" or "nexus" => projectRoot,
            "frontend" => Path.Combine(projectRoot, "frontend"),
            "backend" => Path.Combine(projectRoot, "backend"),
            _ when Directory.Exists(folder) => folder,
            _ => null
        };
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .Trim('.', ',', '?', '!', ':', ';')
            .ToLowerInvariant();
    }

    private static string CleanAppName(string value)
    {
        var target = Normalize(value)
            .Replace("google chorme", "google chrome", StringComparison.OrdinalIgnoreCase)
            .Replace("chorme", "chrome", StringComparison.OrdinalIgnoreCase)
            .Replace("navegador google", "google chrome", StringComparison.OrdinalIgnoreCase)
            .Replace("o google", "google", StringComparison.OrdinalIgnoreCase)
            .Replace("o programa", "", StringComparison.OrdinalIgnoreCase)
            .Replace("programa", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (target.StartsWith("o ", StringComparison.OrdinalIgnoreCase) || target.StartsWith("a ", StringComparison.OrdinalIgnoreCase))
            target = target[2..].Trim();

        return target;
    }

    private static string? TryFindInstalledApp(string appName)
    {
        var appPath = TryFindInAppPaths(appName);
        if (appPath is not null)
            return appPath;

        var shortcut = TryFindStartMenuShortcut(appName);
        if (shortcut is not null)
            return shortcut;

        var executable = TryFindExecutableInCommonFolders(appName);
        if (executable is not null)
            return executable;

        return null;
    }

    private static string? TryFindInAppPaths(string appName)
    {
        var possibleExe = appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appName
            : appName + ".exe";

        var registryPaths = new[]
        {
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{possibleExe}",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\{possibleExe}"
        };

        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var path in registryPaths)
            {
                using var key = root.OpenSubKey(path);
                var value = key?.GetValue(null)?.ToString();

                if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                    return value;
            }
        }

        return null;
    }

    private static string? TryFindStartMenuShortcut(string appName)
    {
        var startMenuRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var root in startMenuRoots.Where(Directory.Exists))
        {
            var shortcuts = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories);
            var match = shortcuts
                .Select(path => new
                {
                    Path = path,
                    Name = Normalize(Path.GetFileNameWithoutExtension(path))
                })
                .Where(item => item.Name.Contains(appName, StringComparison.OrdinalIgnoreCase) || appName.Contains(item.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Name.Length)
                .FirstOrDefault();

            if (match is not null)
                return match.Path;
        }

        return null;
    }

    private static string? TryFindExecutableInCommonFolders(string appName)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            try
            {
                var match = Directory.GetFiles(root, "*.exe", SearchOption.AllDirectories)
                    .Where(path => Normalize(Path.GetFileNameWithoutExtension(path)).Contains(appName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path.Length)
                    .FirstOrDefault();

                if (match is not null)
                    return match;
            }
            catch
            {
                // Ignore protected folders.
            }
        }

        return null;
    }

    private static ComputerActionResult StartStoreApp(string appName, string successMessage)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new ComputerActionResult(false, "Apps da Microsoft Store só podem ser abertos no Windows.", appName);

        var script = $$"""
        $shell = New-Object -ComObject Shell.Application
        $apps = $shell.Namespace('shell:AppsFolder').Items()
        $app = $apps | Where-Object { $_.Name -like '*{{EscapePowerShellLike(appName)}}*' } | Select-Object -First 1
        if ($null -eq $app) { exit 2 }
        $app.InvokeVerb('open')
        """;

        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });

        return process is null
            ? new ComputerActionResult(false, $"Não consegui abrir app da Store: {appName}", appName)
            : new ComputerActionResult(true, successMessage, appName);
    }

    private static IEnumerable<string> DiscoverInstalledApps()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var shortcut in DiscoverStartMenuShortcuts())
            names.Add(Path.GetFileNameWithoutExtension(shortcut));

        foreach (var name in DiscoverStoreApps())
            names.Add(name);

        foreach (var known in names)
            yield return known;
    }

    private static IEnumerable<string> DiscoverStartMenuShortcuts()
    {
        var shortcuts = new List<string>();
        var startMenuRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var root in startMenuRoots.Where(Directory.Exists))
        {
            try
            {
                shortcuts.AddRange(Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories));
            }
            catch
            {
                // Ignore protected or broken Start Menu entries.
            }
        }

        return shortcuts;
    }

    private static IEnumerable<string> DiscoverStoreApps()
    {
        var apps = new List<string>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return apps;

        var script = """
        $shell = New-Object -ComObject Shell.Application
        $shell.Namespace('shell:AppsFolder').Items() | ForEach-Object { $_.Name }
        """;

        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return apps;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3500);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                apps.Add(line);
        }
        catch
        {
        }

        return apps;
    }

    private static string EscapePowerShellLike(string value)
    {
        return value.Replace("'", "''").Replace("[", "`[").Replace("]", "`]");
    }
}
