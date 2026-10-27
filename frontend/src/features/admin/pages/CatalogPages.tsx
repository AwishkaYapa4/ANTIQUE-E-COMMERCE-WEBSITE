import { useRef, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ArrowDown, ArrowUp, Copy, Pencil, Plus, Trash2, Upload } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { ApiError, errorMessage } from '@/services/http';
import type { Category, Collection, MediaAsset } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select, Textarea } from '@/components/ui/Field';
import { Badge, EmptyState, Skeleton } from '@/components/ui/Feedback';
import { Modal, useConfirm } from '@/components/ui/Overlay';
import { Pagination } from '@/components/ui/Misc';
import { formatDate } from '@/utils/format';
import { AdminPage, ImageField, Panel, SearchInput, TableShell, td, th } from '../components/AdminUi';

const groupSchema = z.object({
  name: z.string().trim().min(1, 'Name is required.').max(200),
  slug: z.string().trim().max(200).regex(/^$|^[a-z0-9]+(?:-[a-z0-9]+)*$/, 'Lowercase letters, numbers and dashes.'),
  description: z.string().trim().max(2000),
  imageUrl: z.string().nullable(),
  parentId: z.string(),
  sortOrder: z.number().int().min(0).max(10000),
  isVisible: z.boolean(),
  isFeatured: z.boolean(),
  seoTitle: z.string().trim().max(200),
  seoDescription: z.string().trim().max(500),
});
type GroupValues = z.infer<typeof groupSchema>;

function GroupForm({ kind, initial, categories, onSaved }: { kind: 'category' | 'collection'; initial?: Category | Collection; categories?: Category[]; onSaved: () => void }) {
  const form = useForm<GroupValues>({
    resolver: zodResolver(groupSchema),
    defaultValues: {
      name: initial?.name ?? '', slug: initial?.slug ?? '', description: initial?.description ?? '', imageUrl: initial?.imageUrl ?? null,
      parentId: initial && 'parentId' in initial && initial.parentId ? String(initial.parentId) : '', sortOrder: initial?.sortOrder ?? 0,
      isVisible: initial?.isVisible ?? true, isFeatured: initial && 'isFeatured' in initial ? initial.isFeatured : false,
      seoTitle: initial?.seoTitle ?? '', seoDescription: initial?.seoDescription ?? '',
    },
  });
  const save = useMutation({
    mutationFn: (v: GroupValues) => {
      const body = { ...v, slug: v.slug || null, description: v.description || null, parentId: v.parentId ? Number(v.parentId) : null, seoTitle: v.seoTitle || null, seoDescription: v.seoDescription || null };
      return kind === 'category' ? adminApi.saveCategory(initial?.id ?? null, body) : adminApi.saveCollection(initial?.id ?? null, body);
    },
    onSuccess: () => { toast.success('Saved'); onSaved(); },
    onError: (e) => {
      if (e instanceof ApiError) Object.entries(e.fieldErrors).forEach(([k, v]) => form.setError(k as keyof GroupValues, { message: v[0] }));
      toast.error(errorMessage(e));
    },
  });
  const e = form.formState.errors;
  return (
    <form className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))} noValidate>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required error={e.name?.message}>{(p) => <Input {...p} {...form.register('name')} />}</Field>
        <Field label="URL slug" hint="Generated from the name if empty." error={e.slug?.message}>{(p) => <Input {...p} {...form.register('slug')} />}</Field>
      </div>
      <Field label="Description" error={e.description?.message}>{(p) => <Textarea {...p} rows={3} {...form.register('description')} />}</Field>
      <ImageField label="Image" value={form.watch('imageUrl')} onChange={(url) => form.setValue('imageUrl', url, { shouldDirty: true })} folder="categories" />
      <div className="grid gap-4 sm:grid-cols-2">
        {kind === 'category' && (
          <Field label="Parent category">{(p) => (
            <Select {...p} {...form.register('parentId')}><option value="">None (top level)</option>
              {categories?.filter((c) => c.id !== initial?.id && c.parentId == null).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          )}</Field>
        )}
        <Field label="Display order" hint="Lower numbers show first." error={e.sortOrder?.message}>{(p) => <Input {...p} type="number" {...form.register('sortOrder', { valueAsNumber: true })} />}</Field>
      </div>
      <Checkbox label="Visible in the shop" {...form.register('isVisible')} />
      {kind === 'category' && <Checkbox label="Feature on the homepage" description="Used by the Featured Categories section when no specific categories are chosen." {...form.register('isFeatured')} />}
      <details className="rounded-theme border border-line p-3">
        <summary className="cursor-pointer text-sm font-medium">Search engine listing</summary>
        <div className="mt-3 space-y-3">
          <Field label="SEO title">{(p) => <Input {...p} {...form.register('seoTitle')} />}</Field>
          <Field label="SEO description">{(p) => <Textarea {...p} rows={2} {...form.register('seoDescription')} />}</Field>
        </div>
      </details>
      <Button type="submit" loading={save.isPending}>Save</Button>
    </form>
  );
}

