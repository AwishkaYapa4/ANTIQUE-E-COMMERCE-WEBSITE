import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useFieldArray, useForm, type FieldPath } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Copy, ExternalLink, ImagePlus, MoreHorizontal, Plus, Star, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { ApiError, errorMessage } from '@/services/http';
import type { AdminProduct, InventoryState, ProductStatus, ProductUpsert } from '@/types/api';
import { Button, ButtonLink } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select, Textarea } from '@/components/ui/Field';
import { Badge, EmptyState, ErrorState, InventoryBadge, Notice, PageSpinner } from '@/components/ui/Feedback';
import { Modal, useConfirm } from '@/components/ui/Overlay';
import { Pagination } from '@/components/ui/Misc';
import { cn, conditionLabels, formatDateTime, formatPrice } from '@/utils/format';
import { AdminPage, MediaPicker, Panel, SearchInput, TableShell, td, th } from '../components/AdminUi';
import { SortableImages, type EditableImage } from '../components/SortableImages';
import { RichTextEditor } from '../components/RichTextEditor';
import { productSchema, type ProductFormOutput, type ProductFormValues } from './productSchema';

const statusTone: Record<ProductStatus, 'success' | 'neutral'> = { Published: 'success', Draft: 'neutral', Archived: 'neutral' };

function useProductActions() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
  const onError = (e: unknown) => toast.error(errorMessage(e));
  const status = useMutation({ mutationFn: (v: { id: number; status: ProductStatus }) => adminApi.setProductStatus(v.id, v.status), onSuccess: (p) => { refresh(); queryClient.setQueryData(['admin', 'product', p.id], p); toast.success(`“${p.name}” is now ${p.status.toLowerCase()}`); }, onError });
  const featured = useMutation({ mutationFn: (v: { id: number; on: boolean }) => adminApi.setFeatured(v.id, v.on), onSuccess: refresh, onError });
  const duplicate = useMutation({ mutationFn: adminApi.duplicateProduct, onSuccess: (p) => { refresh(); toast.success('Draft copy created'); navigate(`/admin/products/${p.id}`); }, onError });
  const remove = useMutation({ mutationFn: adminApi.deleteProduct, onSuccess: (r) => { refresh(); toast.success(r.message); }, onError });
  const askDelete = async (id: number, name: string, hasOrders?: boolean) => {
    const ok = await confirm({
      title: hasOrders ? 'Archive this product?' : 'Delete this product?',
      message: hasOrders ? `“${name}” appears in past orders, so it will be archived (hidden from the shop) to keep order history accurate.` : `“${name}” will be permanently deleted. If it has ever been ordered it will be archived instead.`,
      confirmLabel: hasOrders ? 'Archive' : 'Delete', danger: true,
    });
    if (ok) return remove.mutateAsync(id);
  };
  return { status, featured, duplicate, remove, askDelete };
}

