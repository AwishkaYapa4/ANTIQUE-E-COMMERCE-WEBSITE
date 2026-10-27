import { useState } from 'react';
import { Link, NavLink, Outlet, useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { MapPin, Package } from 'lucide-react';
import { toast } from 'sonner';
import { meApi, ordersApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import { useAuth } from '@/features/auth/AuthProvider';
import type { Address, AddressInput } from '@/types/api';
import { Seo } from '@/components/Seo';
import { Button, ButtonLink } from '@/components/ui/Button';
import { Checkbox, Field, Input, Select } from '@/components/ui/Field';
import { EmptyState, ErrorState, Notice, OrderStatusBadge, PageSpinner, PaymentStatusBadge } from '@/components/ui/Feedback';
import { Modal, useConfirm } from '@/components/ui/Overlay';
import { Pagination, SmartImage } from '@/components/ui/Misc';
import { cn, formatDate, formatPrice, SRI_LANKA_DISTRICTS } from '@/utils/format';
import { OrderDetails } from '@/features/customer/orders/OrderViews';

const links = [
  { to: '/account', label: 'Overview', end: true },
  { to: '/account/orders', label: 'Orders' },
  { to: '/account/addresses', label: 'Addresses' },
  { to: '/wishlist', label: 'Wishlist' },
  { to: '/account/settings', label: 'Account settings' },
];

export function AccountLayout() {
  const { me, signOut, isAdmin } = useAuth();
  const navigate = useNavigate();
  return (
    <div className="container-page py-10 sm:py-14">
      <Seo title="My account" noIndex />
      <p className="eyebrow">My account</p>
      <h1 className="mt-2 text-4xl">Hello{me?.displayName ? `, ${me.displayName.split(' ')[0]}` : ''}</h1>
      <div className="mt-10 grid gap-10 lg:grid-cols-[220px_1fr]">
        <nav aria-label="Account" className="flex gap-1 overflow-x-auto lg:flex-col">
          {links.map((l) => (
            <NavLink key={l.to} to={l.to} end={l.end} className={({ isActive }) => cn('shrink-0 rounded-theme px-3 py-2 text-sm', isActive ? 'bg-surface text-ink' : 'text-muted hover:text-ink')}>{l.label}</NavLink>
          ))}
          {isAdmin && <Link to="/admin" className="shrink-0 rounded-theme px-3 py-2 text-sm text-accent">Store admin</Link>}
          <button className="shrink-0 rounded-theme px-3 py-2 text-left text-sm text-muted hover:text-danger" onClick={async () => { await signOut(); navigate('/'); }}>Sign out</button>
        </nav>
        <div className="min-w-0"><Outlet /></div>
      </div>
    </div>
  );
}

const profileSchema = z.object({
  displayName: z.string().trim().max(200),
  phoneNumber: z.string().trim().regex(/^$|^\+?[0-9 ()-]{7,20}$/, 'Enter a valid phone number.'),
});

export function AccountOverview() {
  const { me, firebaseUser } = useAuth();
  const queryClient = useQueryClient();
  const orders = useQuery({ queryKey: ['orders', 1], queryFn: () => ordersApi.mine(1) });
  const form = useForm<z.infer<typeof profileSchema>>({ resolver: zodResolver(profileSchema), values: { displayName: me?.displayName ?? '', phoneNumber: me?.phoneNumber ?? '' } });
  const save = useMutation({
    mutationFn: (v: z.infer<typeof profileSchema>) => meApi.update({ displayName: v.displayName || null, phoneNumber: v.phoneNumber || null }),
    onSuccess: (m) => { queryClient.setQueryData(['me', firebaseUser?.uid], m); toast.success('Profile saved'); },
    onError: (e) => toast.error(errorMessage(e)),
  });
  return (
    <div className="space-y-10">
      {firebaseUser && !firebaseUser.emailVerified && <Notice title="Verify your email">Please verify {firebaseUser.email} to place orders. <Link to="/verify-email" className="link-underline">Verify now</Link></Notice>}
      <section className="rounded-theme border border-line bg-paper p-6">
        <h2 className="text-2xl">Personal information</h2>
        <form className="mt-5 grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((v) => save.mutate(v))} noValidate>
          <Field label="Full name" error={form.formState.errors.displayName?.message}>{(p) => <Input {...p} {...form.register('displayName')} />}</Field>
          <Field label="Phone" error={form.formState.errors.phoneNumber?.message}>{(p) => <Input {...p} type="tel" {...form.register('phoneNumber')} />}</Field>
          <Field label="Email" hint="Your sign-in email. Change it in your Google / Firebase account.">{(p) => <Input {...p} value={me?.email ?? ''} disabled readOnly />}</Field>
          <div className="flex items-end"><Button type="submit" loading={save.isPending}>Save changes</Button></div>
        </form>
      </section>
      <section>
        <div className="mb-4 flex items-end justify-between"><h2 className="text-2xl">Recent orders</h2><Link to="/account/orders" className="text-sm link-underline">View all</Link></div>
        <OrdersTable items={orders.data?.items.slice(0, 3)} loading={orders.isPending} />
      </section>
    </div>
  );
}

function OrdersTable({ items, loading }: { items?: Awaited<ReturnType<typeof ordersApi.mine>>['items']; loading: boolean }) {
  if (loading) return <PageSpinner />;
  if (!items || items.length === 0) return <EmptyState icon={<Package className="h-8 w-8" />} title="No orders yet" description="When you place an order it will appear here." action={<ButtonLink to="/shop" variant="outline">Start browsing</ButtonLink>} />;
  return (
    <ul className="divide-y divide-line rounded-theme border border-line bg-paper">
      {items.map((o) => (
        <li key={o.id}>
          <Link to={`/account/orders/${o.id}`} className="flex flex-wrap items-center gap-4 p-4 hover:bg-bg">
            <SmartImage src={o.firstItemImage} alt="" ratio="1 / 1" className="w-12 rounded-theme" sizes="48px" />
            <div className="min-w-0 flex-1">
              <p className="font-mono text-sm">{o.orderNumber}</p>
              <p className="text-xs text-muted">{formatDate(o.placedAt)} · {o.itemCount} item{o.itemCount > 1 && 's'}</p>
            </div>
            <div className="flex gap-2"><OrderStatusBadge status={o.status} /><PaymentStatusBadge status={o.paymentStatus} /></div>
            <p className="w-28 text-right text-sm font-medium">{formatPrice(o.total)}</p>
          </Link>
        </li>
      ))}
    </ul>
  );
}

export function AccountOrders() {
  const [page, setPage] = useState(1);
  const orders = useQuery({ queryKey: ['orders', page], queryFn: () => ordersApi.mine(page) });
  if (orders.isError) return <ErrorState message={errorMessage(orders.error)} onRetry={() => orders.refetch()} />;
  return (
    <div>
      <h2 className="mb-6 text-3xl">Order history</h2>
      <OrdersTable items={orders.data?.items} loading={orders.isPending} />
      {orders.data && <Pagination page={page} totalPages={orders.data.totalPages} onChange={setPage} />}
    </div>
  );
}

export function AccountOrderDetail() {
  const id = Number(useParams().id);
  const order = useQuery({ queryKey: ['order', id], queryFn: () => ordersApi.get(id) });
  if (order.isPending) return <PageSpinner />;
  if (order.isError) return <ErrorState message={errorMessage(order.error)} onRetry={() => order.refetch()} />;
  return (
    <div>
      <Link to="/account/orders" className="text-sm link-underline">← All orders</Link>
      <h2 className="mt-4 mb-6 text-3xl">Order <span className="font-mono text-2xl">{order.data.orderNumber}</span></h2>
      <OrderDetails order={order.data} />
    </div>
  );
}

const addressSchema = z.object({
  label: z.string().trim().max(64),
  fullName: z.string().trim().min(1, 'Enter the recipient name.').max(200),
  phone: z.string().trim().regex(/^\+?[0-9 ()-]{7,20}$/, 'Enter a valid phone number.'),
  line1: z.string().trim().min(1, 'Enter the street address.').max(200),
  line2: z.string().trim().max(200).nullable(),
  city: z.string().trim().min(1, 'Enter the city.').max(100),
  district: z.enum(SRI_LANKA_DISTRICTS, { message: 'Select a district.' }),
  postalCode: z.string().trim().max(20).nullable(),
  isDefault: z.boolean(),
});

function AddressForm({ initial, onSubmit, busy }: { initial?: Address; onSubmit: (a: AddressInput) => void; busy: boolean }) {
  const form = useForm<z.infer<typeof addressSchema>>({
    resolver: zodResolver(addressSchema),
    defaultValues: initial
      ? { ...initial, district: initial.district as (typeof SRI_LANKA_DISTRICTS)[number] }
      : { label: 'Home', fullName: '', phone: '', line1: '', line2: '', city: '', district: undefined as never, postalCode: '', isDefault: false },
  });
  const e = form.formState.errors;
  return (
    <form className="space-y-4" onSubmit={form.handleSubmit((v) => onSubmit({ ...v, line2: v.line2 || null, postalCode: v.postalCode || null }))} noValidate>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Label" error={e.label?.message}>{(p) => <Input {...p} placeholder="Home, Office…" {...form.register('label')} />}</Field>
        <Field label="Recipient name" required error={e.fullName?.message}>{(p) => <Input {...p} {...form.register('fullName')} />}</Field>
      </div>
      <Field label="Phone" required error={e.phone?.message}>{(p) => <Input {...p} type="tel" {...form.register('phone')} />}</Field>
      <Field label="Address" required error={e.line1?.message}>{(p) => <Input {...p} {...form.register('line1')} />}</Field>
      <Field label="Apartment, building (optional)" error={e.line2?.message}>{(p) => <Input {...p} {...form.register('line2')} />}</Field>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="City" required error={e.city?.message}>{(p) => <Input {...p} {...form.register('city')} />}</Field>
        <Field label="District" required error={e.district?.message}>{(p) => (
          <Select {...p} {...form.register('district')}><option value="">Select…</option>{SRI_LANKA_DISTRICTS.map((d) => <option key={d}>{d}</option>)}</Select>
        )}</Field>
        <Field label="Postal code" error={e.postalCode?.message}>{(p) => <Input {...p} {...form.register('postalCode')} />}</Field>
      </div>
      <Checkbox label="Use as my default address" {...form.register('isDefault')} />
      <Button type="submit" loading={busy}>Save address</Button>
    </form>
  );
}

export function AccountAddresses() {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<Address | 'new' | null>(null);
  const addresses = useQuery({ queryKey: ['addresses'], queryFn: meApi.addresses });
  const onSaved = (list: Address[]) => { queryClient.setQueryData(['addresses'], list); setEditing(null); toast.success('Address saved'); };
  const save = useMutation({
    mutationFn: (a: AddressInput) => (editing && editing !== 'new' ? meApi.updateAddress(editing.id, a) : meApi.addAddress(a)),
    onSuccess: onSaved, onError: (e) => toast.error(errorMessage(e)),
  });
  const remove = useMutation({ mutationFn: meApi.deleteAddress, onSuccess: (l) => queryClient.setQueryData(['addresses'], l), onError: (e) => toast.error(errorMessage(e)) });

  return (
    <div>
      <div className="mb-6 flex items-end justify-between"><h2 className="text-3xl">Saved addresses</h2><Button size="sm" onClick={() => setEditing('new')}>Add address</Button></div>
      {addresses.isPending ? <PageSpinner /> : (addresses.data?.length ?? 0) === 0 ? (
        <EmptyState icon={<MapPin className="h-8 w-8" />} title="No saved addresses" description="Save an address to check out faster." />
      ) : (
        <ul className="grid gap-4 sm:grid-cols-2">
          {addresses.data!.map((a) => (
            <li key={a.id} className="rounded-theme border border-line bg-paper p-5 text-sm">
              <p className="font-medium">{a.label} {a.isDefault && <span className="ml-2 text-xs text-accent">Default</span>}</p>
              <p className="mt-2 text-muted">{a.fullName} · {a.phone}<br />{a.line1}{a.line2 && `, ${a.line2}`}<br />{a.city}, {a.district} {a.postalCode}</p>
              <div className="mt-4 flex gap-3">
                <Button variant="link" onClick={() => setEditing(a)}>Edit</Button>
                <Button variant="link" className="text-danger" onClick={async () => { if (await confirm({ title: 'Delete address?', message: 'This address will be removed from your account.', danger: true, confirmLabel: 'Delete' })) remove.mutate(a.id); }}>Delete</Button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <Modal open={editing !== null} onClose={() => setEditing(null)} title={editing === 'new' ? 'Add address' : 'Edit address'}>
        {editing !== null && <AddressForm key={editing === 'new' ? 'new' : editing.id} initial={editing === 'new' ? undefined : editing} onSubmit={(a) => save.mutate(a)} busy={save.isPending} />}
      </Modal>
    </div>
  );
}

export function AccountSettings() {
  const { me, resetPassword, signOut, firebaseUser } = useAuth();
  const navigate = useNavigate();
  const isPasswordAccount = firebaseUser?.providerData.some((p) => p.providerId === 'password');
  return (
    <div className="space-y-6">
      <h2 className="text-3xl">Account settings</h2>
      <section className="rounded-theme border border-line bg-paper p-6">
        <h3 className="font-body text-sm font-semibold">Sign-in</h3>
        <p className="mt-1 text-sm text-muted">Signed in as {me?.email} via {firebaseUser?.providerData.map((p) => (p.providerId === 'google.com' ? 'Google' : 'email & password')).join(', ')}.</p>
        {isPasswordAccount && <Button variant="outline" size="sm" className="mt-4" onClick={async () => { await resetPassword(me!.email); toast.success('We sent a password reset link to your email.'); }}>Change password</Button>}
      </section>
      <section className="rounded-theme border border-line bg-paper p-6">
        <h3 className="font-body text-sm font-semibold">Privacy</h3>
        <p className="mt-1 text-sm text-muted">We store only what's needed to process your orders. To close your account or request your data, <Link to="/contact" className="link-underline">contact us</Link>.</p>
      </section>
      <Button variant="outline" onClick={async () => { await signOut(); navigate('/'); }}>Sign out</Button>
    </div>
  );
}
