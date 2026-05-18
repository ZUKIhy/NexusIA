import { useEffect, useMemo, useState } from "react";
import { ExternalLink, Music2, Pause, Play, RefreshCw, SkipBack, SkipForward, Volume2 } from "lucide-react";
import {
  API_URL,
  getSpotifyCurrent,
  getSpotifyPlaylists,
  getSpotifyStatus,
  nextSpotify,
  pauseSpotify,
  playSpotify,
  previousSpotify,
  setSpotifyVolume,
} from "../services/api";

export default function Media() {
  const [status, setStatus] = useState(null);
  const [player, setPlayer] = useState(null);
  const [playlists, setPlaylists] = useState([]);
  const [query, setQuery] = useState("");
  const [volume, setVolume] = useState(50);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const progressPercent = useMemo(() => {
    if (!player?.durationMs) return 0;
    return Math.min(100, Math.round((player.progressMs / player.durationMs) * 100));
  }, [player]);

  useEffect(() => {
    refresh();
    const timer = window.setInterval(refreshQuietly, 10000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    if (player?.volumePercent) setVolume(player.volumePercent);
  }, [player?.volumePercent]);

  async function refresh() {
    setLoading(true);
    setError("");

    try {
      const nextStatus = await getSpotifyStatus();
      setStatus(nextStatus);

      if (nextStatus.authenticated) {
        const [nextPlayer, playlistData] = await Promise.all([
          getSpotifyCurrent(),
          getSpotifyPlaylists(),
        ]);

        setPlayer(nextPlayer);
        setPlaylists(playlistData.playlists || []);
      }
    } catch (err) {
      setError(readError(err, "Nao consegui carregar o Spotify."));
    } finally {
      setLoading(false);
    }
  }

  async function refreshQuietly() {
    try {
      const nextStatus = await getSpotifyStatus();
      setStatus(nextStatus);
      if (nextStatus.authenticated) {
        const [nextPlayer, playlistData] = await Promise.all([
          getSpotifyCurrent(),
          getSpotifyPlaylists(),
        ]);

        setPlayer(nextPlayer);
        setPlaylists(playlistData.playlists || []);
      }
    } catch {
      // Silent refresh keeps the media page calm while the backend wakes up.
    }
  }

  async function runAction(action, label) {
    setBusy(label);
    setError("");
    setNotice("");

    try {
      const result = await action();
      setNotice(result?.message || "Comando enviado ao Spotify.");
      await refreshQuietly();
    } catch (err) {
      setError(readError(err, "Nao consegui executar o comando no Spotify."));
    } finally {
      setBusy("");
    }
  }

  function handlePlay() {
    return runAction(() => playSpotify(query.trim()), "play");
  }

  function handleVolumeChange(event) {
    const nextVolume = Number(event.target.value);
    setVolume(nextVolume);
  }

  function commitVolume() {
    return runAction(() => setSpotifyVolume(volume), "volume");
  }

  const hasTrack = Boolean(player?.track);

  return (
    <div className="page media-page">
      <div className="docs-header">
        <div>
          <h1>Nexus Media</h1>
          <p>Controle Spotify, playback e dispositivo ativo.</p>
        </div>

        <button type="button" className="small-action-button" onClick={refresh} disabled={loading}>
          <RefreshCw size={15} />
          {loading ? "Atualizando" : "Atualizar"}
        </button>
      </div>

      {error && <p className="docs-error">{error}</p>}
      {notice && <p className="home-notice">{notice}</p>}

      <section className={`media-player ${player?.isPlaying ? "playing" : ""}`}>
        <div className="media-cover">
          {hasTrack && player.albumImageUrl ? (
            <img src={player.albumImageUrl} alt="" />
          ) : (
            <Music2 size={56} />
          )}
        </div>

        <div className="media-now">
          <span>{statusLabel(status, player)}</span>
          <h2>{hasTrack ? player.track : "Nenhuma musica tocando agora"}</h2>
          <p>{hasTrack ? `${player.artist}${player.album ? ` - ${player.album}` : ""}` : player?.message || "Abra o Spotify em algum dispositivo para iniciar."}</p>

          <div className="media-progress" aria-label="Progresso da musica">
            <div style={{ width: `${progressPercent}%` }} />
          </div>

          <div className="media-time">
            <span>{formatDuration(player?.progressMs)}</span>
            <span>{formatDuration(player?.durationMs)}</span>
          </div>
        </div>
      </section>

      <section className="media-controls panel">
        <div className="media-main-controls">
          <button type="button" className="icon-action-button" onClick={() => runAction(previousSpotify, "previous")} disabled={Boolean(busy)} title="Anterior">
            <SkipBack size={20} />
          </button>

          <button type="button" className="media-play-button" onClick={player?.isPlaying ? () => runAction(pauseSpotify, "pause") : handlePlay} disabled={Boolean(busy)} title={player?.isPlaying ? "Pausar" : "Tocar"}>
            {player?.isPlaying ? <Pause size={24} /> : <Play size={24} />}
          </button>

          <button type="button" className="icon-action-button" onClick={() => runAction(nextSpotify, "next")} disabled={Boolean(busy)} title="Proxima">
            <SkipForward size={20} />
          </button>
        </div>

        <div className="media-search">
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Musica, artista ou playlist"
          />
          <button type="button" className="small-action-button" onClick={handlePlay} disabled={Boolean(busy)}>
            <Play size={15} />
            Tocar
          </button>
        </div>

        <div className="media-volume">
          <Volume2 size={18} />
          <input
            type="range"
            min="0"
            max="100"
            value={volume}
            onChange={handleVolumeChange}
            onMouseUp={commitVolume}
            onTouchEnd={commitVolume}
          />
          <strong>{volume}%</strong>
        </div>
      </section>

      <div className="home-summary">
        <MediaStat label="Spotify" value={status?.enabled ? "Ativo" : "Inativo"} />
        <MediaStat label="OAuth" value={status?.authenticated ? "Conectado" : "Pendente"} />
        <MediaStat label="Dispositivo" value={player?.device || "Nenhum"} />
        <MediaStat label="Playback" value={player?.isPlaying ? "Tocando" : "Pausado"} />
      </div>

      <section className="panel media-playlists">
        <header>
          <div>
            <strong>Playlists</strong>
            <span>{playlists.length} encontradas</span>
          </div>
          <Music2 size={18} />
        </header>

        {playlists.length > 0 ? (
          <div className="media-playlist-grid">
            {playlists.map((playlist) => (
              <article key={playlist.uri} className="media-playlist-card">
                <div className="media-playlist-cover">
                  {playlist.imageUrl ? <img src={playlist.imageUrl} alt="" /> : <Music2 size={24} />}
                </div>
                <div>
                  <strong>{playlist.name}</strong>
                  <span>{playlist.trackCount} faixas{playlist.owner ? ` - ${playlist.owner}` : ""}</span>
                </div>
                <button
                  type="button"
                  className="icon-action-button"
                  onClick={() => runAction(() => playSpotify("", playlist.uri), playlist.uri)}
                  disabled={Boolean(busy)}
                  title="Tocar playlist"
                >
                  <Play size={18} />
                </button>
              </article>
            ))}
          </div>
        ) : (
          <p className="muted">
            {status?.authenticated ? "Nenhuma playlist retornada pelo Spotify." : "Conecte o Spotify para carregar suas playlists."}
          </p>
        )}
      </section>

      {!status?.authenticated && (
        <section className="media-login panel">
          <div>
            <strong>Conectar Spotify</strong>
            <p>Autorize sua conta para liberar playback no Nexus.</p>
          </div>
          <a className="small-action-button" href={`${API_URL}/api/spotify/login`}>
            <ExternalLink size={15} />
            Login
          </a>
        </section>
      )}
    </div>
  );
}

function MediaStat({ label, value }) {
  return (
    <div className="stat-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function statusLabel(status, player) {
  if (!status?.enabled) return "Spotify inativo";
  if (!status?.authenticated) return "Login pendente";
  if (!player?.device) return "Aguardando dispositivo";
  return player.isPlaying ? `Tocando em ${player.device}` : `Pausado em ${player.device}`;
}

function formatDuration(value = 0) {
  if (!value) return "0:00";
  const totalSeconds = Math.floor(value / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = String(totalSeconds % 60).padStart(2, "0");
  return `${minutes}:${seconds}`;
}

function readError(error, fallback) {
  return error?.response?.data?.message || error?.response?.data?.error || error?.message || fallback;
}
