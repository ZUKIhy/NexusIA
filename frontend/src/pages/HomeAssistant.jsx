import { useEffect, useMemo, useState } from "react";
import {
  Fan,
  Lightbulb,
  Clapperboard,
  PlugZap,
  Power,
  RefreshCw,
  Search,
  SlidersHorizontal,
  Thermometer,
  Tv,
  Wifi,
  WifiOff,
} from "lucide-react";
import {
  getHomeAssistantStates,
  getHomeAssistantStatus,
  setHomeLightBrightness,
  toggleHomeEntity,
  turnOffHomeEntity,
  turnOnHomeEntity,
} from "../services/api";

const VISIBLE_DOMAINS = ["light", "switch", "fan", "climate", "cover", "media_player", "scene"];
const CONTROLLABLE_DOMAINS = ["light", "switch", "fan", "media_player", "scene"];

export default function HomeAssistant() {
  const [status, setStatus] = useState(null);
  const [states, setStates] = useState([]);
  const [query, setQuery] = useState("");
  const [domainFilter, setDomainFilter] = useState("all");
  const [loading, setLoading] = useState(true);
  const [busyEntity, setBusyEntity] = useState("");
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const [brightness, setBrightness] = useState(50);

  const visibleStates = useMemo(() => {
    const term = query.trim().toLowerCase();

    return states
      .filter((item) => VISIBLE_DOMAINS.includes(getDomain(item.entity_id)))
      .filter((item) => domainFilter === "all" || getDomain(item.entity_id) === domainFilter)
      .filter((item) => {
        if (!term) return true;
        return `${item.entity_id} ${getFriendlyName(item)}`.toLowerCase().includes(term);
      })
      .sort((a, b) => getFriendlyName(a).localeCompare(getFriendlyName(b), "pt-BR"));
  }, [domainFilter, query, states]);

  const onlineDevices = visibleStates.filter((item) => !["unavailable", "unknown"].includes(item.state));
  const activeDevices = visibleStates.filter((item) => !["off", "unavailable", "unknown"].includes(item.state));

  useEffect(() => {
    refresh();
  }, []);

  async function refresh() {
    setLoading(true);
    setError("");

    try {
      const [statusData, statesData] = await Promise.all([
        getHomeAssistantStatus(),
        getHomeAssistantStates(),
      ]);

      setStatus(statusData);
      setStates(Array.isArray(statesData) ? statesData : []);
      setNotice("Estados atualizados.");
    } catch {
      setError("Nao consegui carregar o portal Home Assistant. Verifique se o backend esta rodando.");
    } finally {
      setLoading(false);
    }
  }

  async function runAction(entity, action) {
    setBusyEntity(entity.entity_id);
    setError("");
    setNotice("");

    try {
      const handlers = {
        on: () => turnOnHomeEntity(entity.entity_id),
        off: () => turnOffHomeEntity(entity.entity_id),
        toggle: () => toggleHomeEntity(entity.entity_id),
      };

      const result = await handlers[action]();
      setNotice(result.success
        ? `${getFriendlyName(entity)}: comando enviado.`
        : `${getFriendlyName(entity)}: Home Assistant nao confirmou sucesso.`);

      await refresh();
    } catch {
      setError(`Falha ao executar comando em ${entity.entity_id}.`);
    } finally {
      setBusyEntity("");
    }
  }

  async function applyBrightness(entity) {
    setBusyEntity(entity.entity_id);
    setError("");
    setNotice("");

    try {
      const result = await setHomeLightBrightness(entity.entity_id, brightness);
      setNotice(result.success
        ? `${getFriendlyName(entity)} ajustado para ${brightness}%.`
        : `${getFriendlyName(entity)} nao confirmou ajuste de brilho.`);

      await refresh();
    } catch {
      setError(`Nao consegui ajustar brilho em ${entity.entity_id}.`);
    } finally {
      setBusyEntity("");
    }
  }

  return (
    <div className="page home-page">
      <div className="docs-header">
        <div>
          <h1>Nexus Home Assistant</h1>
          <p>Portal de controle dos dispositivos conectados ao Home Assistant.</p>
        </div>

        <div className={`home-connection ${status?.online ? "online" : "offline"}`}>
          {status?.online ? <Wifi size={18} /> : <WifiOff size={18} />}
          <span>{status?.online ? "ONLINE" : "OFFLINE"}</span>
        </div>
      </div>

      <div className="home-summary">
        <SummaryCard label="Integração" value={status?.enabled ? "Ativa" : "Inativa"} />
        <SummaryCard label="Dispositivos" value={visibleStates.length} />
        <SummaryCard label="Disponíveis" value={onlineDevices.length} />
        <SummaryCard label="Ligados" value={activeDevices.length} />
      </div>

      <div className="home-toolbar">
        <div className="home-search">
          <Search size={17} />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Buscar por nome ou entity_id"
          />
        </div>

        <select value={domainFilter} onChange={(event) => setDomainFilter(event.target.value)}>
          <option value="all">Todos</option>
          <option value="light">Luzes</option>
          <option value="switch">Switches</option>
          <option value="fan">Ventiladores</option>
          <option value="climate">Clima</option>
          <option value="cover">Cortinas</option>
          <option value="media_player">Midia</option>
          <option value="scene">Cenas</option>
        </select>

        <button type="button" className="small-action-button" onClick={refresh} disabled={loading}>
          <RefreshCw size={15} />
          {loading ? "Atualizando" : "Atualizar"}
        </button>
      </div>

      {notice && <p className="home-notice">{notice}</p>}
      {error && <p className="docs-error">{error}</p>}

      <div className="home-device-grid">
        {visibleStates.map((entity) => (
          <DeviceCard
            key={entity.entity_id}
            entity={entity}
            busy={busyEntity === entity.entity_id}
            brightness={brightness}
            setBrightness={setBrightness}
            onAction={runAction}
            onBrightness={applyBrightness}
          />
        ))}
      </div>

      {!loading && visibleStates.length === 0 && (
        <section className="panel home-empty">
          <p className="muted">Nenhum dispositivo encontrado com os filtros atuais.</p>
        </section>
      )}
    </div>
  );
}

