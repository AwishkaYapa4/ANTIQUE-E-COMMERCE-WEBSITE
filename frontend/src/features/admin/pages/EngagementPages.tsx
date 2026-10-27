import { useState } from 'react';
import { Link } from 'react-router';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { BadgeCheck, Mail, Pencil, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { ApiError, errorMessage } from '@/services/http';
import type { AdminTestimonial, DiscountCode, Enquiry, EnquiryStatus, ReviewStatus } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select, Textarea } from '@/components/ui/Field';
import { Badge, EmptyState, Notice } from '@/components/ui/Feedback';
import { Modal, useConfirm } from '@/components/ui/Overlay';
import { Pagination, Stars } from '@/components/ui/Misc';
import { cn, formatDate, formatDateTime, formatPrice } from '@/utils/format';
import { AdminPage, Panel, SearchInput, TableShell, td, th } from '../components/AdminUi';

// ---------------- Promotions ----------------

const discountSchema = z.object({
  code: z.string().trim().min(1, 'Enter a code.').max(32).regex(/^[A-Za-z0-9_-]+$/, 'Letters, numbers, dashes or underscores.'),
  description: z.string().max(500),
  type: z.enum(['Percentage', 'FixedAmount']),
  value: z.number({ message: 'Enter a value.' }).positive('Must be greater than 0.'),
  minOrderSubtotal: z.number().nonnegative().nullable(),
  maxDiscountAmount: z.number().positive().nullable(),
  startsAt: z.string(),
  endsAt: z.string(),
  usageLimit: z.number().int().positive().nullable(),
  usageLimitPerCustomer: z.number().int().positive().nullable(),
  isActive: z.boolean(),
}).superRefine((v, ctx) => {
  if (v.type === 'Percentage' && v.value > 100) ctx.addIssue({ code: 'custom', path: ['value'], message: 'Percentage must be at most 100.' });
  if (v.startsAt && v.endsAt && v.endsAt <= v.startsAt) ctx.addIssue({ code: 'custom', path: ['endsAt'], message: 'End must be after start.' });
});
type DiscountValues = z.infer<typeof discountSchema>;
const numOrNull = { setValueAs: (v: string) => (v === '' || v == null ? null : Number(v)) };
const toLocalInput = (iso: string | null) => (iso ? new Date(iso).toISOString().slice(0, 16) : '');

function DiscountForm({ initial, onSaved }: { initial?: DiscountCode; onSaved: () => void }) {
  const form = useForm<DiscountValues>({
    resolver: zodResolver(discountSchema),
    defaultValues: initial
      ? { ...initial, description: initial.description ?? '', startsAt: toLocalInput(initial.startsAt), endsAt: toLocalInput(initial.endsAt) }
      : { code: '', description: '', type: 'Percentage', value: 10, minOrderSubtotal: null, maxDiscountAmount: null, startsAt: '', endsAt: '', usageLimit: null, usageLimitPerCustomer: 1, isActive: true },
  });
  const save = useMutation({
    mutationFn: (v: DiscountValues) => adminApi.saveDiscount(initial?.id ?? null, {
      ...v, description: v.description || null, startsAt: v.startsAt ? new Date(v.startsAt).toISOString() : null, endsAt: v.endsAt ? new Date(v.endsAt).toISOString() : null,
    }),
    onSuccess: () => { toast.success('Discount saved'); onSaved(); },
    onError: (e) => { if (e instanceof ApiError) Object.entries(e.fieldErrors).forEach(([k, v]) => form.setError(k as keyof DiscountValues, { message: v[0] })); toast.error(errorMessage(e)); },
  });
  const e = form.formState.errors;
  const type = form.watch('type');
  return (
    <form className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))} noValidate>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Code" required error={e.code?.message} hint="Customers type this at checkout (not case sensitive).">{(p) => <Input {...p} className="uppercase" {...form.register('code')} />}</Field>
        <Field label="Internal description">{(p) => <Input {...p} {...form.register('description')} />}</Field>
        <Field label="Type">{(p) => <Select {...p} {...form.register('type')}><option value="Percentage">Percentage off</option><option value="FixedAmount">Fixed amount off (LKR)</option></Select>}</Field>
        <Field label={type === 'Percentage' ? 'Percentage' : 'Amount (LKR)'} required error={e.value?.message}>{(p) => <Input {...p} type="number" step="any" {...form.register('value', { valueAsNumber: true })} />}</Field>
        <Field label="Minimum order (LKR)" error={e.minOrderSubtotal?.message}>{(p) => <Input {...p} type="number" {...form.register('minOrderSubtotal', numOrNull)} />}</Field>
        {type === 'Percentage' && <Field label="Maximum discount (LKR)" error={e.maxDiscountAmount?.message}>{(p) => <Input {...p} type="number" {...form.register('maxDiscountAmount', numOrNull)} />}</Field>}
        <Field label="Starts" error={e.startsAt?.message}>{(p) => <Input {...p} type="datetime-local" {...form.register('startsAt')} />}</Field>
        <Field label="Expires" error={e.endsAt?.message}>{(p) => <Input {...p} type="datetime-local" {...form.register('endsAt')} />}</Field>
        <Field label="Total uses allowed" hint="Leave empty for unlimited." error={e.usageLimit?.message}>{(p) => <Input {...p} type="number" {...form.register('usageLimit', numOrNull)} />}</Field>
        <Field label="Uses per customer" error={e.usageLimitPerCustomer?.message}>{(p) => <Input {...p} type="number" {...form.register('usageLimitPerCustomer', numOrNull)} />}</Field>
      </div>
      <Checkbox label="Active" {...form.register('isActive')} />
      <Button type="submit" loading={save.isPending}>Save discount</Button>
    </form>
  );
}

