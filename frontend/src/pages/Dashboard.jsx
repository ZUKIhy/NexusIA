import { useEffect, useState } from "react";
import { Brain, CalendarDays, ClipboardList, CloudSun, Cpu, FileSearch, Home, Mail, Music2, Network, ShieldCheck } from "lucide-react";
import { Link } from "react-router-dom";
import NexusCore from "../components/JarvisCore";
import ChatPanel from "../components/ChatPanel";
import StatusPanel from "../components/StatusPanel";
import LogPanel from "../components/LogPanel";
import { createNexusConnection } from "../services/signalr";
import { getIntegrationsStatus, getTodayBriefing } from "../services/api";

export default function Dashboard() {
  const [status, setStatus] = useState("online");
  const [logs, setLogs] = useState(["Sistema iniciado."]);
  const [briefing, setBriefing] = useState(null);
  const [integrations, setIntegrations] = useState(null);

  function addLog(message) {
    setLogs((prev) => [message, ...prev].slice(0, 20));
  }

  useEffect(() => {
    refreshCockpit();
    const cockpitTimer = setInterval(refreshCockpit, 60000);
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

    return () => {
      clearInterval(cockpitTimer);
      connection.stop();
    };
  }, []);

  async function refreshCockpit() {
    try {
      setBriefing(await getTodayBriefing());
    } catch {
      addLog("Briefing real indisponivel.");
    }

    try {
      setIntegrations(await getIntegrationsStatus());
    } catch {
      setIntegrations(null);
    }
  }

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
          <SystemChip icon={<ShieldCheck size={14} />} label="Backend" value={briefing?.nexusStatus || "online"} active />
          <SystemChip icon={<Brain size={14} />} label="Vault" value={`${briefing?.vaultDocuments ?? 0} docs`} active={Boolean(briefing)} />
          <SystemChip icon={<Cpu size={14} />} label="Ollama" value={briefing?.ollamaEnabled ? "ativo" : "standby"} active={briefing?.ollamaEnabled} />
          <SystemChip icon={<Home size={14} />} label="Home" value={briefing?.homeAssistantOnline ? "online" : "offline"} active={briefing?.homeAssistantOnline} />
          <SystemChip icon={<Network size={14} />} label="Network" value={briefing?.network?.status || "monitor"} active={briefing?.network?.status === "online"} />
        </div>

        <div className="nexus-core-stage">
          <NexusCore status={status} />
          <div className="nexus-state-panel">
            <span>Estado atual</span>
            <strong>{getStatusLabel(status)}</strong>
            <p>{briefing?.summary || "Carregando telemetria real do cockpit."}</p>
            <p>{briefing?.suggestion || "Aguardando briefing operacional."}</p>
          </div>
        </div>

        <div className="nexus-quick-actions">
          <Link to="/today"><CalendarDays size={16} /> Briefing</Link>
          <Link to="/docs"><FileSearch size={16} /> Docs</Link>
          <Link to="/network"><Network size={16} /> Rede</Link>
          <Link to="/media"><Music2 size={16} /> Media</Link>
        </div>

        <div className="quick-stats">
          <Stat icon={<CloudSun />} label="Clima" value={briefing?.weather?.current?.temperature !== undefined ? `${Math.round(briefing.weather.current.temperature)} C` : "-"} detail={briefing?.weather?.summary || "Rio Preto"} />
          <Stat icon={<ClipboardList />} label="Tarefas" value={briefing?.openTasks?.length ?? 0} detail="Abertas no vault" />
          <Stat icon={<Network />} label="Rede" value={briefing?.network?.internetOnline ? "Internet online" : "Atencao"} detail={`${briefing?.network?.onlineDevices ?? 0} online / ${briefing?.network?.offlineDevices ?? 0} offline`} />
          <Stat icon={<Mail />} label="Integracoes" value={countConfigured(integrations)} detail="Telegram, agenda e Gmail" />
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

function countConfigured(integrations) {
  if (!integrations) return "-";
  const values = [integrations.telegram, integrations.googleCalendar, integrations.gmail];
  return `${values.filter((item) => item?.configured).length}/3 prontas`;
}
