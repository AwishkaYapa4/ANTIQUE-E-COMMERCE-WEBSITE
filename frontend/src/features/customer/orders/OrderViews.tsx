import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2 } from 'lucide-react';
import { toast } from 'sonner';
import { ordersApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import type { Order } from '@/types/api';
import { Seo } from '@/components/Seo';
import { Button, ButtonLink } from '@/components/ui/Button';
import { Field, Input, Textarea } from '@/components/ui/Field';
import { ErrorState, Notice, OrderStatusBadge, PageSpinner, PaymentStatusBadge } from '@/components/ui/Feedback';
import { useConfirm } from '@/components/ui/Overlay';
import { SmartImage } from '@/components/ui/Misc';
import { formatDateTime, formatPrice, orderStatusLabels, paymentMethodLabels, paymentStatusLabels } from '@/utils/format';

const PROOF_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'];

function PaymentSubmission({ order }: { order: Order }) {
  const queryClient = useQueryClient();
  const [reference, setReference] = useState('');
  const [notes, setNotes] = useState('');
  const [proof, setProof] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const submit = useMutation({
    mutationFn: () => ordersApi.submitPayment(order.id, reference.trim(), notes.trim() || null, proof),
    onSuccess: (o) => { queryClient.setQueryData(['order', o.id], o); toast.success('Thank you — we will verify your payment shortly.'); },
    onError: (e) => setError(errorMessage(e)),
  });
  return (
    <form className="mt-5 space-y-4 border-t border-line pt-5" onSubmit={(e) => {
      e.preventDefault();
      setError(null);
      if (!reference.trim()) return setError('Enter the transfer reference from your bank receipt.');
      if (proof && (!PROOF_TYPES.includes(proof.type) || proof.size > 5 * 1024 * 1024)) return setError('Upload a JPG, PNG, WEBP or PDF under 5 MB.');
      submit.mutate();
    }}>
      <h3 className="font-body text-sm font-semibold">Already paid? Tell us about your transfer</h3>
      {error && <Notice tone="danger">{error}</Notice>}
      <Field label="Transfer reference" required>{(p) => <Input {...p} value={reference} onChange={(e) => setReference(e.target.value)} maxLength={200} />}</Field>
      <Field label="Proof of payment (optional)" hint="Photo or PDF of your receipt, max 5 MB. Only visible to the store.">{(p) => (
        <input {...p} type="file" accept={PROOF_TYPES.join(',')} onChange={(e) => setProof(e.target.files?.[0] ?? null)} className="block w-full text-sm file:mr-3 file:rounded-theme file:border-0 file:bg-surface file:px-3 file:py-2" />
      )}</Field>
      <Field label="Notes (optional)">{(p) => <Textarea {...p} rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} maxLength={1000} />}</Field>
      <Button type="submit" loading={submit.isPending}>Submit payment details</Button>
    </form>
  );
}

function PaymentPanel({ order }: { order: Order }) {
  const i = order.paymentInstructions;
  if (order.paymentStatus === 'PendingVerification') return <Notice title="Payment submitted">We're verifying your transfer. You'll see the status change here once confirmed.</Notice>;
  if (!i) return null;
  return (
    <div className="rounded-theme border border-accent/40 bg-accent/5 p-5">
      <h2 className="font-body text-sm font-semibold">{paymentMethodLabels[i.method]}</h2>
      <p className="mt-1 text-sm">{i.summary}</p>
      {i.bankDetails && (
        <dl className="mt-4 grid gap-x-6 gap-y-1 text-sm sm:grid-cols-[auto_1fr]">
          {i.bankDetails.bankName && <><dt className="text-muted">Bank</dt><dd>{i.bankDetails.bankName}</dd></>}
          {i.bankDetails.accountName && <><dt className="text-muted">Account name</dt><dd>{i.bankDetails.accountName}</dd></>}
          {i.bankDetails.accountNumber && <><dt className="text-muted">Account number</dt><dd className="font-mono">{i.bankDetails.accountNumber}</dd></>}
          {i.bankDetails.branch && <><dt className="text-muted">Branch</dt><dd>{i.bankDetails.branch}</dd></>}
          <dt className="text-muted">Reference</dt><dd className="font-mono">{order.orderNumber}</dd>
        </dl>
      )}
      {i.instructions && <p className="mt-3 text-sm text-muted">{i.instructions}</p>}
      {i.paymentDueAt && <p className="mt-2 text-sm">Please pay by <strong>{formatDateTime(i.paymentDueAt)}</strong>, otherwise the order may be cancelled and the items released.</p>}
      {order.canSubmitPayment && <PaymentSubmission order={order} />}
    </div>
  );
}

