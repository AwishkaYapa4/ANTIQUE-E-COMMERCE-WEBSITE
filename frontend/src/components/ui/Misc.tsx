import { Component, useEffect, useId, useState, type ErrorInfo, type ImgHTMLAttributes, type ReactNode } from 'react';
import { ChevronDown, ChevronLeft, ChevronRight, ImageOff, Star } from 'lucide-react';
import { cn, formatPrice } from '@/utils/format';
import type { ImageDto } from '@/types/api';
import { Button } from './Button';

// ---------- Accordion ----------
export function Accordion({ title, children, defaultOpen = false }: { title: ReactNode; children: ReactNode; defaultOpen?: boolean }) {
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  return (
    <div className="border-b border-line">
      <h3 className="m-0">
        <button
          type="button"
          className="flex w-full items-center justify-between py-4 text-left font-body text-[0.95rem] font-medium text-ink"
          aria-expanded={open}
          aria-controls={id}
          onClick={() => setOpen((o) => !o)}
        >
          {title}
          <ChevronDown className={cn('h-4 w-4 transition-transform', open && 'rotate-180')} aria-hidden />
        </button>
      </h3>
      <div id={id} hidden={!open} className="pb-5 text-[0.95rem] text-ink/85">
        {children}
      </div>
    </div>
  );
}

// ---------- Pagination ----------
export function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (p: number) => void }) {
  if (totalPages <= 1) return null;
  const pages = Array.from({ length: totalPages }, (_, i) => i + 1).filter((p) => p === 1 || p === totalPages || Math.abs(p - page) <= 1);
  return (
    <nav className="flex items-center justify-center gap-1 pt-10" aria-label="Pagination">
      <Button variant="ghost" size="icon" disabled={page <= 1} onClick={() => onChange(page - 1)} aria-label="Previous page">
        <ChevronLeft className="h-4 w-4" />
      </Button>
      {pages.map((p, i) => (
        <span key={p} className="flex items-center">
          {i > 0 && p - pages[i - 1] > 1 && <span className="px-1 text-muted">…</span>}
          <Button variant={p === page ? 'primary' : 'ghost'} size="icon" onClick={() => onChange(p)} aria-current={p === page ? 'page' : undefined}>
            {p}
          </Button>
        </span>
      ))}
      <Button variant="ghost" size="icon" disabled={page >= totalPages} onClick={() => onChange(page + 1)} aria-label="Next page">
        <ChevronRight className="h-4 w-4" />
      </Button>
    </nav>
  );
}

// ---------- Price ----------
export function Price({ price, compareAt, className }: { price: number; compareAt?: number | null; className?: string }) {
  const onSale = compareAt != null && compareAt > price;
  return (
    <span className={cn('inline-flex flex-wrap items-baseline gap-x-2', className)}>
      <span className={cn(onSale && 'text-danger')}>{formatPrice(price)}</span>
      {onSale && (
        <>
          <span className="sr-only">Original price</span>
          <s className="text-[0.85em] text-muted">{formatPrice(compareAt)}</s>
        </>
      )}
    </span>
  );
}

// ---------- Responsive, lazy image with graceful fallback ----------
type SmartImageProps = Omit<ImgHTMLAttributes<HTMLImageElement>, 'src'> & {
  image?: ImageDto | null;
  src?: string | null;
  sizes?: string;
  ratio?: string;
  priority?: boolean;
};

export function SmartImage({ image, src, alt, sizes = '100vw', ratio, priority, className, ...rest }: SmartImageProps) {
  const url = image?.url ?? src ?? null;
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [url]);
  const style = ratio ? { aspectRatio: ratio } : image?.width && image.height ? { aspectRatio: `${image.width} / ${image.height}` } : undefined;
  if (!url || failed) {
    return (
      <div className={cn('flex items-center justify-center bg-surface text-muted', className)} style={style} role="img" aria-label={alt ?? 'Image unavailable'}>
        <ImageOff className="h-8 w-8 opacity-60" aria-hidden />
      </div>
    );
  }
  return (
    <img
      src={url}
      srcSet={image?.srcSet ?? undefined}
      sizes={image?.srcSet ? sizes : undefined}
      alt={alt ?? image?.alt ?? ''}
      width={image?.width ?? undefined}
      height={image?.height ?? undefined}
      loading={priority ? 'eager' : 'lazy'}
      decoding="async"
      fetchPriority={priority ? 'high' : undefined}
      onError={() => setFailed(true)}
      className={cn('object-cover', className)}
      style={style}
      {...rest}
    />
  );
}

// ---------- Stars ----------
export function Stars({ value, size = 'h-4 w-4' }: { value: number; size?: string }) {
  return (
    <span className="inline-flex text-accent" aria-label={`${value} out of 5 stars`} role="img">
      {[1, 2, 3, 4, 5].map((n) => (
        <Star key={n} className={cn(size, n <= Math.round(value) ? 'fill-current' : 'opacity-30')} aria-hidden />
      ))}
    </span>
  );
}

// ---------- Error boundary ----------
export class ErrorBoundary extends Component<{ children: ReactNode; fallback?: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Hook point for an error monitoring service (e.g. Sentry) in production.
    console.error('UI error', error, info.componentStack);
  }

  render() {
    if (!this.state.error) return this.props.children;
    return (
      this.props.fallback ?? (
        <div className="container-page py-24 text-center" role="alert">
          <h1 className="text-4xl">Something went wrong</h1>
          <p className="mt-3 text-muted">Please refresh the page. If it keeps happening, contact us.</p>
          <Button className="mt-6" onClick={() => window.location.reload()}>Refresh page</Button>
        </div>
      )
    );
  }
}
