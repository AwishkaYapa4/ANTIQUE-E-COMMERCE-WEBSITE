import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { ApiError, errorMessage } from '@/services/http';
import { useAuth } from '@/features/auth/AuthProvider';
import type { CommerceSettings, ShippingZone } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, Input, Textarea } from '@/components/ui/Field';
import { Badge, EmptyState, ErrorState, Notice, PageSpinner } from '@/components/ui/Feedback';
import { Modal, useConfirm } from '@/components/ui/Overlay';
import { Pagination } from '@/components/ui/Misc';
import { cn, formatDateTime, formatPrice, SRI_LANKA_DISTRICTS } from '@/utils/format';
import { AdminPage, Panel, TableShell, td, th } from '../components/AdminUi';

const numOrNull = { setValueAs: (v: string | number) => (v === '' || v == null ? null : Number(v)) };

function CommerceForm({ data }: { data: CommerceSettings }) {
  const queryClient = useQueryClient();
  const form = useForm<CommerceSettings>({ defaultValues: data });
  useEffect(() => form.reset(data), [data, form]);
  const save = useMutation({
    mutationFn: adminApi.saveSettings,
    onSuccess: (s) => { queryClient.setQueryData(['admin', 'settings'], s); queryClient.invalidateQueries({ queryKey: ['site'] }); toast.success('Settings saved — they apply immediately'); },
    onError: (e) => { if (e instanceof ApiError) Object.entries(e.fieldErrors).forEach(([k, v]) => form.setError(k as keyof CommerceSettings, { message: v[0] })); toast.error(errorMessage(e)); },
  });
  const r = form.register;
  const err = (k: keyof CommerceSettings) => form.formState.errors[k]?.message as string | undefined;
  const pickup = form.watch('localPickupEnabled');
  const bank = form.watch('bankTransferEnabled');
  return (
    <form onSubmit={form.handleSubmit((v) => save.mutate(v))} className="space-y-6" noValidate>
      <Panel title="Orders & checkout">
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Order number prefix" error={err('orderNumberPrefix')} hint="e.g. AH → AH-261020-7KQ2PM">{(p) => <Input {...p} maxLength={8} {...r('orderNumberPrefix')} />}</Field>
          <Field label="Checkout hold (minutes)" error={err('reservationMinutes')} hint="How long items are reserved during checkout.">{(p) => <Input {...p} type="number" {...r('reservationMinutes', { valueAsNumber: true })} />}</Field>
          <Field label="Currency" hint="Fixed to LKR in this version.">{(p) => <Input {...p} value="LKR" disabled readOnly />}</Field>
        </div>
      </Panel>
      <Panel title="Payment methods">
        <div className="space-y-5">
          <div className="space-y-3">
            <Checkbox label="Cash on delivery" description="Can also be switched off per delivery zone." {...r('cashOnDeliveryEnabled')} />
            <Field label="Maximum order value for cash on delivery (LKR)" hint="Leave empty for no limit." error={err('cashOnDeliveryMaxOrderValue')}>{(p) => <Input {...p} type="number" {...r('cashOnDeliveryMaxOrderValue', numOrNull)} />}</Field>
          </div>
          <div className="space-y-3 border-t border-line pt-5">
            <Checkbox label="Bank transfer (manual verification)" {...r('bankTransferEnabled')} />
            {bank && (
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Bank name" error={err('bankName')}>{(p) => <Input {...p} {...r('bankName')} />}</Field>
                <Field label="Branch" error={err('bankBranch')}>{(p) => <Input {...p} {...r('bankBranch')} />}</Field>
                <Field label="Account name" error={err('bankAccountName')}>{(p) => <Input {...p} {...r('bankAccountName')} />}</Field>
                <Field label="Account number" required error={err('bankAccountNumber')}>{(p) => <Input {...p} {...r('bankAccountNumber')} />}</Field>
                <Field label="Payment window (hours)" hint="Unpaid transfers are cancelled and items released after this. 0 = never." error={err('bankTransferPaymentWindowHours')}>{(p) => <Input {...p} type="number" {...r('bankTransferPaymentWindowHours', { valueAsNumber: true })} />}</Field>
                <Field label="Instructions for customers" className="sm:col-span-2" error={err('bankTransferInstructions')}>{(p) => <Textarea {...p} rows={2} {...r('bankTransferInstructions')} />}</Field>
              </div>
            )}
          </div>
          <Notice>Card payments are not collected in this version. An online payment gateway can be added later through the payment provider interface; it must confirm payments server-to-server.</Notice>
        </div>
      </Panel>
      <Panel title="Delivery & pickup">
        <div className="space-y-4">
          <Field label="Free delivery threshold (LKR)" hint="Orders at or above this amount ship free. Leave empty to disable." error={err('freeShippingThreshold')}>{(p) => <Input {...p} type="number" {...r('freeShippingThreshold', numOrNull)} />}</Field>
          <Field label="Delivery information shown to customers" hint="Describe your real delivery process. Avoid promising times you can't guarantee.">{(p) => <Textarea {...p} rows={2} {...r('deliveryInstructions')} />}</Field>
          <Checkbox label="Offer local pickup" {...r('localPickupEnabled')} />
          {pickup && (
            <>
              <Field label="Pickup address" required error={err('localPickupAddress')}>{(p) => <Textarea {...p} rows={2} {...r('localPickupAddress')} />}</Field>
              <Field label="Pickup instructions">{(p) => <Textarea {...p} rows={2} {...r('localPickupInstructions')} />}</Field>
            </>
          )}
        </div>
      </Panel>
      <Panel title="Other">
        <div className="space-y-4">
          <Checkbox label="Allow product reviews" description="Reviews are always moderated before they appear." {...r('reviewsEnabled')} />
          <Field label="Returns summary (shown on product pages)">{(p) => <Textarea {...p} rows={2} {...r('returnsSummary')} />}</Field>
        </div>
      </Panel>
      <Button type="submit" loading={save.isPending}>Save settings</Button>
    </form>
  );
}

