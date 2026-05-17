import { useEffect, useState } from "react";
import { getConversationLogs } from "../services/api";

export default function Logs() {
  const [content, setContent] = useState("");

  useEffect(() => {
    getConversationLogs().then(data => setContent(data.content)).catch(() => setContent("Não consegui carregar logs. Verifique o backend."));
  }, []);

  return (
    <div className="page">
      <h1>Logs</h1>
      <p>Histórico de conversas salvo no Obsidian.</p>
      <pre className="markdown-preview">{content}</pre>
    </div>
  );
}
