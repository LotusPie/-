const INK = "#211c18";
const CREAM = "#fffaf3";
const WELL = "#f3eadf";
const SKIN = "#f0c3a4";

const CARD_ACCENTS = {
  hide: "#5e3458",
  swap: "#c24c14",
  peek: "#1a655c",
  shield: "#8a5a12",
  block: "#7f2f24",
  double: "#a61d4d",
  steal: "#2e6834",
  respin: "#2f4f86",
  mode: "#3a3f78",
  extra: "#9c3d5c",
  next: "#1f6484",
  pass: "#6a4636",
};

function cardAccent(id) {
  return CARD_ACCENTS[id] || "#7f2f24";
}

function CardShell({ accent, children }) {
  return (
    <g>
      <rect width="240" height="336" rx="22" fill={CREAM} />
      <rect x="5.5" y="5.5" width="229" height="325" rx="18" fill="none" stroke={accent} strokeWidth="5" />
      <rect x="18" y="18" width="204" height="238" rx="14" fill={WELL} />
      <rect x="18" y="18" width="204" height="238" rx="14" fill="none" stroke={accent} strokeWidth="1.25" opacity="0.28" />
      {children}
      <rect x="18" y="266" width="204" height="54" rx="14" fill={accent} />
    </g>
  );
}

function Note({ x, y, color, scale = 1 }) {
  return (
    <g transform={`translate(${x} ${y}) scale(${scale})`} fill={color}>
      <ellipse cx="0" cy="0" rx="8" ry="5.6" transform="rotate(-24)" />
      <rect x="5.6" y="-34" width="3.1" height="36" rx="1.2" />
      <path d="M8.7 -34c12 1.5 18 9 15 16-6-5-13-4-15-1.5z" />
    </g>
  );
}

function HideArt({ accent }) {
  return (
    <g>
      <rect x="70" y="34" width="100" height="196" rx="16" fill={CREAM} stroke={INK} strokeWidth="3" />
      <text x="120" y="156" textAnchor="middle" fontSize="92" fontWeight="700" fill={INK}>
        8
      </text>
      <path
        d="M48 158c12-22 34-28 58-12 8 6 16 6 24 0 26-16 52-6 62 16 6 14 2 28-12 34-14 6-24 2-36-4-10 6-22 10-36 4-18-6-32-16-24-38z"
        fill={accent}
        stroke={INK}
        strokeWidth="3"
      />
      <ellipse cx="94" cy="162" rx="14" ry="9" fill={WELL} stroke={INK} strokeWidth="2.5" />
      <ellipse cx="148" cy="162" rx="14" ry="9" fill={WELL} stroke={INK} strokeWidth="2.5" />
      <path d="M50 162c-14 0-22 10-18 18" fill="none" stroke={accent} strokeWidth="4" strokeLinecap="round" />
      <path d="M190 162c14 0 22 10 18 18" fill="none" stroke={accent} strokeWidth="4" strokeLinecap="round" />
    </g>
  );
}

function arcArrow(x1, y1, cx, cy, x2, y2, color) {
  const dx = x2 - cx;
  const dy = y2 - cy;
  const len = Math.hypot(dx, dy) || 1;
  const ux = dx / len;
  const uy = dy / len;
  const px = -uy;
  const py = ux;
  const back = 16;
  const wing = 8;
  const bx = x2 - ux * back;
  const by = y2 - uy * back;
  return (
    <g>
      <path
        d={`M ${x1} ${y1} Q ${cx} ${cy} ${x2} ${y2}`}
        fill="none"
        stroke={color}
        strokeWidth="6"
        strokeLinecap="round"
      />
      <polygon
        points={`${x2},${y2} ${bx + px * wing},${by + py * wing} ${bx - px * wing},${by - py * wing}`}
        fill={color}
      />
    </g>
  );
}

function SwapArt({ accent }) {
  return (
    <g>
      <rect x="28" y="78" width="80" height="80" rx="16" fill={CREAM} stroke={INK} strokeWidth="3" />
      <text x="68" y="132" textAnchor="middle" fontSize="46" fontWeight="700" fill={INK}>
        3
      </text>
      <rect x="132" y="78" width="80" height="80" rx="16" fill={CREAM} stroke={INK} strokeWidth="3" />
      <text x="172" y="132" textAnchor="middle" fontSize="46" fontWeight="700" fill={INK}>
        9
      </text>
      {arcArrow(104, 70, 120, 40, 140, 70, accent)}
      {arcArrow(136, 166, 120, 204, 100, 166, accent)}
    </g>
  );
}

