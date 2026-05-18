# Spotify Settings

## Variaveis
- SPOTIFY_ENABLED=true
- SPOTIFY_CLIENT_ID=seu_client_id
- SPOTIFY_CLIENT_SECRET=seu_client_secret
- SPOTIFY_REDIRECT_URI=http://127.0.0.1:5000/api/spotify/callback
- SPOTIFY_TOKEN_PATH=secrets/spotify-token.json
- SPOTIFY_LEARNING_ENABLED=true
- SPOTIFY_LEARNING_INTERVAL_HOURS=24

## Endpoints
- GET /api/spotify/status
- GET /api/spotify/login
- GET /api/spotify/callback
- GET /api/spotify/current
- GET /api/spotify/playlists
- POST /api/spotify/learn
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
- Nexus, aprenda meus gostos musicais.
- Nexus, salve meus gostos musicais na memoria.

## Aprendizado musical
- Usa musicas mais ouvidas, artistas mais ouvidos, generos, playlists e historico recente.
- Salva o perfil em 07_Nexus/music-profile.md.
- Salva um resumo em 03_Memory/memory.md para o Nexus usar em conversas.
- Requer autorizar novamente o Spotify quando novos scopes forem adicionados.

## Observacao
- Controle de playback normalmente exige dispositivo Spotify ativo.
- Em muitos casos, a API de playback exige Spotify Premium.
