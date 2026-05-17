import { useEffect, useState } from "react";
import { getComputerStatus, getStatus, getSystemHealth } from "../services/api";

export default function System() {
  const [status, setStatus] = useState(null);
  const [computer, setComputer] = useState(null);
  const [health, setHealth] = useState(null);

  useEffect(() => {
    getStatus().then(setStatus).catch(() => setStatus({ status: "offline", error: "Backend não encontrado" }));
    getComputerStatus().then(setComputer).catch(() => setComputer(null));
    getSystemHealth().then(setHealth).catch(() => setHealth(null));
  }, []);

  return (
    <div className="page system-page">
      <h1>System</h1>
      <p>Status do backend, integrações e controle local do computador.</p>

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
          <p>Digitação na janela ativa: {computer?.canPasteIntoActiveWindow ? "ativo" : "inativo"}</p>
        </section>

        <section className="panel settings-panel">
          <h2>Comandos</h2>
          <p><code>Nexus, abra o Chrome</code></p>
          <p><code>Nexus, abra a pasta Downloads</code></p>
          <p><code>Nexus, pesquise no navegador GPO Edge</code></p>
          <p><code>Nexus, digite texto de teste</code></p>
        </section>
      </div>
    </div>
  );
}
