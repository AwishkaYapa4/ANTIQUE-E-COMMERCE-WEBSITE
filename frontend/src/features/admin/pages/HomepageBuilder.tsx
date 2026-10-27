import { useEffect, useState } from 'react';
import { DndContext, closestCenter, KeyboardSensor, PointerSensor, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core';
import { arrayMove, SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, EyeOff, GripVertical, Pencil, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { ApiError, errorMessage } from '@/services/http';
import type { HomepageSectionDto, SectionType } from '@/types/api';
import { defaultSectionConfig, sectionLabels } from '@/types/sections';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select, Textarea } from '@/components/ui/Field';
import { ErrorState, Notice, PageSpinner } from '@/components/ui/Feedback';
import { Modal, useConfirm } from '@/components/ui/Overlay';
import { AdminPage, ImageField, SearchInput } from '../components/AdminUi';
import { PublishBar } from '../components/PublishBar';
import { RichTextEditor } from '../components/RichTextEditor';
import { useCustomization } from './CustomizationPages';

type Config = Record<string, unknown>;
interface DraftSection { key: string | null; localId: string; type: SectionType; isVisible: boolean; config: Config }

const str = (c: Config, k: string) => (c[k] as string | null | undefined) ?? '';
const summary = (s: DraftSection) => str(s.config, 'title') || str(s.config, 'heading') || (s.type === 'trustFeatures' ? 'Trust features' : sectionLabels[s.type]);

function SortableRow({ section, onEdit, onToggle, onRemove }: { section: DraftSection; onEdit: () => void; onToggle: () => void; onRemove: () => void }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: section.localId });
  return (
    <li ref={setNodeRef} style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`flex items-center gap-3 rounded-theme border bg-paper px-3 py-3 ${isDragging ? 'z-10 border-accent shadow-lg' : 'border-line'} ${section.isVisible ? '' : 'opacity-60'}`}>
      <button type="button" className="cursor-grab rounded-theme p-1 text-muted hover:bg-surface active:cursor-grabbing" aria-label={`Drag to reorder ${summary(section)}`} {...attributes} {...listeners}>
        <GripVertical className="h-5 w-5" />
      </button>
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{summary(section)}</p>
        <p className="text-xs text-muted">{sectionLabels[section.type]}{!section.isVisible && ' · hidden'}</p>
      </div>
      <Button variant="ghost" size="icon" aria-label={section.isVisible ? 'Hide section' : 'Show section'} onClick={onToggle}>{section.isVisible ? <Eye className="h-4 w-4" /> : <EyeOff className="h-4 w-4" />}</Button>
      <Button variant="ghost" size="icon" aria-label="Edit section" onClick={onEdit}><Pencil className="h-4 w-4" /></Button>
      <Button variant="ghost" size="icon" aria-label="Remove section" onClick={onRemove}><Trash2 className="h-4 w-4" /></Button>
    </li>
  );
}

function CtaEditor({ label, value, onChange }: { label: string; value: { label: string; href: string } | null; onChange: (v: { label: string; href: string } | null) => void }) {
  return (
    <fieldset className="rounded-theme border border-line p-3">
      <legend className="px-1 text-sm font-medium">{label}</legend>
      <Checkbox label="Show button" checked={value !== null} onChange={(e) => onChange(e.target.checked ? { label: 'Shop now', href: '/shop' } : null)} />
      {value && (
        <div className="mt-3 grid gap-3 sm:grid-cols-2">
          <Field label="Button text">{(p) => <Input {...p} value={value.label} maxLength={40} onChange={(e) => onChange({ ...value, label: e.target.value })} />}</Field>
          <Field label="Link" hint="/shop, /pages/about or https://…">{(p) => <Input {...p} value={value.href} onChange={(e) => onChange({ ...value, href: e.target.value })} />}</Field>
        </div>
      )}
    </fieldset>
  );
}

