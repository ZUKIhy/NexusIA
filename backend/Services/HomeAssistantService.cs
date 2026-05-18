using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NexusBackend.Services;

public class HomeAssistantService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeAssistantService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsEnabled()
    {
        return (Environment.GetEnvironmentVariable("HOME_ASSISTANT_ENABLED") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public bool RequiresConfirmation()
    {
        return (Environment.GetEnvironmentVariable("HOME_ASSISTANT_REQUIRE_CONFIRMATION") ?? "true")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateClient()
    {
        var url = Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL") ?? "";
        var token = Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN") ?? "";

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("HOME_ASSISTANT_URL nao configurado.");

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("HOME_ASSISTANT_TOKEN nao configurado.");

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    public async Task<bool> PingAsync()
    {
        if (!IsEnabled())
            return false;

        try
        {
            var client = CreateClient();
            var response = await client.GetAsync("api/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetStatesAsync()
    {
        var client = CreateClient();
        var response = await client.GetAsync("api/states");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> GetStateAsync(string entityId)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"api/states/{Uri.EscapeDataString(entityId)}");
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<bool> CallServiceAsync(string domain, string service, object payload)
    {
        var client = CreateClient();
        var json = JsonSerializer.Serialize(payload);

        var response = await client.PostAsync(
            $"api/services/{domain}/{service}",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        return response.IsSuccessStatusCode;
    }

    public Task<bool> TurnOnAsync(string entityId)
    {
        var domain = GetDomain(entityId);

        return CallServiceAsync(domain, "turn_on", new
        {
            entity_id = entityId
        });
    }

    public Task<bool> TurnOffAsync(string entityId)
    {
        var domain = GetDomain(entityId);

        return CallServiceAsync(domain, "turn_off", new
        {
            entity_id = entityId
        });
    }

    public Task<bool> ToggleAsync(string entityId)
    {
        var domain = GetDomain(entityId);

        return CallServiceAsync(domain, "toggle", new
        {
            entity_id = entityId
        });
    }

    public Task<bool> SetLightBrightnessAsync(string entityId, int brightnessPercent)
    {
        var brightness = Math.Clamp((int)(brightnessPercent / 100.0 * 255), 1, 255);

        return CallServiceAsync("light", "turn_on", new
        {
            entity_id = entityId,
            brightness
        });
    }

    private static string GetDomain(string entityId)
    {
        var dotIndex = entityId.IndexOf('.');

        if (dotIndex <= 0)
            throw new ArgumentException("EntityId invalido. Use o formato dominio.nome.", nameof(entityId));

        return entityId[..dotIndex];
    }
}
