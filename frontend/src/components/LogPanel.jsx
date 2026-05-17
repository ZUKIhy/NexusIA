export default function LogPanel({ logs = [] }) {
  return (
    <section className="panel log-panel">
      <header>
        <strong>System Logs</strong>
        <span>Eventos em tempo real</span>
      </header>

      <div className="log-list">
        {logs.length === 0 && <p className="muted">Nenhum log ainda.</p>}
        {logs.map((log, index) => (
          <div key={index} className="log-item">
            <small>{new Date().toLocaleTimeString()}</small>
            <span>{log}</span>
          </div>
        ))}
      </div>
    </section>
  );
}
