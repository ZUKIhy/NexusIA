using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NexusBackend.Services;

public class ElevenLabsService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ElevenLabsService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<byte[]?> TextToSpeechAsync(string text)
    {
        var apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
        var voiceId = Environment.GetEnvironmentVariable("ELEVENLABS_VOICE_ID");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(voiceId))
            return null;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("xi-api-key", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));

        var payload = new
        {
            text,
            model_id = "eleven_multilingual_v2",
            voice_settings = new { stability = 0.45, similarity_boost = 0.85 }
        };

        var response = await client.PostAsync(
            $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync();
    }
}
