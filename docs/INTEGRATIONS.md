# Integracoes do Nexus

Este documento resume o que foi implementado, como funciona e como testar.

## Arquitetura

O Nexus usa uma separacao simples:

- Controllers recebem HTTP e comandos do frontend/chat.
- Services concentram integracoes externas e regras locais.
- Obsidian guarda memoria, logs, perfis e configuracoes humanas.
- Frontend React consome os endpoints do backend via `frontend/src/services/api.js`.

Fluxo comum:

```text
Comando do usuario
-> /api/nexus/chat
-> DetectIntent
-> service local quando existir
-> resposta local ou fallback de IA
-> logs/memoria quando aplicavel
```

## Weather

Arquivos:

```text
backend/Services/WeatherService.cs
backend/Controllers/WeatherController.cs
backend/Services/TodayService.cs
backend/Controllers/JarvisController.cs
vault/07_Nexus/weather-settings.md
```

O `WeatherService` chama Open-Meteo com latitude/longitude de Sao Jose do Rio Preto. Ele converte o JSON em modelos internos, traduz `weather_code`, calcula resumo e recomendacao.

O `TodayService` inclui o clima no briefing. O `JarvisController` detecta comandos de clima antes da conversa casual para evitar que "como esta o tempo?" caia no modelo generico.

Endpoints:

```text
GET /api/weather/current
GET /api/weather/report
```

Teste:

```powershell
Invoke-RestMethod http://localhost:5000/api/weather/report
```

## Spotify

Arquivos:

```text
backend/Services/SpotifyService.cs
backend/Services/SpotifyLearningService.cs
backend/Services/SpotifyLearningBackgroundService.cs
backend/Controllers/SpotifyController.cs
frontend/src/pages/Media.jsx
frontend/src/services/api.js
vault/07_Nexus/spotify-settings.md
```

O `SpotifyService` cuida de OAuth, refresh token, player atual, playlists, play/pause/next/previous/volume e dados para aprendizado musical.

O token fica em:

```text
backend/secrets/spotify-token.json
```

Esse caminho e ignorado pelo Git.

Variaveis:

```env
SPOTIFY_ENABLED=true
SPOTIFY_CLIENT_ID=...
SPOTIFY_CLIENT_SECRET=...
SPOTIFY_REDIRECT_URI=http://127.0.0.1:5000/api/spotify/callback
SPOTIFY_TOKEN_PATH=secrets/spotify-token.json
SPOTIFY_LEARNING_ENABLED=true
SPOTIFY_LEARNING_INTERVAL_HOURS=24
```

No Spotify Developer Dashboard, cadastre:

```text
http://127.0.0.1:5000/api/spotify/callback
```

Depois abra:

```text
http://localhost:5000/api/spotify/login
```

Scopes usados:

```text
user-read-playback-state
user-read-currently-playing
user-modify-playback-state
user-library-read
playlist-read-private
user-top-read
user-read-recently-played
```

Quando novos scopes forem adicionados, e preciso autorizar novamente.

Endpoints:

```text
GET  /api/spotify/status
GET  /api/spotify/login
GET  /api/spotify/callback
GET  /api/spotify/current
GET  /api/spotify/playlists
POST /api/spotify/learn
POST /api/spotify/play
POST /api/spotify/pause
POST /api/spotify/next
POST /api/spotify/previous
POST /api/spotify/volume
```

A pagina `/media` mostra musica atual, capa, progresso, dispositivo, volume, playlists e controles.

## Aprendizado musical

Comando:

```text
Nexus, aprenda meus gostos musicais
```

O aprendizado usa:

- musicas mais ouvidas;
- artistas mais ouvidos;
- generos recorrentes;
- playlists;
- historico recente.

Ele salva:

```text
07_Nexus/music-profile.md
03_Memory/memory.md
```

O job `SpotifyLearningBackgroundService` atualiza automaticamente quando:

```env
SPOTIFY_LEARNING_ENABLED=true
```

Teste:

```powershell
Invoke-RestMethod http://localhost:5000/api/spotify/learn -Method Post
```