function PeekArt({ accent }) {
  return (
    <g>
      <g transform="rotate(9 178 124)">
        <rect x="146" y="62" width="66" height="98" rx="10" fill={CREAM} stroke={INK} strokeWidth="3" />
        <text x="179" y="124" textAnchor="middle" fontSize="46" fontWeight="700" fill={accent}>
          ?
        </text>
      </g>
      <path d="M36 148C60 104 118 104 142 148C118 192 60 192 36 148Z" fill={CREAM} stroke={INK} strokeWidth="3" />
      <circle cx="100" cy="148" r="18" fill={accent} />
      <circle cx="106" cy="148" r="8" fill={INK} />
      <circle cx="110" cy="144" r="3" fill={CREAM} />
      <path d="M48 112C74 96 116 98 136 118" fill="none" stroke={INK} strokeWidth="4" strokeLinecap="round" />
    </g>
  );
}

function ShieldArt({ accent }) {
  return (
    <g>
      <path
        d="M120 32 L198 64 V150 C198 198 160 230 120 252 C80 230 42 198 42 150 V64 Z"
        fill="#f6e2bc"
        stroke={accent}
        strokeWidth="7"
        strokeLinejoin="round"
      />
      <path
        d="M120 54 L178 80 V146 C178 184 150 208 120 226 C90 208 62 184 62 146 V80 Z"
        fill={CREAM}
        stroke={INK}
        strokeWidth="3"
        strokeLinejoin="round"
      />
      <path
        d="M120 86 L156 104 V142 C156 164 140 180 120 192 C100 180 84 164 84 142 V104 Z"
        fill={accent}
      />
      <path d="M120 108 L132 132 H108 Z" fill={CREAM} />
    </g>
  );
}

function BlockArt({ accent }) {
  const bricks = [];
  for (let row = 0; row < 9; row += 1) {
    const offset = row % 2 === 0 ? 0 : -18;
    for (let col = 0; col < 5; col += 1) {
      const x = 30 + offset + col * 34;
      const y = 34 + row * 24;
      if (x < 26 || x + 32 > 152) continue;
      bricks.push(
        <rect
          key={`${row}-${col}`}
          x={x}
          y={y}
          width="32"
          height="22"
          rx="2"
          fill={row % 2 ? "#f7efe6" : CREAM}
          stroke={INK}
          strokeWidth="2.2"
        />,
      );
    }
  }
  return (
    <g>
      {bricks}
      <rect x="140" y="112" width="68" height="24" rx="12" fill={accent} />
      <path d="M214 96 h-18 M220 124 h-22 M214 152 h-18" stroke={accent} strokeWidth="4" strokeLinecap="round" />
    </g>
  );
}

function DoubleArt({ accent }) {
  return (
    <g>
      <circle cx="54" cy="140" r="28" fill={CREAM} stroke={INK} strokeWidth="3" />
      <text x="54" y="152" textAnchor="middle" fontSize="30" fontWeight="700" fill={INK}>
        2
      </text>
      <path d="M86 140 H108" stroke={accent} strokeWidth="7" strokeLinecap="round" />
      <polygon points="104,128 126,140 104,152" fill={accent} />
      <circle cx="172" cy="140" r="50" fill={accent} stroke={INK} strokeWidth="3" />
      <text x="172" y="158" textAnchor="middle" fontSize="56" fontWeight="700" fill={CREAM}>
        4
      </text>
    </g>
  );
}

