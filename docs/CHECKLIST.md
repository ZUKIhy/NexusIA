# Checklist

## Backend

- [ ] Conferir `backend/.env`.
- [ ] Rodar `dotnet restore`.
- [ ] Rodar `dotnet build`.
- [ ] Subir `dotnet run --urls http://localhost:5000`.
- [ ] Abrir `http://localhost:5000`.
- [ ] Testar `/api/nexus/status`.
- [ ] Testar `/api/weather/report`.
- [ ] Testar `/api/spotify/status`.

## Frontend

- [ ] Rodar `npm install`.
- [ ] Rodar `npm.cmd run build`.
- [ ] Rodar `npm.cmd run dev`.
- [ ] Abrir `http://localhost:5173`.
- [ ] Validar `/today`.
- [ ] Validar `/media`.
- [ ] Validar `/network`.

## Spotify

- [ ] Cadastrar redirect URI no Spotify Developer Dashboard.
- [ ] Abrir `/api/spotify/login`.
- [ ] Confirmar `authenticated=true` em `/api/spotify/status`.
- [ ] Deixar Spotify aberto em um dispositivo.
- [ ] Testar `Nexus, tocar minhas musicas`.
- [ ] Testar `Nexus, aprenda meus gostos musicais`.

## Weather

- [ ] Testar `Nexus, relatorio do clima`.
- [ ] Testar `Nexus, briefing do dia`.

## Obsidian

- [ ] Abrir o vault configurado em `VAULT_PATH`.
- [ ] Confirmar memoria em `03_Memory/memory.md`.
- [ ] Confirmar perfil musical em `07_Nexus/music-profile.md`.
- [ ] Confirmar configuracoes em `07_Nexus/`.

## Git e backup

- [ ] Criar backup local em `Backups/`.
- [ ] Garantir que `.env`, `secrets/`, `logs/` e `Backups/` nao entram no Git.
- [ ] Rodar `git status`.
- [ ] Fazer commit.
- [ ] Fazer push.
