using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace NexusBackend.Services;

public class NetworkMonitorService
{
    private readonly ObsidianService _obsidian;

    public NetworkMonitorService(ObsidianService obsidian)
    {
        _obsidian = obsidian;
        EnsureNetworkFiles();
    }

    public IReadOnlyList<NetworkDevice> GetKnownDevices()
    {
        var content = _obsidian.ReadFile("07_Nexus/network-devices.md");
        return ParseDevices(content);
    }

    public async Task<NetworkStatusResult> CheckNetworkStatusAsync()
    {
        var devices = GetKnownDevices();
        var checks = await Task.WhenAll(devices.Select(CheckDeviceAsync));
        var results = checks.ToList();
        var unknownDevices = await DiscoverUnknownDevicesAsync(devices);
        var offline = results.Where(device => device.Monitor && !device.Online).ToList();
        var internetOnline = await CheckInternetAsync();
        var status = internetOnline && offline.Count == 0 && unknownDevices.Count == 0 ? "online" : "attention";

        var result = new NetworkStatusResult
        {
            Status = status,
            CheckedAt = DateTime.Now,
            Devices = results,
            TotalDevices = results.Count,
            OnlineDevices = results.Count(device => device.Online),
            OfflineDevices = results.Count(device => !device.Online),
            MonitoredDevices = results.Count(device => device.Monitor),
            AlertsToday = CountAlertsToday(),
            LastEvent = GetLastNetworkEvent(),
            UnknownDevices = unknownDevices,
            InternetOnline = internetOnline
        };

        LogNetworkCheck(result);

        foreach (var device in offline)
            LogNetworkAlert(device);

        return result;
    }

    public async Task<IReadOnlyList<UnknownNetworkDevice>> DiscoverUnknownDevicesAsync()
    {
        return await DiscoverUnknownDevicesAsync(GetKnownDevices());
    }

