import { motion } from "framer-motion";

const orbitNodes = [
  { className: "n1", size: 11, duration: 18, delay: 0 },
  { className: "n3", size: 8, duration: 24, delay: 0.9 },
  { className: "n5", size: 12, duration: 28, delay: 0.2 },
  { className: "n8", size: 9, duration: 32, delay: 1.1 },
  { className: "n12", size: 11, duration: 22, delay: 0.8 },
];

function statusLabel(status) {
  if (status === "listening") return "VOICE ON";
  if (status === "thinking") return "PROCESSING";
  if (status === "speaking") return "OUTPUT";
  if (status === "error") return "FAULT";
  return "CORE ONLINE";
}

function statusPulse(status) {
  if (status === "thinking") return [1, 1.18, 0.96, 1.12, 1];
  if (status === "listening") return [1, 1.1, 1];
  if (status === "speaking") return [1, 1.14, 1];
  if (status === "error") return [1, 0.92, 1.05, 0.95, 1];
  return [1, 1.04, 1];
}

export default function NexusCore({ status = "online" }) {
  const isThinking = status === "thinking";
  const isSpeaking = status === "speaking";

  return (
    <div className={`core-wrapper reactor-core ${status}`}>
      <div className="reactor-backdrop" />
      <div className="reactor-depth-field" />
      <motion.div
        className="reactor-shadow"
        animate={{ opacity: isThinking ? [0.42, 0.68, 0.42] : [0.34, 0.46, 0.34] }}
        transition={{ repeat: Infinity, duration: isThinking ? 2.6 : 7.5, ease: "easeInOut" }}
      />

      <motion.div
        className="reactor-outer-ring ring-a"
        animate={{ rotate: 360 }}
        transition={{ repeat: Infinity, duration: isThinking ? 24 : 46, ease: "linear" }}
      />
      <motion.div
        className="reactor-outer-ring ring-b"
        animate={{ rotate: -360 }}
        transition={{ repeat: Infinity, duration: isThinking ? 30 : 64, ease: "linear" }}
      />
      <div className="reactor-outer-ring ring-c" />
      <motion.div
        className="reactor-outer-ring ring-d"
        animate={{ rotate: isThinking ? 360 : -360 }}
        transition={{ repeat: Infinity, duration: isThinking ? 18 : 52, ease: "linear" }}
      />

      <div className="reactor-orbits" aria-hidden="true">
        {orbitNodes.map((node) => (
          <motion.span
            key={node.className}
            className={`reactor-node ${node.className}`}
            style={{ "--node-size": `${node.size}px` }}
            animate={{ rotate: 360 }}
            transition={{ repeat: Infinity, duration: isThinking ? node.duration * 0.75 : node.duration, delay: node.delay, ease: "linear" }}
          />
        ))}
      </div>

      <motion.div
        className="reactor-cone"
        animate={{
          opacity: isThinking ? [0.35, 0.72, 0.45] : [0.22, 0.42, 0.22],
          scaleY: isThinking ? [0.94, 1.04, 0.96] : [1, 1.02, 1],
        }}
        transition={{ repeat: Infinity, duration: isThinking ? 2 : 5, ease: "easeInOut" }}
      />

      <motion.div
        className="reactor-core-light"
        animate={{ scale: statusPulse(status), opacity: status === "error" ? [0.7, 1, 0.75] : [0.92, 1, 0.94] }}
        transition={{ repeat: Infinity, duration: isThinking ? 2.2 : 4.5, ease: "easeInOut" }}
      >
        <span />
        <strong>NEXUS</strong>
      </motion.div>

      <motion.div
        className="reactor-voice-wave wave-a"
        animate={{
          opacity: isSpeaking ? [0.2, 0.65, 0.2] : [0.08, 0.18, 0.08],
          scale: isSpeaking ? [0.92, 1.1, 0.96] : [1, 1.03, 1],
        }}
        transition={{ repeat: Infinity, duration: isSpeaking ? 1.6 : 5.8, ease: "easeInOut" }}
      />
      <motion.div
        className="reactor-voice-wave wave-b"
        animate={{
          opacity: isSpeaking ? [0.12, 0.42, 0.12] : [0.04, 0.14, 0.04],
          scale: isSpeaking ? [0.98, 1.18, 1] : [1, 1.05, 1],
        }}
        transition={{ repeat: Infinity, duration: isSpeaking ? 2.1 : 7.2, ease: "easeInOut" }}
      />

      <div className="reactor-scan scan-x" />
      <div className="reactor-scan scan-y" />

      <div className="reactor-status">
        <span />
        <strong>{statusLabel(status)}</strong>
      </div>
    </div>
  );
}
