import { useEffect, useRef, useState, type ReactNode } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ImagePlus, Search, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { errorMessage } from '@/services/http';
import type { MediaAsset } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Field';
import { EmptyState, ErrorState, Skeleton } from '@/components/ui/Feedback';
import { Modal } from '@/components/ui/Overlay';
import { Pagination } from '@/components/ui/Misc';
import { cn } from '@/utils/format';

export function AdminPage({ title, description, actions, children }: { title: string; description?: ReactNode; actions?: ReactNode; children: ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-7xl px-4 py-6 sm:px-8 sm:py-8">
      <title>{`${title} · Admin`}</title>
      <meta name="robots" content="noindex, nofollow" />
      <header className="mb-6 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl sm:text-4xl">{title}</h1>
          {description && <p className="mt-1 text-sm text-muted">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
      </header>
      {children}
    </div>
  );
}

export function Panel({ title, actions, children, className }: { title?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={cn('rounded-theme border border-line bg-paper', className)}>
      {(title || actions) && (
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line px-5 py-3.5">
          {title && <h2 className="font-body text-sm font-semibold tracking-wide">{title}</h2>}
          {actions}
        </div>
      )}
      <div className="p-5">{children}</div>
    </section>
  );
}

export function StatTile({ label, value, hint, tone }: { label: string; value: ReactNode; hint?: ReactNode; tone?: 'warning' }) {
  return (
    <div className={cn('rounded-theme border bg-paper p-5', tone === 'warning' ? 'border-accent/50' : 'border-line')}>
      <p className="text-xs font-medium tracking-wider text-muted uppercase">{label}</p>
      <p className="mt-2 font-heading text-3xl text-ink">{value}</p>
      {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
    </div>
  );
}

/** Debounced search box for admin tables. */
export function SearchInput({ value, onChange, placeholder = 'Search…' }: { value: string; onChange: (v: string) => void; placeholder?: string }) {
  const [local, setLocal] = useState(value);
  useEffect(() => setLocal(value), [value]);
  useEffect(() => {
    const t = setTimeout(() => local !== value && onChange(local), 350);
    return () => clearTimeout(t);
  }, [local, value, onChange]);
  return (
    <div className="relative w-full sm:w-72">
      <Search className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-muted" aria-hidden />
      <Input value={local} onChange={(e) => setLocal(e.target.value)} placeholder={placeholder} className="py-2 pl-9 text-sm" aria-label={placeholder} />
    </div>
  );
}

export function TableShell({ children, loading, error, empty, onRetry }: { children: ReactNode; loading: boolean; error?: unknown; empty?: ReactNode; onRetry?: () => void }) {
  if (loading) return <div className="space-y-2">{Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className="h-12 w-full" />)}</div>;
  if (error) return <ErrorState message={errorMessage(error)} onRetry={onRetry} />;
  if (empty) return <>{empty}</>;
  return <div className="overflow-x-auto rounded-theme border border-line bg-paper">{children}</div>;
}

export const th = 'px-4 py-3 text-left text-xs font-semibold tracking-wider text-muted uppercase whitespace-nowrap';
export const td = 'px-4 py-3 text-sm align-middle';

// ---------- Media library picker (also used for logo, hero, category images) ----------

export function MediaPicker({ open, onClose, onSelect, folder = 'general', multiple = false }: {
  open: boolean; onClose: () => void; onSelect: (assets: MediaAsset[]) => void; folder?: string; multiple?: boolean;
}) {
  const queryClient = useQueryClient();
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState<MediaAsset[]>([]);
  const fileRef = useRef<HTMLInputElement>(null);
  const media = useQuery({ queryKey: ['admin', 'media', q, page], queryFn: () => adminApi.media({ q, page, pageSize: 24 }), enabled: open, placeholderData: keepPreviousData });
  const upload = useMutation({
    mutationFn: async (files: File[]) => Promise.all(files.map((f) => adminApi.uploadMedia(f, folder))),
    onSuccess: (assets) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'media'] });
      toast.success(`${assets.length} image${assets.length > 1 ? 's' : ''} uploaded`);
      setSelected((s) => (multiple ? [...s, ...assets] : assets.slice(0, 1)));
    },
    onError: (e) => toast.error(errorMessage(e)),
  });
  useEffect(() => { if (!open) setSelected([]); }, [open]);

  const toggle = (a: MediaAsset) =>
    setSelected((s) => (s.some((x) => x.id === a.id) ? s.filter((x) => x.id !== a.id) : multiple ? [...s, a] : [a]));

  return (
    <Modal open={open} onClose={onClose} title="Choose images" size="xl"
      footer={<><Button variant="ghost" onClick={onClose}>Cancel</Button><Button disabled={selected.length === 0} onClick={() => { onSelect(selected); onClose(); }}>Use {selected.length || ''} selected</Button></>}>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <SearchInput value={q} onChange={(v) => { setQ(v); setPage(1); }} placeholder="Search images" />
        <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" multiple={multiple} className="hidden"
          onChange={(e) => { const files = Array.from(e.target.files ?? []); if (files.length) upload.mutate(files); e.target.value = ''; }} />
        <Button variant="outline" size="sm" icon={<Upload className="h-4 w-4" />} loading={upload.isPending} onClick={() => fileRef.current?.click()}>Upload new</Button>
      </div>
      <p className="mb-3 text-xs text-muted">JPG, PNG or WEBP up to 10 MB. Images are optimised and resized automatically.</p>
      {media.isPending ? <Skeleton className="h-64 w-full" /> : media.isError ? <ErrorState message={errorMessage(media.error)} /> : media.data.items.length === 0 ? (
        <EmptyState icon={<ImagePlus className="h-8 w-8" />} title="No images yet" description="Upload your first product photo." />
      ) : (
        <>
          <ul className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {media.data.items.map((a) => {
              const isSel = selected.some((x) => x.id === a.id);
              return (
                <li key={a.id}>
                  <button type="button" onClick={() => toggle(a)} aria-pressed={isSel} className={cn('block w-full overflow-hidden rounded-theme border-2', isSel ? 'border-accent' : 'border-transparent')}>
                    <img src={a.url} srcSet={a.srcSet ?? undefined} sizes="160px" alt={a.altText ?? a.fileName} loading="lazy" className="aspect-square w-full object-cover" />
                  </button>
                  <p className="mt-1 truncate text-[0.7rem] text-muted">{a.fileName}</p>
                </li>
              );
            })}
          </ul>
          <Pagination page={page} totalPages={media.data.totalPages} onChange={setPage} />
        </>
      )}
    </Modal>
  );
}

/** Single image field with preview + picker; value is a URL. */
export function ImageField({ label, value, onChange, folder }: { label: string; value: string | null; onChange: (url: string | null) => void; folder?: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div>
      <p className="mb-1.5 text-sm font-medium">{label}</p>
      <div className="flex items-center gap-3">
        <div className="flex h-20 w-28 items-center justify-center overflow-hidden rounded-theme border border-line bg-surface">
          {value ? <img src={value} alt="" className="h-full w-full object-cover" /> : <ImagePlus className="h-6 w-6 text-muted" aria-hidden />}
        </div>
        <div className="flex flex-col gap-1">
          <Button size="sm" variant="outline" onClick={() => setOpen(true)}>{value ? 'Change' : 'Choose image'}</Button>
          {value && <Button size="sm" variant="ghost" onClick={() => onChange(null)}>Remove</Button>}
        </div>
      </div>
      <MediaPicker open={open} onClose={() => setOpen(false)} folder={folder} onSelect={(a) => onChange(a[0]?.url ?? null)} />
    </div>
  );
}