export function PromotionsPage() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<DiscountCode | 'new' | null>(null);
  const list = useQuery({ queryKey: ['admin', 'discounts'], queryFn: adminApi.discounts });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'discounts'] });
  const remove = useMutation({ mutationFn: adminApi.deleteDiscount, onSuccess: (r) => { refresh(); toast.success(r.deleted ? 'Discount deleted' : 'Discount has been used, so it was deactivated instead'); }, onError: (e) => toast.error(errorMessage(e)) });
  const describe = (d: DiscountCode) => (d.type === 'Percentage' ? `${d.value}% off` : `${formatPrice(d.value)} off`) + (d.minOrderSubtotal ? ` over ${formatPrice(d.minOrderSubtotal)}` : '');
  const state = (d: DiscountCode) => {
    const now = Date.now();
    if (!d.isActive) return <Badge>Inactive</Badge>;
    if (d.endsAt && new Date(d.endsAt).getTime() < now) return <Badge>Expired</Badge>;
    if (d.startsAt && new Date(d.startsAt).getTime() > now) return <Badge tone="accent">Scheduled</Badge>;
    if (d.usageLimit != null && d.usedCount >= d.usageLimit) return <Badge>Used up</Badge>;
    return <Badge tone="success">Active</Badge>;
  };
  return (
    <AdminPage title="Promotions" description="Discount codes are validated and applied by the server at checkout." actions={<Button onClick={() => setEditing('new')}><Plus className="h-4 w-4" /> New discount</Button>}>
      <TableShell loading={list.isPending} error={list.error} empty={list.data?.length === 0 ? <EmptyState title="No discount codes yet" action={<Button onClick={() => setEditing('new')}>Create a discount</Button>} /> : undefined}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>Code</th><th className={th}>Discount</th><th className={th}>Status</th><th className={th}>Used</th><th className={th}>Valid</th><th className={th}><span className="sr-only">Actions</span></th></tr></thead>
          <tbody className="divide-y divide-line">
            {list.data?.map((d) => (
              <tr key={d.id}>
                <td className={`${td} font-mono`}>{d.code}<p className="font-body text-xs text-muted">{d.description}</p></td>
                <td className={td}>{describe(d)}</td>
                <td className={td}>{state(d)}</td>
                <td className={td}>{d.usedCount}{d.usageLimit != null && ` / ${d.usageLimit}`}</td>
                <td className={`${td} text-xs text-muted`}>{d.startsAt ? formatDate(d.startsAt) : 'Now'} – {d.endsAt ? formatDate(d.endsAt) : 'no expiry'}</td>
                <td className={`${td} text-right whitespace-nowrap`}>
                  <Button variant="ghost" size="icon" aria-label={`Edit ${d.code}`} onClick={() => setEditing(d)}><Pencil className="h-4 w-4" /></Button>
                  <Button variant="ghost" size="icon" aria-label={`Delete ${d.code}`} onClick={async () => { if (await confirm({ title: 'Delete discount?', message: 'Codes that have been used are deactivated instead, to keep order records accurate.', danger: true, confirmLabel: 'Delete' })) remove.mutate(d.id); }}><Trash2 className="h-4 w-4" /></Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      <Modal open={editing !== null} onClose={() => setEditing(null)} title={editing === 'new' ? 'New discount' : 'Edit discount'} size="lg">
        {editing !== null && <DiscountForm initial={editing === 'new' ? undefined : editing} onSaved={() => { refresh(); setEditing(null); }} />}
      </Modal>
    </AdminPage>
  );
}

// ---------------- Reviews, testimonials, newsletter ----------------

function ReviewsTab() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<ReviewStatus | ''>('Pending');
  const [page, setPage] = useState(1);
  const list = useQuery({ queryKey: ['admin', 'reviews', status, page], queryFn: () => adminApi.reviews({ status: status || undefined, page }), placeholderData: keepPreviousData });
  const refresh = () => { queryClient.invalidateQueries({ queryKey: ['admin', 'reviews'] }); queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] }); };
  const moderate = useMutation({ mutationFn: (v: { id: number; status: ReviewStatus }) => adminApi.moderateReview(v.id, v.status), onSuccess: refresh, onError: (e) => toast.error(errorMessage(e)) });
  const remove = useMutation({ mutationFn: adminApi.deleteReview, onSuccess: refresh, onError: (e) => toast.error(errorMessage(e)) });
  return (
    <>
      <Select aria-label="Review status" value={status} onChange={(e) => { setStatus(e.target.value as ReviewStatus | ''); setPage(1); }} className="mb-4 w-auto py-2 text-sm">
        <option value="Pending">Awaiting moderation</option><option value="Approved">Approved</option><option value="Rejected">Rejected</option><option value="">All</option>
      </Select>
      {(list.data?.items.length ?? 0) === 0 ? <EmptyState title="No reviews here" description="Customer reviews appear here for approval before they are shown." /> : (
        <ul className="space-y-3">
          {list.data!.items.map((r) => (
            <li key={r.id} className="rounded-theme border border-line bg-paper p-4">
              <div className="flex flex-wrap items-center gap-3"><Stars value={r.rating} />{r.title && <span className="font-medium">{r.title}</span>}{r.isVerifiedPurchase && <span className="inline-flex items-center gap-1 text-xs text-success"><BadgeCheck className="h-3.5 w-3.5" /> Verified purchase</span>}<Badge>{r.status}</Badge></div>
              <p className="mt-2 text-sm">{r.body}</p>
              <p className="mt-2 text-xs text-muted">{r.authorName} ({r.authorEmail}) on <a href={`/products/${r.productSlug}`} target="_blank" rel="noreferrer" className="link-underline">{r.productName}</a> · {formatDateTime(r.createdAt)}</p>
              <div className="mt-3 flex gap-2">
                {r.status !== 'Approved' && <Button size="sm" onClick={() => moderate.mutate({ id: r.id, status: 'Approved' })}>Approve</Button>}
                {r.status !== 'Rejected' && <Button size="sm" variant="outline" onClick={() => moderate.mutate({ id: r.id, status: 'Rejected' })}>Reject</Button>}
                <Button size="sm" variant="ghost" className="text-danger" onClick={() => remove.mutate(r.id)}>Delete</Button>
              </div>
            </li>
          ))}
        </ul>
      )}
      {list.data && <Pagination page={page} totalPages={list.data.totalPages} onChange={setPage} />}
    </>
  );
}

