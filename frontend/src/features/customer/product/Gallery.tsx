import { useRef, useState, type PointerEvent, type TouchEvent } from 'react';
import { ChevronLeft, ChevronRight, Expand } from 'lucide-react';
import type { ImageDto } from '@/types/api';
import { Modal } from '@/components/ui/Overlay';
import { SmartImage } from '@/components/ui/Misc';
import { cn } from '@/utils/format';

/** Swipe left/right on touch devices. */
function useSwipe(onPrev: () => void, onNext: () => void) {
  const start = useRef<number | null>(null);
  return {
    onTouchStart: (e: TouchEvent) => { start.current = e.touches[0].clientX; },
    onTouchEnd: (e: TouchEvent) => {
      if (start.current == null) return;
      const dx = e.changedTouches[0].clientX - start.current;
      if (Math.abs(dx) > 40) (dx > 0 ? onPrev : onNext)();
      start.current = null;
    },
  };
}

export function Gallery({ images, name, sold }: { images: ImageDto[]; name: string; sold: boolean }) {
  const [index, setIndex] = useState(0);
  const [fullscreen, setFullscreen] = useState(false);
  const [zoom, setZoom] = useState<{ x: number; y: number } | null>(null);
  const count = images.length;
  const prev = () => setIndex((i) => (i - 1 + count) % count);
  const next = () => setIndex((i) => (i + 1) % count);
  const swipe = useSwipe(prev, next);
  const current = images[index];

  const onMove = (e: PointerEvent<HTMLDivElement>) => {
    if (e.pointerType !== 'mouse') return;
    const rect = e.currentTarget.getBoundingClientRect();
    setZoom({ x: ((e.clientX - rect.left) / rect.width) * 100, y: ((e.clientY - rect.top) / rect.height) * 100 });
  };

  if (count === 0) return <SmartImage alt={name} ratio="4 / 5" className="w-full rounded-theme" />;

  return (
    <div className="flex flex-col-reverse gap-3 md:flex-row">
      {count > 1 && (
        <div className="flex gap-2 overflow-x-auto md:w-20 md:flex-col md:overflow-visible" role="tablist" aria-label="Product images">
          {images.map((img, i) => (
            <button
              key={img.url + i}
              role="tab"
              aria-selected={i === index}
              aria-label={`Show image ${i + 1} of ${count}`}
              onClick={() => setIndex(i)}
              className={cn('w-16 shrink-0 overflow-hidden rounded-theme border-2 transition md:w-full', i === index ? 'border-accent' : 'border-transparent opacity-70 hover:opacity-100')}
            >
              <SmartImage image={img} alt="" ratio="4 / 5" sizes="80px" className="h-full w-full" />
            </button>
          ))}
        </div>
      )}

      <div className="relative flex-1">
        <div
          className="relative cursor-zoom-in overflow-hidden rounded-theme bg-surface"
          onPointerMove={onMove}
          onPointerLeave={() => setZoom(null)}
          onClick={() => setFullscreen(true)}
          {...swipe}
        >
          <SmartImage image={current} alt={current.alt ?? name} ratio="4 / 5" priority={index === 0} sizes="(min-width: 1024px) 50vw, 100vw"
            className={cn('h-full w-full transition-transform duration-200', sold && 'opacity-80')}
            style={zoom ? { transform: 'scale(1.9)', transformOrigin: `${zoom.x}% ${zoom.y}%` } : undefined} />
          {sold && <span className="absolute top-4 left-4 rounded-theme bg-primary px-3 py-1.5 text-xs font-semibold tracking-[0.2em] text-button-ink uppercase">Sold</span>}
        </div>
        <button type="button" onClick={() => setFullscreen(true)} className="absolute right-3 bottom-3 inline-flex h-10 w-10 items-center justify-center rounded-full bg-paper/90 shadow" aria-label="View fullscreen">
          <Expand className="h-4 w-4" />
        </button>
        {count > 1 && (
          <>
            <button type="button" onClick={prev} className="absolute top-1/2 left-3 inline-flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-paper/90 shadow md:hidden" aria-label="Previous image"><ChevronLeft className="h-5 w-5" /></button>
            <button type="button" onClick={next} className="absolute top-1/2 right-3 inline-flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-paper/90 shadow md:hidden" aria-label="Next image"><ChevronRight className="h-5 w-5" /></button>
            <p className="mt-2 text-center text-xs text-muted md:hidden" aria-live="polite">{index + 1} / {count}</p>
          </>
        )}
      </div>

      <Modal open={fullscreen} onClose={() => setFullscreen(false)} title={name} size="full">
        <div className="relative flex items-center justify-center bg-bg" {...swipe}
          onKeyDown={(e) => { if (e.key === 'ArrowLeft') prev(); if (e.key === 'ArrowRight') next(); }}>
          <img src={current.url} srcSet={current.srcSet ?? undefined} sizes="96vw" alt={current.alt ?? name} className="max-h-[78vh] w-auto object-contain" />
          {count > 1 && (
            <>
              <button type="button" onClick={prev} className="absolute left-2 inline-flex h-12 w-12 items-center justify-center rounded-full bg-paper shadow" aria-label="Previous image"><ChevronLeft className="h-6 w-6" /></button>
              <button type="button" onClick={next} className="absolute right-2 inline-flex h-12 w-12 items-center justify-center rounded-full bg-paper shadow" aria-label="Next image"><ChevronRight className="h-6 w-6" /></button>
            </>
          )}
        </div>
        <p className="mt-3 text-center text-sm text-muted">{index + 1} of {count}{current.alt ? ` · ${current.alt}` : ''}</p>
      </Modal>
    </div>
  );
}
