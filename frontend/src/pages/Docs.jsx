import { useEffect, useMemo, useState } from "react";
import { Copy, FileSearch, FileText, Search, Star } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { generateChecklist, getDocFile, searchDocs, standardizeDoc } from "../services/api";

export default function Docs() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [query, setQuery] = useState(searchParams.get("q") || "");
  const [results, setResults] = useState([]);
  const [selectedFile, setSelectedFile] = useState(null);
  const [priorityOnly, setPriorityOnly] = useState(searchParams.get("priority") === "1");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const visibleResults = useMemo(() => {
    return priorityOnly ? results.filter((item) => item.isPrioritySource) : results;
  }, [priorityOnly, results]);

  useEffect(() => {
    const initialQuery = searchParams.get("q");
    const initialPath = searchParams.get("path");

    if (initialQuery) {
      runSearch(initialQuery, { syncUrl: false });
    }

    if (initialPath) {
      openFile(initialPath);
    }
  }, []);

  async function handleSearch(event) {
    event?.preventDefault();
    await runSearch(query);
  }

  async function runSearch(term, options = {}) {
    const cleanTerm = term.trim();
    if (!cleanTerm || loading) return;

    setQuery(cleanTerm);
    setLoading(true);
    setError("");
    setSelectedFile(null);

    if (options.syncUrl !== false) {
      setSearchParams({ q: cleanTerm, ...(priorityOnly ? { priority: "1" } : {}) });
    }

    try {
      const data = await searchDocs(cleanTerm);
      setResults(data.results || []);
    } catch {
      setResults([]);
      setError("Erro ao buscar documentos. Verifique se o backend está rodando.");
    } finally {
      setLoading(false);
    }
  }

  async function openFile(path) {
    if (!path) return;

    setError("");

    try {
      const data = await getDocFile(path);
      setSelectedFile(data);
      setSearchParams((params) => {
        const next = new URLSearchParams(params);
        next.set("path", path);
        if (query.trim()) next.set("q", query.trim());
        if (priorityOnly) next.set("priority", "1");
        return next;
      });
    } catch {
      setError("Não consegui abrir o arquivo selecionado.");
    }
  }

  async function copyPath(path) {
    await navigator.clipboard?.writeText(path);
  }

  async function standardizeSelectedFile() {
    if (!selectedFile?.path) {
      setError("Abra um arquivo primeiro.");
      return;
    }

    try {
      const data = await standardizeDoc(selectedFile.path);
      setError("");
      alert(`Procedimento padronizado criado em: ${data.path}${data.usedOllama ? " usando Ollama" : ""}`);
    } catch {
      setError("Nao consegui criar o procedimento padronizado.");
    }
  }

  async function generateChecklistForSelectedFile() {
    if (!selectedFile?.path) {
      setError("Abra um arquivo primeiro.");
      return;
    }

    try {
      const data = await generateChecklist(selectedFile.path);
      setError("");
      alert(`Checklist criado em: ${data.path}${data.usedOllama ? " usando Ollama" : ""}`);
    } catch {
      setError("Nao consegui gerar o checklist.");
    }
  }

  function togglePriorityOnly() {
    const next = !priorityOnly;
    setPriorityOnly(next);
    setSearchParams((params) => {
      const updated = new URLSearchParams(params);
      if (next) updated.set("priority", "1");
      else updated.delete("priority");
      return updated;
    });
  }

  return (
    <div className="page docs-page">
      <div className="docs-header">
        <div>
          <h1>Docs</h1>
          <p>Pesquise procedimentos, comandos, checklists e notas importadas do Notion.</p>
        </div>

        <FileSearch size={34} aria-hidden="true" />
      </div>

      <form className="docs-search" onSubmit={handleSearch}>
        <Search size={18} aria-hidden="true" />
        <input
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Ex: limpeza, journal, Shift BI, rede, iostat..."
        />
        <button type="submit" disabled={loading || !query.trim()}>
          {loading ? "Buscando..." : "Buscar"}
        </button>
      </form>

      <div className="docs-toolbar">
        <button type="button" className={priorityOnly ? "active" : ""} onClick={togglePriorityOnly}>
          <Star size={15} />
          Priorizar revisados
        </button>
        <span>{visibleResults.length} de {results.length} resultado(s)</span>
      </div>

      {error && <p className="docs-error">{error}</p>}

      <div className="docs-layout">
        <section className="panel docs-results">
          <header>
            <strong>Resultados</strong>
            <span>{visibleResults.length} encontrado(s)</span>
          </header>

          {visibleResults.length === 0 && (
            <p className="muted">Faça uma busca para ver documentos do Obsidian.</p>
          )}

          <div className="docs-result-list">
            {visibleResults.map((item, index) => {
              const path = item.path || item.filePath || item.relativePath || "";
              const title = item.title || path.split(/[\\/]/).pop() || `Resultado ${index + 1}`;
              const folder = item.folder || path.split(/[\\/]/).slice(0, -1).join("/");
              const snippet = item.snippet || item.content || String(item);

              return (
                <article key={`${path}-${index}`} className={`doc-result-card ${selectedFile?.path === path ? "active" : ""}`}>
                  <button
                    className="doc-result"
                    onClick={() => openFile(path)}
                    type="button"
                    disabled={!path}
                  >
                    <FileText size={18} aria-hidden="true" />
                    <div>
                      <strong>{title}</strong>
                      <small>{folder}</small>
                      <p>{String(snippet).replace(/\s+/g, " ").slice(0, 320)}</p>
                    </div>
                  </button>
                  <div className="doc-result-actions">
                    {item.isPrioritySource && <span>Revisado</span>}
                    <button type="button" onClick={() => copyPath(path)}>
                      <Copy size={14} />
                      Copiar caminho
                    </button>
                  </div>
                </article>
              );
            })}
          </div>
        </section>

        <section className="panel docs-preview">
          <header>
            <strong>Pré-visualização</strong>
            <span>{selectedFile?.path || "Nenhum arquivo aberto"}</span>
            <div className="preview-actions">
              <button type="button" className="small-action-button" onClick={standardizeSelectedFile}>
                Padronizar
              </button>
              <button type="button" className="small-action-button" onClick={generateChecklistForSelectedFile}>
                Gerar Checklist
              </button>
            </div>
          </header>

          {!selectedFile && (
            <p className="muted">Clique em um resultado para abrir o conteúdo.</p>
          )}

          {selectedFile && (
            <pre className="markdown-preview">
              {selectedFile.content || selectedFile.text || JSON.stringify(selectedFile, null, 2)}
            </pre>
          )}
        </section>
      </div>
    </div>
  );
}
