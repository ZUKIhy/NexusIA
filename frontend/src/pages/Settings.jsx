import { useEffect, useState } from "react";
import { Volume2 } from "lucide-react";
import { getStatus, getVoiceStatus } from "../services/api";
import { speak } from "../services/speech";

export default function SettingsPage() {
  const [status, setStatus] = useState(null);
  const [voiceStatus, setVoiceStatus] = useState(null);
  const [testingVoice, setTestingVoice] = useState(false);

  useEffect(() => {
    getStatus().then(setStatus).catch(() => setStatus(null));
    getVoiceStatus().then(setVoiceStatus).catch(() => setVoiceStatus(null));
  }, []);

  async function testVoice() {
    setTestingVoice(true);
    await speak("Sistema Nexus online. Voz neural configurada e pronta para operação.");
    setTimeout(() => setTestingVoice(false), 1200);
  }

  return (
    <div className="page">
      <h1>Settings</h1>
      <div className="settings-grid">
        <section className="panel settings-panel">
          <header>
            <div>
              <strong>Voz</strong>
              <span>ElevenLabs com fallback local</span>
            </div>
            <Volume2 size={20} />
          </header>

          <p>Provider: {voiceStatus?.provider || "ElevenLabs"}</p>
          <p>Status: {voiceStatus?.configured ? "Configurado" : "Não configurado"}</p>
          <p>Voice ID: <code>{voiceStatus?.voiceId || "não definido"}</code></p>
          <button className="settings-action" type="button" onClick={testVoice} disabled={testingVoice}>
            {testingVoice ? "Testando..." : "Testar voz"}
          </button>
        </section>

        <section className="panel settings-panel">
          <h2>Idioma</h2>
          <p>Atual: Português Brasil.</p>
          <p>Entrada de voz: `pt-BR`.</p>
          <p>Saída de voz: ElevenLabs quando configurado.</p>
        </section>

        <section className="panel settings-panel">
          <h2>Obsidian</h2>
          <p>Vault ativo:</p>
          <code>{status?.vault || "não carregado"}</code>
          <p>Fontes priorizadas: Procedimentos-Padronizados.</p>
        </section>
      </div>
    </div>
  );
}