export function CategoriesPage() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<Category | 'new' | null>(null);
  const list = useQuery({ queryKey: ['admin', 'categories'], queryFn: adminApi.categories });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'categories'] });
  const remove = useMutation({ mutationFn: adminApi.deleteCategory, onSuccess: () => { refresh(); toast.success('Category deleted'); }, onError: (e) => toast.error(errorMessage(e)) });
  const reorder = useMutation({ mutationFn: adminApi.reorderCategories, onSuccess: refresh, onError: (e) => toast.error(errorMessage(e)) });
  const items = list.data ?? [];
  const move = (index: number, dir: -1 | 1) => {
    const ids = items.map((c) => c.id);
    const target = index + dir;
    if (target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    reorder.mutate(ids);
  };

  return (
    <AdminPage title="Categories" description="Organise products for browsing. Categories with products cannot be deleted — hide them instead." actions={<Button onClick={() => setEditing('new')}><Plus className="h-4 w-4" /> Add category</Button>}>
      <TableShell loading={list.isPending} error={list.error} empty={items.length === 0 ? <EmptyState title="No categories yet" action={<Button onClick={() => setEditing('new')}>Add category</Button>} /> : undefined}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>Order</th><th className={th}>Category</th><th className={th}>Products</th><th className={th}>Visibility</th><th className={th}><span className="sr-only">Actions</span></th></tr></thead>
          <tbody className="divide-y divide-line">
            {items.map((c, i) => (
              <tr key={c.id}>
                <td className={td}>
                  <div className="flex gap-1">
                    <Button variant="ghost" size="icon" aria-label={`Move ${c.name} up`} disabled={i === 0 || reorder.isPending} onClick={() => move(i, -1)}><ArrowUp className="h-4 w-4" /></Button>
                    <Button variant="ghost" size="icon" aria-label={`Move ${c.name} down`} disabled={i === items.length - 1 || reorder.isPending} onClick={() => move(i, 1)}><ArrowDown className="h-4 w-4" /></Button>
                  </div>
                </td>
                <td className={td}>
                  <div className="flex items-center gap-3">
                    {c.imageUrl ? <img src={c.imageUrl} alt="" className="h-10 w-10 rounded-theme object-cover" /> : <div className="h-10 w-10 rounded-theme bg-surface" />}
                    <div><p className="font-medium">{c.parentId && <span className="text-muted">— </span>}{c.name}</p><p className="text-xs text-muted">/category/{c.slug}</p></div>
                  </div>
                </td>
                <td className={td}>{c.productCount}</td>
                <td className={td}><div className="flex gap-1">{c.isVisible ? <Badge tone="success">Visible</Badge> : <Badge>Hidden</Badge>}{c.isFeatured && <Badge tone="accent">Featured</Badge>}</div></td>
                <td className={`${td} text-right whitespace-nowrap`}>
                  <Button variant="ghost" size="icon" aria-label={`Edit ${c.name}`} onClick={() => setEditing(c)}><Pencil className="h-4 w-4" /></Button>
                  <Button variant="ghost" size="icon" aria-label={`Delete ${c.name}`} onClick={async () => {
                    if (await confirm({ title: 'Delete category?', message: c.productCount > 0 ? `“${c.name}” still has ${c.productCount} product(s). Move them first, or hide the category.` : `Delete “${c.name}”? Products are not affected.`, danger: true, confirmLabel: 'Delete' })) remove.mutate(c.id);
                  }}><Trash2 className="h-4 w-4" /></Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      <Modal open={editing !== null} onClose={() => setEditing(null)} title={editing === 'new' ? 'Add category' : 'Edit category'} size="lg">
        {editing !== null && <GroupForm kind="category" initial={editing === 'new' ? undefined : editing} categories={items} onSaved={() => { refresh(); setEditing(null); }} />}
      </Modal>
    </AdminPage>
  );
}

export function CollectionsPage() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<Collection | 'new' | null>(null);
  const list = useQuery({ queryKey: ['admin', 'collections'], queryFn: adminApi.collections });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'collections'] });
  const remove = useMutation({ mutationFn: adminApi.deleteCollection, onSuccess: () => { refresh(); toast.success('Collection deleted'); }, onError: (e) => toast.error(errorMessage(e)) });
  return (
    <AdminPage title="Collections" description="Curated groupings such as “Rare Finds”. Assign products to collections from the product editor." actions={<Button onClick={() => setEditing('new')}><Plus className="h-4 w-4" /> Add collection</Button>}>
      {list.isPending ? <Skeleton className="h-40 w-full" /> : (list.data?.length ?? 0) === 0 ? <EmptyState title="No collections yet" action={<Button onClick={() => setEditing('new')}>Add collection</Button>} /> : (
        <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {list.data!.map((c) => (
            <li key={c.id} className="overflow-hidden rounded-theme border border-line bg-paper">
              {c.imageUrl ? <img src={c.imageUrl} alt="" className="aspect-[16/9] w-full object-cover" /> : <div className="aspect-[16/9] bg-surface" />}
              <div className="p-4">
                <div className="flex items-start justify-between gap-2"><h2 className="text-xl">{c.name}</h2>{!c.isVisible && <Badge>Hidden</Badge>}</div>
                <p className="mt-1 text-sm text-muted">{c.productCount} products · /collections/{c.slug}</p>
                <div className="mt-3 flex gap-2">
                  <Button size="sm" variant="outline" onClick={() => setEditing(c)}>Edit</Button>
                  <Button size="sm" variant="ghost" className="text-danger" onClick={async () => { if (await confirm({ title: 'Delete collection?', message: 'Only the grouping is removed. Products stay in your catalogue.', danger: true, confirmLabel: 'Delete' })) remove.mutate(c.id); }}>Delete</Button>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
      <Modal open={editing !== null} onClose={() => setEditing(null)} title={editing === 'new' ? 'Add collection' : 'Edit collection'} size="lg">
        {editing !== null && <GroupForm kind="collection" initial={editing === 'new' ? undefined : editing} onSaved={() => { refresh(); setEditing(null); }} />}
      </Modal>
    </AdminPage>
  );
}

const FOLDERS = ['products', 'categories', 'homepage', 'branding', 'general'];

export function MediaLibraryPage() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const fileRef = useRef<HTMLInputElement>(null);
  const [q, setQ] = useState('');
  const [folder, setFolder] = useState('');
  const [unused, setUnused] = useState(false);
  const [page, setPage] = useState(1);
  const [uploadFolder, setUploadFolder] = useState('products');
  const [selected, setSelected] = useState<MediaAsset | null>(null);
  const [alt, setAlt] = useState('');
  const [title, setTitle] = useState('');
  const list = useQuery({ queryKey: ['admin', 'media', q, folder, unused, page], queryFn: () => adminApi.media({ q, folder: folder || undefined, unused: unused || undefined, page, pageSize: 30 }), placeholderData: keepPreviousData });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'media'] });
  const upload = useMutation({
    mutationFn: async (files: File[]) => {
      for (const f of files) {
        if (f.size > 10 * 1024 * 1024) throw new Error(`${f.name} is larger than 10 MB.`);
        await adminApi.uploadMedia(f, uploadFolder);
      }
      return files.length;
    },
    onSuccess: (n) => { refresh(); toast.success(`${n} image(s) uploaded`); },
    onError: (e) => { refresh(); toast.error(errorMessage(e)); },
  });
  const update = useMutation({ mutationFn: () => adminApi.updateMedia(selected!.id, { altText: alt || null, title: title || null }), onSuccess: (a) => { setSelected(a); refresh(); toast.success('Saved'); }, onError: (e) => toast.error(errorMessage(e)) });
  const remove = useMutation({ mutationFn: adminApi.deleteMedia, onSuccess: () => { setSelected(null); refresh(); toast.success('Image deleted'); }, onError: (e) => toast.error(errorMessage(e)) });
  const open = (a: MediaAsset) => { setSelected(a); setAlt(a.altText ?? ''); setTitle(a.title ?? ''); };

  return (
    <AdminPage title="Media Library" description="Uploads are checked, stripped of metadata (e.g. GPS), resized and converted to WebP with responsive sizes."
      actions={
        <div className="flex items-center gap-2">
          <Select aria-label="Upload folder" value={uploadFolder} onChange={(e) => setUploadFolder(e.target.value)} className="w-auto py-2 text-sm">{FOLDERS.map((f) => <option key={f}>{f}</option>)}</Select>
          <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" multiple className="hidden" onChange={(e) => { const files = Array.from(e.target.files ?? []); if (files.length) upload.mutate(files); e.target.value = ''; }} />
          <Button icon={<Upload className="h-4 w-4" />} loading={upload.isPending} onClick={() => fileRef.current?.click()}>Upload</Button>
        </div>
      }>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchInput value={q} onChange={(v) => { setQ(v); setPage(1); }} placeholder="Search file name or alt text" />
        <Select aria-label="Folder" value={folder} onChange={(e) => { setFolder(e.target.value); setPage(1); }} className="w-auto py-2 text-sm"><option value="">All folders</option>{FOLDERS.map((f) => <option key={f}>{f}</option>)}</Select>
        <Checkbox label="Unused only" checked={unused} onChange={(e) => { setUnused(e.target.checked); setPage(1); }} />
      </div>
      {list.isPending ? <Skeleton className="h-64 w-full" /> : (list.data?.items.length ?? 0) === 0 ? <EmptyState title="No images" description="Upload JPG, PNG or WEBP images up to 10 MB." /> : (
        <>
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {list.data!.items.map((a) => (
              <li key={a.id}>
                <button type="button" onClick={() => open(a)} className="block w-full overflow-hidden rounded-theme border border-line bg-paper text-left hover:border-accent">
                  <img src={a.url} srcSet={a.srcSet ?? undefined} sizes="200px" alt={a.altText ?? a.fileName} loading="lazy" className="aspect-square w-full object-cover" />
                  <div className="p-2"><p className="truncate text-xs">{a.fileName}</p><p className="text-[0.68rem] text-muted">{a.width}×{a.height} · {a.isInUse ? 'In use' : 'Unused'}</p></div>
                </button>
              </li>
            ))}
          </ul>
          <Pagination page={page} totalPages={list.data!.totalPages} onChange={setPage} />
        </>
      )}
      <Modal open={selected !== null} onClose={() => setSelected(null)} title="Image details" size="lg"
        footer={selected && <>
          <Button variant="ghost" className="mr-auto text-danger" disabled={selected.isInUse} title={selected.isInUse ? 'Remove it from products/pages first' : undefined}
            onClick={async () => { if (await confirm({ title: 'Delete image?', message: 'This permanently removes the file.', danger: true, confirmLabel: 'Delete' })) remove.mutate(selected.id); }}>Delete</Button>
          <Button variant="outline" icon={<Copy className="h-4 w-4" />} onClick={async () => { await navigator.clipboard.writeText(new URL(selected.url, window.location.origin).href); toast.success('URL copied'); }}>Copy URL</Button>
          <Button loading={update.isPending} onClick={() => update.mutate()}>Save details</Button>
        </>}>
        {selected && (
          <div className="grid gap-6 md:grid-cols-2">
            <img src={selected.url} alt={selected.altText ?? ''} className="w-full rounded-theme border border-line" />
            <div className="space-y-4 text-sm">
              <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-muted">
                <dt>File</dt><dd className="text-ink">{selected.fileName}</dd>
                <dt>Size</dt><dd className="text-ink">{selected.width}×{selected.height} · {(selected.sizeBytes / 1024).toFixed(0)} KB</dd>
                <dt>Folder</dt><dd className="text-ink">{selected.folder}</dd>
                <dt>Uploaded</dt><dd className="text-ink">{formatDate(selected.createdAt)}</dd>
                <dt>Status</dt><dd className="text-ink">{selected.isInUse ? 'In use on the site' : 'Not used'}</dd>
              </dl>
              <Field label="Alt text" hint="Describe the image for screen readers and search engines.">{(p) => <Input {...p} value={alt} onChange={(e) => setAlt(e.target.value)} maxLength={300} />}</Field>
              <Field label="Title">{(p) => <Input {...p} value={title} onChange={(e) => setTitle(e.target.value)} maxLength={200} />}</Field>
            </div>
          </div>
        )}
      </Modal>
      <Panel className="mt-8" title="About image storage"><p className="text-sm text-muted">Images are stored with the configured provider (Cloudinary in production, local disk in development). Only URLs and metadata are saved in the database.</p></Panel>
    </AdminPage>
  );
}
