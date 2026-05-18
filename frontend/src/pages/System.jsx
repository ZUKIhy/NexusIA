import { useEffect, useState } from "react";
import { Activity, Brain, Cpu, HardDrive, PlugZap, RefreshCw, ShieldCheck } from "lucide-react";
import { getComputerStatus, getIntegrationsStatus, getStatus, getSystemHealth } from "../services/api";

export default function System() {
  const [status, setStatus] = useState(null);
  const [computer, setComputer] = useState(null);
  const [health, setHealth] = useState(null);
  const [integrations, setIntegrations] = useState(null);

  useEffect(() => {
    refresh();
  }, []);

  function refresh() {
    getStatus().then(setStatus).catch(() => setStatus({ status: "offline", error: "Backend nao encontrado" }));
    getComputerStatus().then(setComputer).catch(() => setComputer(null));
    getSystemHealth().then(setHealth).catch(() => setHealth(null));
    getIntegrationsStatus().then(setIntegrations).catch(() => setIntegrations(null));
  }

  return (
    <div className="page system-page">
      <div className="docs-header">
        <div>
          <h1>System</h1>
          <p>Status do backend, integracoes e controle local do computador.</p>
        </div>
        <button type="button" className="small-action-button" onClick={refresh}>
          <RefreshCw size={15} />
          Atualizar
        </button>
      </div>

      <div className="home-summary">
        <SystemMetric icon={<ShieldCheck />} label="Backend" value={status?.status || "offline"} />
        <SystemMetric icon={<HardDrive />} label="Vault" value={health?.vault?.markdownDocuments ?? 0} />
        <SystemMetric icon={<Cpu />} label="Ollama" value={health?.ollama?.enabled ? "ativo" : "standby"} />
        <SystemMetric icon={<Brain />} label="Operacao" value={health?.operationMode ? "ativa" : "inativa"} />
        <SystemMetric icon={<PlugZap />} label="Integracoes" value={countConfigured(integrations)} />
      </div>

      <div className="settings-grid">
        <section className="panel settings-panel">
          <h2>Health</h2>
          <pre className="markdown-preview">{JSON.stringify(health, null, 2)}</pre>
        </section>

        <section className="panel settings-panel">
          <h2>Backend</h2>
          <pre className="markdown-preview">{JSON.stringify(status, null, 2)}</pre>
        </section>

        <section className="panel settings-panel">
          <h2>Controle Local</h2>
          <p>Apps: {computer?.canOpenApps ? "ativo" : "inativo"}</p>
          <p>Pastas: {computer?.canOpenFolders ? "ativo" : "inativo"}</p>
          <p>URLs: {computer?.canOpenUrls ? "ativo" : "inativo"}</p>
          <p>Digitacao na janela ativa: {computer?.canPasteIntoActiveWindow ? "ativo" : "inativo"}</p>
        </section>

        <section className="panel settings-panel">
          <h2>Integracoes</h2>
          <pre className="markdown-preview">{JSON.stringify(integrations, null, 2)}</pre>
        </section>
      </div>
    </div>
  );
}

function SystemMetric({ icon, label, value }) {
  return (
    <div className="stat-card">
      {icon || <Activity />}
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function countConfigured(integrations) {
  if (!integrations) return "-";
  return [integrations.telegram, integrations.googleCalendar, integrations.gmail].filter((item) => item?.configured).length;
}
