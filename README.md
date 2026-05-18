# N.E.X.U.S - Gabriel

N.E.X.U.S e uma central de comando pessoal e operacional para Gabriel. O projeto combina frontend React, backend .NET 8, vault Obsidian, automacoes locais, integrações externas, monitoramento e um cockpit visual para rotina tecnica.

O objetivo atual e transformar o Nexus em uma camada inteligente em cima dos sistemas que Gabriel usa: ele consulta fontes reais, resume alertas, registra historico, sugere acoes, conversa por chat/Telegram e apoia modo operacao.

## Novidades recentes

- Cockpit/Home V2 conectado a dados reais de briefing, clima, rede, tarefas e integracoes.
- Painel `/zabbix` com status da API, hosts, problemas, relatorio e acknowledge.
- Integração Zabbix via API JSON-RPC com `problem.get`, `host.get`, `trigger.get` e `event.acknowledge`.
- Telegram operacional com envio de alertas, comandos e conversa por polling em background.
- Alertas criticos de internet e Home Assistant enviados por Telegram.
- Google Calendar e Gmail preparados para briefing via token Google.
- `.env.example` atualizado com variaveis de Telegram, Zabbix, Gmail, Calendar, clima e rede.
- Documentacao tecnica atualizada em `docs/` e no vault Obsidian.

## Stack

- Backend: .NET 8 Web API + SignalR.
- Frontend: React + Vite.
- UI: React Router, Lucide Icons, Framer Motion, Recharts.
- Dados locais: Markdown/Obsidian em `vault/`.
- IA: Ollama local, OpenAI/Claude como fallback quando configurados.
- Automacao local: controle assistido do Windows.
- Integrações: Zabbix, Telegram, Spotify, Home Assistant, Weather, Google Calendar e Gmail.

## Estrutura

```text
backend/        API .NET 8, services, controllers e hubs
frontend/       App React/Vite
vault/          Base Obsidian, memoria, logs, docs e historico operacional
docs/           Documentacao tecnica
scripts/        Start, autostart e backup
Backups/        Backups locais ignorados pelo Git
logs/           Logs locais ignorados pelo Git
```

## Como rodar

### Modo simples

```powershell
.\scripts\start-nexus.ps1
```

Esse script sobe backend, frontend e abre a interface.

### Backend

```powershell
cd backend
dotnet restore
dotnet run --urls http://localhost:5000
```

Backend: `http://localhost:5000`

### Frontend

```powershell
cd frontend
npm install
npm.cmd run dev
```

Frontend: `http://localhost:5173`

## Paginas

- `/`: cockpit principal, chat rapido, NexusCore e telemetria real.
- `/today`: briefing do dia.
- `/zabbix`: painel Zabbix, problemas, hosts, relatorio e acknowledge.
- `/network`: monitoramento de rede e dispositivos desconhecidos.
- `/system`: saude do backend, vault, Ollama, GPU e integracoes.
- `/procedures`: procedimentos operacionais.
- `/docs`: busca, leitura, padronizacao e checklist de documentos.
- `/home-assistant`: casa inteligente.
- `/media`: Spotify, playback, playlists e aprendizado musical.
- `/memory`: memoria.
- `/tasks`: tarefas.
- `/logs`: logs.
- `/settings`: configuracoes/testes.

## Principais endpoints

### Nexus

```text
POST /api/nexus/chat
GET  /api/nexus/status
POST /api/nexus/activate
GET  /hubs/nexus
```

### Zabbix

```text
GET  /api/zabbix/status
GET  /api/zabbix/hosts
GET  /api/zabbix/problems?minSeverity=4
POST /api/zabbix/report
POST /api/zabbix/acknowledge
```

### Telegram e integracoes

```text
GET  /api/integrations/status
POST /api/integrations/telegram/send
POST /api/integrations/telegram/alert
GET  /api/integrations/telegram/commands
POST /api/integrations/telegram/command
GET  /api/integrations/calendar/briefing
GET  /api/integrations/gmail/briefing
```

### Rede, clima e sistema

```text
GET  /api/network/status
POST /api/network/check
GET  /api/weather/current
GET  /api/weather/report
GET  /api/system/health
```

## Zabbix

O Nexus consulta a API JSON-RPC do Zabbix e atua como camada de inteligencia:

```text
Zabbix monitora hosts, triggers e problemas
Nexus consulta a API
Nexus classifica alertas
Nexus gera resumo, relatorio e acoes sugeridas
Nexus salva historico no Obsidian
Nexus envia alerta via Telegram, painel e modo operacao
```

Recursos:

- Status da API e versao.
- Hosts monitorados.
- Problemas ativos por severidade.
- Relatorio Markdown.
- Historico em `vault/07_Nexus/Zabbix/`.
- Acknowledge com mensagem.
- Alertas criticos via Telegram quando solicitado.

Variaveis:

```env
ZABBIX_ENABLED=true
ZABBIX_API_URL=http://zabbix.local/zabbix/api_jsonrpc.php
ZABBIX_API_TOKEN=
ZABBIX_USERNAME=
ZABBIX_PASSWORD=
ZABBIX_HOST_LIMIT=100
ZABBIX_PROBLEM_LIMIT=100
```

Preferir `ZABBIX_API_TOKEN`. Se usar login/senha, o Nexus usa `user.login` e tenta `user.logout` ao final de cada operacao autenticada.

## Telegram operacional

O Telegram esta preparado para virar canal operacional do Nexus.

Recursos:

- Envio manual de mensagens.
- Envio de alertas.
- Polling em background.
- Chat restrito ao `TELEGRAM_CHAT_ID`.
- Comandos remotos.
- Conversa comum respondida pelo Nexus.

Comandos:

```text
/help
/status
/today
/network
/zabbix
/alerts
```

Variaveis:

```env
TELEGRAM_ENABLED=true
TELEGRAM_BOT_TOKEN=
TELEGRAM_CHAT_ID=
TELEGRAM_POLLING_ENABLED=true
TELEGRAM_POLLING_INTERVAL_SECONDS=2
TELEGRAM_PROCESS_EXISTING_UPDATES=false
```

## Outras integrações

### Weather

Usa Open-Meteo sem API key para Sao Jose do Rio Preto.

```text
GET /api/weather/current
GET /api/weather/report
```

### Spotify

OAuth Authorization Code Flow, playback, playlists e aprendizado musical.

```text
GET  /api/spotify/status
GET  /api/spotify/login
GET  /api/spotify/current
GET  /api/spotify/playlists
POST /api/spotify/learn
POST /api/spotify/play
POST /api/spotify/pause
POST /api/spotify/next
POST /api/spotify/previous
POST /api/spotify/volume
```

### Home Assistant

Consulta estados e executa acoes com confirmacao para entidades sensiveis.

### Google Calendar e Gmail

Preparados para briefing de agenda e resumo de emails via `GOOGLE_ACCESS_TOKEN`.

## Modo operacao

O modo operacao estrutura respostas tecnicas com:

- Resumo.
- Riscos.
- Passo a passo.
- Validacao.
- Proxima acao.

Arquivos:

```text
vault/07_Nexus/operation-mode.json
vault/07_Nexus/current-session.md
vault/07_Nexus/operation-log.md
```

## Configuracao

Copie o exemplo e preencha apenas localmente:

```powershell
Copy-Item backend\.env.example backend\.env
```

Nunca versionar `.env`, tokens, zips, logs ou secrets.

Arquivos ignorados:

```text
.env
**/.env
.env.*
backend/secrets/
logs/
Backups/
*.zip
```

## Validacao

```powershell
cd backend
dotnet build
```

```powershell
cd frontend
npm.cmd run build
```

Teste rapido:

```powershell
Invoke-RestMethod http://localhost:5000/api/zabbix/status
Invoke-RestMethod http://localhost:5000/api/integrations/status
Invoke-RestMethod http://localhost:5000/api/weather/report
```

## Backups

Backups locais ficam em `Backups/` e nao entram no Git.

Antes de releases ou alteracoes grandes, gere um backup completo da pasta do projeto. O historico operacional fica em:

```text
vault/07_Nexus/operation-log.md
```

## Documentacao

- `docs/NEXUS.md`: documentacao geral.
- `docs/INTEGRATIONS.md`: detalhes das integracoes.
- `docs/CHECKLIST.md`: checklist operacional.
- `docs/PLANO.md`: plano atual.
- `vault/07_Nexus/`: configuracoes e historicos operacionais.

## Status atual validado

- Backend .NET compila.
- Frontend Vite compila.
- Zabbix conectado via API token.
- Telegram configurado e com polling operacional.
- Painel `/zabbix` responde.
- Segredos ficam fora do Git.
