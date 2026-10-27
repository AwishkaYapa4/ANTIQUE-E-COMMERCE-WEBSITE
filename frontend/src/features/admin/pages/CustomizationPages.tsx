import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { ApiError, errorMessage } from '@/services/http';
import type { AdminCustomization, BrandSettings, NavItem, NavigationMenu, Theme } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select, Textarea } from '@/components/ui/Field';
import { ErrorState, PageSpinner } from '@/components/ui/Feedback';
import { cn } from '@/utils/format';
import { AdminPage, ImageField, Panel } from '../components/AdminUi';
import { PublishBar } from '../components/PublishBar';

const tabs = ['Brand & contact', 'Colours & typography', 'Navigation'] as const;

export function useCustomization() {
  return useQuery({ queryKey: ['admin', 'customization'], queryFn: adminApi.customization });
}

function useSaver<T>(fn: (v: T) => Promise<AdminCustomization>, label: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (d) => { queryClient.setQueryData(['admin', 'customization'], d); toast.success(`${label} saved as draft`); },
    onError: (e) => {
      const details = e instanceof ApiError ? Object.values(e.fieldErrors).flat().slice(0, 3).join(' ') : '';
      toast.error(details || errorMessage(e));
    },
  });
}

function BrandForm({ data }: { data: AdminCustomization }) {
  const form = useForm<BrandSettings>({ defaultValues: data.brand });
  useEffect(() => form.reset(data.brand), [data.brand, form]);
  const save = useSaver(adminApi.saveBrand, 'Brand settings');
  const social = form.watch('socialLinks');
  const r = form.register;
  const submit = form.handleSubmit((v) => save.mutate({
    ...v, socialLinks: v.socialLinks.filter((s) => s.platform && s.url),
    ...Object.fromEntries(Object.entries(v).filter(([, x]) => typeof x === 'string').map(([k, x]) => [k, (x as string).trim() === '' && k !== 'storeName' ? null : x])),
  } as BrandSettings));

  return (
    <form onSubmit={submit} className="space-y-6" noValidate>
      <Panel title="Brand">
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Store name" required>{(p) => <Input {...p} {...r('storeName', { required: true })} />}</Field>
          <Field label="Tagline">{(p) => <Input {...p} {...r('tagline')} />}</Field>
          <Field label="Store description" className="md:col-span-2">{(p) => <Textarea {...p} rows={2} {...r('description')} />}</Field>
          <ImageField label="Logo (shown instead of the store name)" value={form.watch('logoUrl')} onChange={(u) => form.setValue('logoUrl', u, { shouldDirty: true })} folder="branding" />
          <ImageField label="Favicon (square, at least 64×64)" value={form.watch('faviconUrl')} onChange={(u) => form.setValue('faviconUrl', u, { shouldDirty: true })} folder="branding" />
        </div>
      </Panel>
      <Panel title="Announcement bar">
        <div className="space-y-4">
          <Checkbox label="Show the announcement bar" {...r('announcementEnabled')} />
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Text" hint="e.g. Islandwide delivery available">{(p) => <Input {...p} maxLength={300} {...r('announcementText')} />}</Field>
            <Field label="Link (optional)" hint="e.g. /shop">{(p) => <Input {...p} {...r('announcementLink')} />}</Field>
          </div>
        </div>
      </Panel>
      <Panel title="Contact information">
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Email">{(p) => <Input {...p} type="email" {...r('contactEmail')} />}</Field>
          <Field label="Phone">{(p) => <Input {...p} type="tel" {...r('contactPhone')} />}</Field>
          <Field label="WhatsApp number" hint="International format, e.g. +94 77 123 4567. Shows a WhatsApp button.">{(p) => <Input {...p} type="tel" {...r('whatsAppNumber')} />}</Field>
          <Field label="Opening hours">{(p) => <Input {...p} {...r('openingHours')} />}</Field>
          <Field label="Address" className="md:col-span-2">{(p) => <Textarea {...p} rows={2} {...r('address')} />}</Field>
          <Field label="Google Maps embed URL (optional)" className="md:col-span-2" hint="Google Maps → Share → Embed a map → copy the src URL.">{(p) => <Input {...p} {...r('mapEmbedUrl')} />}</Field>
        </div>
      </Panel>
      <Panel title="Social media">
        <ul className="space-y-2">
          {social.map((_, i) => (
            <li key={i} className="flex gap-2">
              <Select aria-label="Platform" className="w-40" {...r(`socialLinks.${i}.platform`)}>{['instagram', 'facebook', 'tiktok', 'youtube', 'pinterest', 'x'].map((p) => <option key={p} value={p}>{p}</option>)}</Select>
              <Input aria-label="Profile URL" placeholder="https://" {...r(`socialLinks.${i}.url`)} />
              <Button variant="ghost" size="icon" aria-label="Remove link" onClick={() => form.setValue('socialLinks', social.filter((__, j) => j !== i), { shouldDirty: true })}><Trash2 className="h-4 w-4" /></Button>
            </li>
          ))}
        </ul>
        <Button size="sm" variant="ghost" className="mt-2" icon={<Plus className="h-4 w-4" />} onClick={() => form.setValue('socialLinks', [...social, { platform: 'instagram', url: '' }], { shouldDirty: true })}>Add social link</Button>
      </Panel>
      <Panel title="Footer & newsletter">
        <div className="space-y-4">
          <Field label="Footer description">{(p) => <Textarea {...p} rows={2} {...r('footerDescription')} />}</Field>
          <Field label="Copyright text" hint="Shown after © and the current year.">{(p) => <Input {...p} {...r('copyrightText')} />}</Field>
          <Checkbox label="Show newsletter signup in the footer" {...r('newsletterEnabled')} />
        </div>
      </Panel>
      <Panel title="Default search engine listing">
        <div className="space-y-4">
          <Field label="Default page title">{(p) => <Input {...p} {...r('seoDefaultTitle')} />}</Field>
          <Field label="Default description">{(p) => <Textarea {...p} rows={2} {...r('seoDefaultDescription')} />}</Field>
          <ImageField label="Default social sharing image" value={form.watch('defaultOgImageUrl')} onChange={(u) => form.setValue('defaultOgImageUrl', u, { shouldDirty: true })} folder="branding" />
        </div>
      </Panel>
      <Button type="submit" loading={save.isPending}>Save draft</Button>
    </form>
  );
}

