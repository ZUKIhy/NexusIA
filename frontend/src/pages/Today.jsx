import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, ClipboardList, Home, Network, RefreshCw, ShieldCheck, Sparkles } from "lucide-react";
import { getTodayBriefing } from "../services/api";

export default function Today() {
  const [briefing, setBriefing] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    refresh();
  }, []);

  async function refresh() {
    setLoading(true);
    setError("");

    try {
      setBriefing(await getTodayBriefing());
    } catch {
      setError("Nao consegui carregar o briefing do dia.");
    } finally {
      setLoading(false);
    }
  }

  const network = briefing?.network || {};

  return (
    <div className="page today-page">
      <div className="docs-header">
        <div>
          <h1>Today</h1>
          <p>Briefing do dia, alertas, rede, casa e pendencias do Nexus.</p>
        </div>

        <button type="button" className="small-action-button" onClick={refresh} disabled={loading}>
          <RefreshCw size={15} />
          {loading ? "Atualizando" : "Atualizar"}
        </button>
      </div>

      {error && <p className="docs-error">{error}</p>}

      <section className="today-briefing">
        <div>
          <span>Briefing</span>
          <h2>{briefing?.summary || "Carregando briefing..."}</h2>
          <p>{briefing?.suggestion || "Aguardando dados do Nexus."}</p>
        </div>
        <Sparkles size={34} />
      </section>

      <div className="home-summary">
        <TodayStat icon={<ShieldCheck />} label="Nexus" value={briefing?.nexusStatus || "-"} />
        <TodayStat icon={<Network />} label="Internet" value={network.internetOnline ? "Online" : "Offline"} />
        <TodayStat icon={<Home />} label="Home Assistant" value={briefing?.homeAssistantOnline ? "Online" : "Offline"} />
        <TodayStat icon={<ClipboardList />} label="Tarefas" value={briefing?.openTasks?.length ?? 0} />
        <TodayStat icon={<AlertTriangle />} label="Alertas" value={briefing?.alerts?.length ?? 0} />
      </div>

      <div className="today-grid">
        <section className="panel today-panel">
          <header>
            <strong>Rede</strong>
            <span>{network.status || "-"}</span>
          </header>
          <p>Monitorados: {network.monitoredDevices ?? 0}</p>
          <p>Online: {network.onlineDevices ?? 0}</p>
          <p>Offline: {network.offlineDevices ?? 0}</p>
          <p>Desconhecidos: {network.unknownDevices?.length ?? 0}</p>
        </section>

        <section className="panel today-panel">
          <header>
            <strong>Pendencias</strong>
            <span>{briefing?.openTasks?.length ?? 0} abertas</span>
          </header>
          <List items={briefing?.openTasks} empty="Nenhuma tarefa aberta encontrada." />
        </section>

        <section className="panel today-panel">
          <header>
            <strong>Alertas</strong>
            <span>{briefing?.alerts?.length ?? 0} recentes</span>
          </header>
          {briefing?.alerts?.length > 0 ? (
            <div className="today-list">
              {briefing.alerts.map((alert, index) => (
                <article key={`${alert.time}-${index}`}>
                  <CheckCircle2 size={15} />
                  <div>
                    <strong>{alert.title || alert.type}</strong>
                    <small>{alert.severity || "info"} - {alert.time}</small>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <p className="muted">Nenhum alerta recente.</p>
          )}
        </section>

        <section className="panel today-panel">
          <header>
            <strong>Revisao</strong>
            <span>{briefing?.reviewItems?.length ?? 0} itens</span>
          </header>
          {briefing?.reviewItems?.length > 0 ? (
            <div className="today-list">
              {briefing.reviewItems.map((item, index) => (
                <article key={`${item.path}-${index}`}>
                  <AlertTriangle size={15} />
                  <div>
                    <strong>{item.title}</strong>
                    <small>{item.path}</small>
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <p className="muted">Nada pendente de revisao.</p>
          )}
        </section>
      </div>
    </div>
  );
}

function TodayStat({ icon, label, value }) {
  return (
    <div className="stat-card">
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function List({ items = [], empty }) {
  if (!items.length) return <p className="muted">{empty}</p>;

  return (
    <div className="today-list">
      {items.map((item, index) => (
        <article key={`${item}-${index}`}>
          <ClipboardList size={15} />
          <div>
            <strong>{String(item).replace("- [ ]", "").trim()}</strong>
          </div>
        </article>
      ))}
    </div>
  );
}
