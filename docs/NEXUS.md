# NEXUS Gabriel - Documentacao do Projeto

## Visao geral

O NEXUS e um assistente pessoal tecnico local para Gabriel. Ele combina frontend React, backend .NET, vault Obsidian, Ollama local, OpenAI como ultimo recurso, ElevenLabs para voz e controle local do computador.

O objetivo atual e apoiar operacao tecnica, consulta documental, padronizacao de procedimentos e automacao assistida do PC.

## Fluxo de inteligencia

O fluxo principal de resposta e:

1. Obsidian
2. Ollama local
3. OpenAI como ultimo caso
4. Mock/local se tudo falhar

Quando uma pergunta normal chega ao chat, o backend busca primeiro no vault Obsidian. Se encontrar documentos relevantes, responde com os trechos e caminhos. Se nao encontrar, consulta o Ollama local usando o modelo configurado em `.env`. Quando o Ollama responde, o Nexus salva uma nota em `04_Knowledge/Nexus-Auto/` para reaproveitar depois. A OpenAI so e chamada quando o Obsidian e o Ollama nao resolvem.

## Servicos principais

- Backend: `backend/`, .NET 8, porta `5000`.
- Frontend: `frontend/`, Vite/React, porta `5173`.
- Vault: definido por `VAULT_PATH` no `backend/.env`.
- Ollama: `http://localhost:11434`, modelo `nexus-qwen3b`.
- ElevenLabs: usado via `ELEVENLABS_API_KEY` e `ELEVENLABS_VOICE_ID`.

## Arquivos de configuracao

`backend/.env` guarda as configuracoes reais:

- `OPENAI_API_KEY`
- `OPENAI_MODEL`
- `OLLAMA_ENABLED`
- `OLLAMA_URL`
- `OLLAMA_MODEL`
- `VAULT_PATH`
- `FRONTEND_URL`
- `ELEVENLABS_API_KEY`
- `ELEVENLABS_VOICE_ID`

`backend/.env.example` documenta o formato sem segredos.

## Backend

Controllers principais:

- `JarvisController.cs`: chat principal, comandos locais, modo operacao, busca documental, controle do PC.
- `DocsController.cs`: busca, leitura, padronizacao e checklist de documentos.
- `IndexController.cs`: indexacao do vault.
- `ProceduresController.cs`: lista procedimentos para a pagina `/procedures`.
- `SystemController.cs`: saude do sistema, Ollama, vault e GPU.
- `ComputerController.cs`: abre apps, pastas, URLs, pesquisa e cola texto.

Services principais:

- `ObsidianService`: leitura, escrita e busca Markdown.
- `OpenAIService`: orquestra Obsidian -> Ollama -> OpenAI.
- `OllamaService`: chamada local para `/api/generate`.
- `DocumentIndexService`: gera indice JSON em `07_Nexus/document-index.json`.
- `OperationService`: modo operacao e `07_Nexus/operation-log.md`.
- `ComputerControlService`: controle assistido do Windows.
- `ElevenLabsService`: sintese de voz.

## Frontend

Paginas principais:

- `/`: dashboard e chat.
- `/docs`: busca documental, pre-visualizacao, padronizacao e checklist.
- `/procedures`: visao filtrada dos procedimentos.
- `/memory`: memoria.
- `/tasks`: tarefas.
- `/logs`: logs.
- `/system`: saude do backend, Ollama, vault, GPU e controle local.
- `/settings`: configuracoes/testes de voz.

## Modo operacao

Comandos:

- `Nexus, entrar em modo operação.`
- `Nexus, sair do modo operação.`
- `Nexus, iniciar atendimento.`
- `Nexus, iniciar atendimento sobre limpeza journal.`
- `Nexus, gerar relatório de atendimento.`
- `Nexus, criar passagem de turno.`

Quando ativo, respostas normais sao estruturadas com:

- Resumo
- Riscos
- Passo a passo
- Validacao
- Proxima acao

Arquivos usados:

- `07_Nexus/operation-mode.json`: estado tecnico usado pelo backend.
- `07_Nexus/operation-mode.md`: estado legivel no Obsidian.
- `07_Nexus/current-session.md`: sessao operacional atual, contexto, procedimentos, acoes, pendencias e proxima acao.
- `07_Nexus/operation-log.md`: historico de eventos.

Ao iniciar atendimento, o Nexus cria/atualiza `current-session.md`. Ao consultar procedimentos em modo operacao, ele registra o melhor resultado na sessao.

## Log operacional

O arquivo `07_Nexus/operation-log.md` registra:

- Buscas documentais
- Arquivos abertos
- Padronizacoes
- Checklists gerados
- Ativacao/desativacao do modo operacao
- Atendimento iniciado
- Relatorio de atendimento
- Passagem de turno

Ele serve como historico para relatorios de atendimento e passagem de turno.

## Documentos e procedimentos

Fontes prioritarias:

1. `06_Documents/Procedimentos-Padronizados`
2. `06_Documents/Base-de-Conhecimento/Zuculin`
3. `04_Knowledge`
4. `07_Nexus`