const colorFields: [keyof Theme, string, string][] = [
  ['backgroundColor', 'Background', 'Main page background'],
  ['surfaceColor', 'Secondary background', 'Panels, footer, image placeholders'],
  ['textColor', 'Text', 'Body and headings'],
  ['mutedTextColor', 'Muted text', 'Captions and hints'],
  ['primaryColor', 'Primary', 'Dark accents, badges, links'],
  ['accentColor', 'Accent', 'Gold highlights, focus rings'],
  ['secondaryColor', 'Secondary', 'Supporting accent'],
  ['borderColor', 'Borders', 'Dividers and outlines'],
  ['buttonColor', 'Button', 'Main buttons'],
  ['buttonTextColor', 'Button text', 'Text on main buttons'],
  ['successColor', 'Success', 'Confirmations, “available”'],
  ['errorColor', 'Error', 'Errors, sale prices'],
];

const defaultTheme: Theme = {
  backgroundColor: '#F8F5EF', surfaceColor: '#F0E8DC', textColor: '#29231E', mutedTextColor: '#77716B', primaryColor: '#45382B', accentColor: '#B08D57',
  secondaryColor: '#777660', borderColor: '#E5DED3', buttonColor: '#45382B', buttonTextColor: '#FFFFFF', successColor: '#47745B', errorColor: '#B84D4D',
  headingFont: 'Cormorant Garamond', bodyFont: 'Inter', baseFontSize: 16, headingScale: 125, borderRadius: 4,
};