const blankZone: ShippingZone = { id: null, name: '', isActive: true, cashOnDeliveryAvailable: true, estimatedDelivery: null, sortOrder: 0, districts: [], rates: [{ id: null, name: 'Standard delivery', fee: 0, minSubtotal: null, maxSubtotal: null, isActive: true }] };

function ShippingZones() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const zones = useQuery({ queryKey: ['admin', 'zones'], queryFn: adminApi.zones });
  const [editing, setEditing] = useState<ShippingZone | null>(null);
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin', 'zones'] });
  const save = useMutation({ mutationFn: adminApi.saveZone, onSuccess: () => { refresh(); setEditing(null); toast.success('Shipping zone saved'); }, onError: (e) => toast.error(errorMessage(e)) });
  const remove = useMutation({ mutationFn: adminApi.deleteZone, onSuccess: refresh, onError: (e) => toast.error(errorMessage(e)) });
  const assigned = new Set((zones.data ?? []).filter((z) => z.id !== editing?.id).flatMap((z) => z.districts));
  const uncovered = SRI_LANKA_DISTRICTS.filter((d) => !(zones.data ?? []).some((z) => z.isActive && z.districts.includes(d)));

  return (
    <Panel title="Delivery zones & rates" actions={<Button size="sm" icon={<Plus className="h-4 w-4" />} onClick={() => setEditing(structuredClone(blankZone))}>Add zone</Button>}>
      <p className="mb-4 text-sm text-muted">Group districts into zones and set the delivery fee for each. Fees are calculated by the server at checkout using only these rates.</p>
      {uncovered.length > 0 && zones.data && <div className="mb-4"><Notice>No delivery to: {uncovered.join(', ')}. Customers there can still use local pickup if enabled.</Notice></div>}
      {zones.isPending ? <PageSpinner /> : (zones.data?.length ?? 0) === 0 ? <EmptyState title="No delivery zones" description="Add a zone to start offering delivery." /> : (
        <ul className="space-y-3">
          {zones.data!.map((z) => (
            <li key={z.id} className="rounded-theme border border-line p-4">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="font-medium">{z.name} {!z.isActive && <Badge>Inactive</Badge>} {z.cashOnDeliveryAvailable && <Badge tone="accent">COD</Badge>}</p>
                  <p className="mt-1 text-xs text-muted">{z.districts.join(', ') || 'No districts'}</p>
                  <p className="mt-1 text-sm">{z.rates.filter((r) => r.isActive).map((r) => `${r.name}: ${formatPrice(r.fee)}${r.minSubtotal ? ` (from ${formatPrice(r.minSubtotal)})` : ''}`).join(' · ')}</p>
                </div>
                <div className="flex gap-1">
                  <Button size="sm" variant="ghost" onClick={() => setEditing(structuredClone(z))}>Edit</Button>
                  <Button size="sm" variant="ghost" className="text-danger" onClick={async () => { if (await confirm({ title: 'Delete zone?', message: `Delivery to ${z.districts.length} district(s) will stop.`, danger: true, confirmLabel: 'Delete' })) remove.mutate(z.id!); }}>Delete</Button>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
      <Modal open={editing !== null} onClose={() => setEditing(null)} title={editing?.id ? 'Edit zone' : 'New zone'} size="lg"
        footer={<Button loading={save.isPending} disabled={!editing?.name.trim()} onClick={() => editing && save.mutate(editing)}>Save zone</Button>}>
        {editing && (
          <div className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Zone name" required>{(p) => <Input {...p} value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} />}</Field>
              <Field label="Delivery estimate (optional)" hint="Only state what you can deliver on, e.g. “Usually 2–4 working days”.">{(p) => <Input {...p} value={editing.estimatedDelivery ?? ''} onChange={(e) => setEditing({ ...editing, estimatedDelivery: e.target.value || null })} />}</Field>
            </div>
            <div className="flex flex-wrap gap-6">
              <Checkbox label="Active" checked={editing.isActive} onChange={(e) => setEditing({ ...editing, isActive: e.target.checked })} />
              <Checkbox label="Cash on delivery available" checked={editing.cashOnDeliveryAvailable} onChange={(e) => setEditing({ ...editing, cashOnDeliveryAvailable: e.target.checked })} />
            </div>
            <fieldset>
              <legend className="mb-2 text-sm font-medium">Districts</legend>
              <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                {SRI_LANKA_DISTRICTS.map((d) => {
                  const taken = assigned.has(d);
                  return <Checkbox key={d} label={<span className={cn(taken && 'text-muted')}>{d}{taken && ' (other zone)'}</span>} disabled={taken} checked={editing.districts.includes(d)}
                    onChange={() => setEditing({ ...editing, districts: editing.districts.includes(d) ? editing.districts.filter((x) => x !== d) : [...editing.districts, d] })} />;
                })}
              </div>
            </fieldset>
            <fieldset>
              <legend className="mb-2 text-sm font-medium">Rates</legend>
              <p className="mb-2 text-xs text-muted">The cheapest active rate matching the order value applies. Use order value ranges for tiered pricing.</p>
              <ul className="space-y-2">
                {editing.rates.map((rate, i) => (
                  <li key={i} className="grid gap-2 rounded-theme border border-line p-2 sm:grid-cols-[1.5fr_1fr_1fr_1fr_auto]">
                    <Input aria-label="Rate name" placeholder="Name" value={rate.name} onChange={(e) => setEditing({ ...editing, rates: editing.rates.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)) })} />
                    <Input aria-label="Fee" type="number" placeholder="Fee" value={rate.fee} onChange={(e) => setEditing({ ...editing, rates: editing.rates.map((x, j) => (j === i ? { ...x, fee: Number(e.target.value) } : x)) })} />
                    <Input aria-label="Minimum order value" type="number" placeholder="Min order" value={rate.minSubtotal ?? ''} onChange={(e) => setEditing({ ...editing, rates: editing.rates.map((x, j) => (j === i ? { ...x, minSubtotal: e.target.value ? Number(e.target.value) : null } : x)) })} />
                    <Input aria-label="Maximum order value" type="number" placeholder="Max order" value={rate.maxSubtotal ?? ''} onChange={(e) => setEditing({ ...editing, rates: editing.rates.map((x, j) => (j === i ? { ...x, maxSubtotal: e.target.value ? Number(e.target.value) : null } : x)) })} />
                    <Button variant="ghost" size="icon" aria-label="Remove rate" onClick={() => setEditing({ ...editing, rates: editing.rates.filter((_, j) => j !== i) })}><Trash2 className="h-4 w-4" /></Button>
                  </li>
                ))}
              </ul>
              <Button size="sm" variant="ghost" className="mt-2" icon={<Plus className="h-4 w-4" />} onClick={() => setEditing({ ...editing, rates: [...editing.rates, { id: null, name: '', fee: 0, minSubtotal: null, maxSubtotal: null, isActive: true }] })}>Add rate</Button>
            </fieldset>
          </div>
        )}
      </Modal>
    </Panel>
  );
}

export function StoreSettingsPage() {
  const q = useQuery({ queryKey: ['admin', 'settings'], queryFn: adminApi.settings });
  if (q.isPending) return <PageSpinner />;
  if (q.isError) return <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} />;
  return (
    <AdminPage title="Store Settings" description="Operational settings apply immediately (no draft). Brand and appearance live under Website Customization.">
      <div className="grid gap-6 xl:grid-cols-2">
        <CommerceForm data={q.data} />
        <ShippingZones />
      </div>
    </AdminPage>
  );
}

export function AuditLogPage() {
  const [page, setPage] = useState(1);
  const list = useQuery({ queryKey: ['admin', 'audit', page], queryFn: () => adminApi.auditLogs({ page }), placeholderData: keepPreviousData });
  return (
    <AdminPage title="Audit Log" description="Every administrative change, who made it and when.">
      <TableShell loading={list.isPending} error={list.error} empty={list.data?.items.length === 0 ? <EmptyState title="No activity yet" /> : undefined}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>When</th><th className={th}>Who</th><th className={th}>Action</th><th className={th}>Details</th></tr></thead>
          <tbody className="divide-y divide-line">
            {list.data?.items.map((l) => (
              <tr key={l.id}><td className={`${td} text-xs whitespace-nowrap text-muted`}>{formatDateTime(l.createdAt)}</td><td className={`${td} text-xs`}>{l.userEmail ?? 'System'}</td><td className={`${td} font-mono text-xs`}>{l.action}</td><td className={td}>{l.summary}</td></tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      {list.data && <Pagination page={page} totalPages={list.data.totalPages} onChange={setPage} />}
    </AdminPage>
  );
}

export function AdminProfilePage() {
  const { me, resetPassword } = useAuth();
  return (
    <AdminPage title="Admin Profile">
      <div className="max-w-xl space-y-6">
        <Panel title="Your account">
          <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
            <dt className="text-muted">Name</dt><dd>{me?.displayName ?? '—'}</dd>
            <dt className="text-muted">Email</dt><dd>{me?.email}</dd>
            <dt className="text-muted">Role</dt><dd>{me?.role}</dd>
            <dt className="text-muted">Permissions</dt><dd className="flex flex-wrap gap-1">{me?.permissions.map((p) => <Badge key={p}>{p}</Badge>)}</dd>
          </dl>
          <div className="mt-4 flex gap-2">
            <Link to="/account" className="text-sm link-underline">Edit name & phone</Link>
            <Button size="sm" variant="link" onClick={async () => { await resetPassword(me!.email); toast.success('Password reset email sent'); }}>Change password</Button>
          </div>
        </Panel>
        <Panel title="Admin access">
          <p className="text-sm text-muted">For security, admin roles can only be granted or removed by someone with server access, using the command <code className="rounded bg-surface px-1">dotnet run -- admin grant email@example.com</code>. They are never set from the browser.</p>
        </Panel>
      </div>
    </AdminPage>
  );
}

export function AdminNotFound() {
  return <AdminPage title="Page not found"><Link to="/admin" className="link-underline">Back to dashboard</Link></AdminPage>;
}