O botao `Padronizar` em `/docs` tenta usar Ollama para transformar a nota em procedimento tecnico. Se Ollama nao responder, cria um template com o conteudo original.

O botao `Checklist` cria checklists em:

`06_Documents/Checklists/Gerados/`

## Indexacao

Endpoints:

- `GET /api/index`
- `POST /api/index/rebuild`

O indice e salvo em:

`07_Nexus/document-index.json`

Ele contem path, titulo, tipo, status, tags, data de modificacao, tamanho e preview.

## Sistema

Endpoints:

- `GET /api/system/health`
- `GET /api/system/ollama`
- `GET /api/system/vault`
- `GET /api/system/gpu`

O painel `/system` mostra o JSON consolidado de saude.

Campos importantes:

- Nexus/backend/frontend online
- Vault conectado
- Total de documentos Markdown
- Ollama online/modelo atual
- GPU via `nvidia-smi`
- OpenAI configurada como ultimo recurso
- Autostart ativo
- Ultimo backup
- Modo operacao

## Controle do computador

Comandos suportados:

- `Nexus, abra o WhatsApp`
- `Nexus, abra o Chrome`
- `Nexus, abra a pasta Downloads`
- `Nexus, abra https://google.com`
- `Nexus, pesquise no navegador criar GPO`
- `Nexus, digite texto de teste`

Por seguranca, o Nexus nao deve apagar arquivos, executar comandos destrutivos ou manipular credenciais sem confirmacao explicita.

## Voz

O frontend usa:

- Reconhecimento de voz do navegador para wake word.
- ElevenLabs via backend para TTS.
- Voz do navegador como fallback.

## Personalidade conversacional

O Nexus possui uma camada de conversa natural para interações leves, sem atrapalhar o modo técnico.

Arquivos:

- `07_Nexus/personality.md`: identidade, tom e exemplos de conversa.
- `07_Nexus/conversation-memory.md`: preferências, contexto recente e resumos de conversas casuais.

Intents:

- `casual_conversation`: bom dia, boa noite, tudo bem, estou cansado, conversa comigo.
- `conversation_starter`: puxe assunto, sobre o que podemos falar, me diga algo interessante.

Regras:

- Responder em português do Brasil.
- Ser natural, calmo e breve.
- Fazer no máximo uma pergunta.
- Não fingir emoções humanas profundas.
- Salvar resumos úteis em `conversation-memory.md`.

Exemplos:

- `Nexus, bom dia.`
- `Nexus, como está hoje?`
- `Nexus, estou cansado.`
- `Nexus, puxe assunto.`

## Autostart

Scripts:

- `scripts/start-nexus.ps1`: sobe Ollama, backend e frontend.
- `scripts/start-nexus.bat`: atalho simples para rodar o PowerShell.
- `scripts/install-autostart.ps1`: cria atalho no Startup do Windows.

Atalho instalado em:

`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Nexus Autostart.lnk`

## Backup

Script:

`scripts/backup-vault.ps1`

Destino:

`C:\Users\User\OneDrive\Documentos\nexus-gabriel\Backups\NexusVault\nexus-vault-YYYY-MM-DD-HHMMSS.zip`

O script mantem somente os 10 backups mais recentes.

## Testes recomendados

- `Nexus, está por aí?`
- `Nexus, que horas são?`
- `Nexus, entrar em modo operação.`
- `Nexus, procure procedimento sobre journal.`
- `Nexus, me explique o procedimento de limpeza journal.`
- `Nexus, gerar relatório de atendimento.`
- Abrir `/docs`, buscar `journal`, clicar `Padronizar`.
- Abrir `/docs`, clicar `Checklist`.
- Abrir `/procedures` e filtrar `Limpeza`.
- Abrir `/system` e validar Ollama, vault e autostart.

## Ultima validacao

- `dotnet build`: OK.
- `npm.cmd run build`: OK.
- `POST /api/docs/standardize`: criou procedimento usando Ollama.
- `POST /api/docs/checklist`: criou checklist usando Ollama.
- `GET /api/docs/file`: registra `arquivo_aberto` no operation log.
- Chat com termo novo sem resultado no Obsidian: usou Ollama e salvou nota em `04_Knowledge/Nexus-Auto/`.
- Repeticao do mesmo termo: encontrou a nota gerada no Obsidian primeiro.
- Modo operacao: cria `operation-mode.md`, atualiza `current-session.md`, registra procedimento consultado, gera relatorio e passagem de turno.
- Backup: cria ZIP no OneDrive e mantem os ultimos 10.
- Personalidade: conversa casual com Ollama, presença natural, puxar assunto e memória conversacional.

## Comandos manuais uteis

```powershell
cd C:\Users\User\Downloads\nexus-gabriel-starter\nexus-gabriel
.\scripts\start-nexus.ps1
.\scripts\backup-vault.ps1
```

```powershell
cd backend
dotnet build
dotnet run --urls http://localhost:5000
```

