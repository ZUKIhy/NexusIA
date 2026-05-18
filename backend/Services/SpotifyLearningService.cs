using System.Text;

namespace NexusBackend.Services;

public class SpotifyLearningService
{
    private readonly SpotifyService _spotify;
    private readonly ObsidianService _obsidian;
    private readonly MemoryService _memory;

    public SpotifyLearningService(
        SpotifyService spotify,
        ObsidianService obsidian,
        MemoryService memory)
    {
        _spotify = spotify;
        _obsidian = obsidian;
        _memory = memory;
    }

    public async Task<SpotifyLearningResult> LearnAsync()
    {
        if (!_spotify.IsEnabled() || !_spotify.IsConfigured())
            return SpotifyLearningResult.Failed("Spotify nao esta habilitado/configurado.");

        if (!_spotify.IsAuthenticated())
            return SpotifyLearningResult.Failed("Spotify ainda nao esta autenticado.");

        var topTracksMedium = await _spotify.GetTopTracksAsync("medium_term", 20);
        var topTracksShort = await _spotify.GetTopTracksAsync("short_term", 20);
        var topArtistsMedium = await _spotify.GetTopArtistsAsync("medium_term", 20);
        var topArtistsShort = await _spotify.GetTopArtistsAsync("short_term", 20);
        var recentTracks = await _spotify.GetRecentlyPlayedAsync(20);
        var playlists = await _spotify.GetPlaylistsAsync();

        var topGenres = topArtistsMedium
            .SelectMany(artist => artist.Genres)
            .GroupBy(genre => genre, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(12)
            .Select(group => new SpotifyGenrePreference { Name = group.Key, Count = group.Count() })
            .ToList();

        var profile = new SpotifyMusicProfile
        {
            LearnedAt = DateTime.Now,
            TopTracksMediumTerm = topTracksMedium,
            TopTracksShortTerm = topTracksShort,
            TopArtistsMediumTerm = topArtistsMedium,
            TopArtistsShortTerm = topArtistsShort,
            RecentTracks = recentTracks,
            Playlists = playlists,
            TopGenres = topGenres
        };

        var markdown = BuildProfileMarkdown(profile);
        _obsidian.WriteFile("07_Nexus/music-profile.md", markdown);

        var memoryText = BuildMemorySummary(profile);
        _memory.Save(memoryText);

        return new SpotifyLearningResult
        {
            Success = true,
            Message = "Aprendi seu perfil musical e salvei na memoria do Nexus.",
            Profile = profile,
            MemorySummary = memoryText,
            ProfilePath = "07_Nexus/music-profile.md"
        };
    }

    private static string BuildProfileMarkdown(SpotifyMusicProfile profile)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Perfil Musical do Gabriel");
        builder.AppendLine();
        builder.AppendLine($"Atualizado em: {profile.LearnedAt:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine("## Resumo para o Nexus");
        builder.AppendLine(BuildMemorySummary(profile));
        builder.AppendLine();
        AppendTracks(builder, "Musicas mais ouvidas - medio prazo", profile.TopTracksMediumTerm);
        AppendTracks(builder, "Musicas mais ouvidas - curto prazo", profile.TopTracksShortTerm);
        AppendArtists(builder, "Artistas mais ouvidos - medio prazo", profile.TopArtistsMediumTerm);
        AppendArtists(builder, "Artistas mais ouvidos - curto prazo", profile.TopArtistsShortTerm);
        AppendGenres(builder, profile.TopGenres);
        AppendPlaylists(builder, profile.Playlists);
        AppendRecent(builder, profile.RecentTracks);
        return builder.ToString();
    }

    private static string BuildMemorySummary(SpotifyMusicProfile profile)
    {
        var topTracks = profile.TopTracksMediumTerm.Take(5)
            .Select(track => $"{track.Name} ({track.Artist})");
        var topArtists = profile.TopArtistsMediumTerm.Take(5)
            .Select(artist => artist.Name);
        var topGenres = profile.TopGenres.Take(6)
            .Select(genre => genre.Name);
        var ownPlaylists = profile.Playlists
            .Where(playlist => playlist.Owner.Equals("gzuca12", StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .Select(playlist => playlist.Name);

        return
            "Gabriel tem preferencias musicais aprendidas pelo Spotify. " +
            $"Musicas fortes: {JoinOrFallback(topTracks, "sem dados")}. " +
            $"Artistas fortes: {JoinOrFallback(topArtists, "sem dados")}. " +
            $"Generos recorrentes: {JoinOrFallback(topGenres, "sem dados")}. " +
            $"Playlists dele: {JoinOrFallback(ownPlaylists, "sem playlists proprias detectadas")}. " +
            "Quando ele pedir musica sem especificar, priorize essas preferencias e playlists.";
    }

    private static void AppendTracks(StringBuilder builder, string title, IReadOnlyList<SpotifyTopTrackItem> tracks)
    {
        builder.AppendLine($"## {title}");
        if (tracks.Count == 0)
        {
            builder.AppendLine("- Sem dados.");
            builder.AppendLine();
            return;
        }

        foreach (var track in tracks)
            builder.AppendLine($"- {track.Rank}. {track.Name} - {track.Artist} ({track.Album})");

        builder.AppendLine();
    }

    private static void AppendArtists(StringBuilder builder, string title, IReadOnlyList<SpotifyTopArtistItem> artists)
    {
        builder.AppendLine($"## {title}");
        if (artists.Count == 0)
        {
            builder.AppendLine("- Sem dados.");
            builder.AppendLine();
            return;
        }

        foreach (var artist in artists)
            builder.AppendLine($"- {artist.Rank}. {artist.Name} | generos: {JoinOrFallback(artist.Genres, "nao informado")}");

        builder.AppendLine();
    }

    private static void AppendGenres(StringBuilder builder, IReadOnlyList<SpotifyGenrePreference> genres)
    {
        builder.AppendLine("## Generos recorrentes");
        if (genres.Count == 0)
        {
            builder.AppendLine("- Sem dados.");
            builder.AppendLine();
            return;
        }

        foreach (var genre in genres)
            builder.AppendLine($"- {genre.Name} ({genre.Count})");

        builder.AppendLine();
    }

    private static void AppendPlaylists(StringBuilder builder, IReadOnlyList<SpotifyPlaylistItem> playlists)
    {
        builder.AppendLine("## Playlists");
        if (playlists.Count == 0)
        {
            builder.AppendLine("- Sem dados.");
            builder.AppendLine();
            return;
        }

        foreach (var playlist in playlists.Take(30))
            builder.AppendLine($"- {playlist.Name} | {playlist.TrackCount} faixas | dono: {playlist.Owner}");

        builder.AppendLine();
    }

    private static void AppendRecent(StringBuilder builder, IReadOnlyList<SpotifyRecentTrackItem> tracks)
    {
        builder.AppendLine("## Historico recente");
        if (tracks.Count == 0)
        {
            builder.AppendLine("- Sem dados.");
            builder.AppendLine();
            return;
        }

        foreach (var track in tracks.Take(20))
            builder.AppendLine($"- {track.Name} - {track.Artist} | {track.PlayedAt}");

        builder.AppendLine();
    }

    private static string JoinOrFallback(IEnumerable<string> values, string fallback)
    {
        var clean = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return clean.Count == 0 ? fallback : string.Join(", ", clean);
    }
}

public class SpotifyLearningResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string ProfilePath { get; set; } = "";
    public string MemorySummary { get; set; } = "";
    public SpotifyMusicProfile? Profile { get; set; }

    public static SpotifyLearningResult Failed(string message)
    {
        return new SpotifyLearningResult { Success = false, Message = message };
    }
}

public class SpotifyMusicProfile
{
    public DateTime LearnedAt { get; set; }
    public List<SpotifyTopTrackItem> TopTracksMediumTerm { get; set; } = new();
    public List<SpotifyTopTrackItem> TopTracksShortTerm { get; set; } = new();
    public List<SpotifyTopArtistItem> TopArtistsMediumTerm { get; set; } = new();
    public List<SpotifyTopArtistItem> TopArtistsShortTerm { get; set; } = new();
    public List<SpotifyRecentTrackItem> RecentTracks { get; set; } = new();
    public List<SpotifyPlaylistItem> Playlists { get; set; } = new();
    public List<SpotifyGenrePreference> TopGenres { get; set; } = new();
}

public class SpotifyGenrePreference
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}