function StealArt({ accent }) {
  return (
    <g>
      <path
        d="M34 214c6-48 32-62 58-58 10 2 16 10 18 18-18 10-28 36-24 62-16 8-40 6-52-22z"
        fill={SKIN}
        stroke={INK}
        strokeWidth="3"
        strokeLinejoin="round"
      />
      <circle cx="156" cy="104" r="36" fill="#f4d48a" stroke={INK} strokeWidth="3" />
      <circle cx="156" cy="104" r="26" fill="none" stroke={accent} strokeWidth="3" />
      <text x="156" y="116" textAnchor="middle" fontSize="36" fontWeight="700" fill={INK}>
        1
      </text>
      <rect x="118" y="96" width="20" height="58" rx="10" fill={SKIN} stroke={INK} strokeWidth="3" transform="rotate(-24 128 125)" />
      <rect x="138" y="108" width="20" height="54" rx="10" fill={SKIN} stroke={INK} strokeWidth="3" transform="rotate(-6 148 135)" />
      <rect x="158" y="116" width="20" height="48" rx="10" fill={SKIN} stroke={INK} strokeWidth="3" transform="rotate(14 168 140)" />
      <rect x="96" y="132" width="22" height="40" rx="11" fill={SKIN} stroke={INK} strokeWidth="3" transform="rotate(-48 107 152)" />
    </g>
  );
}

function RespinArt({ accent }) {
  const cx = 120;
  const cy = 136;
  const r = 74;
  const colors = ["#7f2f24", "#1d5c55", "#8a5a12", "#7f2f24", "#1d5c55", "#8a5a12"];
  const turn = 24;
  const slices = colors.map((fill, index) => {
    const a0 = ((turn + index * 60) * Math.PI) / 180;
    const a1 = ((turn + (index + 1) * 60) * Math.PI) / 180;
    const x0 = cx + r * Math.sin(a0);
    const y0 = cy - r * Math.cos(a0);
    const x1 = cx + r * Math.sin(a1);
    const y1 = cy - r * Math.cos(a1);
    return (
      <path
        key={fill + index}
        d={`M ${cx} ${cy} L ${x0} ${y0} A ${r} ${r} 0 0 1 ${x1} ${y1} Z`}
        fill={fill}
        stroke={INK}
        strokeWidth="2"
      />
    );
  });
  return (
    <g>
      <path d="M34 70c-16 40-6 92 34 124" fill="none" stroke={accent} strokeWidth="4" strokeLinecap="round" />
      <path d="M206 64c18 36 12 90-28 126" fill="none" stroke={accent} strokeWidth="4" strokeLinecap="round" />
      <path d="M48 92c-8 8-12 8-18 4" fill="none" stroke={accent} strokeWidth="3" strokeLinecap="round" />
      <path d="M196 86c8 6 12 4 16-2" fill="none" stroke={accent} strokeWidth="3" strokeLinecap="round" />
      {slices}
      <circle cx={cx} cy={cy} r={r} fill="none" stroke={INK} strokeWidth="3" />
      <circle cx={cx} cy={cy} r="16" fill={CREAM} stroke={INK} strokeWidth="3" />
      <polygon points="120,46 110,70 130,70" fill={INK} />
    </g>
  );
}

function ModeArt({ accent }) {
  const panels = [
    { n: "1", notes: 1, on: false },
    { n: "2", notes: 2, on: true },
    { n: "3", notes: 3, on: false },
  ];
  return (
    <g>
      {panels.map((panel, index) => {
        const x = 28 + index * 64;
        const noteColor = panel.on ? CREAM : accent;
        return (
          <g key={panel.n}>
            <rect
              x={x}
              y="36"
              width="58"
              height="196"
              rx="12"
              fill={panel.on ? accent : CREAM}
              stroke={panel.on ? INK : accent}
              strokeWidth={panel.on ? 3.5 : 2.5}
            />
            {panel.on ? (
              <g>
                <circle cx={x + 44} cy="52" r="9" fill={CREAM} />
                <path d={`M${x + 39} 52 l3.2 3.4 6.2-7`} fill="none" stroke={accent} strokeWidth="2.4" strokeLinecap="round" />
              </g>
            ) : null}
            <text
              x={x + 29}
              y="118"
              textAnchor="middle"
              fontSize="42"
              fontWeight="700"
              fill={panel.on ? CREAM : INK}
            >
              {panel.n}
            </text>
            {Array.from({ length: panel.notes }, (_, note) => (
              <Note
                key={note}
                x={x + 29 - (panel.notes - 1) * 9 + note * 18}
                y={196}
                color={noteColor}
                scale={0.55}
              />
            ))}
          </g>
        );
      })}
    </g>
  );
}