function TestimonialsTab() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<AdminTestimonial | 'new' | null>(null);
  const [form, setForm] = useState<Omit<AdminTestimonial, 'id'>>({ quote: '', authorName: '', authorLocation: '', rating: null, isPublished: true, sortOrder: 0 });
  const list = useQuery({ queryKey: ['admin', 'testimonials'], queryFn: adminApi.testimonials });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'testimonials'] });
  const save = useMutation({ mutationFn: () => adminApi.saveTestimonial(editing === 'new' ? null : editing!.id, { ...form, authorLocation: form.authorLocation || null }), onSuccess: () => { refresh(); setEditing(null); toast.success('Saved'); }, onError: (e) => toast.error(errorMessage(e)) });
  const remove = useMutation({ mutationFn: adminApi.deleteTestimonial, onSuccess: refresh, onError: (e) => toast.error(errorMessage(e)) });
  const open = (t: AdminTestimonial | 'new') => { setEditing(t); setForm(t === 'new' ? { quote: '', authorName: '', authorLocation: '', rating: null, isPublished: true, sortOrder: 0 } : t); };
  return (
    <>
      <Notice>Only add testimonials from real customers, with their permission. None are pre-filled.</Notice>
      <Button className="my-4" size="sm" icon={<Plus className="h-4 w-4" />} onClick={() => open('new')}>Add testimonial</Button>
      <ul className="grid gap-3 md:grid-cols-2">
        {list.data?.map((t) => (
          <li key={t.id} className="rounded-theme border border-line bg-paper p-4 text-sm">
            <p className="font-heading text-lg">“{t.quote}”</p>
            <p className="mt-2 text-muted">{t.authorName}{t.authorLocation && ` · ${t.authorLocation}`} {!t.isPublished && <Badge>Hidden</Badge>}</p>
            <div className="mt-2 flex gap-2"><Button size="sm" variant="ghost" onClick={() => open(t)}>Edit</Button><Button size="sm" variant="ghost" className="text-danger" onClick={() => remove.mutate(t.id)}>Delete</Button></div>
          </li>
        ))}
      </ul>
      <Modal open={editing !== null} onClose={() => setEditing(null)} title="Testimonial" footer={<Button loading={save.isPending} disabled={!form.quote.trim() || !form.authorName.trim()} onClick={() => save.mutate()}>Save</Button>}>
        <div className="space-y-4">
          <Field label="Quote" required>{(p) => <Textarea {...p} value={form.quote} maxLength={2000} onChange={(e) => setForm({ ...form, quote: e.target.value })} />}</Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Customer name" required>{(p) => <Input {...p} value={form.authorName} onChange={(e) => setForm({ ...form, authorName: e.target.value })} />}</Field>
            <Field label="Location">{(p) => <Input {...p} value={form.authorLocation ?? ''} onChange={(e) => setForm({ ...form, authorLocation: e.target.value })} />}</Field>
            <Field label="Rating (optional)">{(p) => <Select {...p} value={form.rating ?? ''} onChange={(e) => setForm({ ...form, rating: e.target.value ? Number(e.target.value) : null })}><option value="">None</option>{[5, 4, 3, 2, 1].map((n) => <option key={n} value={n}>{n} stars</option>)}</Select>}</Field>
            <Field label="Order">{(p) => <Input {...p} type="number" value={form.sortOrder} onChange={(e) => setForm({ ...form, sortOrder: Number(e.target.value) })} />}</Field>
          </div>
          <Checkbox label="Show on the website" checked={form.isPublished} onChange={(e) => setForm({ ...form, isPublished: e.target.checked })} />
        </div>
      </Modal>
    </>
  );
}