function ThemeForm({ data }: { data: AdminCustomization }) {
  const [theme, setTheme] = useState<Theme>(data.theme);
  useEffect(() => setTheme(data.theme), [data.theme]);
  const save = useSaver(adminApi.saveTheme, 'Theme');
  const set = <K extends keyof Theme>(k: K, v: Theme[K]) => setTheme((t) => ({ ...t, [k]: v }));
  const fontsHref = `https://fonts.googleapis.com/css2?family=${theme.headingFont.replaceAll(' ', '+')}:wght@500&family=${theme.bodyFont.replaceAll(' ', '+')}&display=swap`;

  return (
    <div className="grid gap-6 xl:grid-cols-[1fr_380px]">
      <link rel="stylesheet" href={fontsHref} />
      <div className="space-y-6">
        <Panel title="Colours">
          <div className="grid gap-4 sm:grid-cols-2">
            {colorFields.map(([key, label, hint]) => (
              <Field key={key} label={label} hint={hint}>{(p) => (
                <div className="flex gap-2">
                  <input type="color" aria-label={`${label} colour picker`} value={theme[key] as string} onChange={(e) => set(key, e.target.value.toUpperCase())} className="h-10 w-12 cursor-pointer rounded-theme border border-line bg-paper p-1" />
                  <Input {...p} value={theme[key] as string} onChange={(e) => set(key, e.target.value)} maxLength={7} className="font-mono uppercase" />
                </div>
              )}</Field>
            ))}
          </div>
        </Panel>
        <Panel title="Typography & shape">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Heading font">{(p) => <Select {...p} value={theme.headingFont} onChange={(e) => set('headingFont', e.target.value)}>{data.headingFonts.map((f) => <option key={f}>{f}</option>)}</Select>}</Field>
            <Field label="Body font">{(p) => <Select {...p} value={theme.bodyFont} onChange={(e) => set('bodyFont', e.target.value)}>{data.bodyFonts.map((f) => <option key={f}>{f}</option>)}</Select>}</Field>
            <Field label={`Base font size: ${theme.baseFontSize}px`}>{(p) => <input {...p} type="range" min={14} max={20} value={theme.baseFontSize} onChange={(e) => set('baseFontSize', Number(e.target.value))} className="w-full accent-[var(--c-primary)]" />}</Field>
            <Field label={`Heading scale: ${(theme.headingScale / 100).toFixed(2)}`} hint="How much larger each heading level is.">{(p) => <input {...p} type="range" min={110} max={150} value={theme.headingScale} onChange={(e) => set('headingScale', Number(e.target.value))} className="w-full accent-[var(--c-primary)]" />}</Field>
            <Field label={`Corner radius: ${theme.borderRadius}px`}>{(p) => <input {...p} type="range" min={0} max={16} value={theme.borderRadius} onChange={(e) => set('borderRadius', Number(e.target.value))} className="w-full accent-[var(--c-primary)]" />}</Field>
          </div>
        </Panel>
        <div className="flex gap-2">
          <Button loading={save.isPending} onClick={() => save.mutate(theme)}>Save draft</Button>
          <Button variant="ghost" onClick={() => setTheme(defaultTheme)}>Reset to default palette</Button>
        </div>
      </div>
      <div className="xl:sticky xl:top-36 xl:self-start">
        <Panel title="Live preview">
          <div style={{ background: theme.backgroundColor, color: theme.textColor, fontFamily: `'${theme.bodyFont}', sans-serif`, fontSize: theme.baseFontSize, borderRadius: theme.borderRadius, borderColor: theme.borderColor }} className="border p-5">
            <p style={{ color: theme.accentColor, letterSpacing: '0.2em' }} className="text-xs uppercase">New arrival</p>
            <h3 style={{ fontFamily: `'${theme.headingFont}', serif`, color: theme.textColor, fontSize: `${(theme.headingScale / 100) * 1.6}rem` }} className="mt-2">Victorian Mantel Clock</h3>
            <p style={{ color: theme.mutedTextColor }} className="mt-2 text-sm">Walnut case · Very good condition</p>
            <div style={{ background: theme.surfaceColor, borderRadius: theme.borderRadius }} className="mt-4 h-24" />
            <div className="mt-4 flex gap-2">
              <span style={{ background: theme.buttonColor, color: theme.buttonTextColor, borderRadius: theme.borderRadius }} className="px-4 py-2 text-sm">Add to cart</span>
              <span style={{ border: `1px solid ${theme.primaryColor}`, color: theme.primaryColor, borderRadius: theme.borderRadius }} className="px-4 py-2 text-sm">Wishlist</span>
            </div>
            <p className="mt-4 text-sm"><span style={{ color: theme.successColor }}>● Available</span> · <span style={{ color: theme.errorColor }}>Sale</span></p>
          </div>
          <p className="mt-3 text-xs text-muted">Check that text stays easy to read against backgrounds (aim for strong contrast).</p>
        </Panel>
      </div>
    </div>
  );
}