function ExtraArt({ accent }) {
  return (
    <g>
      <g opacity="0.38">
        <Note x={64} y={176} color={INK} scale={1.15} />
      </g>
      <Note x={124} y={156} color={accent} scale={2.15} />
      <circle cx="186" cy="68" r="20" fill={accent} stroke={INK} strokeWidth="3" />
      <path d="M186 56 v24 M174 68 h24" stroke={CREAM} strokeWidth="4" strokeLinecap="round" />
    </g>
  );
}

function PersonBust({ x, y, fill }) {
  return (
    <g stroke={INK} strokeWidth="3">
      <path
        d={`M${x - 20} ${y + 52} v-20 q0 -16 12 -18 h16 q12 2 12 18 v20 z`}
        fill={fill}
      />
      <circle cx={x} cy={y} r="14" fill={fill} />
    </g>
  );
}

function NextArt({ accent }) {
  return (
    <g>
      <PersonBust x={46} y={48} fill={CREAM} />
      <PersonBust x={102} y={48} fill={CREAM} />
      <circle cx="176" cy="78" r="40" fill={accent} opacity="0.16" />
      <PersonBust x={176} y={44} fill={accent} />
      <g transform="translate(168 108) rotate(-42) translate(-96 0)" fill={SKIN} stroke={INK} strokeWidth="3" strokeLinejoin="round">
        <rect x="0" y="8" width="40" height="46" rx="16" />
        <rect x="34" y="14" width="18" height="16" rx="8" />
        <rect x="32" y="30" width="20" height="14" rx="7" />
        <rect x="36" y="-8" width="62" height="18" rx="9" />
        <rect x="8" y="-2" width="16" height="28" rx="8" transform="rotate(-48 16 12)" />
      </g>
    </g>
  );
}

function PassArt({ accent }) {
  return (
    <g strokeLinecap="round" strokeLinejoin="round">
      <rect x="30" y="168" width="96" height="30" rx="8" fill={CREAM} stroke={INK} strokeWidth="3" />
      <circle cx="54" cy="183" r="10" fill={WELL} stroke={INK} strokeWidth="2.5" />
      <circle cx="84" cy="183" r="10" fill={accent} stroke={INK} strokeWidth="2.5" />
      <path d="M48 166c0-16 18-26 30-14 12-12 30-2 30 14" fill="none" stroke={INK} strokeWidth="3" />
      <path d="M44 166 h8 M96 166 h8" stroke={INK} strokeWidth="3" />
      <path d="M46 198 v34 M108 198 v34" stroke={INK} strokeWidth="3" />
      <path d="M128 148c16-16 32-16 44-4" fill="none" stroke={accent} strokeWidth="4" />
      <polygon points="168,132 184,150 160,154" fill={accent} />
      <circle cx="186" cy="70" r="14" fill={SKIN} stroke={INK} strokeWidth="3" />
      <path d="M186 86 v36" stroke={INK} strokeWidth="4" />
      <path d="M186 98 L164 116" stroke={INK} strokeWidth="4" />
      <path d="M186 100 L206 112" stroke={INK} strokeWidth="4" />
      <path d="M186 122 L160 168" stroke={INK} strokeWidth="4" />
      <path d="M186 122 L214 156" stroke={INK} strokeWidth="4" />
      <path d="M214 156 l8 4" stroke={INK} strokeWidth="4" />
    </g>
  );
}

const ARTS = {
  hide: HideArt,
  swap: SwapArt,
  peek: PeekArt,
  shield: ShieldArt,
  block: BlockArt,
  double: DoubleArt,
  steal: StealArt,
  respin: RespinArt,
  mode: ModeArt,
  extra: ExtraArt,
  next: NextArt,
  pass: PassArt,
};

export function CardFace({ id, name }) {
  const accent = cardAccent(id);
  const Art = ARTS[id];
  return (
    <div className="card-face" style={{ "--accent": accent }}>
      <svg viewBox="0 0 240 336" aria-hidden="true">
        <CardShell accent={accent}>{Art ? <Art accent={accent} /> : null}</CardShell>
      </svg>
      <h3 className="card-face-name">{name}</h3>
    </div>
  );
}