export function ProductListPage() {
  const [params, setParams] = useSearchParams();
  const page = Number(params.get('page') ?? 1);
  const q = params.get('q') ?? '';
  const status = (params.get('status') || undefined) as ProductStatus | undefined;
  const inventory = (params.get('inventory') || undefined) as InventoryState | undefined;
  const categoryId = params.get('categoryId') ? Number(params.get('categoryId')) : undefined;
  const set = (k: string, v: string | undefined) => setParams((p) => { const n = new URLSearchParams(p); if (v) n.set(k, v); else n.delete(k); if (k !== 'page') n.delete('page'); return n; });
  const categories = useQuery({ queryKey: ['admin', 'categories'], queryFn: adminApi.categories });
  const list = useQuery({ queryKey: ['admin', 'products', { q, status, inventory, categoryId, page }], queryFn: () => adminApi.products({ q, status, inventory, categoryId, page, pageSize: 25 }), placeholderData: keepPreviousData });
  const actions = useProductActions();
  const [menu, setMenu] = useState<number | null>(null);

  return (
    <AdminPage title="Products" description="Every antique in your catalogue, with publication and inventory status." actions={<ButtonLink to="/admin/products/new"><Plus className="h-4 w-4" /> Add product</ButtonLink>}>
      <div className="mb-4 flex flex-wrap gap-2">
        <SearchInput value={q} onChange={(v) => set('q', v || undefined)} placeholder="Search name or SKU" />
        <Select aria-label="Status" value={status ?? ''} onChange={(e) => set('status', e.target.value || undefined)} className="w-auto py-2 text-sm">
          <option value="">All statuses</option><option value="Published">Published</option><option value="Draft">Draft</option><option value="Archived">Archived</option>
        </Select>
        <Select aria-label="Inventory" value={inventory ?? ''} onChange={(e) => set('inventory', e.target.value || undefined)} className="w-auto py-2 text-sm">
          <option value="">All inventory</option><option value="Available">Available</option><option value="Reserved">Reserved</option><option value="Sold">Sold</option>
        </Select>
        <Select aria-label="Category" value={categoryId ?? ''} onChange={(e) => set('categoryId', e.target.value || undefined)} className="w-auto py-2 text-sm">
          <option value="">All categories</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </Select>
      </div>
      <TableShell loading={list.isPending} error={list.error} onRetry={() => list.refetch()}
        empty={list.data?.items.length === 0 ? <EmptyState icon={<ImagePlus className="h-8 w-8" />} title="No products found" description={q || status ? 'Try different filters.' : 'Add your first antique to get started.'} action={<ButtonLink to="/admin/products/new">Add product</ButtonLink>} /> : undefined}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>Product</th><th className={th}>Status</th><th className={th}>Inventory</th><th className={`${th} text-right`}>Price</th><th className={th}>Updated</th><th className={th}><span className="sr-only">Actions</span></th></tr></thead>
          <tbody className="divide-y divide-line">
            {list.data?.items.map((p) => (
              <tr key={p.id} className="hover:bg-bg">
                <td className={td}>
                  <div className="flex items-center gap-3">
                    {p.imageUrl ? <img src={p.imageUrl} alt="" className="h-12 w-10 rounded-theme object-cover" loading="lazy" /> : <div className="h-12 w-10 rounded-theme bg-surface" />}
                    <div className="min-w-0">
                      <Link to={`/admin/products/${p.id}`} className="font-medium hover:text-accent">{p.name}</Link>
                      <p className="text-xs text-muted"><span className="font-mono">{p.sku}</span>{p.categoryName && ` · ${p.categoryName}`}{p.isFeatured && <Star className="ml-1 inline h-3 w-3 fill-accent text-accent" aria-label="Featured" />}</p>
                    </div>
                  </div>
                </td>
                <td className={td}><Badge tone={statusTone[p.status]}>{p.status}</Badge></td>
                <td className={td}><InventoryBadge state={p.inventoryState} /><p className="mt-1 text-xs text-muted">{p.quantityAvailable} avail · {p.quantityReserved} held · {p.quantitySold} sold</p></td>
                <td className={`${td} text-right whitespace-nowrap`}>{formatPrice(p.price)}</td>
                <td className={`${td} text-xs text-muted whitespace-nowrap`}>{formatDateTime(p.updatedAt)}</td>
                <td className={`${td} relative text-right`}>
                  <Button variant="ghost" size="icon" aria-label={`Actions for ${p.name}`} aria-expanded={menu === p.id} onClick={() => setMenu(menu === p.id ? null : p.id)}><MoreHorizontal className="h-4 w-4" /></Button>
                  {menu === p.id && (
                    <div className="absolute right-4 z-20 mt-1 w-48 rounded-theme border border-line bg-paper py-1 text-left shadow-lg" onMouseLeave={() => setMenu(null)}>
                      <Link to={`/admin/products/${p.id}`} className="block px-4 py-2 text-sm hover:bg-surface">Edit</Link>
                      {p.status !== 'Published' && <button className="block w-full px-4 py-2 text-left text-sm hover:bg-surface" onClick={() => { actions.status.mutate({ id: p.id, status: 'Published' }); setMenu(null); }}>Publish</button>}
                      {p.status === 'Published' && <button className="block w-full px-4 py-2 text-left text-sm hover:bg-surface" onClick={() => { actions.status.mutate({ id: p.id, status: 'Draft' }); setMenu(null); }}>Unpublish</button>}
                      <button className="block w-full px-4 py-2 text-left text-sm hover:bg-surface" onClick={() => { actions.featured.mutate({ id: p.id, on: !p.isFeatured }); setMenu(null); }}>{p.isFeatured ? 'Remove from featured' : 'Mark as featured'}</button>
                      <button className="block w-full px-4 py-2 text-left text-sm hover:bg-surface" onClick={() => { actions.duplicate.mutate(p.id); setMenu(null); }}>Duplicate as draft</button>
                      {p.status === 'Published' && <a href={`/products/${p.slug}`} target="_blank" rel="noopener noreferrer" className="block px-4 py-2 text-sm hover:bg-surface">View in shop</a>}
                      <button className="block w-full px-4 py-2 text-left text-sm text-danger hover:bg-surface" onClick={() => { setMenu(null); actions.askDelete(p.id, p.name); }}>Delete / archive</button>
                    </div>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      {list.data && <Pagination page={page} totalPages={list.data.totalPages} onChange={(n) => set('page', String(n))} />}
    </AdminPage>
  );
}

const emptyValues: ProductFormValues = {
  name: '', slug: '', sku: '', shortDescription: '', description: '', price: NaN, compareAtPrice: NaN, categoryId: '', collectionIds: [],
  condition: 'Good', conditionNotes: '', signsOfWear: '', scratches: '', damage: '', restorationHistory: '', missingParts: '', functionalStatus: '',
  material: '', color: '', dimensions: '', weightKg: NaN, era: '', approximateAge: '', provenance: '', hasProvenanceEvidence: false, provenanceEvidence: '',
  initialQuantity: 1, maxPerOrder: 1, status: 'Draft', isFeatured: false, isDeliveryAvailable: true, showWhenSold: true, seoTitle: '', seoDescription: '', attributes: [],
};

function toFormValues(p: AdminProduct): ProductFormValues {
  const s = (v: string | null) => v ?? '';
  return {
    name: p.name, slug: p.slug, sku: p.sku, shortDescription: s(p.shortDescription), description: s(p.description), price: p.price,
    compareAtPrice: p.compareAtPrice ?? NaN, categoryId: p.categoryId ? String(p.categoryId) : '', collectionIds: p.collectionIds, condition: p.condition,
    conditionNotes: s(p.conditionNotes), signsOfWear: s(p.signsOfWear), scratches: s(p.scratches), damage: s(p.damage), restorationHistory: s(p.restorationHistory),
    missingParts: s(p.missingParts), functionalStatus: s(p.functionalStatus), material: s(p.material), color: s(p.color), dimensions: s(p.dimensions),
    weightKg: p.weightKg ?? NaN, era: s(p.era), approximateAge: s(p.approximateAge), provenance: s(p.provenance), hasProvenanceEvidence: p.hasProvenanceEvidence,
    provenanceEvidence: s(p.provenanceEvidence), initialQuantity: p.quantityAvailable, maxPerOrder: p.maxPerOrder, status: p.status, isFeatured: p.isFeatured,
    isDeliveryAvailable: p.isDeliveryAvailable, showWhenSold: p.showWhenSold, seoTitle: s(p.seoTitle), seoDescription: s(p.seoDescription), attributes: p.attributes,
  };
}

const nullIfEmpty = (v: string | undefined) => (v && v.trim() ? v.trim() : null);

function StockPanel({ product }: { product: AdminProduct }) {
  const queryClient = useQueryClient();
  const [adjusting, setAdjusting] = useState(false);
  const [qty, setQty] = useState(String(product.quantityAvailable));
  const [reason, setReason] = useState('');
  const confirm = useConfirm();
  const movements = useQuery({ queryKey: ['admin', 'movements', product.id], queryFn: () => adminApi.movements(product.id) });
  const update = (p: AdminProduct) => { queryClient.setQueryData(['admin', 'product', p.id], p); queryClient.invalidateQueries({ queryKey: ['admin', 'movements', p.id] }); queryClient.invalidateQueries({ queryKey: ['admin', 'products'] }); };
  const adjust = useMutation({ mutationFn: () => adminApi.adjustStock(product.id, Number(qty), reason), onSuccess: (p) => { update(p); setAdjusting(false); setReason(''); toast.success('Stock updated'); }, onError: (e) => toast.error(errorMessage(e)) });
  const markSold = useMutation({ mutationFn: () => adminApi.markSold(product.id, 'Sold in store'), onSuccess: (p) => { update(p); toast.success('Marked as sold'); }, onError: (e) => toast.error(errorMessage(e)) });

  return (
    <Panel title="Inventory" actions={<InventoryBadge state={product.inventoryState} />}>
      <dl className="grid grid-cols-3 gap-3 text-center">
        {[['Available', product.quantityAvailable], ['Reserved', product.quantityReserved], ['Sold', product.quantitySold]].map(([l, v]) => (
          <div key={l} className="rounded-theme bg-bg p-3"><dt className="text-xs text-muted">{l}</dt><dd className="font-heading text-2xl">{v}</dd></div>
        ))}
      </dl>
      <p className="mt-3 text-xs text-muted">Reserved units are held by customers at checkout and are released automatically if they don't complete their order.</p>
      <div className="mt-4 flex flex-wrap gap-2">
        <Button size="sm" variant="outline" onClick={() => setAdjusting(true)}>Correct stock</Button>
        <Button size="sm" variant="outline" disabled={product.quantityAvailable === 0} loading={markSold.isPending}
          onClick={async () => { if (await confirm({ title: 'Mark as sold?', message: 'Use this when the piece was sold outside the website (e.g. in your shop). It will show as Sold.', confirmLabel: 'Mark sold' })) markSold.mutate(); }}>Mark sold (offline sale)</Button>
      </div>
      <h3 className="mt-6 mb-2 text-xs font-semibold tracking-wider text-muted uppercase">History</h3>
      <ul className="max-h-60 divide-y divide-line overflow-y-auto text-xs">
        {movements.data?.map((m) => (
          <li key={m.id} className="flex justify-between gap-3 py-2">
            <span>{m.type.replace(/([A-Z])/g, ' $1').trim()}{m.orderNumber && <> · <span className="font-mono">{m.orderNumber}</span></>}{m.reason && <span className="text-muted"> — {m.reason}</span>}</span>
            <span className="shrink-0 text-muted">{m.availableChange !== 0 && `${m.availableChange > 0 ? '+' : ''}${m.availableChange} avail · `}{formatDateTime(m.createdAt)}{m.userName && ` · ${m.userName}`}</span>
          </li>
        ))}
        {movements.data?.length === 0 && <li className="py-2 text-muted">No stock changes yet.</li>}
      </ul>
      <Modal open={adjusting} onClose={() => setAdjusting(false)} title="Correct available stock" size="sm"
        footer={<><Button variant="ghost" onClick={() => setAdjusting(false)}>Cancel</Button><Button loading={adjust.isPending} disabled={!reason.trim() || qty === ''} onClick={() => adjust.mutate()}>Save correction</Button></>}>
        <div className="space-y-4">
          <Field label="Available quantity" hint="Reserved and sold units are not affected.">{(p) => <Input {...p} type="number" min={0} value={qty} onChange={(e) => setQty(e.target.value)} />}</Field>
          <Field label="Reason" required hint="Recorded in the audit log.">{(p) => <Input {...p} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Stock count correction" />}</Field>
        </div>
      </Modal>
    </Panel>
  );
}

export function ProductEditorPage() {
  const { id } = useParams();
  const isNew = !id;
  const productId = Number(id);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const actions = useProductActions();
  const [images, setImages] = useState<EditableImage[]>([]);
  const [picker, setPicker] = useState(false);

  const product = useQuery({ queryKey: ['admin', 'product', productId], queryFn: () => adminApi.product(productId), enabled: !isNew });
  const categories = useQuery({ queryKey: ['admin', 'categories'], queryFn: adminApi.categories });
  const collections = useQuery({ queryKey: ['admin', 'collections'], queryFn: adminApi.collections });

  const form = useForm<ProductFormValues, unknown, ProductFormOutput>({ resolver: zodResolver(productSchema), defaultValues: emptyValues });
  const { register, formState: { errors, isDirty }, setError } = form;
  const attributes = useFieldArray({ control: form.control, name: 'attributes' });

  useEffect(() => {
    if (!product.data) return;
    form.reset(toFormValues(product.data));
    setImages(product.data.images.map((i) => ({ key: `img-${i.id}`, mediaAssetId: i.mediaAssetId, url: i.url, altText: i.altText ?? '' })));
  }, [product.data, form]);

  const save = useMutation({
    mutationFn: (v: ProductFormOutput) => {
      const body: ProductUpsert = {
        ...v, slug: nullIfEmpty(v.slug), shortDescription: nullIfEmpty(v.shortDescription), description: nullIfEmpty(v.description),
        compareAtPrice: v.compareAtPrice && v.compareAtPrice > 0 ? v.compareAtPrice : null, categoryId: v.categoryId ? Number(v.categoryId) : null,
        conditionNotes: nullIfEmpty(v.conditionNotes), signsOfWear: nullIfEmpty(v.signsOfWear), scratches: nullIfEmpty(v.scratches), damage: nullIfEmpty(v.damage),
        restorationHistory: nullIfEmpty(v.restorationHistory), missingParts: nullIfEmpty(v.missingParts), functionalStatus: nullIfEmpty(v.functionalStatus),
        material: nullIfEmpty(v.material), color: nullIfEmpty(v.color), dimensions: nullIfEmpty(v.dimensions), era: nullIfEmpty(v.era),
        approximateAge: nullIfEmpty(v.approximateAge), provenance: nullIfEmpty(v.provenance), provenanceEvidence: nullIfEmpty(v.provenanceEvidence),
        seoTitle: nullIfEmpty(v.seoTitle), seoDescription: nullIfEmpty(v.seoDescription),
        attributes: v.attributes.filter((a) => a.name && a.value),
        images: images.map((i) => ({ mediaAssetId: i.mediaAssetId, url: i.mediaAssetId ? null : i.url, altText: i.altText || null })),
        version: product.data?.version ?? null,
      };
      return isNew ? adminApi.createProduct(body) : adminApi.updateProduct(productId, body);
    },
    onSuccess: (p) => {
      queryClient.setQueryData(['admin', 'product', p.id], p);
      queryClient.invalidateQueries({ queryKey: ['admin', 'products'] });
      toast.success(isNew ? 'Product created' : 'Changes saved');
      if (isNew) navigate(`/admin/products/${p.id}`, { replace: true });
    },
    onError: (e) => {
      if (e instanceof ApiError) Object.entries(e.fieldErrors).forEach(([k, v]) => setError(k as FieldPath<ProductFormValues>, { message: v[0] }));
      toast.error(errorMessage(e));
    },
  });

  if (!isNew && product.isPending) return <PageSpinner />;
  if (!isNew && product.isError) return <ErrorState message={errorMessage(product.error)} onRetry={() => product.refetch()} />;
  const p = product.data;
  const status = form.watch('status');
  const hasEvidence = form.watch('hasProvenanceEvidence');
  const selectedCollections = form.watch('collectionIds');

  const submit = form.handleSubmit((v) => {
    if (v.status === 'Published' && images.length === 0) { toast.error('Add at least one photo before publishing.'); return; }
    save.mutate(v);
  }, () => toast.error('Please fix the highlighted fields.'));

  const text = (name: FieldPath<ProductFormValues>, label: string, opts: { required?: boolean; hint?: string; type?: string; className?: string } = {}) => (
    <Field label={label} required={opts.required} hint={opts.hint} error={(errors as Record<string, { message?: string }>)[name]?.message} className={opts.className}>
      {(fp) => <Input {...fp} type={opts.type} step={opts.type === 'number' ? 'any' : undefined} {...register(name, opts.type === 'number' ? { valueAsNumber: true } : undefined)} />}
    </Field>
  );
  const area = (name: FieldPath<ProductFormValues>, label: string, hint?: string) => (
    <Field label={label} hint={hint} error={(errors as Record<string, { message?: string }>)[name]?.message}>{(fp) => <Textarea {...fp} rows={2} {...register(name)} />}</Field>
  );

  return (
    <AdminPage
      title={isNew ? 'Add product' : p?.name ?? 'Edit product'}
      description={p && <>SKU <span className="font-mono">{p.sku}</span> · last updated {formatDateTime(p.updatedAt)}{p.hasOrders && ' · has order history'}</>}
      actions={
        <>
          {p && p.status === 'Published' && <a href={`/products/${p.slug}`} target="_blank" rel="noopener noreferrer" className="inline-flex items-center gap-1 px-3 text-sm link-underline">View <ExternalLink className="h-3.5 w-3.5" /></a>}
          {p && <Button variant="ghost" size="sm" icon={<Copy className="h-4 w-4" />} onClick={() => actions.duplicate.mutate(p.id)}>Duplicate</Button>}
          {p && <Button variant="ghost" size="sm" icon={<Trash2 className="h-4 w-4" />} className="text-danger" onClick={async () => { const r = await actions.askDelete(p.id, p.name, p.hasOrders); if (r?.deleted) navigate('/admin/products'); }}>{p.hasOrders ? 'Archive' : 'Delete'}</Button>}
          <Button onClick={submit} loading={save.isPending}>{isNew ? 'Create product' : 'Save changes'}</Button>
        </>
      }
    >
      <form onSubmit={submit} noValidate className="grid gap-6 xl:grid-cols-[1fr_340px]">
        <div className="space-y-6">
          <Panel title="Basic information">
            <div className="space-y-4">
              {text('name', 'Product name', { required: true })}
              <div className="grid gap-4 sm:grid-cols-2">
                {text('sku', 'SKU', { required: true, hint: 'Your unique stock code, e.g. FUR-0123.' })}
                {text('slug', 'URL slug', { hint: 'Leave empty to generate from the name.' })}
              </div>
              <Field label="Short description" hint="Shown in quick view and search results (max 500)." error={errors.shortDescription?.message}>{(fp) => <Textarea {...fp} rows={2} {...register('shortDescription')} />}</Field>
              <div>
                <p className="mb-1.5 text-sm font-medium">Detailed description</p>
                <RichTextEditor value={form.watch('description') ?? ''} onChange={(html) => form.setValue('description', html, { shouldDirty: true })} />
              </div>
            </div>
          </Panel>

          <Panel title="Photos" actions={<Button size="sm" variant="outline" icon={<ImagePlus className="h-4 w-4" />} onClick={() => setPicker(true)}>Add photos</Button>}>
            {images.length === 0 ? <p className="text-sm text-muted">No photos yet. Good photography sells antiques — add several angles and close-ups of any marks or damage.</p> : <SortableImages images={images} onChange={setImages} />}
            <p className="mt-3 text-xs text-muted">Drag to reorder. The first photo is the main image. Alt text helps screen-reader users and search engines.</p>
            <MediaPicker open={picker} onClose={() => setPicker(false)} folder="products" multiple onSelect={(assets) => setImages((cur) => [
              ...cur, ...assets.filter((a) => !cur.some((c) => c.mediaAssetId === a.id)).map((a) => ({ key: `asset-${a.id}-${Date.now()}`, mediaAssetId: a.id, url: a.url, altText: a.altText ?? '' })),
            ].slice(0, 20))} />
          </Panel>

          <Panel title="Condition report">
            <div className="space-y-4">
              <Field label="Overall condition" required>{(fp) => (
                <Select {...fp} {...register('condition')}>{Object.entries(conditionLabels).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>
              )}</Field>
              {area('conditionNotes', 'Condition notes', 'A short honest summary of the piece’s condition.')}
              <div className="grid gap-4 md:grid-cols-2">
                {area('signsOfWear', 'Signs of wear')}
                {area('scratches', 'Scratches')}
                {area('damage', 'Damage')}
                {area('restorationHistory', 'Restoration history')}
                {area('missingParts', 'Missing parts')}
                {area('functionalStatus', 'Functional status', 'For clocks, lamps, mechanisms.')}
              </div>
            </div>
          </Panel>

          <Panel title="Details">
            <div className="grid gap-4 sm:grid-cols-2">
              {text('era', 'Era / period', { hint: 'e.g. Victorian, Colonial, Art Deco' })}
              {text('approximateAge', 'Approximate age', { hint: 'e.g. Late 19th century' })}
              {text('material', 'Material')}
              {text('color', 'Colour')}
              {text('dimensions', 'Dimensions', { hint: 'e.g. W 66 × D 45 × H 92 cm' })}
              {text('weightKg', 'Weight (kg)', { type: 'number' })}
            </div>
            <div className="mt-5">
              <p className="mb-2 text-sm font-medium">Additional attributes</p>
              <ul className="space-y-2">
                {attributes.fields.map((f, i) => (
                  <li key={f.id} className="flex gap-2">
                    <Input placeholder="Name (e.g. Maker)" aria-label="Attribute name" {...register(`attributes.${i}.name`)} />
                    <Input placeholder="Value" aria-label="Attribute value" {...register(`attributes.${i}.value`)} />
                    <Button variant="ghost" size="icon" aria-label="Remove attribute" onClick={() => attributes.remove(i)}><Trash2 className="h-4 w-4" /></Button>
                  </li>
                ))}
              </ul>
              <Button size="sm" variant="ghost" className="mt-2" icon={<Plus className="h-4 w-4" />} onClick={() => attributes.append({ name: '', value: '' })}>Add attribute</Button>
            </div>
          </Panel>

          <Panel title="History & provenance">
            <div className="space-y-4">
              {area('provenance', 'Provenance / item history', 'Only what you know. Leave empty if unknown.')}
              <Checkbox label="I hold documentation supporting this provenance" description="Only tick this if you have evidence (receipts, certificates, letters). The shop then shows “Documentation held”." {...register('hasProvenanceEvidence')} />
              {hasEvidence && area('provenanceEvidence', 'Describe the documentation')}
              <Notice>The shop never describes items as authenticated or original unless you record supporting documentation here.</Notice>
            </div>
          </Panel>

          <Panel title="Search engine listing">
            <div className="space-y-4">
              {text('seoTitle', 'SEO title', { hint: 'Defaults to the product name.' })}
              <Field label="SEO description" hint="Defaults to the short description." error={errors.seoDescription?.message}>{(fp) => <Textarea {...fp} rows={2} {...register('seoDescription')} />}</Field>
            </div>
          </Panel>
        </div>

        <div className="space-y-6">
          <Panel title="Publishing">
            <div className="space-y-4">
              <Field label="Status">{(fp) => (
                <Select {...fp} {...register('status')}><option value="Draft">Draft (hidden)</option><option value="Published">Published (visible in shop)</option><option value="Archived">Archived</option></Select>
              )}</Field>
              {status === 'Published' && images.length === 0 && <p className="text-xs text-danger">Add at least one photo to publish.</p>}
              <Checkbox label="Featured" description="Shown in the homepage Featured section." {...register('isFeatured')} />
              <Checkbox label="Keep visible after it sells" description="Shows with a Sold badge (good for showcasing your range)." {...register('showWhenSold')} />
              <Checkbox label="Delivery available" description="Untick for pickup-only items (large or fragile)." {...register('isDeliveryAvailable')} />
            </div>
          </Panel>

          <Panel title="Pricing (LKR)">
            <div className="space-y-4">
              {text('price', 'Price', { required: true, type: 'number' })}
              {text('compareAtPrice', 'Compare-at price', { type: 'number', hint: 'Optional. Higher original price to show a sale.' })}
            </div>
          </Panel>

          {isNew ? (
            <Panel title="Stock">
              <div className="grid grid-cols-2 gap-4">
                {text('initialQuantity', 'Quantity', { type: 'number', hint: 'Usually 1.' })}
                {text('maxPerOrder', 'Max per order', { type: 'number' })}
              </div>
            </Panel>
          ) : p && (
            <>
              <StockPanel product={p} />
              <Panel title="Order limit">{text('maxPerOrder', 'Max per order', { type: 'number' })}</Panel>
            </>
          )}

          <Panel title="Organisation">
            <div className="space-y-4">
              <Field label="Category">{(fp) => (
                <Select {...fp} {...register('categoryId')}><option value="">Uncategorised</option>{categories.data?.map((c) => <option key={c.id} value={c.id}>{c.parentId ? '— ' : ''}{c.name}</option>)}</Select>
              )}</Field>
              <fieldset>
                <legend className="mb-2 text-sm font-medium">Collections</legend>
                <div className="space-y-2">
                  {collections.data?.map((c) => (
                    <Checkbox key={c.id} label={c.name} checked={selectedCollections.includes(c.id)}
                      onChange={() => form.setValue('collectionIds', selectedCollections.includes(c.id) ? selectedCollections.filter((x) => x !== c.id) : [...selectedCollections, c.id], { shouldDirty: true })} />
                  ))}
                  {collections.data?.length === 0 && <p className="text-xs text-muted">No collections yet. <Link to="/admin/collections" className="link-underline">Create one</Link>.</p>}
                </div>
              </fieldset>
            </div>
          </Panel>
          {isDirty && <p className={cn('text-xs text-accent')}>You have unsaved changes.</p>}
        </div>
      </form>
    </AdminPage>
  );
}

export function InventoryPage() {
  const reservations = useQuery({ queryKey: ['admin', 'reservations'], queryFn: adminApi.reservations, refetchInterval: 30_000 });
  const reserved = useQuery({ queryKey: ['admin', 'products', { inventory: 'Reserved' }], queryFn: () => adminApi.products({ inventory: 'Reserved', pageSize: 50 }) });
  const sold = useQuery({ queryKey: ['admin', 'products', { inventory: 'Sold' }], queryFn: () => adminApi.products({ inventory: 'Sold', pageSize: 10, sort: 'updated' }) });
  return (
    <AdminPage title="Inventory" description="Live view of checkout holds and recently sold pieces. Stock corrections are made on each product and logged.">
      <div className="grid gap-6 lg:grid-cols-2">
        <Panel title="Active checkout reservations">
          {reservations.isPending ? <PageSpinner /> : (reservations.data?.length ?? 0) === 0 ? <p className="text-sm text-muted">No items are currently held at checkout.</p> : (
            <ul className="divide-y divide-line text-sm">
              {reservations.data!.map((r) => (
                <li key={r.id} className="flex justify-between gap-3 py-2"><Link to={`/admin/products/${r.productId}`} className="link-underline">{r.productName}</Link><span className="text-muted">{r.customerEmail} · until {formatDateTime(r.expiresAt)}</span></li>
              ))}
            </ul>
          )}
          <p className="mt-3 text-xs text-muted">Holds expire automatically and the item returns to sale.</p>
        </Panel>
        <Panel title="Reserved products">
          {(reserved.data?.items.length ?? 0) === 0 ? <p className="text-sm text-muted">None.</p> : (
            <ul className="divide-y divide-line text-sm">{reserved.data!.items.map((p) => <li key={p.id} className="py-2"><Link to={`/admin/products/${p.id}`} className="link-underline">{p.name}</Link></li>)}</ul>
          )}
        </Panel>
        <Panel title="Recently sold" className="lg:col-span-2" actions={<Link to="/admin/products?inventory=Sold" className="text-sm link-underline">All sold</Link>}>
          {(sold.data?.items.length ?? 0) === 0 ? <p className="text-sm text-muted">Nothing sold yet.</p> : (
            <ul className="divide-y divide-line text-sm">{sold.data!.items.map((p) => <li key={p.id} className="flex justify-between py-2"><Link to={`/admin/products/${p.id}`} className="link-underline">{p.name}</Link><span>{formatPrice(p.price)}</span></li>)}</ul>
          )}
        </Panel>
      </div>
    </AdminPage>
  );
}
