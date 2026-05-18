import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, BellRing, CheckCircle2, ClipboardCheck, FileText, RefreshCw, Search, ServerCog, ShieldAlert } from "lucide-react";
import { acknowledgeZabbixEvent, generateZabbixReport, getZabbixHosts, getZabbixProblems, getZabbixStatus } from "../services/api";

const SEVERITIES = [
  { value: "", label: "Todas" },
  { value: 2, label: "Warning+" },
  { value: 3, label: "Average+" },
  { value: 4, label: "High+" },
  { value: 5, label: "Disaster" },
];

export default function Zabbix() {
  const [status, setStatus] = useState(null);
  const [hosts, setHosts] = useState([]);
  const [problems, setProblems] = useState([]);
  const [severity, setSeverity] = useState(4);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");

  const visibleProblems = useMemo(() => {
    const term = query.trim().toLowerCase();
    if (!term) return problems;

    return problems.filter((problem) =>
      `${problem.name} ${problem.hostName} ${problem.severityName} ${problem.eventId}`.toLowerCase().includes(term)
    );
  }, [problems, query]);

  useEffect(() => {
    refresh();
  }, [severity]);

  async function refresh() {
    setLoading(true);
    setError("");
    setNotice("");

    try {
      const [nextStatus, nextHosts, nextProblems] = await Promise.all([
        getZabbixStatus(),
        getZabbixHosts(),
        getZabbixProblems(severity),
      ]);

      setStatus(nextStatus);
      setHosts(nextHosts.hosts || []);
      setProblems(nextProblems.problems || []);
      setNotice("Painel Zabbix atualizado.");
    } catch {
      setError("Nao consegui consultar o Zabbix. Verifique backend e variaveis ZABBIX_*.");
    } finally {
      setLoading(false);
    }
  }

  async function createReport(notifyCritical = false) {
    setLoading(true);
    setError("");
    setNotice("");

    try {
      const report = await generateZabbixReport({
        minSeverity: severity === "" ? null : Number(severity),
        saveToObsidian: true,
        notifyCritical,
      });

      setNotice(report.summary || "Relatorio Zabbix salvo no Obsidian.");
      setProblems(report.problems || problems);
    } catch {
      setError("Falha ao gerar relatorio Zabbix.");
    } finally {
      setLoading(false);
    }
  }

  async function acknowledge(problem) {
    const message = `Reconhecido pelo Nexus em ${new Date().toLocaleString("pt-BR")}. Em atendimento operacional.`;
    setError("");
    setNotice("");

    try {
      const result = await acknowledgeZabbixEvent({ eventId: problem.eventId, message });
      setNotice(result.message || "Evento reconhecido.");
      await refresh();
    } catch {
      setError("Nao consegui reconhecer o evento no Zabbix.");
    }
  }

  return (
    <div className="page zabbix-page">
      <div className="docs-header">
        <div>
          <h1>Zabbix</h1>
          <p>Camada inteligente de alertas, relatorios, historico e resposta operacional.</p>
        </div>

        <div className={`network-status ${status?.status === "online" ? "online" : "attention"}`}>
          {status?.status === "online" ? <CheckCircle2 size={18} /> : <ShieldAlert size={18} />}
          <span>{status?.status || "offline"}</span>
        </div>
      </div>

      <div className="home-summary">
        <SummaryCard icon={<ServerCog />} label="API" value={status?.configured ? "Configurada" : "Pendente"} />
        <SummaryCard icon={<FileText />} label="Versao" value={status?.version || "-"} />
        <SummaryCard icon={<ServerCog />} label="Hosts" value={hosts.length} />
        <SummaryCard icon={<AlertTriangle />} label="Problemas" value={problems.length} />
        <SummaryCard icon={<BellRing />} label="Criticos" value={problems.filter((problem) => problem.severity >= 4).length} />
      </div>

      <div className="home-toolbar">
        <div className="home-search">
          <Search size={17} />
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Buscar por host, evento ou severidade" />
        </div>

        <select value={severity} onChange={(event) => setSeverity(event.target.value)}>
          {SEVERITIES.map((item) => (
            <option key={item.label} value={item.value}>{item.label}</option>
          ))}
        </select>

        <button type="button" className="small-action-button" onClick={refresh} disabled={loading}>
          <RefreshCw size={15} />
          Atualizar
        </button>
        <button type="button" className="small-action-button" onClick={() => createReport(false)} disabled={loading}>
          <FileText size={15} />
          Relatorio
        </button>
        <button type="button" className="small-action-button" onClick={() => createReport(true)} disabled={loading}>
          <BellRing size={15} />
          Alertar
        </button>
      </div>

      {notice && <p className="home-notice">{notice}</p>}
      {error && <p className="docs-error">{error}</p>}

      <section className="network-overview">
        <div>
          <ServerCog size={16} />
          <span>{status?.message || "Aguardando status do Zabbix."}</span>
        </div>
        <div>
          <ClipboardCheck size={16} />
          <span>Relatorios sao salvos em 07_Nexus/Zabbix no Obsidian.</span>
        </div>
      </section>

      <div className="network-device-grid zabbix-problem-grid">
        {visibleProblems.map((problem) => (
          <article key={problem.eventId} className={`network-device ${problem.acknowledged ? "online" : "critical"}`}>
            <header>
              <div className="home-device-icon"><AlertTriangle size={22} /></div>
              <div>
                <h2>{problem.name}</h2>
                <p>{problem.hostName}</p>
              </div>
            </header>

            <div className="home-device-state">
              <span>{problem.severityName}</span>
              <strong>{problem.acknowledged ? "Reconhecido" : "Aberto"}</strong>
            </div>

            <dl className="network-meta">
              <div><dt>EventId</dt><dd>{problem.eventId}</dd></div>
              <div><dt>Inicio</dt><dd>{formatDate(problem.startedAt)}</dd></div>
              <div><dt>Suprimido</dt><dd>{problem.suppressed ? "sim" : "nao"}</dd></div>
              <div><dt>Tags</dt><dd>{problem.tags?.slice(0, 2).join(", ") || "-"}</dd></div>
            </dl>

            <small>{problem.suggestedAction}</small>

            <div className="procedure-actions">
              <button type="button" onClick={() => acknowledge(problem)} disabled={problem.acknowledged}>
                Reconhecer
              </button>
            </div>
          </article>
        ))}
      </div>

      {!loading && visibleProblems.length === 0 && (
        <section className="panel home-empty">
          <p className="muted">Nenhum problema encontrado com o filtro atual.</p>
        </section>
      )}
    </div>
  );
}

function SummaryCard({ icon, label, value }) {
  return (
    <div className="stat-card">
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function formatDate(value) {
  if (!value) return "-";
  return new Date(value).toLocaleString("pt-BR");
}
