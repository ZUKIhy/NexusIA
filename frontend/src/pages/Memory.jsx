import { useEffect, useState } from "react";
import { getMemory } from "../services/api";

export default function Memory() {
  const [content, setContent] = useState("");

  useEffect(() => {
    getMemory().then(data => setContent(data.content)).catch(() => setContent("Não consegui carregar a memória. Verifique o backend."));
  }, []);

  return (
    <div className="page">
      <h1>Memória</h1>
      <p>Conteúdo salvo no Obsidian em <code>vault/03_Memory/memory.md</code>.</p>
      <pre className="markdown-preview">{content}</pre>
    </div>
  );
}
