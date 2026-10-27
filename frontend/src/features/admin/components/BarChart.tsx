import { useState } from 'react';
import { formatPrice } from '@/utils/format';

export interface BarDatum {
  label: string;
  /** Short tick label (e.g. "12 Oct"). */
  tick: string;
  value: number;
  detail?: string;
}

/**
 * Single-series bar chart (one hue, no legend – the title names the series).
 * Thin bars with 4px rounded tops anchored to the baseline, 2px gaps, recessive grid,
 * per-bar hover/focus tooltip with a full-height hit target, and a screen-reader table.
 */
export function BarChart({ data, title, formatValue = formatPrice, height = 220 }: { data: BarDatum[]; title: string; formatValue?: (v: number) => string; height?: number }) {
  const [active, setActive] = useState<number | null>(null);
  const width = 720;
  const pad = { top: 12, right: 8, bottom: 26, left: 56 };
  const innerW = width - pad.left - pad.right;
  const innerH = height - pad.top - pad.bottom;
  const max = Math.max(1, ...data.map((d) => d.value));
  const niceMax = (() => {
    const p = 10 ** Math.floor(Math.log10(max));
    return Math.ceil(max / p) * p;
  })();
  const slot = innerW / Math.max(1, data.length);
  const barW = Math.max(2, Math.min(28, slot - 2));
  const ticks = [0, 0.5, 1].map((t) => t * niceMax);
  const labelEvery = Math.ceil(data.length / 8);
  const y = (v: number) => pad.top + innerH - (v / niceMax) * innerH;
  const a = active != null ? data[active] : null;

  return (
    <figure className="relative">
      <figcaption className="sr-only">{title}</figcaption>
      <svg viewBox={`0 0 ${width} ${height}`} className="h-auto w-full" role="img" aria-label={title} onMouseLeave={() => setActive(null)}>
        {ticks.map((t) => (
          <g key={t}>
            <line x1={pad.left} x2={width - pad.right} y1={y(t)} y2={y(t)} stroke="var(--c-line)" strokeWidth={1} />
            <text x={pad.left - 8} y={y(t)} dy="0.32em" textAnchor="end" fontSize="11" fill="var(--c-muted)">
              {t >= 1000 ? `${Math.round(t / 1000)}k` : Math.round(t)}
            </text>
          </g>
        ))}
        {data.map((d, i) => {
          const x = pad.left + i * slot + (slot - barW) / 2;
          const h = Math.max(0, innerH - (y(d.value) - pad.top));
          const r = Math.min(4, barW / 2, h);
          const top = pad.top + innerH - h;
          return (
            <g key={d.label}>
              {h > 0 && (
                <path
                  d={`M${x},${pad.top + innerH} V${top + r} Q${x},${top} ${x + r},${top} H${x + barW - r} Q${x + barW},${top} ${x + barW},${top + r} V${pad.top + innerH} Z`}
                  fill="var(--c-primary)"
                  opacity={active == null || active === i ? 1 : 0.45}
                />
              )}
              {/* hit target: the whole column, larger than the mark */}
              <rect x={pad.left + i * slot} y={pad.top} width={slot} height={innerH} fill="transparent" tabIndex={0}
                aria-label={`${d.label}: ${formatValue(d.value)}${d.detail ? `, ${d.detail}` : ''}`}
                onMouseEnter={() => setActive(i)} onFocus={() => setActive(i)} onBlur={() => setActive(null)} />
              {i % labelEvery === 0 && (
                <text x={pad.left + i * slot + slot / 2} y={height - 8} textAnchor="middle" fontSize="11" fill="var(--c-muted)">{d.tick}</text>
              )}
            </g>
          );
        })}
        <line x1={pad.left} x2={width - pad.right} y1={pad.top + innerH} y2={pad.top + innerH} stroke="var(--c-muted)" strokeWidth={1} />
      </svg>
      {a && active != null && (
        <div
          className="pointer-events-none absolute top-0 rounded-theme border border-line bg-paper px-3 py-2 text-xs shadow-md"
          style={{ left: `clamp(0px, calc(${((pad.left + active * slot + slot / 2) / width) * 100}% - 70px), calc(100% - 140px))` }}
          role="status"
        >
          <p className="text-muted">{a.label}</p>
          <p className="text-sm font-medium text-ink">{formatValue(a.value)}</p>
          {a.detail && <p className="text-muted">{a.detail}</p>}
        </div>
      )}
      <table className="sr-only">
        <caption>{title}</caption>
        <thead><tr><th>Date</th><th>Value</th></tr></thead>
        <tbody>{data.map((d) => <tr key={d.label}><td>{d.label}</td><td>{formatValue(d.value)}{d.detail ? ` (${d.detail})` : ''}</td></tr>)}</tbody>
      </table>
    </figure>
  );
}
