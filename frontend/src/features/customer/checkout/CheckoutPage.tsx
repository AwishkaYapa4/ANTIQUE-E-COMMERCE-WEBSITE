import { useEffect, useMemo, useState } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Check, Clock, Lock } from 'lucide-react';
import { toast } from 'sonner';
import { checkoutApi, meApi, siteApi } from '@/services/api';
import { ApiError, errorMessage } from '@/services/http';
import { useAuth } from '@/features/auth/AuthProvider';
import type { CheckoutQuote, CheckoutReservation } from '@/types/api';
import { Seo } from '@/components/Seo';
import { Button, ButtonLink } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select, Textarea } from '@/components/ui/Field';
import { EmptyState, Notice, PageSpinner, Skeleton } from '@/components/ui/Feedback';
import { SmartImage } from '@/components/ui/Misc';
import { cn, formatPrice, newIdempotencyKey, paymentMethodLabels, SRI_LANKA_DISTRICTS } from '@/utils/format';
import { checkoutSchema, stepFields, type CheckoutValues } from './checkoutSchema';

const STEPS = ['Contact', 'Delivery', 'Payment', 'Review'];

function useDebounced<T>(value: T, ms = 400) {
  const [v, setV] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

function Countdown({ until }: { until: string }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);
  const ms = Math.max(0, new Date(until).getTime() - now);
  const m = Math.floor(ms / 60000);
  const s = Math.floor((ms % 60000) / 1000).toString().padStart(2, '0');
  return <span className="font-medium tabular-nums">{ms === 0 ? 'expired' : `${m}:${s}`}</span>;
}

function Summary({ quote, loading }: { quote?: CheckoutQuote; loading: boolean }) {
  if (!quote) return <Skeleton className="h-64 w-full" />;
  return (
    <div className={cn('rounded-theme border border-line bg-paper p-6', loading && 'opacity-70')} aria-busy={loading}>
      <h2 className="font-body text-xs font-semibold tracking-[0.18em] uppercase">Order summary</h2>
      <ul className="mt-4 divide-y divide-line">
        {quote.lines.map((l) => (
          <li key={l.id} className="flex gap-3 py-3">
            <SmartImage image={l.image} alt={l.name} ratio="4 / 5" className="w-14 shrink-0 rounded-theme" sizes="56px" />
            <div className="min-w-0 flex-1 text-sm">
              <p className="font-medium">{l.name}</p>
              {l.quantity > 1 && <p className="text-muted">Qty {l.quantity}</p>}
              {!l.isPurchasable && <p className="text-danger">{l.issue}</p>}
            </div>
            <p className="text-sm">{formatPrice(l.lineTotal)}</p>
          </li>
        ))}
      </ul>
      <dl className="mt-4 space-y-2 border-t border-line pt-4 text-sm">
        <div className="flex justify-between"><dt>Subtotal</dt><dd>{formatPrice(quote.subtotal)}</dd></div>
        {quote.discount > 0 && <div className="flex justify-between text-success"><dt>Discount ({quote.discountCode})</dt><dd>−{formatPrice(quote.discount)}</dd></div>}
        <div className="flex justify-between">
          <dt>{quote.shipping.rateName ?? 'Delivery'}</dt>
          <dd>{quote.shipping.isAvailable ? (quote.shipping.fee === 0 ? 'Free' : formatPrice(quote.shipping.fee)) : '—'}</dd>
        </div>
        {quote.shipping.estimatedDelivery && <p className="text-xs text-muted">{quote.shipping.estimatedDelivery}</p>}
        <div className="flex justify-between border-t border-line pt-3 text-base font-medium"><dt>Total</dt><dd>{formatPrice(quote.total)}</dd></div>
      </dl>
      <p className="mt-4 flex items-center gap-2 text-xs text-muted"><Lock className="h-3.5 w-3.5" aria-hidden /> Totals are calculated securely by our server. No card details are collected.</p>
    </div>
  );
}

