using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusBackend.Services;

public class SpotifyService
{
    private const string AccountsBaseUrl = "https://accounts.spotify.com";
    private const string ApiBaseUrl = "https://api.spotify.com/v1";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly object _tokenLock = new();

    public SpotifyService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public bool IsEnabled()
    {
        return (Environment.GetEnvironmentVariable("SPOTIFY_ENABLED") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsConfigured()
    {
        var clientId = GetClientId();
        var clientSecret = GetClientSecret();

        return !string.IsNullOrWhiteSpace(clientId) &&
            !clientId.Equals("seu_client_id", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(clientSecret) &&
            !clientSecret.Equals("seu_client_secret", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(GetRedirectUri());
    }

    public bool IsAuthenticated()
    {
        var token = LoadToken();
        return !string.IsNullOrWhiteSpace(token?.RefreshToken) ||
            (!string.IsNullOrWhiteSpace(token?.AccessToken) && token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2));
    }

    public SpotifyStatus GetStatus()
    {
        return new SpotifyStatus
        {
            Enabled = IsEnabled(),
            Configured = IsConfigured(),
            Authenticated = IsAuthenticated(),
            RedirectUri = GetRedirectUri(),
            TokenPath = GetTokenPath()
        };
    }

    public string BuildLoginUrl()
    {
        EnsureEnabledAndConfigured();

        var scopes = string.Join(" ", new[]
        {
            "user-read-playback-state",
            "user-read-currently-playing",
            "user-modify-playback-state",
            "user-library-read",
            "playlist-read-private"
        });

        var query = new Dictionary<string, string>
        {
            ["client_id"] = GetClientId(),
            ["response_type"] = "code",
            ["redirect_uri"] = GetRedirectUri(),
            ["scope"] = scopes,
            ["state"] = Guid.NewGuid().ToString("N")
        };

        return $"{AccountsBaseUrl}/authorize?{BuildFormUrlEncoded(query)}";
    }

    public async Task ExchangeCodeAsync(string code)
    {
        EnsureEnabledAndConfigured();

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{AccountsBaseUrl}/api/token");
        request.Headers.Authorization = BuildBasicAuthHeader();
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = GetRedirectUri()
        });

        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Spotify OAuth falhou: {response.StatusCode} - {json}");

        var token = JsonSerializer.Deserialize<SpotifyTokenResponse>(json, JsonOptions()) ??
            throw new InvalidOperationException("Spotify nao retornou token valido.");

        SaveToken(new SpotifyStoredToken
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            TokenType = token.TokenType,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 60))
        });
    }

    public async Task<SpotifyPlaybackState> GetCurrentAsync()
    {
        var response = await SendApiAsync(HttpMethod.Get, "me/player");

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return new SpotifyPlaybackState { IsPlaying = false, Message = "Nenhum dispositivo Spotify ativo agora." };

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("consultar playback", response.StatusCode, json));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var item = root.TryGetProperty("item", out var itemElement) ? itemElement : default;
        var device = root.TryGetProperty("device", out var deviceElement) ? deviceElement : default;

        return new SpotifyPlaybackState
        {
            IsPlaying = root.TryGetProperty("is_playing", out var playingElement) && playingElement.GetBoolean(),
            Track = GetString(item, "name"),
            Artist = GetArtists(item),
            Album = GetAlbumName(item),
            AlbumImageUrl = GetAlbumImageUrl(item),
            Device = GetString(device, "name"),
            VolumePercent = GetInt(device, "volume_percent"),
            ProgressMs = GetInt(root, "progress_ms"),
            DurationMs = GetInt(item, "duration_ms"),
            Message = "Spotify conectado."
        };
    }

    public async Task<string> PlayAsync(SpotifyPlayRequest request)
    {
        var payload = await BuildPlayPayloadAsync(request);
        var response = await SendApiAsync(HttpMethod.Put, "me/player/play", payload);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("iniciar playback", response.StatusCode, body));

        if (!string.IsNullOrWhiteSpace(request.Query))
            return $"Tocando no Spotify: {request.Query}.";

        return "Tocando suas musicas no Spotify.";
    }

    public async Task<string> PauseAsync()
    {
        var response = await SendApiAsync(HttpMethod.Put, "me/player/pause");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("pausar playback", response.StatusCode, body));

        return "Musica pausada no Spotify.";
    }

    public async Task<string> NextAsync()
    {
        var response = await SendApiAsync(HttpMethod.Post, "me/player/next");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("avancar musica", response.StatusCode, body));

        return "Passei para a proxima musica no Spotify.";
    }

    public async Task<string> PreviousAsync()
    {
        var response = await SendApiAsync(HttpMethod.Post, "me/player/previous");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("voltar musica", response.StatusCode, body));

        return "Voltei para a musica anterior no Spotify.";
    }

    public async Task<string> SetVolumeAsync(int volumePercent)
    {
        var volume = Math.Clamp(volumePercent, 0, 100);
        var response = await SendApiAsync(HttpMethod.Put, $"me/player/volume?volume_percent={volume}");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("ajustar volume", response.StatusCode, body));

        return $"Volume do Spotify ajustado para {volume}%.";
    }

    private async Task<object> BuildPlayPayloadAsync(SpotifyPlayRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ContextUri))
            return new { context_uri = request.ContextUri };

        if (request.Uris.Count > 0)
            return new { uris = request.Uris.Take(50).ToArray() };

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var searchResult = await SearchBestAsync(request.Query);
            if (!string.IsNullOrWhiteSpace(searchResult.ContextUri))
                return new { context_uri = searchResult.ContextUri };

            if (!string.IsNullOrWhiteSpace(searchResult.TrackUri))
                return new { uris = new[] { searchResult.TrackUri } };
        }

        var savedTracks = await GetSavedTrackUrisAsync();
        if (savedTracks.Count > 0)
            return new { uris = savedTracks.Take(50).ToArray() };

        return new { };
    }

    private async Task<SpotifySearchResult> SearchBestAsync(string query)
    {
        var endpoint = $"search?q={Uri.EscapeDataString(query)}&type=playlist,album,artist,track&limit=1";
        var response = await SendApiAsync(HttpMethod.Get, endpoint);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("buscar musica", response.StatusCode, json));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var playlistUri = GetFirstUri(root, "playlists");
        if (!string.IsNullOrWhiteSpace(playlistUri))
            return new SpotifySearchResult { ContextUri = playlistUri };

        var albumUri = GetFirstUri(root, "albums");
        if (!string.IsNullOrWhiteSpace(albumUri))
            return new SpotifySearchResult { ContextUri = albumUri };

        var artistUri = GetFirstUri(root, "artists");
        if (!string.IsNullOrWhiteSpace(artistUri))
            return new SpotifySearchResult { ContextUri = artistUri };

        return new SpotifySearchResult { TrackUri = GetFirstUri(root, "tracks") };
    }

    private async Task<List<string>> GetSavedTrackUrisAsync()
    {
        var response = await SendApiAsync(HttpMethod.Get, "me/tracks?limit=50");
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildSpotifyError("buscar musicas curtidas", response.StatusCode, json));

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return items.EnumerateArray()
            .Select(item => item.TryGetProperty("track", out var track) ? GetString(track, "uri") : "")
            .Where(uri => !string.IsNullOrWhiteSpace(uri))
            .ToList();
    }

    private async Task<HttpResponseMessage> SendApiAsync(HttpMethod method, string endpoint, object? payload = null)
    {
        EnsureEnabledAndConfigured();
        var accessToken = await GetAccessTokenAsync();

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(method, $"{ApiBaseUrl}/{endpoint.TrimStart('/')}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (payload is not null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions());
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var token = LoadToken();

        if (token is null)
            throw new InvalidOperationException("Spotify ainda nao autenticado. Abra /api/spotify/login primeiro.");

        if (!string.IsNullOrWhiteSpace(token.AccessToken) && token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            return token.AccessToken;

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
            throw new InvalidOperationException("Token do Spotify expirou e nao ha refresh token. Faça login novamente.");

        return await RefreshTokenAsync(token);
    }

    private async Task<string> RefreshTokenAsync(SpotifyStoredToken currentToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{AccountsBaseUrl}/api/token");
        request.Headers.Authorization = BuildBasicAuthHeader();
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = currentToken.RefreshToken
        });

        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nao consegui renovar token Spotify: {response.StatusCode} - {json}");

        var refreshed = JsonSerializer.Deserialize<SpotifyTokenResponse>(json, JsonOptions()) ??
            throw new InvalidOperationException("Spotify nao retornou refresh token valido.");

        var newToken = new SpotifyStoredToken
        {
            AccessToken = refreshed.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                ? currentToken.RefreshToken
                : refreshed.RefreshToken,
            TokenType = refreshed.TokenType,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, refreshed.ExpiresIn - 60))
        };

        SaveToken(newToken);
        return newToken.AccessToken;
    }

    private SpotifyStoredToken? LoadToken()
    {
        var path = GetTokenPath();
        if (!File.Exists(path))
            return null;

        lock (_tokenLock)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SpotifyStoredToken>(json, JsonOptions());
        }
    }

    private void SaveToken(SpotifyStoredToken token)
    {
        var path = GetTokenPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        lock (_tokenLock)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(token, JsonOptions(indented: true)));
        }
    }

    private void EnsureEnabledAndConfigured()
    {
        if (!IsEnabled())
            throw new InvalidOperationException("Spotify nao esta habilitado. Configure SPOTIFY_ENABLED=true no backend/.env.");

        if (!IsConfigured())
            throw new InvalidOperationException("Spotify nao esta configurado. Defina SPOTIFY_CLIENT_ID, SPOTIFY_CLIENT_SECRET e SPOTIFY_REDIRECT_URI.");
    }

    private AuthenticationHeaderValue BuildBasicAuthHeader()
    {
        var raw = $"{GetClientId()}:{GetClientSecret()}";
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
    }

    private string GetTokenPath()
    {
        var configured = Environment.GetEnvironmentVariable("SPOTIFY_TOKEN_PATH") ?? "secrets/spotify-token.json";
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configured));
    }

    private static string GetClientId() => Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? "";
    private static string GetClientSecret() => Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET") ?? "";
    private static string GetRedirectUri() => Environment.GetEnvironmentVariable("SPOTIFY_REDIRECT_URI") ?? "http://localhost:5000/api/spotify/callback";

    private static JsonSerializerOptions JsonOptions(bool indented = false)
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = indented
        };
    }

    private static string BuildFormUrlEncoded(Dictionary<string, string> values)
    {
        return string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static string BuildSpotifyError(string action, System.Net.HttpStatusCode statusCode, string body)
    {
        if ((int)statusCode == 401)
            return $"Nao consegui {action}: login do Spotify expirado. Abra /api/spotify/login novamente.";

        if ((int)statusCode == 403)
            return $"Nao consegui {action}: o Spotify recusou a acao. Verifique se sua conta tem Premium e se o app recebeu as permissoes de playback.";

        if ((int)statusCode == 404)
            return $"Nao consegui {action}: nenhum dispositivo Spotify ativo encontrado. Abra o Spotify no celular ou computador e tente de novo.";

        return $"Nao consegui {action}: Spotify retornou {(int)statusCode}. {body}";
    }

    private static string GetFirstUri(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var collection))
            return "";

        if (!collection.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return "";

        var first = items.EnumerateArray().FirstOrDefault(item => item.ValueKind == JsonValueKind.Object);
        return first.ValueKind == JsonValueKind.Object ? GetString(first, "uri") : "";
    }

    private static string GetArtists(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("artists", out var artists) ||
            artists.ValueKind != JsonValueKind.Array)
            return "";

        return string.Join(", ", artists.EnumerateArray()
            .Select(artist => GetString(artist, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name)));
    }

    private static string GetAlbumName(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("album", out var album))
            return "";

        return GetString(album, "name");
    }

    private static string GetAlbumImageUrl(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("album", out var album) ||
            !album.TryGetProperty("images", out var images) ||
            images.ValueKind != JsonValueKind.Array)
            return "";

        var first = images.EnumerateArray().FirstOrDefault(image => image.ValueKind == JsonValueKind.Object);
        return first.ValueKind == JsonValueKind.Object ? GetString(first, "url") : "";
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
    }

    private static int GetInt(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.TryGetInt32(out var number)
                ? number
                : 0;
    }
}

public class SpotifyStatus
{
    public bool Enabled { get; set; }
    public bool Configured { get; set; }
    public bool Authenticated { get; set; }
    public string RedirectUri { get; set; } = "";
    public string TokenPath { get; set; } = "";
}

public class SpotifyPlayRequest
{
    public string? Query { get; set; }
    public string? ContextUri { get; set; }
    public List<string> Uris { get; set; } = new();
}

public class SpotifyVolumeRequest
{
    public int VolumePercent { get; set; }
}

public class SpotifyPlaybackState
{
    public bool IsPlaying { get; set; }
    public string Track { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Album { get; set; } = "";
    public string AlbumImageUrl { get; set; } = "";
    public string Device { get; set; } = "";
    public int VolumePercent { get; set; }
    public int ProgressMs { get; set; }
    public int DurationMs { get; set; }
    public string Message { get; set; } = "";
}

internal class SpotifyStoredToken
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string TokenType { get; set; } = "Bearer";
    public DateTimeOffset ExpiresAt { get; set; }
}

internal class SpotifyTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";
}

internal class SpotifySearchResult
{
    public string ContextUri { get; set; } = "";
    public string TrackUri { get; set; } = "";
}