function ProductPicker({ value, onChange }: { value: number[] | null; onChange: (v: number[] | null) => void }) {
  const [q, setQ] = useState('');
  const results = useQuery({ queryKey: ['admin', 'products', 'picker', q], queryFn: () => adminApi.products({ q, status: 'Published', pageSize: 12 }) });
  const ids = value ?? [];
  const chosen = useQuery({ queryKey: ['admin', 'products', 'chosen', ids], queryFn: () => Promise.all(ids.map((id) => adminApi.product(id))), enabled: ids.length > 0 });
  return (
    <div className="space-y-3">
      <Checkbox label="Choose specific products" description="Otherwise products marked “Featured” are shown automatically." checked={value !== null} onChange={(e) => onChange(e.target.checked ? [] : null)} />
      {value !== null && (
        <>
          <ul className="flex flex-wrap gap-2">
            {chosen.data?.map((p) => (
              <li key={p.id} className="inline-flex items-center gap-2 rounded-full border border-line bg-bg py-1 pr-1 pl-3 text-xs">{p.name}
                <button type="button" aria-label={`Remove ${p.name}`} onClick={() => onChange(ids.filter((x) => x !== p.id))} className="rounded-full p-1 hover:bg-surface">×</button></li>
            ))}
          </ul>
          <SearchInput value={q} onChange={setQ} placeholder="Search published products" />
          <ul className="max-h-48 divide-y divide-line overflow-y-auto rounded-theme border border-line">
            {results.data?.items.filter((p) => !ids.includes(p.id)).map((p) => (
              <li key={p.id}><button type="button" className="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-surface" onClick={() => onChange([...ids, p.id])}><Plus className="h-3.5 w-3.5" /> {p.name}</button></li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

function ListEditor<T extends Record<string, string | null>>({ items, onChange, fields, blank, max }: {
  items: T[]; onChange: (v: T[]) => void; blank: T; max: number;
  fields: { key: keyof T & string; label: string; kind?: 'image' | 'select'; options?: string[] }[];
}) {
  return (
    <div className="space-y-3">
      {items.map((item, i) => (
        <div key={i} className="rounded-theme border border-line p-3">
          <div className="grid gap-3 sm:grid-cols-2">
            {fields.map((f) => f.kind === 'image'
              ? <ImageField key={f.key} label={f.label} value={item[f.key]} folder="homepage" onChange={(u) => onChange(items.map((x, j) => (j === i ? { ...x, [f.key]: u } : x)))} />
              : f.kind === 'select'
                ? <Field key={f.key} label={f.label}>{(p) => <Select {...p} value={item[f.key] ?? ''} onChange={(e) => onChange(items.map((x, j) => (j === i ? { ...x, [f.key]: e.target.value } : x)))}>{f.options!.map((o) => <option key={o}>{o}</option>)}</Select>}</Field>
                : <Field key={f.key} label={f.label}>{(p) => <Input {...p} value={item[f.key] ?? ''} onChange={(e) => onChange(items.map((x, j) => (j === i ? { ...x, [f.key]: e.target.value } : x)))} />}</Field>)}
          </div>
          <Button variant="ghost" size="sm" className="mt-2 text-danger" onClick={() => onChange(items.filter((_, j) => j !== i))}>Remove</Button>
        </div>
      ))}
      {items.length < max && <Button variant="outline" size="sm" icon={<Plus className="h-4 w-4" />} onClick={() => onChange([...items, { ...blank }])}>Add item</Button>}
    </div>
  );
}

function SectionEditor({ section, onChange }: { section: DraftSection; onChange: (config: Config) => void }) {
  const c = section.config;
  const set = (k: string, v: unknown) => onChange({ ...c, [k]: v });
  const categories = useQuery({ queryKey: ['admin', 'categories'], queryFn: adminApi.categories, enabled: section.type === 'featuredCategories' });
  const collections = useQuery({ queryKey: ['admin', 'collections'], queryFn: adminApi.collections, enabled: section.type === 'collectionProducts' });
  const text = (k: string, label: string, hint?: string, max = 150) => <Field label={label} hint={hint}>{(p) => <Input {...p} value={str(c, k)} maxLength={max} onChange={(e) => set(k, e.target.value || null)} />}</Field>;
  const area = (k: string, label: string, hint?: string, max = 3000) => <Field label={label} hint={hint}>{(p) => <Textarea {...p} rows={4} value={str(c, k)} maxLength={max} onChange={(e) => set(k, e.target.value || null)} />}</Field>;
  const select = (k: string, label: string, options: [string, string][]) => <Field label={label}>{(p) => <Select {...p} value={str(c, k)} onChange={(e) => set(k, e.target.value)}>{options.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</Select>}</Field>;
  const limit = (max: number) => <Field label="Number of items">{(p) => <Input {...p} type="number" min={1} max={max} value={Number(c.limit ?? 8)} onChange={(e) => set('limit', Math.max(1, Math.min(max, Number(e.target.value) || 1)))} />}</Field>;
  const image = (k = 'imageUrl', label = 'Image') => <ImageField label={label} value={(c[k] as string | null) ?? null} folder="homepage" onChange={(u) => set(k, u)} />;
  const cta = (k: string, label: string) => <CtaEditor label={label} value={(c[k] as { label: string; href: string } | null) ?? null} onChange={(v) => set(k, v)} />;

  switch (section.type) {
    case 'hero':
      return (
        <div className="space-y-4">
          {image('imageUrl', 'Background image (wide, at least 2000px)')}
          {text('eyebrow', 'Small text above the title', undefined, 80)}
          {text('title', 'Headline')}
          {area('subtitle', 'Supporting text', undefined, 400)}
          {cta('primaryCta', 'Main button')}
          {cta('secondaryCta', 'Second button')}
          <div className="grid gap-4 sm:grid-cols-3">
            {select('alignment', 'Text alignment', [['left', 'Left'], ['center', 'Centre'], ['right', 'Right']])}
            {select('height', 'Height', [['medium', 'Medium'], ['large', 'Large'], ['full', 'Full screen']])}
            <Field label={`Image darkening: ${Number(c.overlayOpacity ?? 35)}%`} hint="Keeps text readable.">{(p) => <input {...p} type="range" min={0} max={80} value={Number(c.overlayOpacity ?? 35)} onChange={(e) => set('overlayOpacity', Number(e.target.value))} className="w-full accent-[var(--c-primary)]" />}</Field>
          </div>
        </div>
      );
    case 'featuredCategories': {
      const ids = (c.categoryIds as number[] | null) ?? null;
      return (
        <div className="space-y-4">
          {text('title', 'Title')}{text('subtitle', 'Subtitle', undefined, 400)}{limit(12)}
          <Checkbox label="Choose specific categories" description="Otherwise categories marked “Feature on homepage” are shown." checked={ids !== null} onChange={(e) => set('categoryIds', e.target.checked ? [] : null)} />
          {ids !== null && <div className="grid gap-2 sm:grid-cols-2">{categories.data?.map((cat) => <Checkbox key={cat.id} label={cat.name} checked={ids.includes(cat.id)} onChange={() => set('categoryIds', ids.includes(cat.id) ? ids.filter((x) => x !== cat.id) : [...ids, cat.id])} />)}</div>}
        </div>
      );
    }
    case 'featuredProducts':
      return <div className="space-y-4">{text('title', 'Title')}{text('subtitle', 'Subtitle', undefined, 400)}{limit(24)}<ProductPicker value={(c.productIds as number[] | null) ?? null} onChange={(v) => set('productIds', v)} />{cta('cta', 'Link beside the title')}</div>;
    case 'newArrivals':
      return <div className="space-y-4">{text('title', 'Title')}{text('subtitle', 'Subtitle', undefined, 400)}{limit(24)}<p className="text-sm text-muted">Shows the most recently published available pieces automatically.</p>{cta('cta', 'Link beside the title')}</div>;
    case 'collectionProducts':
      return (
        <div className="space-y-4">
          {text('title', 'Title')}{text('subtitle', 'Subtitle', undefined, 400)}{limit(24)}
          <Field label="Collection">{(p) => <Select {...p} value={c.collectionId == null ? '' : String(c.collectionId)} onChange={(e) => set('collectionId', e.target.value ? Number(e.target.value) : null)}><option value="">Choose a collection…</option>{collections.data?.map((col) => <option key={col.id} value={col.id}>{col.name}</option>)}</Select>}</Field>
          {cta('cta', 'Link beside the title')}
        </div>
      );
    case 'imageText':
      return (
        <div className="space-y-4">
          {image()}{text('eyebrow', 'Small text above the heading', undefined, 80)}{text('heading', 'Heading')}
          {area('body', 'Text', 'Separate paragraphs with a blank line.')}{cta('cta', 'Button')}
          <div className="grid gap-4 sm:grid-cols-2">{select('imagePosition', 'Image position', [['left', 'Left'], ['right', 'Right']])}{select('background', 'Background', [['surface', 'Soft'], ['background', 'Page'], ['dark', 'Dark']])}</div>
        </div>
      );
    case 'promoBanner':
      return <div className="space-y-4">{image('imageUrl', 'Background image (optional)')}{text('eyebrow', 'Small text', undefined, 80)}{text('heading', 'Heading')}{area('text', 'Text', undefined, 500)}{cta('cta', 'Button')}{select('theme', 'Style', [['dark', 'Dark'], ['light', 'Light'], ['accent', 'Accent colour']])}</div>;
    case 'testimonials':
      return <div className="space-y-4">{text('title', 'Title')}{text('subtitle', 'Subtitle', undefined, 400)}{limit(12)}<Notice>Testimonials are managed under Reviews → Testimonials. The section is hidden on the site until at least one testimonial is published.</Notice></div>;
    case 'newsletter':
      return <div className="space-y-4">{text('title', 'Title')}{area('text', 'Text', undefined, 400)}{text('buttonLabel', 'Button text', undefined, 40)}</div>;
    case 'richText':
      return <div className="space-y-4">{text('title', 'Title (optional)')}<RichTextEditor value={str(c, 'html')} onChange={(html) => set('html', html)} />{select('width', 'Width', [['narrow', 'Narrow (reading width)'], ['wide', 'Wide']])}</div>;
    case 'eraShowcase':
      return (
        <div className="space-y-4">{text('title', 'Title')}{text('subtitle', 'Subtitle', undefined, 400)}
          <ListEditor items={(c.items as { label: string; imageUrl: string | null; href: string }[]) ?? []} onChange={(v) => set('items', v)} max={12}
            blank={{ label: '', imageUrl: null, href: '/shop?era=' }} fields={[{ key: 'label', label: 'Era / style' }, { key: 'href', label: 'Link' }, { key: 'imageUrl', label: 'Image', kind: 'image' }]} />
        </div>
      );
    case 'trustFeatures':
      return (
        <div className="space-y-4">{text('title', 'Title (optional)')}
          <ListEditor items={(c.items as { icon: string; title: string; text: string }[]) ?? []} onChange={(v) => set('items', v)} max={6}
            blank={{ icon: 'gem', title: '', text: '' }} fields={[{ key: 'icon', label: 'Icon', kind: 'select', options: ['gem', 'camera', 'shield', 'truck', 'search', 'heart', 'award', 'clock', 'package', 'sparkles'] }, { key: 'title', label: 'Title' }, { key: 'text', label: 'Text' }]} />
          <p className="text-xs text-muted">Only make claims you can stand behind — avoid promising authenticity you can’t document.</p>
        </div>
      );
    default:
      return null;
  }
}

let idCounter = 0;
const toDraft = (s: HomepageSectionDto): DraftSection => ({ key: s.key, localId: s.key, type: s.type, isVisible: s.isVisible, config: s.config });

export function HomepageBuilderPage() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const q = useCustomization();
  const [sections, setSections] = useState<DraftSection[]>([]);
  const [dirty, setDirty] = useState(false);
  const [editing, setEditing] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }), useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }));

  useEffect(() => {
    if (q.data && !dirty) setSections(q.data.sections.map(toDraft));
  }, [q.data, dirty]);

  const update = (next: DraftSection[]) => { setSections(next); setDirty(true); };
  const save = useMutation({
    mutationFn: () => adminApi.saveHomepage(sections.map((s) => ({ key: s.key, type: s.type, isVisible: s.isVisible, config: s.config }))),
    onSuccess: (d) => { queryClient.setQueryData(['admin', 'customization'], d); setDirty(false); toast.success('Homepage draft saved'); },
    onError: (e) => {
      const msgs = e instanceof ApiError ? Object.entries(e.fieldErrors).map(([k, v]) => {
        const idx = Number(/sections\[(\d+)\]/.exec(k)?.[1]);
        return `${Number.isNaN(idx) ? '' : `${summary(sections[idx])}: `}${v.join(' ')}`;
      }) : [];
      toast.error(msgs.length ? msgs.join('\n') : errorMessage(e));
    },
  });

  const onDragEnd = (e: DragEndEvent) => {
    if (!e.over || e.active.id === e.over.id) return;
    update(arrayMove(sections, sections.findIndex((s) => s.localId === e.active.id), sections.findIndex((s) => s.localId === e.over!.id)));
  };

  if (q.isPending) return <PageSpinner />;
  if (q.isError) return <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} />;
  const current = sections.find((s) => s.localId === editing);

  return (
    <AdminPage title="Homepage Builder" description="Drag sections to reorder, hide or edit them. Save the draft, preview, then publish."
      actions={<><Button variant="outline" icon={<Plus className="h-4 w-4" />} onClick={() => setAdding(true)}>Add section</Button><Button loading={save.isPending} disabled={!dirty} onClick={() => save.mutate()}>Save draft</Button></>}>
      <PublishBar data={q.data} />
      {dirty && <div className="mb-4"><Notice>You have unsaved changes in the builder. Save the draft before previewing or publishing.</Notice></div>}
      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={sections.map((s) => s.localId)} strategy={verticalListSortingStrategy}>
          <ol className="space-y-2" aria-label="Homepage sections">
            {sections.map((s) => (
              <SortableRow key={s.localId} section={s} onEdit={() => setEditing(s.localId)}
                onToggle={() => update(sections.map((x) => (x.localId === s.localId ? { ...x, isVisible: !x.isVisible } : x)))}
                onRemove={async () => { if (await confirm({ title: 'Remove section?', message: `“${summary(s)}” will be removed from the draft.`, danger: true, confirmLabel: 'Remove' })) update(sections.filter((x) => x.localId !== s.localId)); }} />
            ))}
          </ol>
        </SortableContext>
      </DndContext>
      {sections.length === 0 && <p className="py-10 text-center text-muted">The homepage has no sections. Add one to get started.</p>}

      <Modal open={adding} onClose={() => setAdding(false)} title="Add a section" size="lg">
        <ul className="grid gap-2 sm:grid-cols-2">
          {q.data.sectionTypes.map((t) => (
            <li key={t}><button type="button" className="w-full rounded-theme border border-line p-4 text-left hover:border-accent" onClick={() => {
              const localId = `new-${++idCounter}-${Date.now()}`;
              update([...sections, { key: null, localId, type: t, isVisible: true, config: structuredClone(defaultSectionConfig[t]) }]);
              setAdding(false);
              setEditing(localId);
            }}><span className="font-medium">{sectionLabels[t]}</span></button></li>
          ))}
        </ul>
      </Modal>

      <Modal open={Boolean(current)} onClose={() => setEditing(null)} title={current ? `Edit: ${sectionLabels[current.type]}` : ''} size="lg"
        footer={<Button onClick={() => setEditing(null)}>Done</Button>}>
        {current && <SectionEditor section={current} onChange={(config) => update(sections.map((x) => (x.localId === current.localId ? { ...x, config } : x)))} />}
      </Modal>
    </AdminPage>
  );
}
