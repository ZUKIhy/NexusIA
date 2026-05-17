import { motion } from "framer-motion";

const nodes = [
  { id: "a", x: 78, y: 142, r: 2.8, dx: 8, dy: -7, d: 5.8 },
  { id: "b", x: 118, y: 88, r: 2.2, dx: -9, dy: 6, d: 6.4 },
  { id: "c", x: 168, y: 54, r: 3.1, dx: 7, dy: 8, d: 7.1 },
  { id: "d", x: 226, y: 70, r: 2.5, dx: -8, dy: -8, d: 6.2 },
  { id: "e", x: 282, y: 54, r: 2.4, dx: 10, dy: 9, d: 7.6 },
  { id: "f", x: 342, y: 94, r: 3.2, dx: -7, dy: 7, d: 6.8 },
  { id: "g", x: 390, y: 148, r: 2.5, dx: 9, dy: -5, d: 7.4 },
  { id: "h", x: 58, y: 224, r: 2.3, dx: 8, dy: 8, d: 7.2 },
  { id: "i", x: 116, y: 258, r: 3.3, dx: -7, dy: -9, d: 6.5 },
  { id: "j", x: 176, y: 206, r: 2.6, dx: 8, dy: 6, d: 6.9 },
  { id: "k", x: 232, y: 222, r: 3.8, dx: -8, dy: 8, d: 5.9 },
  { id: "l", x: 286, y: 188, r: 2.7, dx: 7, dy: -7, d: 6.6 },
  { id: "m", x: 350, y: 244, r: 2.4, dx: -10, dy: 6, d: 7.8 },
  { id: "n", x: 408, y: 268, r: 3.1, dx: 8, dy: -8, d: 6.1 },
  { id: "o", x: 96, y: 340, r: 2.9, dx: -7, dy: 7, d: 7.5 },
  { id: "p", x: 158, y: 384, r: 2.5, dx: 9, dy: -6, d: 6.7 },
  { id: "q", x: 226, y: 356, r: 3.2, dx: -7, dy: -8, d: 5.7 },
  { id: "r", x: 292, y: 382, r: 2.3, dx: 8, dy: 7, d: 7.3 },
  { id: "s", x: 366, y: 346, r: 3.4, dx: -8, dy: -7, d: 6.3 },
  { id: "t", x: 430, y: 330, r: 2.2, dx: 7, dy: 8, d: 7.7 },
];

const links = [
  ["a", "b"], ["a", "h"], ["a", "j"], ["b", "c"], ["b", "j"], ["c", "d"],
  ["c", "j"], ["d", "e"], ["d", "l"], ["e", "f"], ["e", "l"], ["f", "g"],
  ["f", "m"], ["g", "n"], ["h", "i"], ["h", "o"], ["i", "j"], ["i", "o"],
  ["j", "k"], ["j", "q"], ["k", "l"], ["k", "q"], ["l", "m"], ["l", "s"],
  ["m", "n"], ["m", "s"], ["n", "t"], ["o", "p"], ["o", "q"], ["p", "q"],
  ["q", "r"], ["r", "s"], ["s", "t"], ["b", "h"], ["c", "k"], ["f", "l"],
  ["i", "p"], ["k", "s"], ["q", "s"],
];

const thoughtPaths = [
  "M84 150 C138 92 220 86 286 138 S380 184 430 270",
  "M112 330 C162 250 226 240 292 188 S366 116 414 148",
  "M126 92 C202 156 214 236 178 310 S238 392 356 350",
  "M62 226 C142 204 210 250 270 316 S358 386 436 326",
  "M168 56 C210 116 260 150 338 94 S398 122 390 148",
];

const nodeMap = Object.fromEntries(nodes.map((node) => [node.id, node]));

function pulseFor(status) {
  if (status === "listening") return 1.18;
  if (status === "thinking") return 1.26;
  if (status === "speaking") return 1.22;
  if (status === "error") return 0.92;
  return 1.08;
}

function statusLabel(status) {
  if (status === "listening") return "OUVINDO";
  if (status === "thinking") return "PENSANDO";
  if (status === "speaking") return "RESPONDENDO";
  if (status === "error") return "ERRO";
  return "NEURAL ONLINE";
}

