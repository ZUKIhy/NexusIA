import { useMemo, useRef, useState } from "react";
import { ExternalLink, Mic, Radio, Send } from "lucide-react";
import { Link } from "react-router-dom";
import { ensureMicrophoneAccess, getRecognition, getSpeechErrorMessage, speak } from "../services/speech";
import { sendMessage } from "../services/api";

const WAKE_WORD = "nexus";

export default function ChatPanel({ onStatus, onLog }) {
  const [input, setInput] = useState("");
  const [messages, setMessages] = useState([{ role: "assistant", content: "Olá, Gabriel. Estou online." }]);
  const [isListening, setIsListening] = useState(false);
  const [alwaysListening, setAlwaysListening] = useState(false);
  const [awaitingCommand, setAwaitingCommand] = useState(false);
  const [voiceMessage, setVoiceMessage] = useState("");
  const recognitionRef = useRef(null);
  const alwaysListeningRef = useRef(false);
  const awaitingCommandRef = useRef(false);
  const lastTranscriptRef = useRef("");

  const voiceSupported = useMemo(() => {
    return Boolean(window.SpeechRecognition || window.webkitSpeechRecognition);
  }, []);

  async function submit(text = input) {
    const message = text.trim();
    if (!message) return;

    setMessages((prev) => [...prev, { role: "user", content: message }]);
    setInput("");
    setVoiceMessage("");
    setAwaitingState(false);
    onStatus?.("thinking");

    try {
      const data = await sendMessage(message);
      setMessages((prev) => [
        ...prev,
        {
          role: "assistant",
          content: data.answer,
          intent: data.intent,
          docsQuery: data.docsQuery,
          docsResults: data.docsResults || [],
        },
      ]);
      onStatus?.("speaking");
      speak(getSpeakableAnswer(data));
      onLog?.(`Resposta gerada. Intent: ${data.intent}`);
      setTimeout(() => onStatus?.(alwaysListeningRef.current ? "listening" : "online"), 1200);
    } catch {
      const errorText = "Não consegui conectar ao backend. Verifique se o .NET está rodando.";
      setMessages((prev) => [...prev, { role: "assistant", content: errorText }]);
      onStatus?.("error");
      onLog?.(errorText);
    }
  }

  async function startVoice() {
    if (isListening) {
      stopRecognition();
      return;
    }

    await startRecognition({ continuous: false, wakeMode: false });
  }

  async function toggleAlwaysListening() {
    if (alwaysListeningRef.current) {
      setAlwaysListeningState(false);
      setAwaitingState(false);
      stopRecognition();
      setVoiceMessage("Modo sempre ouvindo desativado.");
      onStatus?.("online");
      return;
    }

    setAlwaysListeningState(true);
    await startRecognition({ continuous: true, wakeMode: true });
  }

  async function startRecognition({ continuous, wakeMode }) {
    try {
      setVoiceMessage("Verificando microfone...");
      const microphoneLabel = await ensureMicrophoneAccess();
      setVoiceMessage(`Microfone autorizado: ${microphoneLabel}`);

      const recognition = getRecognition({
        lang: "pt-BR",
        continuous,
        onStart: () => {
          setIsListening(true);
          setVoiceMessage(wakeMode ? "Sempre ouvindo: diga Nexus." : "Ouvindo...");
          onStatus?.("listening");
          onLog?.(wakeMode ? "Modo sempre ouvindo ativado." : "Microfone ativado.");
        },
        onResult: (text) => {
          const transcript = text.trim();
          if (!transcript || transcript === lastTranscriptRef.current) return;

          lastTranscriptRef.current = transcript;
          wakeMode ? handleWakeTranscript(transcript) : handleOneShotTranscript(transcript);
        },
        onEnd: () => {
          setIsListening(false);

          if (alwaysListeningRef.current) {
            window.setTimeout(() => startRecognition({ continuous: true, wakeMode: true }), 350);
            return;
          }

          onStatus?.("online");
          onLog?.("Microfone finalizado.");
        },
        onError: (event) => {
          const message = getSpeechErrorMessage(event?.error);
          setIsListening(false);
          setVoiceMessage(message);
          onStatus?.("error");
          onLog?.(message);
        },
      });

      recognitionRef.current = recognition;
      recognition.start();
    } catch (error) {
      setAlwaysListeningState(false);
      setVoiceMessage(error.message);
      onLog?.(error.message);
      alert(error.message);
    }
  }

  function handleOneShotTranscript(transcript) {
    setInput(transcript);
    setVoiceMessage(`Ouvi: ${transcript}`);
    onLog?.(`Voz detectada: ${transcript}`);
    submit(transcript);
  }

  function handleWakeTranscript(transcript) {
    const normalized = transcript.toLowerCase();
    setVoiceMessage(`Ouvi: ${transcript}`);
    onLog?.(`Voz detectada: ${transcript}`);

    if (awaitingCommandRef.current) {
      submit(transcript);
      return;
    }

    if (!normalized.includes(WAKE_WORD)) return;

    const command = extractCommandAfterWakeWord(transcript);

    if (command) {
      submit(command);
      return;
    }

    setAwaitingState(true);
    const prompt = "Sim, Gabriel. O que você precisa?";
    setMessages((prev) => [...prev, { role: "assistant", content: prompt }]);
    setVoiceMessage("Nexus ativado. Aguardando comando...");
    speak(prompt);
  }

  function stopRecognition() {
    recognitionRef.current?.stop();
    recognitionRef.current = null;
    setIsListening(false);
  }

  function setAlwaysListeningState(value) {
    alwaysListeningRef.current = value;
    setAlwaysListening(value);
  }

  function setAwaitingState(value) {
    awaitingCommandRef.current = value;
    setAwaitingCommand(value);
  }

  return (
    <section className="panel chat-panel">
      <header>
        <div>
          <strong>Assistant Chat</strong>
          <span>Comando por texto, voz e wake word</span>
        </div>
      </header>

      <div className="messages">
        {messages.map((msg, index) => (
          <div key={index} className={`message ${msg.role}`}>
            <small>{msg.role === "user" ? "Você" : "Nexus"}</small>
            <p>{msg.content}</p>
            {msg.intent === "search_docs" && (
              <div className="message-actions">
                <Link to={`/docs?q=${encodeURIComponent(msg.docsQuery || "")}`}>
                  <ExternalLink size={14} />
                  Abrir busca no Docs
                </Link>
                {msg.docsResults?.[0]?.path && (
                  <Link to={`/docs?q=${encodeURIComponent(msg.docsQuery || "")}&path=${encodeURIComponent(msg.docsResults[0].path)}`}>
                    <ExternalLink size={14} />
                    Abrir mais relevante
                  </Link>
                )}
              </div>
            )}
          </div>
        ))}
      </div>

      {voiceMessage && <p className={`voice-status ${awaitingCommand ? "armed" : ""}`}>{voiceMessage}</p>}
      {!voiceSupported && <p className="voice-status error">Voz disponível apenas em navegadores com SpeechRecognition, como Chrome ou Edge.</p>}

      <div className="wake-controls">
        <button
          onClick={toggleAlwaysListening}
          className={`wake-button ${alwaysListening ? "active" : ""}`}
          type="button"
        >
          <Radio size={16} />
          {alwaysListening ? "Sempre ouvindo ativo" : "Ativar sempre ouvindo"}
        </button>
        {awaitingCommand && <span>Aguardando seu comando</span>}
      </div>

      <div className="chat-input">
        <button
          onClick={startVoice}
          className={`icon-button ${isListening && !alwaysListening ? "listening" : ""}`}
          title={isListening && !alwaysListening ? "Parar de ouvir" : "Falar uma vez"}
          type="button"
          disabled={alwaysListening}
        >
          <Mic size={18} />
        </button>
        <input
          value={input}
          onChange={(event) => setInput(event.target.value)}
          onKeyDown={(event) => event.key === "Enter" && submit()}
          placeholder="Digite ou fale um comando para o Nexus..."
        />
        <button onClick={() => submit()} className="send-button" type="button">
          <Send size={18} />
        </button>
      </div>
    </section>
  );
}

function extractCommandAfterWakeWord(transcript) {
  const index = transcript.toLowerCase().indexOf(WAKE_WORD);
  if (index < 0) return "";

  return transcript
    .slice(index + WAKE_WORD.length)
    .replace(/^(,|\s|por favor|preciso que|quero que|pode)+/i, "")
    .trim();
}

function getSpeakableAnswer(data) {
  if (data.intent === "search_docs") {
    const first = data.docsResults?.[0]?.title;
    return first
      ? `Encontrei documentos no Obsidian. O mais relevante parece ser: ${first}.`
      : "Não encontrei documentos relacionados no Obsidian.";
  }

  return data.answer;
}
