# N.E.X.U.S - Gabriel

Assistente pessoal local para Gabriel, com frontend React, backend .NET, memoria em Obsidian, monitoramento de rede/casa, clima de Sao Jose do Rio Preto e integracao Spotify.

## O que existe hoje

- Frontend React + Vite em `frontend/`.
- Backend .NET 8 Web API em `backend/`.
- Chat principal em `/api/nexus/chat`.
- SignalR para eventos em tempo real.
- Memoria, tarefas, logs e documentacao em Markdown/Obsidian.
- Busca documental e geracao de procedimentos/checklists.
- Modo operacao para atendimento tecnico.
- Home Assistant para status/controle seguro da casa.
- Network Monitor para internet, dispositivos conhecidos e desconhecidos.
- Weather com Open-Meteo para Sao Jose do Rio Preto.
- Spotify com OAuth, playback, playlists, aba Media e aprendizado musical.
- Ollama local como inteligencia principal offline.
- OpenAI como fallback quando configurado.
- ElevenLabs e voz do navegador para fala.
- Scripts de autostart e backup.

## Como rodar

### Backend

```powershell
cd backend
dotnet restore
dotnet run --urls http://localhost:5000
```

Backend: `http://localhost:5000`

O backend carrega configuracoes de `backend/.env`. Esse arquivo nao deve ir para o Git.

### Frontend

```powershell
cd frontend
npm install
npm.cmd run dev
```

Frontend: `http://localhost:5173`

## Principais paginas

- `/`: dashboard e chat.
- `/today`: briefing do dia.
- `/docs`: busca e leitura de documentos.
- `/procedures`: procedimentos.
- `/home-assistant`: casa inteligente.
- `/media`: Spotify, musica atual, playlists e controles.
- `/network`: monitoramento de rede.
- `/tasks`: tarefas.
- `/logs`: logs.
- `/system`: saude do sistema.
- `/settings`: configuracoes/testes.

## Integracoes principais

### Weather

Usa Open-Meteo sem API key para Sao Jose do Rio Preto.

Endpoints:

```text
GET /api/weather/current
GET /api/weather/report
```

Comandos:

```text
Nexus, relatorio do clima
Nexus, como esta o tempo?
Nexus, vai chover hoje?
```

### Spotify

Usa OAuth Authorization Code Flow.

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

Comandos:

```text
Nexus, tocar minhas musicas
Nexus, tocar playlist foco
Nexus, pausar musica
Nexus, proxima musica
Nexus, musica anterior
Nexus, volume 40%
Nexus, o que esta tocando?
Nexus, aprenda meus gostos musicais
```

O aprendizado musical salva:

```text
07_Nexus/music-profile.md
03_Memory/memory.md
```

## Seguranca

Nao versionar:

```text
backend/.env
backend/secrets/
Backups/
logs/
```

Tokens e credenciais ficam em `backend/.env` ou `backend/secrets/`.

## Validacao rapida

```powershell
cd backend
dotnet build
```

```powershell
cd frontend
npm.cmd run build
```

## Documentacao

- `docs/NEXUS.md`: documentacao geral.
- `docs/INTEGRATIONS.md`: detalhes das integracoes.
- `docs/CHECKLIST.md`: checklist operacional.
- `docs/PLANO.md`: plano atual.
