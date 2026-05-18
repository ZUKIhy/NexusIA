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
- Zabbix: painel, relatorios, problemas ativos, hosts e acknowledge via API JSON-RPC.

## Endpoints V2
- GET /api/integrations/status
- POST /api/integrations/telegram/send
- POST /api/integrations/telegram/alert
- GET /api/integrations/telegram/commands
- POST /api/integrations/telegram/command
- GET /api/integrations/calendar/briefing
- GET /api/integrations/gmail/briefing
- GET /api/zabbix/status
- GET /api/zabbix/hosts
- GET /api/zabbix/problems
- POST /api/zabbix/report
- POST /api/zabbix/acknowledge

## Telegram operacional
- Polling em background habilitado por TELEGRAM_POLLING_ENABLED.
- Chat restrito ao TELEGRAM_CHAT_ID.
- Comandos: /status, /today, /network, /zabbix, /alerts, /help.
- Mensagens comuns sao respondidas pelo Nexus.
- Alertas criticos podem ser enviados automaticamente pelo canal.