    public NetworkResolveResult ResolveUnknownDevice(NetworkResolveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Ip))
            return new NetworkResolveResult(false, "IP nao informado.");

        var ip = request.Ip.Trim();
        var mac = request.Mac.Trim();
        var name = string.IsNullOrWhiteSpace(request.Name) ? $"Dispositivo {ip}" : request.Name.Trim();
        var type = string.IsNullOrWhiteSpace(request.Type) ? "desconhecido" : request.Type.Trim();

        if (request.Known)
        {
            var alreadyKnown = GetKnownDevices().Any(device => device.Ip.Equals(ip, StringComparison.OrdinalIgnoreCase));
            if (!alreadyKnown)
            {
                _obsidian.AppendToFile(
                    "07_Nexus/network-devices.md",
                    $"\n\n## Dispositivos reconhecidos pelo Nexus\n- {name}\n  - IP: {ip}\n  - Tipo: {type}\n  - Monitorar: sim\n  - Critico: nao\n"
                );
            }

            MarkUnknownDeviceResolved(ip, mac, "conhecido");
            _obsidian.AppendToFile(
                "07_Nexus/network-log.md",
                $"\n\n### {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nEvento: dispositivo reconhecido\nNome: {name}\nIP: {ip}\nMAC: {mac}\n"
            );

            return new NetworkResolveResult(true, $"{name} foi marcado como conhecido.");
        }

        MarkUnknownDeviceResolved(ip, mac, "nao_conhecido");
        _obsidian.AppendToFile(
            "07_Nexus/network-alerts.md",
            $"\n\n### {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nAlerta: dispositivo nao reconhecido confirmado\nIP: {ip}\nMAC: {mac}\nAcao recomendada: verificar roteador, bloquear se necessario e trocar senha do Wi-Fi se houver risco.\n"
        );

        return new NetworkResolveResult(true, $"Dispositivo {ip} marcado como nao conhecido.");
    }

    public async Task<NetworkDeviceStatus?> CheckDeviceByNameAsync(string query)
    {
        var normalizedQuery = Normalize(query);
        var device = GetKnownDevices()
            .FirstOrDefault(item =>
                Normalize(item.Name).Contains(normalizedQuery) ||
                Normalize(item.Ip).Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase));

        return device is null ? null : await CheckDeviceAsync(device);
    }

    private async Task<NetworkDeviceStatus> CheckDeviceAsync(NetworkDevice device)
    {
        var result = new NetworkDeviceStatus
        {
            Name = device.Name,
            Ip = device.Ip,
            Type = device.Type,
            Monitor = device.Monitor,
            Critical = device.Critical,
            LastCheckedAt = DateTime.Now
        };

        if (string.IsNullOrWhiteSpace(device.Ip))
        {
            result.Online = false;
            result.Status = "ip_missing";
            return result;
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(device.Ip, 1600);

            result.Online = reply.Status == IPStatus.Success;
            result.Status = result.Online ? "online" : reply.Status.ToString();
            result.LatencyMs = result.Online ? reply.RoundtripTime : null;
        }
        catch (Exception ex)
        {
            result.Online = false;
            result.Status = ex.GetType().Name;
        }

        return result;
    }

    private static async Task<bool> CheckInternetAsync()
    {
        var targets = (Environment.GetEnvironmentVariable("NETWORK_INTERNET_CHECK_TARGETS") ?? "1.1.1.1,8.8.8.8")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var target in targets)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target, 1200);
                if (reply.Status == IPStatus.Success)
                    return true;
            }
            catch
            {
                // Try next target.
            }
        }

        return false;
    }

    private async Task<List<UnknownNetworkDevice>> DiscoverUnknownDevicesAsync(IReadOnlyList<NetworkDevice> knownDevices)
    {
        await SweepConfiguredSubnetsAsync();

        var knownIps = knownDevices
            .Select(device => device.Ip)
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resolved = GetResolvedUnknownKeys();
        var pending = GetPendingUnknownKeys();
        var discovered = await ReadArpTableAsync();
        var unknown = new List<UnknownNetworkDevice>();

        foreach (var item in discovered)
        {
            if (knownIps.Contains(item.Ip) || IsIgnoredDiscoveryIp(item.Ip))
                continue;

            var key = BuildUnknownKey(item.Ip, item.Mac);
            if (resolved.Contains(key))
                continue;

            var device = new UnknownNetworkDevice
            {
                Ip = item.Ip,
                Mac = item.Mac,
                DetectedAt = DateTime.Now,
                Source = "arp"
            };

            unknown.Add(device);

            if (pending.Contains(key))
                continue;

            RegisterUnknownDevice(device);
            pending.Add(key);
        }

        return unknown.OrderBy(device => device.Ip).ToList();
    }

    private static async Task SweepConfiguredSubnetsAsync()
    {
        if (!DiscoveryEnabled())
            return;

        var ips = GetDiscoveryIps()
            .Where(ip => !IsIgnoredDiscoveryIp(ip))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ips.Length == 0)
            return;

        using var throttler = new SemaphoreSlim(48);
        var tasks = ips.Select(async ip =>
        {
            await throttler.WaitAsync();

            try
            {
                using var ping = new Ping();
                await ping.SendPingAsync(ip, 350);
            }
            catch
            {
                // Best-effort discovery. Some devices block ICMP but may still appear in ARP.
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private static async Task<List<ArpEntry>> ReadArpTableAsync()
    {
        var entries = new List<ArpEntry>();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "arp",
                Arguments = "-a",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return entries;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            foreach (Match match in Regex.Matches(output, @"(?<ip>(?:\d{1,3}\.){3}\d{1,3})\s+(?<mac>(?:[0-9a-f]{2}[-:]){5}[0-9a-f]{2})\s+", RegexOptions.IgnoreCase))
            {
                entries.Add(new ArpEntry(
                    match.Groups["ip"].Value.Trim(),
                    match.Groups["mac"].Value.Trim().Replace('-', ':').ToLowerInvariant()
                ));
            }
        }
        catch
        {
            // ARP discovery is best-effort. Ping monitoring remains available if this fails.
        }

        return entries.DistinctBy(entry => $"{entry.Ip}|{entry.Mac}").ToList();
    }

    private void EnsureNetworkFiles()
    {
        _obsidian.EnsureFile(
            "07_Nexus/network-monitoring.md",
            """
            # Monitoramento de Rede

            O Nexus monitora status, IP, tipo de dispositivo e eventos basicos da rede.

            ## Escopo atual
            - Verificacao online/offline por ping.
            - Varredura leve por ping no range configurado para popular ARP.
            - Deteccao de novos dispositivos via tabela ARP local.
            - Registro de eventos e alertas no Obsidian.
            - Sem captura de conteudo de trafego.
            """
        );

        _obsidian.EnsureFile(
            "07_Nexus/network-devices.md",
            """
            # Dispositivos da Rede

            ## Infraestrutura
            - Gateway
              - IP: 172.16.32.1
              - Tipo: roteador
              - Monitorar: sim
              - Critico: sim

            ## Casa inteligente
            - Home Assistant
              - IP: 172.16.32.73
              - Tipo: automacao
              - Monitorar: sim
              - Critico: sim

            ## Dispositivos pessoais
            - PC Nexus
              - IP: 127.0.0.1
              - Tipo: computador
              - Monitorar: sim
              - Critico: nao

            ## Regras
            - Alertar se Home Assistant cair.
            - Alertar se Gateway cair.
            - Nao registrar conteudo de trafego, apenas status e metadados.
            """
        );

        _obsidian.EnsureFile(
            "07_Nexus/network-alerts.md",
            """
            # Alertas de Rede

            ## Regras
            - Se servidor principal ficar offline, alertar.
            - Se dispositivo desconhecido entrar na rede, registrar.
            - Se consumo de rede for anormal, registrar.
            - Se Home Assistant cair, alertar.

            ## Eventos
            """
        );

        _obsidian.EnsureFile(
            "07_Nexus/network-unknown-devices.md",
            """
            # Dispositivos Desconhecidos da Rede

            ## Pendentes

            ## Resolvidos
            """
        );

        _obsidian.EnsureFile(
            "07_Nexus/network-log.md",
            """
            # Log de Rede

            ## Eventos
            """
        );
    }

    private void LogNetworkCheck(NetworkStatusResult result)
    {
        var offline = result.Devices
            .Where(device => device.Monitor && !device.Online)
            .Select(device => $"{device.Name} ({device.Ip})")
            .ToArray();

        var unknown = result.UnknownDevices.Select(device => $"{device.Ip} ({device.Mac})").ToArray();
        var summary = offline.Length == 0 && unknown.Length == 0
            ? "Todos os dispositivos monitorados responderam e nenhum desconhecido foi detectado."
            : string.Join(" | ", new[]
            {
                offline.Length == 0 ? "" : "Offline: " + string.Join(", ", offline),
                unknown.Length == 0 ? "" : "Desconhecidos: " + string.Join(", ", unknown)
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

        _obsidian.AppendToFile(
            "07_Nexus/network-log.md",
            $"\n\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nStatus: {result.Status}\nInternet: {(result.InternetOnline ? "online" : "offline")}\nMonitorados: {result.MonitoredDevices}\nOnline: {result.OnlineDevices}\nOffline: {result.OfflineDevices}\nDesconhecidos: {result.UnknownDevices.Count}\nResumo: {summary}\n"
        );
    }

    private void LogNetworkAlert(NetworkDeviceStatus device)
    {
        if (!device.Critical)
            return;

        _obsidian.AppendToFile(
            "07_Nexus/network-alerts.md",
            $"\n\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nTipo: dispositivo offline\nDispositivo: {device.Name}\nIP: {device.Ip}\nStatus: critico\nAcao sugerida: verificar energia/rede do host\nDetalhe: {device.Status}\n"
        );
    }

    private void RegisterUnknownDevice(UnknownNetworkDevice device)
    {
        _obsidian.AppendToFile(
            "07_Nexus/network-unknown-devices.md",
            $"\n- Pendente\n  - IP: {device.Ip}\n  - MAC: {device.Mac}\n  - Detectado: {device.DetectedAt:yyyy-MM-dd HH:mm:ss}\n  - Origem: {device.Source}\n"
        );

        _obsidian.AppendToFile(
            "07_Nexus/network-alerts.md",
            $"\n\n## {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nTipo: novo dispositivo conectado\nDispositivo: desconhecido\nIP: {device.Ip}\nMAC: {device.Mac}\nStatus: pendente\nAcao sugerida: Gabriel, voce conhece este dispositivo?\n"
        );
    }

    private void MarkUnknownDeviceResolved(string ip, string mac, string decision)
    {
        _obsidian.AppendToFile(
            "07_Nexus/network-unknown-devices.md",
            $"\n- Resolvido\n  - IP: {ip}\n  - MAC: {mac}\n  - Decisao: {decision}\n  - Resolvido em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n"
        );
    }

    private int CountAlertsToday()
    {
        var content = _obsidian.ReadFile("07_Nexus/network-alerts.md");
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        return Regex.Matches(content, Regex.Escape(today)).Count;
    }

    private string GetLastNetworkEvent()
    {
        var content = _obsidian.ReadFile("07_Nexus/network-log.md");
        var matches = Regex.Matches(content, @"#{2,3}\s+(.+)");
        return matches.Count == 0 ? "" : matches[^1].Groups[1].Value.Trim();
    }

    private HashSet<string> GetPendingUnknownKeys()
    {
        return GetUnknownKeysFromSection("Pendente");
    }

    private HashSet<string> GetResolvedUnknownKeys()
    {
        return GetUnknownKeysFromSection("Resolvido");
    }

    private HashSet<string> GetUnknownKeysFromSection(string marker)
    {
        var content = _obsidian.ReadFile("07_Nexus/network-unknown-devices.md");
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pattern = $@"-\s*{Regex.Escape(marker)}\s*\n\s*-\s*IP:\s*(?<ip>[^\r\n]+)\s*\n\s*-\s*MAC:\s*(?<mac>[^\r\n]+)";

        foreach (Match match in Regex.Matches(content, pattern, RegexOptions.IgnoreCase))
            keys.Add(BuildUnknownKey(match.Groups["ip"].Value.Trim(), match.Groups["mac"].Value.Trim()));

        return keys;
    }

    private static bool DiscoveryEnabled()
    {
        return (Environment.GetEnvironmentVariable("NETWORK_DISCOVERY_ENABLED") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetDiscoveryIps()
    {
        var configured = Environment.GetEnvironmentVariable("NETWORK_DISCOVERY_SUBNETS");
        var subnets = string.IsNullOrWhiteSpace(configured)
            ? GetLocalPrivateSlash24Subnets()
            : configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var subnet in subnets)
        {
            foreach (var ip in ExpandSlash24Subnet(subnet))
                yield return ip;
        }
    }

    private static IEnumerable<string> GetLocalPrivateSlash24Subnets()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address.ToString())
            .Where(IsPrivateIpv4)
            .Select(ip => string.Join('.', ip.Split('.').Take(3)) + ".0/24")
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ExpandSlash24Subnet(string subnet)
    {
        var clean = subnet.Trim();
        var baseIp = clean.Split('/')[0];
        var parts = baseIp.Split('.');

        if (parts.Length != 4 || !clean.EndsWith("/24", StringComparison.OrdinalIgnoreCase))
            yield break;

        var prefix = $"{parts[0]}.{parts[1]}.{parts[2]}";

        for (var host = 1; host <= 254; host++)
            yield return $"{prefix}.{host}";
    }

    private static bool IsPrivateIpv4(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address))
            return false;

        var bytes = address.GetAddressBytes();

        return bytes[0] == 10 ||
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168);
    }

    private static string BuildUnknownKey(string ip, string mac)
    {
        return $"{ip.Trim()}|{mac.Trim().Replace('-', ':').ToLowerInvariant()}";
    }

    private static bool IsIgnoredDiscoveryIp(string ip)
    {
        return ip.StartsWith("224.", StringComparison.OrdinalIgnoreCase) ||
            ip.StartsWith("239.", StringComparison.OrdinalIgnoreCase) ||
            ip == "255.255.255.255" ||
            ip.EndsWith(".255", StringComparison.OrdinalIgnoreCase) ||
            ip == "0.0.0.0";
    }

    private static IReadOnlyList<NetworkDevice> ParseDevices(string content)
    {
        var devices = new List<NetworkDevice>();
        NetworkDevice? current = null;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("- ") && !line.Contains(':'))
            {
                if (current is not null && !string.IsNullOrWhiteSpace(current.Name) && !string.IsNullOrWhiteSpace(current.Ip))
                    devices.Add(current);

                current = new NetworkDevice
                {
                    Name = line[2..].Trim(),
                    Monitor = true
                };
                continue;
            }

            if (current is null)
                continue;

            var match = Regex.Match(line, @"^-\s*(IP|Tipo|Monitorar|Critico|Crítico):\s*(.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;

            var key = Normalize(match.Groups[1].Value);
            var value = match.Groups[2].Value.Trim();

            if (key == "ip")
                current.Ip = value;
            else if (key == "tipo")
                current.Type = value;
            else if (key == "monitorar")
                current.Monitor = IsYes(value);
            else if (key is "critico")
                current.Critical = IsYes(value);
        }

        if (current is not null && !string.IsNullOrWhiteSpace(current.Name) && !string.IsNullOrWhiteSpace(current.Ip))
            devices.Add(current);

        return devices;
    }

    private static bool IsYes(string value)
    {
        return value.Equals("sim", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string text)
    {
        return text.Trim().ToLowerInvariant()
            .Replace("á", "a")
            .Replace("à", "a")
            .Replace("ã", "a")
            .Replace("â", "a")
            .Replace("é", "e")
            .Replace("ê", "e")
            .Replace("í", "i")
            .Replace("ó", "o")
            .Replace("ô", "o")
            .Replace("õ", "o")
            .Replace("ú", "u")
            .Replace("ç", "c");
    }
}

