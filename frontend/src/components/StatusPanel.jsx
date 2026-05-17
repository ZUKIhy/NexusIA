export default function StatusPanel({ status }) {
  const label = {
    online: "Online",
    listening: "Ouvindo",
    thinking: "Pensando",
    speaking: "Falando",
    error: "Erro",
  }[status] || status;

  return (
    <section className="panel status-panel">
      <strong>Status</strong>
      <div className={`status-badge ${status}`}>{label}</div>
      <p>Estados do sistema em tempo real: online, ouvindo, pensando e falando.</p>
    </section>
  );
}
