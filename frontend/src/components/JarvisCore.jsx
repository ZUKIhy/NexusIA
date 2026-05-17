import { motion } from "framer-motion";

const orbitNodes = [
  { className: "n1", size: 12, duration: 10, delay: 0 },
  { className: "n3", size: 10, duration: 18, delay: 0.9 },
  { className: "n5", size: 14, duration: 21, delay: 0.2 },
  { className: "n6", size: 8, duration: 16, delay: 1.6 },
  { className: "n8", size: 11, duration: 24, delay: 1.1 },
  { className: "n10", size: 9, duration: 15, delay: 0.5 },
  { className: "n12", size: 13, duration: 17, delay: 0.8 },
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

  return (
    <div className={`core-wrapper reactor-core ${status}`}>
      <div className="reactor-backdrop" />
      <motion.div
        className="reactor-shadow"
        animate={{ opacity: isThinking ? [0.45, 0.82, 0.45] : [0.3, 0.52, 0.3] }}
        transition={{ repeat: Infinity, duration: isThinking ? 1.8 : 5.6, ease: "easeInOut" }}
      />

      <motion.div
        className="reactor-outer-ring ring-a"
        animate={{ rotate: 360 }}
        transition={{ repeat: Infinity, duration: isThinking ? 14 : 28, ease: "linear" }}
      />
      <motion.div
        className="reactor-outer-ring ring-b"
        animate={{ rotate: -360 }}
        transition={{ repeat: Infinity, duration: isThinking ? 18 : 40, ease: "linear" }}
      />
      <motion.div
        className="reactor-outer-ring ring-c"
        animate={{ rotate: 360 }}
        transition={{ repeat: Infinity, duration: isThinking ? 24 : 58, ease: "linear" }}
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
        animate={{ scale: statusPulse(status), opacity: status === "error" ? [0.6, 1, 0.65] : [0.9, 1, 0.92] }}
        transition={{ repeat: Infinity, duration: isThinking ? 1.8 : 3.6, ease: "easeInOut" }}
      >
        <span />
      </motion.div>

      <div className="reactor-scan scan-x" />
      <div className="reactor-scan scan-y" />

      <div className="reactor-status">
        <span />
        <strong>{statusLabel(status)}</strong>
      </div>
    </div>
  );
}