function MenuEditor({ menu, onChange }: { menu: NavigationMenu; onChange: (m: NavigationMenu) => void }) {
  const update = (items: NavItem[]) => onChange({ ...menu, items });
  const move = (i: number, dir: -1 | 1) => {
    const items = [...menu.items];
    const j = i + dir;
    if (j < 0 || j >= items.length) return;
    [items[i], items[j]] = [items[j], items[i]];
    update(items);
  };
  return (
    <Panel title={{ main: 'Main menu (header)', 'footer-shop': 'Footer column 1', 'footer-help': 'Footer column 2' }[menu.location]}>
      {menu.location !== 'main' && <Field label="Column title" className="mb-4">{(p) => <Input {...p} value={menu.title} onChange={(e) => onChange({ ...menu, title: e.target.value })} />}</Field>}
      <ul className="space-y-2">
        {menu.items.map((item, i) => (
          <li key={i} className="rounded-theme border border-line p-2">
            <div className="flex flex-wrap gap-2">
              <Input aria-label="Label" placeholder="Label" value={item.label} onChange={(e) => update(menu.items.map((x, j) => (j === i ? { ...x, label: e.target.value } : x)))} className="min-w-32 flex-1" />
              <Input aria-label="Link" placeholder="/shop or https://…" value={item.href} onChange={(e) => update(menu.items.map((x, j) => (j === i ? { ...x, href: e.target.value } : x)))} className="min-w-40 flex-1" />
              <Button variant="ghost" size="icon" aria-label="Move up" onClick={() => move(i, -1)} disabled={i === 0}><ArrowUp className="h-4 w-4" /></Button>
              <Button variant="ghost" size="icon" aria-label="Move down" onClick={() => move(i, 1)} disabled={i === menu.items.length - 1}><ArrowDown className="h-4 w-4" /></Button>
              <Button variant="ghost" size="icon" aria-label="Remove" onClick={() => update(menu.items.filter((_, j) => j !== i))}><Trash2 className="h-4 w-4" /></Button>
            </div>
            {menu.location === 'main' && (
              <div className="mt-2 space-y-2 border-l-2 border-line pl-3">
                {(item.children ?? []).map((child, k) => (
                  <div key={k} className="flex gap-2">
                    <Input aria-label="Sub-item label" placeholder="Sub-item" value={child.label} onChange={(e) => update(menu.items.map((x, j) => (j === i ? { ...x, children: x.children!.map((c, m) => (m === k ? { ...c, label: e.target.value } : c)) } : x)))} />
                    <Input aria-label="Sub-item link" placeholder="/category/…" value={child.href} onChange={(e) => update(menu.items.map((x, j) => (j === i ? { ...x, children: x.children!.map((c, m) => (m === k ? { ...c, href: e.target.value } : c)) } : x)))} />
                    <Button variant="ghost" size="icon" aria-label="Remove sub-item" onClick={() => update(menu.items.map((x, j) => (j === i ? { ...x, children: x.children!.filter((_, m) => m !== k) } : x)))}><Trash2 className="h-4 w-4" /></Button>
                  </div>
                ))}
                <Button size="sm" variant="link" className="text-xs" onClick={() => update(menu.items.map((x, j) => (j === i ? { ...x, children: [...(x.children ?? []), { label: '', href: '/' }] } : x)))}>+ Add dropdown item</Button>
              </div>
            )}
          </li>
        ))}
      </ul>
      <Button size="sm" variant="ghost" className="mt-2" icon={<Plus className="h-4 w-4" />} onClick={() => update([...menu.items, { label: '', href: '/' }])}>Add link</Button>
    </Panel>
  );
}

function NavigationForm({ data }: { data: AdminCustomization }) {
  const [menus, setMenus] = useState(data.navigation);
  useEffect(() => setMenus(data.navigation), [data.navigation]);
  const save = useSaver(adminApi.saveNavigation, 'Navigation');
  return (
    <div className="space-y-6">
      {menus.map((m) => <MenuEditor key={m.location} menu={m} onChange={(next) => setMenus(menus.map((x) => (x.location === next.location ? next : x)))} />)}
      <p className="text-xs text-muted">Links must be site paths (e.g. /shop, /category/vintage-clocks, /pages/about) or full https:// addresses.</p>
      <Button loading={save.isPending} onClick={() => save.mutate(menus.map((m) => ({ ...m, items: m.items.filter((i) => i.label && i.href).map((i) => ({ ...i, children: i.children?.filter((c) => c.label && c.href) ?? null })) })))}>Save draft</Button>
    </div>
  );
}

export function CustomizationPage() {
  const [tab, setTab] = useState<(typeof tabs)[number]>(tabs[0]);
  const q = useCustomization();
  if (q.isPending) return <PageSpinner />;
  if (q.isError) return <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} />;
  return (
    <AdminPage title="Website Customization" description="Changes are saved as a draft. Preview them, then publish when ready.">
      <PublishBar data={q.data} />
      <div className="mb-6 flex gap-1 overflow-x-auto border-b border-line" role="tablist">
        {tabs.map((t) => (
          <button key={t} role="tab" aria-selected={tab === t} onClick={() => setTab(t)} className={cn('-mb-px shrink-0 border-b-2 px-4 py-2.5 text-sm', tab === t ? 'border-primary text-ink' : 'border-transparent text-muted hover:text-ink')}>{t}</button>
        ))}
      </div>
      {tab === 'Brand & contact' && <BrandForm data={q.data} />}
      {tab === 'Colours & typography' && <ThemeForm data={q.data} />}
      {tab === 'Navigation' && <NavigationForm data={q.data} />}
    </AdminPage>
  );
}