## Home Assistant

Configurado por:

```env
HOME_ASSISTANT_ENABLED=true
HOME_ASSISTANT_URL=...
HOME_ASSISTANT_TOKEN=...
HOME_ASSISTANT_REQUIRE_CONFIRMATION=true
```

O Nexus consulta estados e executa acoes com confirmacao para entidades sensiveis.

## Network Monitor

O `NetworkMonitorService` verifica internet, dispositivos conhecidos, dispositivos desconhecidos e alertas.

Arquivos no vault:

```text
07_Nexus/network-devices.md
07_Nexus/network-monitoring.md
07_Nexus/network-alerts.md
07_Nexus/network-log.md
```

## Seguranca

Nunca versionar:

```text
backend/.env
backend/secrets/
logs/
Backups/
```

O `.gitignore` cobre esses caminhos.

## Release Nexus V2 - Cockpit e integracoes

Novos endpoints:

```text
GET  /api/integrations/status
POST /api/integrations/telegram/send
POST /api/integrations/telegram/alert
GET  /api/integrations/telegram/commands
POST /api/integrations/telegram/command
GET  /api/integrations/calendar/briefing
GET  /api/integrations/gmail/briefing
```

Telegram usa `TELEGRAM_ENABLED`, `TELEGRAM_BOT_TOKEN` e `TELEGRAM_CHAT_ID`. Comandos suportados: `/status`, `/network`, `/today` e `/alerts`.

Google Calendar usa `GOOGLE_CALENDAR_ENABLED`, `GOOGLE_ACCESS_TOKEN` e `GOOGLE_CALENDAR_ID` para gerar briefing das proximas 24 horas.

Gmail usa `GMAIL_ENABLED`, `GOOGLE_ACCESS_TOKEN`, `GMAIL_USER_ID` e `GMAIL_BRIEFING_QUERY` para resumir emails recentes.

Sem credenciais, os endpoints retornam `configured=false` e mensagens de integracao nao configurada, sem expor segredos.

## Zabbix

O Nexus agora pode operar como camada inteligente em cima do Zabbix.

Fontes oficiais usadas:

- `https://www.zabbix.com/documentation/current/en/manual/api`
- `https://www.zabbix.com/documentation/current/en/manual/api/reference/problem/get`
- `https://www.zabbix.com/documentation/current/en/manual/api/reference/event/acknowledge`
- `https://www.zabbix.com/documentation/current/en/manual/api/reference/host/get`

Arquivos:

```text
backend/Services/ZabbixService.cs
backend/Controllers/ZabbixController.cs
frontend/src/pages/Zabbix.jsx
vault/07_Nexus/zabbix-settings.md
vault/07_Nexus/zabbix-history.md
vault/07_Nexus/zabbix-actions.md
```

Configuracao:

```env
ZABBIX_ENABLED=true
ZABBIX_API_URL=https://zabbix.example.com/zabbix/api_jsonrpc.php
ZABBIX_API_TOKEN=
ZABBIX_USERNAME=
ZABBIX_PASSWORD=
ZABBIX_HOST_LIMIT=100
ZABBIX_PROBLEM_LIMIT=100
```

Preferir `ZABBIX_API_TOKEN`. Se usar `ZABBIX_USERNAME` e `ZABBIX_PASSWORD`, o Nexus faz `user.login` e tenta `user.logout` no final de cada operacao autenticada para evitar sessoes abertas.

Endpoints:

```text
GET  /api/zabbix/status
GET  /api/zabbix/hosts
GET  /api/zabbix/problems?minSeverity=4
POST /api/zabbix/report
POST /api/zabbix/acknowledge
```

O painel `/zabbix` mostra status da API, hosts, problemas filtrados por severidade, relatorio para Obsidian, envio de alerta critico por Telegram e acknowledge com mensagem.

O chat entende comandos como:

```text
Nexus, status do Zabbix.
Nexus, gerar relatorio do Zabbix.
Nexus, mostre alertas criticos do Zabbix.
```