function NewsletterTab() {
  const [page, setPage] = useState(1);
  const list = useQuery({ queryKey: ['admin', 'newsletter', page], queryFn: () => adminApi.newsletter(page), placeholderData: keepPreviousData });
  const exportCsv = () => {
    const rows = (list.data?.items ?? []).filter((s) => s.status === 'Subscribed').map((s) => s.email);
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob([['email', ...rows].join('\n')], { type: 'text/csv' }));
    a.download = 'newsletter-subscribers.csv';
    a.click();
  };
  return (
    <>
      <div className="mb-4 flex items-center justify-between"><p className="text-sm text-muted">{list.data?.totalCount ?? 0} subscribers. Export to use with your email service.</p><Button size="sm" variant="outline" icon={<Mail className="h-4 w-4" />} onClick={exportCsv}>Export this page (CSV)</Button></div>
      <TableShell loading={list.isPending} error={list.error} empty={list.data?.items.length === 0 ? <EmptyState title="No subscribers yet" /> : undefined}>
        <table className="w-full"><tbody className="divide-y divide-line">{list.data?.items.map((s) => <tr key={s.id}><td className={td}>{s.email}</td><td className={td}><Badge tone={s.status === 'Subscribed' ? 'success' : 'neutral'}>{s.status}</Badge></td><td className={`${td} text-xs text-muted`}>{s.source} · {formatDate(s.createdAt)}</td></tr>)}</tbody></table>
      </TableShell>
      {list.data && <Pagination page={page} totalPages={list.data.totalPages} onChange={setPage} />}
    </>
  );
}

export function ReviewsPage() {
  const [tab, setTab] = useState<'Reviews' | 'Testimonials' | 'Newsletter'>('Reviews');
  return (
    <AdminPage title="Reviews & community" description="Moderate product reviews, manage testimonials and view newsletter subscribers.">
      <div className="mb-6 flex gap-1 border-b border-line" role="tablist">
        {(['Reviews', 'Testimonials', 'Newsletter'] as const).map((t) => (
          <button key={t} role="tab" aria-selected={tab === t} onClick={() => setTab(t)} className={cn('-mb-px border-b-2 px-4 py-2.5 text-sm', tab === t ? 'border-primary' : 'border-transparent text-muted')}>{t}</button>
        ))}
      </div>
      {tab === 'Reviews' && <ReviewsTab />}
      {tab === 'Testimonials' && <TestimonialsTab />}
      {tab === 'Newsletter' && <NewsletterTab />}
    </AdminPage>
  );
}

