import { useEffect, useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, Clock, Network, RefreshCw, Router, Search, Server, ShieldAlert, WifiOff } from "lucide-react";
import { checkNetwork, getNetworkStatus, resolveUnknownNetworkDevice } from "../services/api";

const FILTERS = [
  { key: "all", label: "Todos" },
  { key: "online", label: "Online" },
  { key: "offline", label: "Offline" },
  { key: "critical", label: "Criticos" },
  { key: "servidor", label: "Servidores" },
  { key: "automacao", label: "Casa inteligente" },
  { key: "unknown", label: "Desconhecidos" },
];

export default function NetworkMonitor() {
  const [status, setStatus] = useState(null);
  const [filter, setFilter] = useState("all");
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const devices = status?.devices || [];
  const unknownDevices = status?.unknownDevices || [];
  const visibleDevices = useMemo(() => {
    const term = query.trim().toLowerCase();

    return devices
      .filter((device) => {
        if (filter === "unknown") return false;
        if (filter === "online") return device.online;
        if (filter === "offline") return !device.online;
        if (filter === "critical") return device.critical;
        if (filter === "servidor") return normalize(device.type).includes("servidor");
        if (filter === "automacao") return normalize(device.type).includes("automacao");
        return true;
      })
      .filter((device) => {
        if (!term) return true;
        return `${device.name} ${device.ip} ${device.type}`.toLowerCase().includes(term);
      });
  }, [devices, filter, query]);

  useEffect(() => {
    refresh();
  }, []);

  async function refresh() {
    setLoading(true);
    setError("");

    try {
      const data = await getNetworkStatus();
      setStatus(data);
      setNotice("Monitoramento atualizado.");
    } catch {
      setError("Nao consegui carregar o monitoramento de rede. Verifique o backend.");
    } finally {
      setLoading(false);
    }
  }

  async function runCheck() {
    setLoading(true);
    setError("");
    setNotice("");

    try {
      const data = await checkNetwork();
      setStatus(data);
      setNotice("Verificacao executada e registrada no Obsidian.");
    } catch {
      setError("Falha ao executar verificacao de rede.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page network-page">
      <div className="docs-header">
        <div>
          <h1>Nexus Network</h1>
          <p>Monitoramento simples e seguro de dispositivos online/offline.</p>
        </div>

        <div className={`network-status ${status?.status === "online" ? "online" : "attention"}`}>
          {status?.status === "online" ? <CheckCircle2 size={18} /> : <ShieldAlert size={18} />}
          <span>{status?.status === "online" ? "REDE OK" : "ATENCAO"}</span>
        </div>
      </div>

      <div className="home-summary">
        <SummaryCard icon={<Network />} label="Monitorados" value={status?.monitoredDevices ?? 0} />
        <SummaryCard icon={<Router />} label="Internet" value={status?.internetOnline ? "Online" : "Offline"} />
        <SummaryCard icon={<CheckCircle2 />} label="Online" value={status?.onlineDevices ?? 0} />
        <SummaryCard icon={<WifiOff />} label="Offline" value={status?.offlineDevices ?? 0} />
        <SummaryCard icon={<AlertTriangle />} label="Alertas hoje" value={status?.alertsToday ?? 0} />
      </div>

      {unknownDevices.length > 0 && filter !== "online" && filter !== "offline" && filter !== "critical" && filter !== "servidor" && filter !== "automacao" && (
        <section className="network-unknown-panel">
          <header>
            <div>
              <h2>Novo dispositivo detectado</h2>
              <p>Gabriel, o Nexus encontrou IP/MAC que ainda nao estao no seu mapa de rede.</p>
            </div>
            <AlertTriangle size={24} />
          </header>

          <div className="network-unknown-list">
            {unknownDevices.map((device) => (
              <UnknownDevicePrompt
                key={`${device.ip}-${device.mac}`}
                device={device}
                onResolved={refresh}
                setError={setError}
                setNotice={setNotice}
              />
            ))}
          </div>
        </section>
      )}

      <section className="network-overview">
        <div>
          <Clock size={16} />
          <span>Ultima verificacao: {formatDate(status?.checkedAt)}</span>
        </div>
        <div>
          <Router size={16} />
          <span>Ultimo evento: {status?.lastEvent || "Sem eventos registrados"}</span>
        </div>
      </section>

      <div className="home-toolbar">
        <div className="home-search">
          <Search size={17} />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Buscar por nome, IP ou tipo"
          />
        </div>

        <select value={filter} onChange={(event) => setFilter(event.target.value)}>
          {FILTERS.map((item) => (
            <option key={item.key} value={item.key}>{item.label}</option>
          ))}
        </select>

        <button type="button" className="small-action-button" onClick={runCheck} disabled={loading}>
          <RefreshCw size={15} />
          {loading ? "Verificando" : "Verificar agora"}
        </button>
      </div>

      {notice && <p className="home-notice">{notice}</p>}
      {error && <p className="docs-error">{error}</p>}

      <div className="network-device-grid">
        {visibleDevices.map((device) => (
          <article key={`${device.name}-${device.ip}`} className={`network-device ${device.online ? "online" : "offline"} ${device.critical ? "critical" : ""}`}>
            <header>
              <div className="home-device-icon">{getDeviceIcon(device.type)}</div>
              <div>
                <h2>{device.name}</h2>
                <p>{device.ip}</p>
              </div>
            </header>

            <div className="home-device-state">
              <span>Status</span>
              <strong>{device.online ? "Online" : "Offline"}</strong>
            </div>

            <dl className="network-meta">
              <div>
                <dt>Tipo</dt>
                <dd>{device.type || "nao informado"}</dd>
              </div>
              <div>
                <dt>Latencia</dt>
                <dd>{device.latencyMs !== null && device.latencyMs !== undefined ? `${device.latencyMs} ms` : "-"}</dd>
              </div>
              <div>
                <dt>Critico</dt>
                <dd>{device.critical ? "sim" : "nao"}</dd>
              </div>
              <div>
                <dt>Monitorar</dt>
                <dd>{device.monitor ? "sim" : "nao"}</dd>
              </div>
            </dl>

            <small>{device.status}</small>
          </article>
        ))}
      </div>

      {!loading && visibleDevices.length === 0 && (
        <section className="panel home-empty">
          <p className="muted">
            {filter === "unknown" && unknownDevices.length > 0
              ? "Os dispositivos desconhecidos aparecem no alerta acima."
              : "Nenhum dispositivo encontrado com os filtros atuais."}
          </p>
        </section>
      )}
    </div>
  );
}

function UnknownDevicePrompt({ device, onResolved, setError, setNotice }) {
  const [name, setName] = useState(`Dispositivo ${device.ip}`);
  const [type, setType] = useState("desconhecido");
  const [saving, setSaving] = useState(false);

  async function resolve(known) {
    setSaving(true);
    setError("");
    setNotice("");

    try {
      const result = await resolveUnknownNetworkDevice({
        ip: device.ip,
        mac: device.mac,
        known,
        name,
        type,
      });

      setNotice(result.message || "Dispositivo resolvido.");
      await onResolved();
    } catch {
      setError("Nao consegui registrar sua decisao sobre o dispositivo novo.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <article className="network-unknown-device">
      <div>
        <strong>{device.ip}</strong>
        <span>{device.mac}</span>
        <small>Detectado por {device.source || "arp"} em {formatDate(device.detectedAt)}</small>
      </div>

      <input
        value={name}
        onChange={(event) => setName(event.target.value)}
        placeholder="Nome do dispositivo"
      />

      <select value={type} onChange={(event) => setType(event.target.value)}>
        <option value="desconhecido">Tipo desconhecido</option>
        <option value="celular">Celular</option>
        <option value="computador">Computador</option>
        <option value="automacao">Casa inteligente</option>
        <option value="servidor">Servidor</option>
        <option value="tv">TV / Midia</option>
        <option value="visitante">Visitante</option>
      </select>

      <div>
        <button type="button" onClick={() => resolve(true)} disabled={saving}>
          Conheco
        </button>
        <button type="button" className="danger-action" onClick={() => resolve(false)} disabled={saving}>
          Nao conheco
        </button>
      </div>
    </article>
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

function getDeviceIcon(type = "") {
  const normalized = normalize(type);

  if (normalized.includes("servidor")) return <Server size={22} />;
  if (normalized.includes("roteador")) return <Router size={22} />;
  if (normalized.includes("automacao")) return <Network size={22} />;

  return <Network size={22} />;
}

function normalize(value = "") {
  return value
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "");
}

function formatDate(value) {
  if (!value) return "-";
  return new Date(value).toLocaleString("pt-BR");
}