```powershell
cd frontend
npm.cmd run dev
```

## Integracoes recentes

### Weather

O clima de Sao Jose do Rio Preto foi integrado com Open-Meteo.

Arquivos:

- `backend/Services/WeatherService.cs`
- `backend/Controllers/WeatherController.cs`
- `vault/07_Nexus/weather-settings.md`

Endpoints:

- `GET /api/weather/current`
- `GET /api/weather/report`

O briefing do dia inclui clima, chance de chuva, UV e recomendacao pratica.

### Spotify e Media

O Spotify foi integrado com OAuth Authorization Code Flow.

Arquivos:

- `backend/Services/SpotifyService.cs`
- `backend/Controllers/SpotifyController.cs`
- `frontend/src/pages/Media.jsx`
- `vault/07_Nexus/spotify-settings.md`

A aba `/media` mostra musica atual, capa, progresso, playlists e controles.

Comandos:

- `Nexus, tocar minhas musicas.`
- `Nexus, tocar playlist foco.`
- `Nexus, pausar musica.`
- `Nexus, proxima musica.`
- `Nexus, musica anterior.`
- `Nexus, volume 40%.`

### Aprendizado musical

O Nexus aprende gostos musicais pelo Spotify.

Arquivos:

- `backend/Services/SpotifyLearningService.cs`
- `backend/Services/SpotifyLearningBackgroundService.cs`
- `07_Nexus/music-profile.md`
- `03_Memory/memory.md`

Comando:

- `Nexus, aprenda meus gostos musicais.`

O aprendizado usa musicas mais ouvidas, artistas mais ouvidos, generos, playlists e historico recente. Quando novos scopes forem adicionados, abra novamente `/api/spotify/login`.

Mais detalhes estao em `docs/INTEGRATIONS.md`.

## Release Nexus V2 - Cockpit e integracoes

A Home V2 agora consome dados reais de `GET /api/today` e `GET /api/integrations/status`, mantendo o NexusCore em tempo real via SignalR e o chat rapido via `POST /api/nexus/chat`.

As paginas `/system`, `/network` e `/procedures` seguem o visual V2. O painel `/system` tambem mostra o status das integracoes externas.

Novos componentes backend:

- `IntegrationsController.cs`
- `TelegramService.cs`
- `GoogleCalendarService.cs`
- `GmailService.cs`

Endpoints novos:

- `GET /api/integrations/status`
- `POST /api/integrations/telegram/send`
- `POST /api/integrations/telegram/alert`
- `GET /api/integrations/telegram/commands`
- `POST /api/integrations/telegram/command`
- `GET /api/integrations/calendar/briefing`
- `GET /api/integrations/gmail/briefing`

Variaveis estao documentadas em `.env.example` e `backend/.env.example`. Nenhum token deve ser versionado.

## Zabbix

O Nexus possui uma camada Zabbix via API JSON-RPC:

- Consulta status da API.
- Lista hosts monitorados.
- Lista problemas ativos por severidade.
- Classifica problemas por severidade e acknowledgement.
- Gera relatorio Markdown.
- Salva historico em `vault/07_Nexus/Zabbix/`.
- Registra acoes em `vault/07_Nexus/zabbix-actions.md`.
- Reconhece eventos com `event.acknowledge`.
- Envia alertas criticos pelo Telegram quando solicitado.
- Exibe painel `/zabbix`.
- Responde comandos de chat e respeita modo operacao.

Endpoints:

- `GET /api/zabbix/status`
- `GET /api/zabbix/hosts`
- `GET /api/zabbix/problems?minSeverity=4`
- `POST /api/zabbix/report`
- `POST /api/zabbix/acknowledge`

Variaveis:

- `ZABBIX_ENABLED`
- `ZABBIX_API_URL`
- `ZABBIX_API_TOKEN`
- `ZABBIX_USERNAME`
- `ZABBIX_PASSWORD`
- `ZABBIX_HOST_LIMIT`
- `ZABBIX_PROBLEM_LIMIT`

## Telegram operacional

O Telegram pode ser usado como canal ativo do Nexus.

Recursos:

- Envio manual por `POST /api/integrations/telegram/send`.
- Envio de alertas por `TelegramService.SendAlertAsync`.
- Polling em background por `TelegramPollingBackgroundService`.
- Chat restrito ao `TELEGRAM_CHAT_ID`.
- Comandos `/status`, `/today`, `/network`, `/zabbix`, `/alerts`, `/help`.
- Mensagens comuns respondidas pelo Nexus.
- Alertas automaticos de internet e Home Assistant pelo `AlertBackgroundService`.

Variaveis:

- `TELEGRAM_ENABLED`
- `TELEGRAM_BOT_TOKEN`
- `TELEGRAM_CHAT_ID`
- `TELEGRAM_POLLING_ENABLED`
- `TELEGRAM_POLLING_INTERVAL_SECONDS`
- `TELEGRAM_PROCESS_EXISTING_UPDATES`
