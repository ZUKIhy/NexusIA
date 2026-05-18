import { useEffect, useState } from "react";
import { Brain, CalendarDays, ClipboardList, Cpu, FileSearch, Home, Mic2, Music2, Network, ShieldCheck } from "lucide-react";
import { Link } from "react-router-dom";
import NexusCore from "../components/JarvisCore";
import ChatPanel from "../components/ChatPanel";
import StatusPanel from "../components/StatusPanel";
import LogPanel from "../components/LogPanel";
import { createNexusConnection } from "../services/signalr";

export default function Dashboard() {
  const [status, setStatus] = useState("online");
  const [logs, setLogs] = useState(["Sistema iniciado."]);

  function addLog(message) {
    setLogs((prev) => [message, ...prev].slice(0, 20));
  }

  useEffect(() => {
    const connection = createNexusConnection({
      onLog: (payload) => addLog(payload.message || "Log recebido."),
      onActivated: (payload) => {
        setStatus("listening");
        addLog(payload.message || "Nexus ativado.");
      },
      onThinking: () => setStatus("thinking"),
      onSpeaking: () => {
        setStatus("speaking");
        addLog("Nexus esta falando.");
      },
      onMemorySaved: (payload) => addLog(`Memoria salva: ${payload.content}`),
      onTaskCreated: (payload) => addLog(`Tarefa criada: ${payload.content}`),
    });

    connection
      .start()
      .then(() => addLog("SignalR conectado."))
      .catch(() => addLog("SignalR nao conectou. O backend esta rodando?"));

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
          <StatusPill status={status} />
        </div>

        <div className="nexus-system-topbar">
          <SystemChip icon={<ShieldCheck size={14} />} label="Backend" value="online" active />
          <SystemChip icon={<Brain size={14} />} label="Vault" value="conectado" active />
          <SystemChip icon={<Cpu size={14} />} label="Ollama" value="GPU" active />
          <SystemChip icon={<Home size={14} />} label="Home" value="online" active />
          <SystemChip icon={<Network size={14} />} label="Network" value="monitor" active />
        </div>

        <div className="nexus-core-stage">
          <NexusCore status={status} />
          <div className="nexus-state-panel">
            <span>Estado atual</span>
            <strong>{getStatusLabel(status)}</strong>
            <p>Motor principal: Ollama local</p>
            <p>Memoria: Obsidian conectada</p>
          </div>
        </div>

        <div className="nexus-quick-actions">
          <Link to="/today"><CalendarDays size={16} /> Briefing</Link>
          <Link to="/docs"><FileSearch size={16} /> Docs</Link>
          <Link to="/network"><Network size={16} /> Rede</Link>
          <Link to="/media"><Music2 size={16} /> Media</Link>
        </div>

        <div className="quick-stats">
          <Stat icon={<Brain />} label="Memoria" value="Obsidian conectado" detail="Sincronizacao ativa" />
          <Stat icon={<Mic2 />} label="Voz" value="Ativa" detail="Wake word pronta" />
          <Stat icon={<ClipboardList />} label="Tarefas" value="Local" detail="Pendencias no vault" />
          <Stat icon={<Cpu />} label="IA" value="Ollama + OpenAI" detail="Fallback configurado" />
        </div>
      </section>

      <ChatPanel onStatus={setStatus} onLog={addLog} />
      <StatusPanel status={status} />
      <LogPanel logs={logs} />
    </div>
  );
}

function StatusPill({ status }) {
  return (
    <div className={`nexus-status-pill ${status}`}>
      <span />
      <strong>{getStatusLabel(status)}</strong>
    </div>
  );
}

function SystemChip({ icon, label, value, active }) {
  return (
    <article className={`system-chip ${active ? "active" : ""}`}>
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function Stat({ icon, label, value, detail }) {
  return (
    <div className="stat-card">
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
      <small>{detail}</small>
    </div>
  );
}

function getStatusLabel(status) {
  if (status === "listening") return "Escutando";
  if (status === "thinking") return "Processando";
  if (status === "speaking") return "Falando";
  if (status === "error") return "Alerta";
  return "Pronto";
}
