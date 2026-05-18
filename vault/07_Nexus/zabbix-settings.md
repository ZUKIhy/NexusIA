# Zabbix Settings

O Nexus funciona como camada inteligente em cima do Zabbix.

Fluxo:

```text
Zabbix monitora hosts, triggers e problemas
Nexus consulta a API JSON-RPC
Nexus classifica alertas
Nexus gera resumo, relatorio e acoes sugeridas
Nexus salva historico no Obsidian
Nexus envia alertas via Telegram, painel e modo operacao
```

## Variaveis

```env
ZABBIX_ENABLED=true
ZABBIX_API_URL=https://zabbix.example.com/zabbix/api_jsonrpc.php
ZABBIX_API_TOKEN=
ZABBIX_USERNAME=
ZABBIX_PASSWORD=
ZABBIX_HOST_LIMIT=100
ZABBIX_PROBLEM_LIMIT=100
```

Preferir `ZABBIX_API_TOKEN`. Se usar `ZABBIX_USERNAME` e `ZABBIX_PASSWORD`, o Nexus usa `user.login` e chama `user.logout` ao final de cada operacao autenticada.

## Endpoints

- `GET /api/zabbix/status`
- `GET /api/zabbix/hosts`
- `GET /api/zabbix/problems?minSeverity=4`
- `POST /api/zabbix/report`
- `POST /api/zabbix/acknowledge`

## Painel

- `/zabbix`

## Severidades

- 0: Not classified
- 1: Information
- 2: Warning
- 3: Average
- 4: High
- 5: Disaster
