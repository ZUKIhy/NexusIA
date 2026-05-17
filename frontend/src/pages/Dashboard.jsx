import { useEffect, useState } from "react";
import { Brain, ClipboardList, Cpu, Mic2 } from "lucide-react";
import NexusCore from "../components/JarvisCore";
import ChatPanel from "../components/ChatPanel";
import StatusPanel from "../components/StatusPanel";
import LogPanel from "../components/LogPanel";
import { createNexusConnection } from "../services/signalr";

export default function Dashboard() {
  const [status, setStatus] = useState("online");
  const [logs, setLogs] = useState(["Sistema iniciado."]);

  function addLog(message) {
    setLogs(prev => [message, ...prev].slice(0, 20));
  }

  useEffect(() => {
    const connection = createNexusConnection({
      onLog: payload => addLog(payload.message || "Log recebido."),
      onActivated: payload => {
        setStatus("listening");
        addLog(payload.message || "Nexus ativado.");
      },
      onThinking: () => setStatus("thinking"),
      onSpeaking: () => {
        setStatus("speaking");
        addLog("Nexus está falando.");
      },
      onMemorySaved: payload => addLog(`Memória salva: ${payload.content}`),
      onTaskCreated: payload => addLog(`Tarefa criada: ${payload.content}`),
    });

    connection.start().then(() => addLog("SignalR conectado.")).catch(() => addLog("SignalR não conectou. O backend está rodando?"));

    return () => connection.stop();
  }, []);

  return (
    <div className="dashboard-grid">
      <section className="hero-panel">
        <div className="hero-top">
          <div>
            <h1>N.E.X.U.S</h1>
            <p>Centro de comando pessoal</p>
          </div>
          <div className="online-dot">ONLINE</div>
        </div>

        <NexusCore status={status} />

        <div className="quick-stats">
          <Stat icon={<Brain />} label="Memória" value="Obsidian" />
          <Stat icon={<Mic2 />} label="Voz" value="Ativa" />
          <Stat icon={<ClipboardList />} label="Tarefas" value="Local" />
          <Stat icon={<Cpu />} label="IA" value="OpenAI" />
        </div>
      </section>

      <ChatPanel onStatus={setStatus} onLog={addLog} />
      <StatusPanel status={status} />
      <LogPanel logs={logs} />
    </div>
  );
}

function Stat({ icon, label, value }) {
  return (
    <div className="stat-card">
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}