export function OrderDetails({ order }: { order: Order }) {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const cancel = useMutation({
    mutationFn: () => ordersApi.cancel(order.id, null),
    onSuccess: (o) => { queryClient.setQueryData(['order', o.id], o); queryClient.invalidateQueries({ queryKey: ['orders'] }); toast.success('Your order has been cancelled.'); },
    onError: (e) => toast.error(errorMessage(e)),
  });
  return (
    <div className="space-y-8">
      <div className="flex flex-wrap items-center gap-3">
        <OrderStatusBadge status={order.status} />
        <PaymentStatusBadge status={order.paymentStatus} />
        <span className="text-sm text-muted">Placed {formatDateTime(order.placedAt)}</span>
      </div>
      <PaymentPanel order={order} />
      {order.trackingNumber && (
        <Notice title="Tracking">
          {order.trackingCarrier} · <span className="font-mono">{order.trackingNumber}</span>
          {order.trackingUrl && <> · <a href={order.trackingUrl} target="_blank" rel="noopener noreferrer" className="link-underline">Track parcel</a></>}
        </Notice>
      )}
      <div className="grid gap-8 md:grid-cols-[1fr_300px]">
        <ul className="divide-y divide-line border-y border-line">
          {order.items.map((i) => (
            <li key={i.productId} className="flex gap-4 py-4">
              <SmartImage src={i.imageUrl} alt={i.productName} ratio="4 / 5" className="w-16 shrink-0 rounded-theme" sizes="64px" />
              <div className="flex-1 text-sm">
                <Link to={`/products/${i.productSlug}`} className="font-heading text-lg hover:text-accent">{i.productName}</Link>
                <p className="text-muted">SKU {i.sku}{i.quantity > 1 && ` · Qty ${i.quantity}`}</p>
              </div>
              <p className="text-sm">{formatPrice(i.lineTotal)}</p>
            </li>
          ))}
        </ul>
        <dl className="space-y-2 text-sm">
          <div className="flex justify-between"><dt>Subtotal</dt><dd>{formatPrice(order.subtotal)}</dd></div>
          {order.discountTotal > 0 && <div className="flex justify-between text-success"><dt>Discount {order.discountCode}</dt><dd>−{formatPrice(order.discountTotal)}</dd></div>}
          <div className="flex justify-between"><dt>{order.shippingRateName ?? 'Delivery'}</dt><dd>{order.shippingTotal === 0 ? 'Free' : formatPrice(order.shippingTotal)}</dd></div>
          <div className="flex justify-between border-t border-line pt-2 text-base font-medium"><dt>Total</dt><dd>{formatPrice(order.total)}</dd></div>
          {order.refundedTotal > 0 && <div className="flex justify-between text-muted"><dt>Refunded</dt><dd>{formatPrice(order.refundedTotal)}</dd></div>}
          <div className="pt-4 text-muted">
            <p className="font-medium text-ink">{order.fulfillmentMethod === 'Delivery' ? 'Delivery address' : 'Local pickup'}</p>
            {order.address && <p>{order.address.line1}{order.address.line2 && `, ${order.address.line2}`}<br />{order.address.city}, {order.address.district} {order.address.postalCode}</p>}
            <p className="mt-2">{order.contact.fullName} · {order.contact.phone}</p>
          </div>
        </dl>
      </div>
      {order.history.length > 0 && (
        <section>
          <h2 className="mb-3 font-body text-xs font-semibold tracking-[0.18em] uppercase">Order updates</h2>
          <ol className="space-y-2 border-l border-line pl-5 text-sm">
            {order.history.map((h, idx) => (
              <li key={idx}>
                <span className="text-muted">{formatDateTime(h.createdAt)}</span> — {h.status ? orderStatusLabels[h.status] : h.paymentStatus ? `Payment: ${paymentStatusLabels[h.paymentStatus]}` : ''}
                {h.note && <span className="text-muted"> · {h.note}</span>}
              </li>
            ))}
          </ol>
        </section>
      )}
      {order.canCancel && (
        <Button variant="outline" loading={cancel.isPending} onClick={async () => {
          if (await confirm({ title: 'Cancel this order?', message: 'The items will be released for other customers. This cannot be undone.', confirmLabel: 'Cancel order', danger: true })) cancel.mutate();
        }}>Cancel order</Button>
      )}
    </div>
  );
}

export function OrderConfirmationPage() {
  const id = Number(useParams().id);
  const order = useQuery({ queryKey: ['order', id], queryFn: () => ordersApi.get(id), enabled: Number.isFinite(id) });
  if (order.isPending) return <PageSpinner />;
  if (order.isError) return <ErrorState message={errorMessage(order.error)} onRetry={() => order.refetch()} />;
  const o = order.data;
  return (
    <div className="container-page max-w-4xl py-12 sm:py-16">
      <Seo title="Order confirmed" noIndex />
      <div className="mb-10 text-center">
        <CheckCircle2 className="mx-auto h-12 w-12 text-success" aria-hidden />
        <h1 className="mt-4 text-4xl">Thank you for your order</h1>
        <p className="mt-3 text-muted">Order <strong className="font-mono text-ink">{o.orderNumber}</strong> has been received. A summary is below and in your account.</p>
      </div>
      <OrderDetails order={o} />
      <div className="mt-10 flex flex-wrap justify-center gap-3">
        <ButtonLink to="/account/orders" variant="outline">View my orders</ButtonLink>
        <ButtonLink to="/shop">Continue shopping</ButtonLink>
      </div>
    </div>
  );
}
