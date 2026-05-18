# Integracoes do Nexus

## Ativas
- Weather: Sao Jose do Rio Preto
- Spotify
- Home Assistant
- Network Monitor
- Ollama
- Obsidian

## Planejadas
- Telegram
- Google Calendar
- Gmail

## Seguranca
- Tokens e secrets ficam no backend/.env ou backend/secrets.
- Nunca salvar tokens no Obsidian.
- Enviar emails e executar acoes sensiveis exige confirmacao.

## Release Nexus V2
- Telegram: endpoints prontos para envio, alertas e comandos; depende de TELEGRAM_BOT_TOKEN e TELEGRAM_CHAT_ID.
- Google Calendar: briefing pronto; depende de GOOGLE_ACCESS_TOKEN.
- Gmail: resumo de emails pronto; depende de GOOGLE_ACCESS_TOKEN.
- Home V2: conectada ao briefing real e ao status das integracoes.

## Endpoints V2
- GET /api/integrations/status
- POST /api/integrations/telegram/send
- POST /api/integrations/telegram/alert
- GET /api/integrations/telegram/commands
- POST /api/integrations/telegram/command
- GET /api/integrations/calendar/briefing
- GET /api/integrations/gmail/briefing