export default function CheckoutPage() {
  const { me, firebaseUser } = useAuth();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [reservation, setReservation] = useState<CheckoutReservation | null>(null);
  const [idempotencyKey] = useState(newIdempotencyKey);
  const [appliedCode, setAppliedCode] = useState<string | null>(null);

  const addresses = useQuery({ queryKey: ['addresses'], queryFn: meApi.addresses });
  const shipping = useQuery({ queryKey: ['shipping-options'], queryFn: siteApi.shippingOptions, staleTime: 5 * 60_000 });

  const form = useForm<CheckoutValues>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: {
      fullName: me?.displayName ?? '', email: me?.email ?? '', phone: me?.phoneNumber ?? '', fulfillmentMethod: 'Delivery',
      line1: '', line2: '', city: '', district: '', postalCode: '', saveAddress: true, notes: '', paymentMethod: 'CashOnDelivery', discountCode: '',
    },
  });
  const { register, formState: { errors } } = form;
  const [fulfillment, district, paymentMethod] = useWatch({ control: form.control, name: ['fulfillmentMethod', 'district', 'paymentMethod'] });

  // Prefill from the default saved address once.
  useEffect(() => {
    const a = addresses.data?.find((x) => x.isDefault) ?? addresses.data?.[0];
    if (a && !form.getValues('line1')) {
      form.reset({ ...form.getValues(), fullName: form.getValues('fullName') || a.fullName, phone: form.getValues('phone') || a.phone,
        line1: a.line1, line2: a.line2 ?? '', city: a.city, district: a.district, postalCode: a.postalCode ?? '', saveAddress: false });
    }
  }, [addresses.data, form]);

  const quoteInput = useDebounced(useMemo(() => ({
    fulfillmentMethod: fulfillment, district: fulfillment === 'Delivery' && district ? district : null, discountCode: appliedCode, paymentMethod,
  }), [fulfillment, district, appliedCode, paymentMethod]));
  const quote = useQuery({ queryKey: ['quote', quoteInput], queryFn: () => checkoutApi.quote(quoteInput), placeholderData: (prev) => prev });

  const reserve = useMutation({
    mutationFn: checkoutApi.reserve,
    onSuccess: (r) => { setReservation(r); setStep(3); },
    onError: (e) => { toast.error(errorMessage(e)); queryClient.invalidateQueries({ queryKey: ['cart'] }); queryClient.invalidateQueries({ queryKey: ['quote'] }); },
  });

  const place = useMutation({
    mutationFn: (v: CheckoutValues) => checkoutApi.placeOrder({
      idempotencyKey, checkoutToken: reservation?.checkoutToken ?? null,
      contact: { fullName: v.fullName, email: v.email, phone: v.phone }, fulfillmentMethod: v.fulfillmentMethod,
      address: v.fulfillmentMethod === 'Delivery' ? { line1: v.line1, line2: v.line2 || null, city: v.city, district: v.district, postalCode: v.postalCode || null } : null,
      paymentMethod: v.paymentMethod, discountCode: appliedCode, notes: v.notes || null, saveAddress: v.saveAddress, expectedTotal: quote.data?.total ?? null,
    }),
    onSuccess: (order) => {
      queryClient.removeQueries({ queryKey: ['cart'] });
      queryClient.invalidateQueries({ queryKey: ['orders'] });
      queryClient.setQueryData(['order', order.id], order);
      navigate(`/order-confirmation/${order.id}`, { replace: true });
    },
    onError: (e) => {
      if (e instanceof ApiError && e.code === 'total_changed') { toast.warning(e.message); queryClient.invalidateQueries({ queryKey: ['quote'] }); }
      else if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) {
        Object.entries(e.fieldErrors).forEach(([k, v]) => form.setError(k.replace(/^contact\.|^address\./, '') as keyof CheckoutValues, { message: v[0] }));
        toast.error('Please check the highlighted fields.');
        setStep(0);
      } else toast.error(errorMessage(e));
    },
  });

  if (firebaseUser && !firebaseUser.emailVerified) {
    return (
      <div className="container-page max-w-xl py-20">
        <Notice title="Please verify your email to place an order">
          We sent a verification link to {firebaseUser.email}. <Link to="/verify-email?redirect=/checkout" className="link-underline">Verify now</Link>
        </Notice>
      </div>
    );
  }
  if (quote.isPending) return <PageSpinner />;
  if (quote.data && quote.data.lines.length === 0) {
    return <EmptyState title="Your cart is empty" description="Add a piece to your cart to check out." action={<ButtonLink to="/shop">Browse the collection</ButtonLink>} />;
  }
  if (quote.data?.lines.some((l) => !l.isPurchasable) && step < 3) return <Navigate to="/cart" replace />;

  const next = async () => {
    const valid = await form.trigger(stepFields[step]);
    if (!valid) return;
    if (step === 2) {
      // Check against a fresh server quote for the *current* selections (the summary quote is debounced).
      const v = form.getValues();
      const input = { fulfillmentMethod: v.fulfillmentMethod, district: v.fulfillmentMethod === 'Delivery' ? v.district || null : null, discountCode: appliedCode, paymentMethod: v.paymentMethod };
      let fresh: CheckoutQuote;
      try {
        fresh = await queryClient.fetchQuery({ queryKey: ['quote', input], queryFn: () => checkoutApi.quote(input), staleTime: 0 });
      } catch (e) {
        toast.error(errorMessage(e));
        return;
      }
      if (!fresh.canPlaceOrder) { toast.error(fresh.issues[0] ?? 'Please review your order.'); return; }
      reserve.mutate();
      return;
    }
    setStep((s) => s + 1);
  };

  const q = quote.data;
  const methods = q?.paymentMethods ?? [];

  return (
    <div className="container-page py-10 sm:py-14">
      <Seo title="Checkout" noIndex />
      <h1 className="text-4xl">Checkout</h1>
      <ol className="mt-8 flex flex-wrap gap-x-6 gap-y-2 text-sm" aria-label="Checkout progress">
        {STEPS.map((label, i) => (
          <li key={label} className={cn('flex items-center gap-2', i === step ? 'text-ink' : 'text-muted')} aria-current={i === step ? 'step' : undefined}>
            <span className={cn('flex h-6 w-6 items-center justify-center rounded-full border text-xs', i < step ? 'border-success bg-success text-paper' : i === step ? 'border-ink' : 'border-line')}>
              {i < step ? <Check className="h-3.5 w-3.5" /> : i + 1}
            </span>
            {label}
          </li>
        ))}
      </ol>

      <div className="mt-10 grid gap-10 lg:grid-cols-[1fr_380px]">
        <form onSubmit={(e) => { e.preventDefault(); if (step === 3) form.handleSubmit((v) => place.mutate(v))(); else next(); }} noValidate className="space-y-6">
          {step === 0 && (
            <section className="space-y-4">
              <h2 className="text-2xl">Contact information</h2>
              <Field label="Full name" required error={errors.fullName?.message}>{(p) => <Input {...p} autoComplete="name" {...register('fullName')} />}</Field>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Email" required error={errors.email?.message}>{(p) => <Input {...p} type="email" autoComplete="email" {...register('email')} />}</Field>
                <Field label="Phone number" required error={errors.phone?.message} hint="We'll call to arrange delivery.">{(p) => <Input {...p} type="tel" autoComplete="tel" {...register('phone')} />}</Field>
              </div>
            </section>
          )}

          {step === 1 && (
            <section className="space-y-4">
              <h2 className="text-2xl">Delivery</h2>
              <fieldset className="grid gap-3 sm:grid-cols-2">
                <legend className="sr-only">Delivery method</legend>
                <label className={cn('cursor-pointer rounded-theme border p-4', fulfillment === 'Delivery' ? 'border-primary bg-paper' : 'border-line')}>
                  <input type="radio" value="Delivery" className="mr-2 accent-[var(--c-primary)]" {...register('fulfillmentMethod')} /> Deliver to my address
                </label>
                <label className={cn('rounded-theme border p-4', !shipping.data?.localPickupEnabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer', fulfillment === 'LocalPickup' ? 'border-primary bg-paper' : 'border-line')}>
                  <input type="radio" value="LocalPickup" disabled={!shipping.data?.localPickupEnabled} className="mr-2 accent-[var(--c-primary)]" {...register('fulfillmentMethod')} /> Collect in person
                  {!shipping.data?.localPickupEnabled && <span className="mt-1 block text-xs text-muted">Not currently offered</span>}
                </label>
              </fieldset>

              {fulfillment === 'Delivery' ? (
                <>
                  {(addresses.data?.length ?? 0) > 0 && (
                    <Field label="Use a saved address">{(p) => (
                      <Select {...p} defaultValue="" onChange={(e) => {
                        const a = addresses.data!.find((x) => x.id === Number(e.target.value));
                        if (a) form.reset({ ...form.getValues(), line1: a.line1, line2: a.line2 ?? '', city: a.city, district: a.district, postalCode: a.postalCode ?? '', saveAddress: false });
                      }}>
                        <option value="" disabled>Choose…</option>
                        {addresses.data!.map((a) => <option key={a.id} value={a.id}>{a.label}: {a.line1}, {a.city}</option>)}
                      </Select>
                    )}</Field>
                  )}
                  <Field label="Address" required error={errors.line1?.message}>{(p) => <Input {...p} autoComplete="address-line1" {...register('line1')} />}</Field>
                  <Field label="Apartment, building (optional)" error={errors.line2?.message}>{(p) => <Input {...p} autoComplete="address-line2" {...register('line2')} />}</Field>
                  <div className="grid gap-4 sm:grid-cols-3">
                    <Field label="City / town" required error={errors.city?.message}>{(p) => <Input {...p} autoComplete="address-level2" {...register('city')} />}</Field>
                    <Field label="District" required error={errors.district?.message}>{(p) => (
                      <Select {...p} {...register('district')}>
                        <option value="">Select…</option>
                        {SRI_LANKA_DISTRICTS.map((d) => (
                          <option key={d} value={d}>{d}{shipping.data && !shipping.data.deliverableDistricts.includes(d) ? ' (no delivery)' : ''}</option>
                        ))}
                      </Select>
                    )}</Field>
                    <Field label="Postal code" error={errors.postalCode?.message}>{(p) => <Input {...p} autoComplete="postal-code" {...register('postalCode')} />}</Field>
                  </div>
                  <Checkbox label="Save this address to my account" {...register('saveAddress')} />
                  {shipping.data?.deliveryInstructions && <p className="text-sm text-muted">{shipping.data.deliveryInstructions}</p>}
                </>
              ) : (
                <Notice title="Collection address">
                  <p className="whitespace-pre-line">{shipping.data?.localPickupAddress}</p>
                  {shipping.data?.localPickupInstructions && <p className="mt-2">{shipping.data.localPickupInstructions}</p>}
                </Notice>
              )}
              {q && !q.shipping.isAvailable && q.shipping.unavailableReason && district && <Notice tone="danger">{q.shipping.unavailableReason}</Notice>}
              <Field label="Order notes (optional)" error={errors.notes?.message}>{(p) => <Textarea {...p} rows={3} placeholder="Delivery instructions, best time to call…" {...register('notes')} />}</Field>
            </section>
          )}

          {step === 2 && (
            <section className="space-y-4">
              <h2 className="text-2xl">Payment method</h2>
              <fieldset className="space-y-3">
                <legend className="sr-only">Payment method</legend>
                {methods.map((m) => (
                  <label key={m.method} className={cn('block rounded-theme border p-4', m.isAvailable ? 'cursor-pointer' : 'cursor-not-allowed opacity-55', paymentMethod === m.method ? 'border-primary bg-paper' : 'border-line')}>
                    <span className="flex items-center gap-2 font-medium">
                      <input type="radio" value={m.method} disabled={!m.isAvailable} className="accent-[var(--c-primary)]" {...register('paymentMethod')} />
                      {m.name}
                    </span>
                    <span className="mt-1 block pl-6 text-sm text-muted">{m.isAvailable ? m.description : m.unavailableReason}</span>
                  </label>
                ))}
              </fieldset>
              <div>
                <label htmlFor="discount" className="mb-1.5 block text-sm font-medium">Discount code</label>
                <div className="flex gap-2">
                  <Input id="discount" {...register('discountCode')} className="uppercase" />
                  <Button variant="outline" onClick={() => setAppliedCode(form.getValues('discountCode')?.trim() || null)}>Apply</Button>
                </div>
                {q?.discountError && appliedCode && <p className="mt-1 text-sm text-danger" role="alert">{q.discountError}</p>}
                {q?.discountCode && q.discount > 0 && <p className="mt-1 text-sm text-success">Code {q.discountCode} applied.</p>}
              </div>
              {q && q.issues.length > 0 && <Notice tone="danger"><ul className="list-disc pl-4">{q.issues.map((i) => <li key={i}>{i}</li>)}</ul></Notice>}
            </section>
          )}

          {step === 3 && (
            <section className="space-y-5">
              <h2 className="text-2xl">Review your order</h2>
              {reservation && (
                <Notice title="Your pieces are reserved">
                  <span className="inline-flex items-center gap-1.5"><Clock className="h-4 w-4" aria-hidden /> Held for you for <Countdown until={reservation.expiresAt} />. Complete your order before then.</span>
                </Notice>
              )}
              <dl className="divide-y divide-line rounded-theme border border-line bg-paper text-sm">
                <div className="flex justify-between gap-4 p-4"><dt className="text-muted">Contact</dt><dd className="text-right">{form.getValues('fullName')}<br />{form.getValues('email')} · {form.getValues('phone')}</dd></div>
                <div className="flex justify-between gap-4 p-4"><dt className="text-muted">{fulfillment === 'Delivery' ? 'Deliver to' : 'Collection'}</dt>
                  <dd className="text-right">{fulfillment === 'Delivery' ? <>{form.getValues('line1')}{form.getValues('line2') && `, ${form.getValues('line2')}`}<br />{form.getValues('city')}, {form.getValues('district')} {form.getValues('postalCode')}</> : shipping.data?.localPickupAddress}</dd></div>
                <div className="flex justify-between gap-4 p-4"><dt className="text-muted">Payment</dt><dd>{paymentMethodLabels[paymentMethod]}</dd></div>
              </dl>
              {paymentMethod === 'BankTransfer' && <p className="text-sm text-muted">After placing your order you'll see our bank details. Your order is confirmed once we verify the transfer.</p>}
            </section>
          )}

          <div className="flex flex-wrap items-center justify-between gap-3 border-t border-line pt-6">
            {step > 0 ? <Button variant="ghost" onClick={() => setStep((s) => s - 1)} disabled={place.isPending}>Back</Button> : <Link to="/cart" className="text-sm link-underline">Return to cart</Link>}
            {step < 3 ? (
              <Button type="submit" size="lg" loading={reserve.isPending}>{step === 2 ? 'Review order' : 'Continue'}</Button>
            ) : (
              <Button type="submit" size="lg" loading={place.isPending} disabled={!q?.canPlaceOrder}>Place order · {q && formatPrice(q.total)}</Button>
            )}
          </div>
        </form>

        <aside className="lg:sticky lg:top-28 lg:self-start"><Summary quote={q} loading={quote.isFetching} /></aside>
      </div>
    </div>
  );
}