function SummaryCard({ label, value }) {
  return (
    <div className="stat-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function DeviceCard({ entity, busy, brightness, setBrightness, onAction, onBrightness }) {
  const domain = getDomain(entity.entity_id);
  const isUnavailable = ["unavailable", "unknown"].includes(entity.state);
  const isScene = domain === "scene";
  const canControl = CONTROLLABLE_DOMAINS.includes(domain) && (!isUnavailable || isScene);
  const canDim = domain === "light" && !isUnavailable;

  return (
    <article className={`home-device ${entity.state === "on" ? "active" : ""} ${isUnavailable ? "unavailable" : ""}`}>
      <header>
        <div className="home-device-icon">{getIcon(domain)}</div>
        <div>
          <h2>{getFriendlyName(entity)}</h2>
          <p>{entity.entity_id}</p>
        </div>
      </header>

      <div className="home-device-state">
        <span>Estado</span>
        <strong>{formatState(entity.state)}</strong>
      </div>

      {canDim && (
        <div className="home-brightness">
          <label>
            <SlidersHorizontal size={15} />
            Brilho {brightness}%
          </label>
          <input
            type="range"
            min="1"
            max="100"
            value={brightness}
            onChange={(event) => setBrightness(Number(event.target.value))}
          />
        </div>
      )}

      <footer>
        <button type="button" onClick={() => onAction(entity, "on")} disabled={!canControl || busy}>
          <Power size={15} />
          {isScene ? "Ativar" : "Ligar"}
        </button>
        {!isScene && (
          <>
            <button type="button" onClick={() => onAction(entity, "off")} disabled={!canControl || busy}>
              <Power size={15} />
              Desligar
            </button>
            <button type="button" onClick={() => onAction(entity, "toggle")} disabled={!canControl || busy}>
              Alternar
            </button>
          </>
        )}
        {canDim && (
          <button type="button" onClick={() => onBrightness(entity)} disabled={busy}>
            Aplicar brilho
          </button>
        )}
      </footer>
    </article>
  );
}

function getDomain(entityId = "") {
  return entityId.split(".")[0] || "";
}

function getFriendlyName(entity) {
  return entity?.attributes?.friendly_name || entity?.entity_id || "Dispositivo";
}

function formatState(state) {
  const labels = {
    on: "Ligado",
    off: "Desligado",
    unavailable: "Indisponível",
    unknown: "Desconhecido",
    playing: "Reproduzindo",
    paused: "Pausado",
    idle: "Ocioso",
  };

  return labels[state] || state;
}

function getIcon(domain) {
  const icons = {
    light: <Lightbulb size={22} />,
    switch: <PlugZap size={22} />,
    fan: <Fan size={22} />,
    climate: <Thermometer size={22} />,
    media_player: <Tv size={22} />,
    scene: <Clapperboard size={22} />,
  };

  return icons[domain] || <PlugZap size={22} />;
}
