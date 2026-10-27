import { useEffect, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileText, Phone } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { errorMessage } from '@/services/http';
import type { AdminOrder, OrderStatus, PaymentMethod, PaymentStatus } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select, Textarea } from '@/components/ui/Field';
import { Badge, EmptyState, ErrorState, Notice, OrderStatusBadge, PageSpinner, PaymentStatusBadge } from '@/components/ui/Feedback';
import { Modal } from '@/components/ui/Overlay';
import { Pagination } from '@/components/ui/Misc';
import { formatDateTime, formatPrice, orderStatusLabels, paymentMethodLabels, paymentStatusLabels } from '@/utils/format';
import { AdminPage, Panel, SearchInput, TableShell, td, th } from '../components/AdminUi';
import { RecentOrdersTable } from './DashboardPages';

export function OrderListPage() {
  const [params, setParams] = useSearchParams();
  const get = (k: string) => params.get(k) || undefined;
  const page = Number(params.get('page') ?? 1);
  const query = { q: get('q'), status: get('status') as OrderStatus | undefined, paymentStatus: get('paymentStatus') as PaymentStatus | undefined, paymentMethod: get('paymentMethod') as PaymentMethod | undefined, from: get('from'), to: get('to'), page, pageSize: 25 };
  const set = (k: string, v?: string) => setParams((p) => { const n = new URLSearchParams(p); if (v) n.set(k, v); else n.delete(k); if (k !== 'page') n.delete('page'); return n; });
  const list = useQuery({ queryKey: ['admin', 'orders', query], queryFn: () => adminApi.orders(query), placeholderData: keepPreviousData });

  return (
    <AdminPage title="Orders" description="Order status and payment status are tracked separately.">
      <div className="mb-4 flex flex-wrap items-end gap-2">
        <SearchInput value={query.q ?? ''} onChange={(v) => set('q', v)} placeholder="Order no., name, email, phone" />
        <Select aria-label="Order status" value={query.status ?? ''} onChange={(e) => set('status', e.target.value)} className="w-auto py-2 text-sm">
          <option value="">All statuses</option>{Object.entries(orderStatusLabels).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </Select>
        <Select aria-label="Payment status" value={query.paymentStatus ?? ''} onChange={(e) => set('paymentStatus', e.target.value)} className="w-auto py-2 text-sm">
          <option value="">All payments</option>{Object.entries(paymentStatusLabels).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </Select>
        <Select aria-label="Payment method" value={query.paymentMethod ?? ''} onChange={(e) => set('paymentMethod', e.target.value)} className="w-auto py-2 text-sm">
          <option value="">All methods</option>{Object.entries(paymentMethodLabels).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </Select>
        <Input type="date" aria-label="From date" value={query.from ?? ''} onChange={(e) => set('from', e.target.value)} className="w-auto py-2 text-sm" />
        <Input type="date" aria-label="To date" value={query.to ?? ''} onChange={(e) => set('to', e.target.value)} className="w-auto py-2 text-sm" />
      </div>
      <TableShell loading={list.isPending} error={list.error} onRetry={() => list.refetch()} empty={list.data?.items.length === 0 ? <EmptyState title="No orders match" /> : undefined}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>Order</th><th className={th}>Customer</th><th className={th}>Status</th><th className={th}>Payment</th><th className={th}>Method</th><th className={`${th} text-right`}>Total</th></tr></thead>
          <tbody className="divide-y divide-line">
            {list.data?.items.map((o) => (
              <tr key={o.id} className="hover:bg-bg">
                <td className={td}><Link to={`/admin/orders/${o.id}`} className="font-mono text-xs link-underline">{o.orderNumber}</Link><p className="text-xs text-muted">{formatDateTime(o.placedAt)}</p></td>
                <td className={td}>{o.customerName}<p className="text-xs text-muted">{o.customerEmail}</p></td>
                <td className={td}><OrderStatusBadge status={o.status} /></td>
                <td className={td}><PaymentStatusBadge status={o.paymentStatus} /></td>
                <td className={`${td} text-xs`}>{paymentMethodLabels[o.paymentMethod]}<p className="text-muted">{o.fulfillmentMethod === 'LocalPickup' ? 'Pickup' : 'Delivery'}</p></td>
                <td className={`${td} text-right whitespace-nowrap`}>{formatPrice(o.total)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      {list.data && <Pagination page={page} totalPages={list.data.totalPages} onChange={(n) => set('page', String(n))} />}
    </AdminPage>
  );
}

function ProofLink({ orderId, proof }: { orderId: number; proof: AdminOrder['payments'][number]['proofs'][number] }) {
  const [busy, setBusy] = useState(false);
  return (
    <Button variant="link" className="text-xs" disabled={busy} onClick={async () => {
      setBusy(true);
      try {
        const blob = await adminApi.proofBlob(orderId, proof.id);
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank', 'noopener');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      } catch (e) { toast.error(errorMessage(e)); } finally { setBusy(false); }
    }}><FileText className="h-3.5 w-3.5" /> {proof.fileName}</Button>
  );
}

export function OrderDetailPage() {
  const id = Number(useParams().id);
  const queryClient = useQueryClient();
  const q = useQuery({ queryKey: ['admin', 'order', id], queryFn: () => adminApi.order(id) });
  const [statusModal, setStatusModal] = useState<OrderStatus | null>(null);
  const [paymentModal, setPaymentModal] = useState<PaymentStatus | null>(null);
  const [refundOpen, setRefundOpen] = useState(false);
  const [note, setNote] = useState('');
  const [restock, setRestock] = useState(true);
  const [tracking, setTracking] = useState({ carrier: '', number: '', url: '' });
  const [reference, setReference] = useState('');
  const [refund, setRefund] = useState({ amount: '', note: '', reference: '' });
  const [adminNotes, setAdminNotes] = useState('');

  useEffect(() => {
    if (!q.data) return;
    setAdminNotes(q.data.adminNotes ?? '');
    setTracking({ carrier: q.data.order.trackingCarrier ?? '', number: q.data.order.trackingNumber ?? '', url: q.data.order.trackingUrl ?? '' });
  }, [q.data]);

  const onUpdated = (o: AdminOrder) => {
    queryClient.setQueryData(['admin', 'order', id], o);
    queryClient.invalidateQueries({ queryKey: ['admin', 'orders'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'dashboard'] });
    setStatusModal(null); setPaymentModal(null); setRefundOpen(false); setNote(''); setReference('');
    toast.success('Order updated');
  };
  const onError = (e: unknown) => toast.error(errorMessage(e));
  const updateStatus = useMutation({
    mutationFn: (status: OrderStatus) => adminApi.updateOrderStatus(id, { status, note: note || null, restock, trackingCarrier: tracking.carrier || null, trackingNumber: tracking.number || null, trackingUrl: tracking.url || null, expectedVersion: q.data?.version }),
    onSuccess: onUpdated, onError,
  });
  const updatePayment = useMutation({ mutationFn: (status: PaymentStatus) => adminApi.updatePaymentStatus(id, { status, note: note || null, reference: reference || null }), onSuccess: onUpdated, onError });
  const recordRefund = useMutation({ mutationFn: () => adminApi.recordRefund(id, { amount: Number(refund.amount), note: refund.note || null, reference: refund.reference || null }), onSuccess: onUpdated, onError });
  const saveDetails = useMutation({ mutationFn: () => adminApi.updateOrderDetails(id, { adminNotes: adminNotes || null, trackingCarrier: tracking.carrier || null, trackingNumber: tracking.number || null, trackingUrl: tracking.url || null }), onSuccess: onUpdated, onError });

  if (q.isPending) return <PageSpinner />;
  if (q.isError) return <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} />;
  const d = q.data;
  const o = d.order;
  const refundable = o.total - o.refundedTotal;

  return (
    <AdminPage title={`Order ${o.orderNumber}`} description={<>Placed {formatDateTime(o.placedAt)} · {paymentMethodLabels[o.paymentMethod]} · {o.fulfillmentMethod === 'LocalPickup' ? 'Local pickup' : 'Delivery'}</>}
      actions={<Link to="/admin/orders" className="text-sm link-underline">← All orders</Link>}>
      <div className="grid gap-6 xl:grid-cols-[1fr_360px]">
        <div className="space-y-6">
          <Panel title="Order status" actions={<OrderStatusBadge status={o.status} />}>
            {d.nextStatuses.length === 0 ? <p className="text-sm text-muted">This order is {o.status.toLowerCase()} — no further status changes are possible.</p> : (
              <div className="flex flex-wrap gap-2">
                {d.nextStatuses.map((s) => <Button key={s} size="sm" variant={s === 'Cancelled' ? 'ghost' : 'outline'} className={s === 'Cancelled' ? 'text-danger' : undefined} onClick={() => setStatusModal(s)}>Mark {orderStatusLabels[s].toLowerCase()}</Button>)}
              </div>
            )}
            {o.status === 'Cancelled' && <p className="mt-3 text-sm text-muted">Cancelled{o.cancellationReason && `: ${o.cancellationReason}`}. Stock {d.stockRestored ? 'was returned to sale' : 'was not restocked'}.</p>}
          </Panel>

          <Panel title="Payment" actions={<PaymentStatusBadge status={o.paymentStatus} />}>
            {o.paymentStatus === 'PendingVerification' && <div className="mb-4"><Notice title="Verify this payment">Check your bank statement for the reference below before marking it paid.</Notice></div>}
            {d.paymentDueAt && o.paymentStatus === 'Unpaid' && o.status === 'Pending' && <p className="mb-3 text-sm text-muted">Payment due by {formatDateTime(d.paymentDueAt)} — unpaid bank transfers are cancelled automatically after this.</p>}
            <ul className="divide-y divide-line text-sm">
              {d.payments.map((p) => (
                <li key={p.id} className="py-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span>{p.kind === 'Refund' ? 'Refund' : 'Payment'} · {formatPrice(p.amount)} <Badge tone={p.status === 'Succeeded' ? 'success' : p.status === 'Failed' ? 'danger' : 'neutral'}>{p.status}</Badge></span>
                    <span className="text-xs text-muted">{formatDateTime(p.createdAt)}</span>
                  </div>
                  {p.reference && <p className="mt-1 text-xs">Reference: <span className="font-mono">{p.reference}</span></p>}
                  {p.notes && <p className="mt-1 text-xs text-muted">{p.notes}</p>}
                  {p.verifiedAt && <p className="mt-1 text-xs text-muted">Verified {formatDateTime(p.verifiedAt)}{p.verifiedBy && ` by ${p.verifiedBy}`}</p>}
                  {p.proofs.map((f) => <ProofLink key={f.id} orderId={o.id} proof={f} />)}
                </li>
              ))}
            </ul>
            <div className="mt-4 flex flex-wrap gap-2">
              {d.nextPaymentStatuses.filter((s) => s !== 'Refunded').map((s) => (
                <Button key={s} size="sm" variant={s === 'Paid' ? 'primary' : 'outline'} disabled={s === 'Paid' && o.status === 'Cancelled'} onClick={() => setPaymentModal(s)}>
                  {s === 'Paid' ? (o.paymentMethod === 'CashOnDelivery' ? 'Record cash received' : 'Mark as paid (verified)') : `Mark ${paymentStatusLabels[s].toLowerCase()}`}
                </Button>
              ))}
              {o.paymentStatus === 'Paid' && <Button size="sm" variant="ghost" onClick={() => { setRefund({ amount: String(refundable), note: '', reference: '' }); setRefundOpen(true); }}>Record refund</Button>}
            </div>
            {o.refundedTotal > 0 && <p className="mt-3 text-sm text-muted">Refunded so far: {formatPrice(o.refundedTotal)}</p>}
          </Panel>

          <Panel title="Items">
            <ul className="divide-y divide-line">
              {o.items.map((i) => (
                <li key={i.productId} className="flex items-center gap-3 py-3 text-sm">
                  {i.imageUrl ? <img src={i.imageUrl} alt="" className="h-14 w-11 rounded-theme object-cover" /> : <div className="h-14 w-11 rounded-theme bg-surface" />}
                  <div className="flex-1"><Link to={`/admin/products/${i.productId}`} className="font-medium link-underline">{i.productName}</Link><p className="text-xs text-muted">SKU {i.sku} · {formatPrice(i.unitPrice)} × {i.quantity}</p></div>
                  <span>{formatPrice(i.lineTotal)}</span>
                </li>
              ))}
            </ul>
            <dl className="mt-4 ml-auto max-w-xs space-y-1 text-sm">
              <div className="flex justify-between"><dt>Subtotal</dt><dd>{formatPrice(o.subtotal)}</dd></div>
              {o.discountTotal > 0 && <div className="flex justify-between"><dt>Discount ({o.discountCode})</dt><dd>−{formatPrice(o.discountTotal)}</dd></div>}
              <div className="flex justify-between"><dt>{o.shippingRateName ?? 'Shipping'}</dt><dd>{formatPrice(o.shippingTotal)}</dd></div>
              <div className="flex justify-between border-t border-line pt-1 font-medium"><dt>Total</dt><dd>{formatPrice(o.total)}</dd></div>
            </dl>
          </Panel>

          <Panel title="History">
            <ol className="space-y-2 text-sm">
              {d.history.map((h, i) => (
                <li key={i} className="flex flex-wrap gap-x-2">
                  <span className="text-muted">{formatDateTime(h.createdAt)}</span>
                  <span>{h.toStatus ? `${h.fromStatus ? `${orderStatusLabels[h.fromStatus]} → ` : ''}${orderStatusLabels[h.toStatus]}` : h.toPaymentStatus ? `Payment ${h.fromPaymentStatus ? `${paymentStatusLabels[h.fromPaymentStatus]} → ` : ''}${paymentStatusLabels[h.toPaymentStatus]}` : 'Note'}</span>
                  {h.note && <span className="text-muted">· {h.note}</span>}
                  {h.changedBy && <span className="text-muted">· {h.changedBy}</span>}
                </li>
              ))}
            </ol>
          </Panel>
        </div>

        <div className="space-y-6">
          <Panel title="Customer">
            <p className="font-medium">{o.contact.fullName}</p>
            <p className="text-sm"><a href={`mailto:${o.contact.email}`} className="link-underline">{o.contact.email}</a></p>
            <p className="flex items-center gap-1 text-sm"><Phone className="h-3.5 w-3.5" aria-hidden /><a href={`tel:${o.contact.phone}`} className="link-underline">{o.contact.phone}</a></p>
            <p className="mt-2 text-xs text-muted"><Link to={`/admin/customers/${d.userId}`} className="link-underline">View customer</Link> · {d.customerOrderCount} order(s)</p>
            {o.address && <p className="mt-4 text-sm">{o.address.line1}{o.address.line2 && <>, {o.address.line2}</>}<br />{o.address.city}<br />{o.address.district} {o.address.postalCode}</p>}
            {o.notes && <div className="mt-4"><Notice title="Customer note">{o.notes}</Notice></div>}
          </Panel>
          <Panel title="Tracking & internal notes">
            <div className="space-y-3">
              <Field label="Courier">{(p) => <Input {...p} value={tracking.carrier} onChange={(e) => setTracking({ ...tracking, carrier: e.target.value })} />}</Field>
              <Field label="Tracking number">{(p) => <Input {...p} value={tracking.number} onChange={(e) => setTracking({ ...tracking, number: e.target.value })} />}</Field>
              <Field label="Tracking link">{(p) => <Input {...p} type="url" value={tracking.url} onChange={(e) => setTracking({ ...tracking, url: e.target.value })} placeholder="https://" />}</Field>
              <Field label="Admin notes" hint="Never shown to the customer.">{(p) => <Textarea {...p} rows={4} value={adminNotes} onChange={(e) => setAdminNotes(e.target.value)} />}</Field>
              <Button size="sm" loading={saveDetails.isPending} onClick={() => saveDetails.mutate()}>Save</Button>
            </div>
          </Panel>
        </div>
      </div>

      <Modal open={statusModal !== null} onClose={() => setStatusModal(null)} title={statusModal ? `Mark as ${orderStatusLabels[statusModal].toLowerCase()}` : ''} size="sm"
        footer={<><Button variant="ghost" onClick={() => setStatusModal(null)}>Back</Button><Button variant={statusModal === 'Cancelled' ? 'danger' : 'primary'} loading={updateStatus.isPending} onClick={() => statusModal && updateStatus.mutate(statusModal)}>Confirm</Button></>}>
        <div className="space-y-4">
          {statusModal === 'Shipped' && o.fulfillmentMethod === 'Delivery' && (
            <>
              <Field label="Courier">{(p) => <Input {...p} value={tracking.carrier} onChange={(e) => setTracking({ ...tracking, carrier: e.target.value })} />}</Field>
              <Field label="Tracking number">{(p) => <Input {...p} value={tracking.number} onChange={(e) => setTracking({ ...tracking, number: e.target.value })} />}</Field>
            </>
          )}
          {statusModal === 'Cancelled' && (
            <>
              <Checkbox label="Return items to sale" description="Untick if the item was damaged or is no longer available." checked={restock} onChange={(e) => setRestock(e.target.checked)} />
              {o.paymentStatus === 'Paid' && <Notice tone="danger">This order is paid. Cancelling does not refund automatically — record the refund separately after returning the money.</Notice>}
            </>
          )}
          <Field label={statusModal === 'Cancelled' ? 'Reason (shown to the customer)' : 'Note for the customer (optional)'}>{(p) => <Textarea {...p} rows={2} value={note} onChange={(e) => setNote(e.target.value)} />}</Field>
        </div>
      </Modal>

      <Modal open={paymentModal !== null} onClose={() => setPaymentModal(null)} title={paymentModal ? `Payment: ${paymentStatusLabels[paymentModal]}` : ''} size="sm"
        footer={<><Button variant="ghost" onClick={() => setPaymentModal(null)}>Back</Button><Button loading={updatePayment.isPending} onClick={() => paymentModal && updatePayment.mutate(paymentModal)}>Confirm</Button></>}>
        <div className="space-y-4">
          {paymentModal === 'Paid' && <p className="text-sm">Only mark as paid after confirming the money has arrived in your account (or cash was received).</p>}
          <Field label="Bank / receipt reference (optional)">{(p) => <Input {...p} value={reference} onChange={(e) => setReference(e.target.value)} />}</Field>
          <Field label="Note">{(p) => <Textarea {...p} rows={2} value={note} onChange={(e) => setNote(e.target.value)} />}</Field>
        </div>
      </Modal>

      <Modal open={refundOpen} onClose={() => setRefundOpen(false)} title="Record a refund" size="sm"
        footer={<><Button variant="ghost" onClick={() => setRefundOpen(false)}>Back</Button><Button loading={recordRefund.isPending} disabled={!(Number(refund.amount) > 0 && Number(refund.amount) <= refundable)} onClick={() => recordRefund.mutate()}>Record refund</Button></>}>
        <div className="space-y-4">
          <p className="text-sm text-muted">Record a refund you have already sent to the customer. Up to {formatPrice(refundable)} can be refunded.</p>
          <Field label="Amount (LKR)" required>{(p) => <Input {...p} type="number" min={0} max={refundable} step="0.01" value={refund.amount} onChange={(e) => setRefund({ ...refund, amount: e.target.value })} />}</Field>
          <Field label="Reference">{(p) => <Input {...p} value={refund.reference} onChange={(e) => setRefund({ ...refund, reference: e.target.value })} />}</Field>
          <Field label="Note">{(p) => <Textarea {...p} rows={2} value={refund.note} onChange={(e) => setRefund({ ...refund, note: e.target.value })} />}</Field>
        </div>
      </Modal>
    </AdminPage>
  );
}

export function CustomersPage() {
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);
  const list = useQuery({ queryKey: ['admin', 'customers', q, page], queryFn: () => adminApi.customers({ q, page, pageSize: 25 }), placeholderData: keepPreviousData });
  return (
    <AdminPage title="Customers" description="Accounts are created when someone signs in for the first time.">
      <div className="mb-4"><SearchInput value={q} onChange={(v) => { setQ(v); setPage(1); }} placeholder="Name, email or phone" /></div>
      <TableShell loading={list.isPending} error={list.error} empty={list.data?.items.length === 0 ? <EmptyState title="No customers found" /> : undefined}>
        <table className="w-full">
          <thead className="border-b border-line bg-bg"><tr><th className={th}>Customer</th><th className={th}>Role</th><th className={th}>Orders</th><th className={`${th} text-right`}>Paid total</th><th className={th}>Joined</th></tr></thead>
          <tbody className="divide-y divide-line">
            {list.data?.items.map((c) => (
              <tr key={c.id} className="hover:bg-bg">
                <td className={td}><Link to={`/admin/customers/${c.id}`} className="font-medium link-underline">{c.displayName ?? c.email}</Link><p className="text-xs text-muted">{c.email}{!c.emailVerified && ' · unverified'}</p></td>
                <td className={td}>{c.role === 'Admin' ? <Badge tone="dark">Admin</Badge> : <Badge>Customer</Badge>}</td>
                <td className={td}>{c.orderCount}</td>
                <td className={`${td} text-right`}>{formatPrice(c.paidTotal)}</td>
                <td className={`${td} text-xs text-muted`}>{formatDateTime(c.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </TableShell>
      {list.data && <Pagination page={page} totalPages={list.data.totalPages} onChange={setPage} />}
    </AdminPage>
  );
}

export function CustomerDetailPage() {
  const id = Number(useParams().id);
  const q = useQuery({ queryKey: ['admin', 'customer', id], queryFn: () => adminApi.customer(id) });
  if (q.isPending) return <PageSpinner />;
  if (q.isError) return <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} />;
  const { customer: c, addresses, orders } = q.data;
  return (
    <AdminPage title={c.displayName ?? c.email} description={<>{c.email} · joined {formatDateTime(c.createdAt)}{c.lastLoginAt && ` · last seen ${formatDateTime(c.lastLoginAt)}`}</>} actions={<Link to="/admin/customers" className="text-sm link-underline">← All customers</Link>}>
      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        <Panel title={`Orders (${c.orderCount}) · ${formatPrice(c.paidTotal)} paid`}><RecentOrdersTable orders={orders} /></Panel>
        <div className="space-y-6">
          <Panel title="Contact"><p className="text-sm">{c.phoneNumber ?? 'No phone saved'}</p><p className="text-sm">{c.emailVerified ? 'Email verified' : 'Email not verified'}</p><p className="mt-2 text-xs text-muted">Role: {c.role}. Roles are changed only via the server CLI.</p></Panel>
          <Panel title="Addresses">
            {addresses.length === 0 ? <p className="text-sm text-muted">None saved.</p> : <ul className="space-y-3 text-sm">{addresses.map((a) => <li key={a.id}><p className="font-medium">{a.label}{a.isDefault && ' (default)'}</p><p className="text-muted">{a.line1}, {a.city}, {a.district}</p></li>)}</ul>}
          </Panel>
        </div>
      </div>
    </AdminPage>
  );
}
