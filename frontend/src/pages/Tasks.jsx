import { useEffect, useState } from "react";
import { getTasks } from "../services/api";

export default function Tasks() {
  const [content, setContent] = useState("");

  useEffect(() => {
    getTasks().then(data => setContent(data.content)).catch(() => setContent("Não consegui carregar tarefas. Verifique o backend."));
  }, []);

  return (
    <div className="page">
      <h1>Tarefas</h1>
      <p>Conteúdo salvo no Obsidian em <code>vault/05_Tasks/tasks.md</code>.</p>
      <pre className="markdown-preview">{content}</pre>
    </div>
  );
}