export default function NexusCore({ status = "online" }) {
  const pulse = pulseFor(status);

  return (
    <div className={`core-wrapper neural-core ${status}`}>
      <motion.div
        className="neural-aura"
        animate={{ scale: [1, pulse, 1], opacity: [0.58, 0.88, 0.58] }}
        transition={{ repeat: Infinity, duration: status === "thinking" ? 1.4 : 3.2, ease: "easeInOut" }}
      />

      <svg className="brain-field" viewBox="0 0 500 440" role="img" aria-label="Nexus neural network">
        <defs>
          <filter id="brain-glow" x="-40%" y="-40%" width="180%" height="180%">
            <feGaussianBlur stdDeviation="5" result="blur" />
            <feColorMatrix
              in="blur"
              type="matrix"
              values="0 0 0 0 0.18 0 0 0 0 0.7 0 0 0 0 1 0 0 0 0.85 0"
              result="glow"
            />
            <feMerge>
              <feMergeNode in="glow" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>

          <linearGradient id="brain-stroke" x1="80" y1="60" x2="420" y2="380" gradientUnits="userSpaceOnUse">
            <stop stopColor="#c7f3ff" />
            <stop offset="0.52" stopColor="#39b9ff" />
            <stop offset="1" stopColor="#ff9c45" />
          </linearGradient>

          <radialGradient id="brain-fill" cx="50%" cy="48%" r="58%">
            <stop stopColor="rgba(189, 241, 255, 0.48)" />
            <stop offset="0.52" stopColor="rgba(30, 155, 255, 0.18)" />
            <stop offset="1" stopColor="rgba(5, 16, 30, 0.02)" />
          </radialGradient>
        </defs>

        <motion.g
          className="brain-silhouette"
          filter="url(#brain-glow)"
          animate={{ opacity: [0.62, 0.88, 0.62], scale: [1, 1.015, 1] }}
          transition={{ repeat: Infinity, duration: 4.8, ease: "easeInOut" }}
          style={{ transformOrigin: "250px 220px" }}
        >
          <path d="M124 214c-20-18-28-50-11-78 15-25 42-31 66-25 15-31 55-49 92-36 23 8 39 24 48 43 31-5 60 10 74 36 14 26 8 60-13 78 15 30 8 67-18 88-23 19-57 20-83 5-22 24-60 30-90 12-24-14-37-38-38-63-12-2-20-8-27-14-13-12-15-30 0-46Z" />
          <path d="M153 198c14-24 39-31 63-18 8-30 42-47 70-31 18 10 24 27 23 45 22-8 48 0 61 21" />
          <path d="M170 255c20 8 40 4 55-10 4 24 28 41 53 34 13-4 24-13 31-25 18 10 39 9 56-5" />
          <path d="M198 136c12 22 32 28 54 20 19-7 36-2 49 12" />
          <path d="M214 324c8-23 27-37 50-35 23 2 41-7 52-26" />
          <path d="M132 223c33-5 62 6 86 32" />
        </motion.g>

        <g className="network-lines">
          {links.map(([fromId, toId], index) => {
            const from = nodeMap[fromId];
            const to = nodeMap[toId];

            return (
              <motion.line
                key={`${fromId}-${toId}`}
                x1={from.x}
                y1={from.y}
                x2={to.x}
                y2={to.y}
                animate={{
                  x1: [from.x, from.x + from.dx, from.x - from.dx * 0.5, from.x],
                  y1: [from.y, from.y + from.dy, from.y - from.dy * 0.5, from.y],
                  x2: [to.x, to.x + to.dx, to.x - to.dx * 0.5, to.x],
                  y2: [to.y, to.y + to.dy, to.y - to.dy * 0.5, to.y],
                  opacity: [0.18, 0.58, 0.24],
                }}
                transition={{ repeat: Infinity, duration: 5 + (index % 7), delay: index * 0.04, ease: "easeInOut" }}
              />
            );
          })}
        </g>

        <g className="thought-pulses">
          {thoughtPaths.map((path, index) => (
            <motion.path
              key={path}
              d={path}
              pathLength="1"
              animate={{
                strokeDashoffset: [1, 0],
                opacity: status === "thinking" ? [0, 0.95, 0] : [0, 0.35, 0],
              }}
              transition={{
                repeat: Infinity,
                duration: status === "thinking" ? 1.4 + index * 0.16 : 5.8 + index * 0.35,
                delay: index * 0.22,
                ease: "easeInOut",
              }}
            />
          ))}
        </g>

        <g className="network-nodes">
          {nodes.map((node, index) => (
            <motion.circle
              key={node.id}
              cx={node.x}
              cy={node.y}
              r={node.r}
              animate={{
                cx: [node.x, node.x + node.dx, node.x - node.dx * 0.7, node.x],
                cy: [node.y, node.y + node.dy, node.y - node.dy * 0.7, node.y],
                r: [node.r, node.r * 1.75, node.r],
                opacity: [0.72, 1, 0.72],
              }}
              transition={{ repeat: Infinity, duration: node.d, delay: index * 0.08, ease: "easeInOut" }}
            />
          ))}
        </g>
      </svg>

      <div className="neural-status">
        <span />
        <strong>{statusLabel(status)}</strong>
      </div>
    </div>
  );
}