public class NetworkDevice
{
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Monitor { get; set; }
    public bool Critical { get; set; }
}

public class NetworkDeviceStatus : NetworkDevice
{
    public bool Online { get; set; }
    public string Status { get; set; } = "";
    public long? LatencyMs { get; set; }
    public DateTime LastCheckedAt { get; set; }
}

public class NetworkStatusResult
{
    public string Status { get; set; } = "";
    public DateTime CheckedAt { get; set; }
    public List<NetworkDeviceStatus> Devices { get; set; } = new();
    public int TotalDevices { get; set; }
    public int OnlineDevices { get; set; }
    public int OfflineDevices { get; set; }
    public int MonitoredDevices { get; set; }
    public int AlertsToday { get; set; }
    public string LastEvent { get; set; } = "";
    public List<UnknownNetworkDevice> UnknownDevices { get; set; } = new();
    public bool InternetOnline { get; set; }
}

public class UnknownNetworkDevice
{
    public string Ip { get; set; } = "";
    public string Mac { get; set; } = "";
    public DateTime DetectedAt { get; set; }
    public string Source { get; set; } = "";
}

public class NetworkResolveRequest
{
    public string Ip { get; set; } = "";
    public string Mac { get; set; } = "";
    public bool Known { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public class NetworkResolveResult
{
    public NetworkResolveResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; set; }
    public string Message { get; set; }
}

internal record ArpEntry(string Ip, string Mac);
