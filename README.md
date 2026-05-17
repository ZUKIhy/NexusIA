# N.E.X.U.S — Gabriel

Starter completo para criar um assistente estilo Nexus com:

- React + Vite no frontend
- .NET Web API no backend
- SignalR para eventos em tempo real
- OpenAI para cérebro
- Obsidian/Markdown para memória
- Voz no navegador com Web Speech API
- Fala com SpeechSynthesis
- Estrutura para ElevenLabs
- Python para detecção de palmas

## Como rodar

### Backend

```bash
cd backend
cp .env.example .env
dotnet restore
dotnet run
```

Edite o arquivo `.env` e coloque sua chave:

```env
OPENAI_API_KEY=sua_chave_aqui
VAULT_PATH=../vault
FRONTEND_URL=http://localhost:5173
```

Backend: `http://localhost:5127`

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend: `http://localhost:5173`

### Obsidian

Abra a pasta `vault/` no Obsidian.

Arquivos principais:

```txt
vault/03_Memory/memory.md
vault/04_Logs/conversations.md
vault/05_Tasks/tasks.md
vault/06_Commands/catalog.md
```

### Detector de palmas

```bash
cd python-agent
python -m venv .venv
```

Windows:

```bash
.venv\Scripts\activate
```

Mac/Linux:

```bash
source .venv/bin/activate
```

Depois:

```bash
pip install -r requirements.txt
python clap_detector.py
```

## Comandos para testar

```txt
Nexus, olá.
Nexus, lembre que eu estudo programação à noite.
Nexus, o que você lembra sobre mim?
Nexus, crie uma tarefa para estudar React amanhã.
Nexus, mostre minhas tarefas.
```
