# Plano de execucao

## Feito

- Frontend React + backend .NET rodando localmente.
- Chat principal com intents locais.
- Memoria no Obsidian.
- Tarefas, logs e busca documental.
- Modo operacao.
- Ollama local e OpenAI como fallback.
- ElevenLabs e voz do navegador.
- Home Assistant.
- Network Monitor.
- Today dashboard.
- Weather com Open-Meteo.
- Spotify OAuth, playback e aba Media.
- Playlists no Media.
- Aprendizado musical e memoria automatica.
- Backup local e push para GitHub.

## Proximo bloco recomendado

1. Telegram para alertas e comandos pelo celular.
2. Google Calendar para agenda no briefing.
3. Gmail para resumo de emails.
4. Melhorar permissoes/confirmacoes para acoes sensiveis.
5. Criar painel de memoria do usuario com gostos, rotina e preferencias.

## Validacao continua

Antes de cada push:

```powershell
cd backend
dotnet build
```

```powershell
cd frontend
npm.cmd run build
```
