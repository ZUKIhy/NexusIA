import { useEffect, useMemo, useState } from "react";
import { ClipboardCheck, FileText, Filter } from "lucide-react";
import { generateChecklist, getProcedures, sendMessage, standardizeDoc } from "../services/api";

const filters = [
  "Todos",
  "Padronizados",
  "Pendentes de revisão",
  "Limpeza",
  "Rede",
  "Shift BI",
  "Shift LIS",
  "Servidores",
  "Banco de Dados",
  "Checklists",
];

export default function Procedures() {
  const [data, setData] = useState(null);
  const [activeFilter, setActiveFilter] = useState("Todos");
  const [error, setError] = useState("");

  useEffect(() => {
    loadProcedures();
  }, []);

  async function loadProcedures() {
    try {
      setData(await getProcedures());
      setError("");
    } catch {
      setError("Não consegui carregar os procedimentos.");
    }
  }

  const items = useMemo(() => {
    const all = data?.items || [];

    if (activeFilter === "Todos") return all;
    if (activeFilter === "Padronizados") return all.filter((item) => item.standardized);
    if (activeFilter === "Pendentes de revisão") return all.filter((item) => item.pendingReview);

    return all.filter((item) => item.category === activeFilter);
  }, [activeFilter, data]);

  async function standardize(path) {
    try {
      const result = await standardizeDoc(path);
      alert(`Procedimento criado em: ${result.path}`);
      await loadProcedures();
    } catch {
      alert("Não consegui padronizar este procedimento.");
    }
  }

  async function checklist(path) {
    try {
      const result = await generateChecklist(path);
      alert(`Checklist criado em: ${result.path}`);
      await loadProcedures();
    } catch {
      alert("Não consegui gerar checklist.");
    }
  }

  async function useInOperation(item) {
    try {
      await sendMessage(`Nexus, iniciar atendimento sobre ${item.title}`);
      await sendMessage(`Nexus, procure procedimento sobre ${item.title}`);
      alert("Procedimento enviado para o modo operação.");
    } catch {
      alert("Não consegui usar este procedimento no modo operação.");
    }
  }

  function openInDocs(path) {
    window.location.href = `/docs?path=${encodeURIComponent(path)}`;
  }

  return (
    <div className="page procedures-page">
      <div className="docs-header">
        <div>
          <h1>Procedimentos</h1>
          <p>Base operacional revisada, importada e priorizada para uso no modo operação.</p>
        </div>
        <ClipboardCheck size={34} aria-hidden="true" />
      </div>

      <div className="procedure-stats">
        <div>
          <strong>{data?.total || 0}</strong>
          <span>Total</span>
        </div>
        <div>
          <strong>{data?.standardized || 0}</strong>
          <span>Padronizados</span>
        </div>
        <div>
          <strong>{data?.pendingReview || 0}</strong>
          <span>Pendentes</span>
        </div>
      </div>

      <div className="procedure-filters">
        <Filter size={16} />
        {filters.map((filter) => (
          <button
            key={filter}
            type="button"
            className={filter === activeFilter ? "active" : ""}
            onClick={() => setActiveFilter(filter)}
          >
            {filter}
          </button>
        ))}
      </div>

      {error && <p className="docs-error">{error}</p>}

      <div className="procedure-grid">
        {items.map((item) => (
          <article key={item.path} className="procedure-card">
            <div>
              <FileText size={18} />
              <span>{item.category}</span>
            </div>
            <h2>{item.title}</h2>
            <p>{String(item.preview || "").replace(/\s+/g, " ").slice(0, 220)}</p>
            <small>{item.path}</small>
            <dl className="procedure-meta">
              <div><dt>Tipo</dt><dd>{item.type || "nota"}</dd></div>
              <div><dt>Status</dt><dd>{item.status || "sem status"}</dd></div>
              <div><dt>Última alteração</dt><dd>{new Date(item.modifiedAt).toLocaleString("pt-BR")}</dd></div>
            </dl>
            <footer>
              {item.standardized && <span>Padronizado</span>}
              {item.pendingReview && <span>Revisar</span>}
            </footer>
            <div className="procedure-actions">
              <button type="button" onClick={() => openInDocs(item.path)}>Abrir</button>
              <button type="button" onClick={() => standardize(item.path)}>Padronizar</button>
              <button type="button" onClick={() => checklist(item.path)}>Checklist</button>
              <button type="button" onClick={() => useInOperation(item)}>Usar no modo operação</button>
            </div>
          </article>
        ))}
      </div>
    </div>
  );
}
