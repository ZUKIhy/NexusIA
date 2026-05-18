using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NexusBackend.Services;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly ObsidianService _obsidian;
    private readonly OperationService _operation;

    public SystemController(ObsidianService obsidian, OperationService operation)
    {
        _obsidian = obsidian;
        _operation = operation;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        var vault = BuildVaultStatus();
        var ollama = BuildOllamaStatus();
        var gpu = BuildGpuStatus();

        return Ok(new
        {
            nexus = "online",
            backend = "online",
            frontend = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173",
            vault,
            ollama,
            gpu,
            openai = new
            {
                configured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
                status = "configurada; usada apenas como ultimo recurso"
            },
            claude = BuildClaudeStatus(),
            backup = BuildBackupStatus(),
            autostart = System.IO.File.Exists(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "Nexus Autostart.lnk")),
            operationMode = _operation.IsOperationMode()
        });
    }

    [HttpGet("ollama")]
    public IActionResult Ollama() => Ok(BuildOllamaStatus());

    [HttpGet("vault")]
    public IActionResult Vault() => Ok(BuildVaultStatus());

    [HttpGet("gpu")]
    public IActionResult Gpu() => Ok(BuildGpuStatus());

    private object BuildVaultStatus()
    {
        var fullVault = _obsidian.GetFullPath("");
        var files = Directory.Exists(fullVault)
            ? Directory.GetFiles(fullVault, "*.md", SearchOption.AllDirectories)
            : Array.Empty<string>();

        var latest = files
            .Select(file => new FileInfo(file))
            .OrderByDescending(file => file.LastWriteTime)
            .FirstOrDefault();

        return new
        {
            connected = Directory.Exists(fullVault),
            path = fullVault,
            markdownDocuments = files.Length,
            lastUpdate = latest?.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
            lastIndex = SafeFileTime(_obsidian.GetFullPath("07_Nexus/document-index.json"))
        };
    }

    private static object BuildClaudeStatus()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
        var configured = !string.IsNullOrWhiteSpace(apiKey) &&
                         !apiKey.Equals("coloque_sua_chave_aqui", StringComparison.OrdinalIgnoreCase);

        return new
        {
            enabled = (Environment.GetEnvironmentVariable("CLAUDE_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            configured,
            model = Environment.GetEnvironmentVariable("CLAUDE_MODEL") ?? "",
            useForChat = (Environment.GetEnvironmentVariable("CLAUDE_USE_FOR_CHAT") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            useForStandardize = (Environment.GetEnvironmentVariable("CLAUDE_USE_FOR_STANDARDIZE") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            useForChecklist = (Environment.GetEnvironmentVariable("CLAUDE_USE_FOR_CHECKLIST") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            useForReports = (Environment.GetEnvironmentVariable("CLAUDE_USE_FOR_REPORTS") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static object BuildOllamaStatus()
    {
        var url = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
        var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "nexus-qwen3b";

        return new
        {
            enabled = (Environment.GetEnvironmentVariable("OLLAMA_ENABLED") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
            url,
            model,
            online = IsPortListening(11434)
        };
    }

    private static object BuildGpuStatus()
    {
        try
        {
            var startInfo = new ProcessStartInfo("nvidia-smi", "--query-gpu=name,memory.used,memory.total --format=csv,noheader")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return new { available = false, name = "NVIDIA GeForce RTX 3060 12GB", detail = "nvidia-smi indisponivel" };

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2500);

            return new
            {
                available = process.ExitCode == 0,
                detail = string.IsNullOrWhiteSpace(output) ? "GPU nao detectada via nvidia-smi" : output
            };
        }
        catch
        {
            return new { available = false, name = "NVIDIA GeForce RTX 3060 12GB", detail = "nvidia-smi indisponivel" };
        }
    }

    private static object BuildBackupStatus()
    {
        var backupRoot = @"C:\Users\User\OneDrive\Documentos\nexus-gabriel\Backups\NexusVault";
        if (!Directory.Exists(backupRoot))
            backupRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Backups", "NexusVault"));

        var latest = Directory.Exists(backupRoot)
            ? Directory.GetFiles(backupRoot, "nexus-vault-*.zip")
                .Select(file => new FileInfo(file))
                .OrderByDescending(file => file.LastWriteTime)
                .FirstOrDefault()
            : null;

        return new
        {
            path = backupRoot,
            exists = Directory.Exists(backupRoot),
            lastBackup = latest?.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
            lastBackupFile = latest?.FullName ?? ""
        };
    }

    private static bool IsPortListening(int port)
    {
        try
        {
            var output = RunPowerShell($"(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {{ $_.LocalPort -eq {port} }} | Select-Object -First 1).LocalPort");
            return output.Contains(port.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string RunPowerShell(string command)
    {
        var startInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"{command}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null) return "";

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(2500);
        return output;
    }

    private static string SafeFileTime(string path)
    {
        return System.IO.File.Exists(path) ? System.IO.File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss") : "";
    }
}