// ---------------- Enquiries ----------------

const enquiryStatuses: EnquiryStatus[] = ['New', 'InProgress', 'Resolved', 'Spam'];

export function EnquiriesPage() {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<EnquiryStatus | ''>('');
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const [open, setOpen] = useState<Enquiry | null>(null);
  const [notes, setNotes] = useState('');
  const list = useQuery({ queryKey: ['admin', 'enquiries', status, q, page], queryFn: () => adminApi.enquiries({ status: status || undefined, q, page }), placeholderData: keepPreviousData });
  const update = useMutation({
    mutationFn: (v: { id: number; status: EnquiryStatus; adminNotes: string | null }) => adminApi.updateEnquiry(v.id, { status: v.status, adminNotes: v.adminNotes }),
    onSuccess: (e) => { setOpen(e); queryClient.invalidateQueries({ queryKey: ['admin', 'enquiries'] }); queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] }); toast.success('Enquiry updated'); },
    onError: (e) => toast.error(errorMessage(e)),
  });
  return (
    <AdminPage title="Enquiries" description="Questions from the contact form and “Ask about this item”. Spam is filtered out by default.">
      <div className="mb-4 flex flex-wrap gap-2">
        <SearchInput value={q} onChange={(v) => { setQ(v); setPage(1); }} placeholder="Search name, email, message" />
        <Select aria-label="Status" value={status} onChange={(e) => { setStatus(e.target.value as EnquiryStatus | ''); setPage(1); }} className="w-auto py-2 text-sm">
          <option value="">All (except spam)</option>{enquiryStatuses.map((s) => <option key={s} value={s}>{s === 'InProgress' ? 'In progress' : s}</option>)}
        </Select>
      </div>
      <TableShell loading={list.isPending} error={list.error} empty={list.data?.items.length === 0 ? <EmptyState title="No enquiries" /> : undefined}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>From</th><th className={th}>About</th><th className={th}>Status</th><th className={th}>Received</th></tr></thead>
          <tbody className="divide-y divide-line">
            {list.data?.items.map((e) => (
              <tr key={e.id} className={cn('cursor-pointer hover:bg-bg', e.status === 'New' && 'font-medium')} onClick={() => { setOpen(e); setNotes(e.adminNotes ?? ''); }}>
                <td className={td}><button type="button" className="text-left link-underline">{e.name}</button><p className="text-xs font-normal text-muted">{e.email}</p></td>
                <td className={`${td} max-w-sm`}><p className="truncate">{e.productName ?? e.subject ?? 'General enquiry'}</p><p className="truncate text-xs font-normal text-muted">{e.message}</p></td>
                <td className={td}><Badge tone={e.status === 'New' ? 'warning' : e.status === 'Resolved' ? 'success' : 'neutral'}>{e.status === 'InProgress' ? 'In progress' : e.status}</Badge></td>
                <td className={`${td} text-xs font-normal text-muted`}>{formatDateTime(e.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      {list.data && <Pagination page={page} totalPages={list.data.totalPages} onChange={setPage} />}
      <Modal open={open !== null} onClose={() => setOpen(null)} title="Enquiry" size="lg">
        {open && (
          <div className="space-y-4">
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
              <dt className="text-muted">From</dt><dd>{open.name}</dd>
              <dt className="text-muted">Email</dt><dd><a href={`mailto:${open.email}?subject=${encodeURIComponent(`Re: ${open.productName ?? open.subject ?? 'your enquiry'}`)}`} className="link-underline">{open.email}</a></dd>
              {open.phone && <><dt className="text-muted">Phone</dt><dd><a href={`tel:${open.phone}`} className="link-underline">{open.phone}</a></dd></>}
              {open.productName && <><dt className="text-muted">Item</dt><dd><Link to={`/admin/products/${open.productId}`} className="link-underline">{open.productName}</Link></dd></>}
              <dt className="text-muted">Received</dt><dd>{formatDateTime(open.createdAt)}</dd>
            </dl>
            <Panel><p className="text-sm whitespace-pre-line">{open.message}</p></Panel>
            <Field label="Internal notes">{(p) => <Textarea {...p} rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />}</Field>
            <div className="flex flex-wrap gap-2">
              {enquiryStatuses.map((s) => <Button key={s} size="sm" variant={open.status === s ? 'primary' : 'outline'} loading={update.isPending && update.variables?.status === s} onClick={() => update.mutate({ id: open.id, status: s, adminNotes: notes || null })}>{s === 'InProgress' ? 'In progress' : s}</Button>)}
            </div>
          </div>
        )}
      </Modal>
    </AdminPage>
  );
}
