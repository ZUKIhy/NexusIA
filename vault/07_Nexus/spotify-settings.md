# Spotify Settings

## Variaveis
- SPOTIFY_ENABLED=true
- SPOTIFY_CLIENT_ID=seu_client_id
- SPOTIFY_CLIENT_SECRET=seu_client_secret
- SPOTIFY_REDIRECT_URI=http://127.0.0.1:5000/api/spotify/callback
- SPOTIFY_TOKEN_PATH=secrets/spotify-token.json

## Endpoints
- GET /api/spotify/status
- GET /api/spotify/login
- GET /api/spotify/callback
- GET /api/spotify/current
- POST /api/spotify/play
- POST /api/spotify/pause
- POST /api/spotify/next
- POST /api/spotify/previous
- POST /api/spotify/volume

## Comandos
- Nexus, tocar minhas musicas.
- Nexus, tocar playlist foco.
- Nexus, pausar musica.
- Nexus, proxima musica.
- Nexus, musica anterior.
- Nexus, volume 40%.
- Nexus, o que esta tocando?

## Observacao
- Controle de playback normalmente exige dispositivo Spotify ativo.
- Em muitos casos, a API de playback exige Spotify Premium.
