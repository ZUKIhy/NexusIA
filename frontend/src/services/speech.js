import { API_URL } from "./api";

export function getRecognition({ lang = "pt-BR", continuous = false, interimResults = false, onResult, onStart, onEnd, onError }) {
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

  if (!SpeechRecognition) {
    throw new Error("Seu navegador não suporta reconhecimento de voz. Teste no Chrome ou Edge.");
  }

  const recognition = new SpeechRecognition();
  recognition.lang = lang;
  recognition.continuous = continuous;
  recognition.interimResults = interimResults;

  recognition.onstart = () => onStart?.();
  recognition.onend = () => onEnd?.();
  recognition.onerror = (event) => onError?.(event);
  recognition.onresult = (event) => {
    const lastResult = event.results?.[event.results.length - 1];
    const text = lastResult?.[0]?.transcript || "";
    onResult?.(text);
  };

  return recognition;
}

export async function ensureMicrophoneAccess() {
  if (!navigator.mediaDevices?.getUserMedia) {
    throw new Error("Seu navegador não permite testar o microfone nesta página.");
  }

  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    const [track] = stream.getAudioTracks();
    const label = track?.label || "microfone padrão do navegador";

    stream.getTracks().forEach((item) => item.stop());

    return label;
  } catch (error) {
    if (error?.name === "NotAllowedError") {
      throw new Error("Permissão do microfone bloqueada. Libere o microfone para localhost no navegador.");
    }

    if (error?.name === "NotFoundError") {
      throw new Error("O navegador não encontrou nenhum microfone disponível.");
    }

    if (error?.name === "NotReadableError") {
      throw new Error("O microfone está ocupado por outro aplicativo ou não pôde ser lido pelo navegador.");
    }

    throw new Error(`Não consegui acessar o microfone: ${error?.message || error?.name || "erro desconhecido"}.`);
  }
}

export function getSpeechErrorMessage(errorCode) {
  const messages = {
    "not-allowed": "Permissão do microfone bloqueada. Libere o microfone para localhost no navegador.",
    "service-not-allowed": "O serviço de voz do navegador bloqueou a captura. Teste no Chrome ou Edge.",
    "no-speech": "Não detectei fala. Tente falar mais perto do microfone.",
    "audio-capture": "Não encontrei microfone disponível no sistema.",
    network: "Erro de rede no reconhecimento de voz do navegador.",
    aborted: "Reconhecimento de voz cancelado.",
  };

  return messages[errorCode] || `Erro no reconhecimento de voz: ${errorCode || "desconhecido"}.`;
}

export async function speak(text) {
  const playedWithElevenLabs = await speakWithElevenLabs(text);

  if (playedWithElevenLabs) return;

  speakWithBrowser(text);
}

async function speakWithElevenLabs(text) {
  if (!text) return false;

  try {
    const response = await fetch(`${API_URL}/api/voice/tts`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text }),
    });

    if (!response.ok) return false;

    const blob = await response.blob();
    const audioUrl = URL.createObjectURL(blob);
    const audio = new Audio(audioUrl);

    audio.onended = () => URL.revokeObjectURL(audioUrl);
    audio.onerror = () => URL.revokeObjectURL(audioUrl);

    await audio.play();
    return true;
  } catch {
    return false;
  }
}

function speakWithBrowser(text) {
  if (!window.speechSynthesis || !text) return;

  window.speechSynthesis.cancel();

  const run = () => {
    const voices = window.speechSynthesis.getVoices();
    const voice = pickJarvisLikeVoice(voices);
    const utterance = new SpeechSynthesisUtterance(text);

    if (voice) {
      utterance.voice = voice;
      utterance.lang = voice.lang;
    } else {
      utterance.lang = "pt-BR";
    }

    utterance.rate = 0.9;
    utterance.pitch = 0.72;
    utterance.volume = 1;

    window.speechSynthesis.speak(utterance);
  };

  if (window.speechSynthesis.getVoices().length > 0) {
    run();
    return;
  }

  window.speechSynthesis.onvoiceschanged = run;
}

function pickJarvisLikeVoice(voices) {
  if (!voices.length) return null;

  return [...voices]
    .map((voice) => ({ voice, score: scoreVoice(voice) }))
    .sort((a, b) => b.score - a.score)[0]?.voice || null;
}

function scoreVoice(voice) {
  const name = voice.name.toLowerCase();
  const lang = voice.lang.toLowerCase();
  let score = 0;

  if (lang === "pt-br") score += 35;
  if (lang.startsWith("pt")) score += 20;
  if (lang === "en-gb") score += 28;
  if (lang.startsWith("en")) score += 16;

  if (name.includes("antonio")) score += 55;
  if (name.includes("daniel")) score += 50;
  if (name.includes("guy")) score += 45;
  if (name.includes("brian")) score += 44;
  if (name.includes("george")) score += 42;
  if (name.includes("ryan")) score += 40;
  if (name.includes("arthur")) score += 38;
  if (name.includes("male")) score += 22;

  if (name.includes("natural")) score += 18;
  if (name.includes("online")) score += 12;
  if (name.includes("microsoft")) score += 8;
  if (name.includes("google")) score += 6;

  if (name.includes("female")) score -= 30;
  if (name.includes("maria")) score -= 12;
  if (name.includes("francisca")) score -= 12;

  return score;
}
