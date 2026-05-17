using NexusBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace NexusBackend.Controllers;

[ApiController]
[Route("api/voice")]
public class VoiceController : ControllerBase
{
    private readonly ElevenLabsService _elevenLabs;

    public VoiceController(ElevenLabsService elevenLabs)
    {
        _elevenLabs = elevenLabs;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var apiKey = Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
        var voiceId = Environment.GetEnvironmentVariable("ELEVENLABS_VOICE_ID");

        return Ok(new
        {
            configured = !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(voiceId),
            voiceId = string.IsNullOrWhiteSpace(voiceId) ? null : voiceId,
            provider = "ElevenLabs",
            fallback = "Browser SpeechSynthesis"
        });
    }

    [HttpPost("tts")]
    public async Task<IActionResult> TextToSpeech([FromBody] TtsRequest request)
    {
        var audio = await _elevenLabs.TextToSpeechAsync(request.Text);

        if (audio is null)
            return BadRequest(new { error = "ElevenLabs não configurado ou falhou. Use SpeechSynthesis no frontend como fallback." });

        return File(audio, "audio/mpeg");
    }
}

public class TtsRequest
{
    public string Text { get; set; } = "";
}
