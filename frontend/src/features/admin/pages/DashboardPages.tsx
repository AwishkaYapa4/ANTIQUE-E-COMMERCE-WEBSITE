import { useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Clock } from 'lucide-react';
import { adminApi } from '@/services/adminApi';
import { errorMessage } from '@/services/http';
import type { AdminOrderListItem, BreakdownRow, RevenueBreakdown } from '@/types/api';
import { ErrorState, OrderStatusBadge, PageSpinner, PaymentStatusBadge } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Field';
import { formatDate, formatDateTime, formatPrice, orderStatusLabels, paymentMethodLabels } from '@/utils/format';
import { AdminPage, Panel, StatTile, td, th } from '../components/AdminUi';
import { BarChart } from '../components/BarChart';

const shortDate = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short' });

function RevenueTiles({ revenue }: { revenue: RevenueBreakdown }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <StatTile label="Revenue (paid, net)" value={formatPrice(revenue.netPaid)} hint={`${revenue.paidOrders} paid orders, after refunds`} />
      <StatTile label="Outstanding (unpaid)" value={formatPrice(revenue.outstanding)} hint="Open orders awaiting payment" />
      <StatTile label="Refunded" value={formatPrice(revenue.refunded)} />
      <StatTile label="Cancelled (unpaid)" value={formatPrice(revenue.cancelled)} hint="Not counted as revenue" />
    </div>
  );
}

export function RecentOrdersTable({ orders }: { orders: AdminOrderListItem[] }) {
  if (orders.length === 0) return <p className="text-sm text-muted">No orders yet.</p>;
  return (
    <div className="-mx-5 overflow-x-auto">
      <table className="w-full">
        <thead><tr className="border-b border-line"><th className={th}>Order</th><th className={th}>Customer</th><th className={th}>Status</th><th className={th}>Payment</th><th className={`${th} text-right`}>Total</th></tr></thead>
        <tbody className="divide-y divide-line">
          {orders.map((o) => (
            <tr key={o.id} className="hover:bg-bg">
              <td className={td}><Link to={`/admin/orders/${o.id}`} className="font-mono text-xs link-underline">{o.orderNumber}</Link><p className="text-xs text-muted">{formatDateTime(o.placedAt)}</p></td>
              <td className={td}>{o.customerName}</td>
              <td className={td}><OrderStatusBadge status={o.status} /></td>
              <td className={td}><PaymentStatusBadge status={o.paymentStatus} /></td>
              <td className={`${td} text-right`}>{formatPrice(o.total)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function DashboardPage() {
  const q = useQuery({ queryKey: ['admin', 'dashboard'], queryFn: adminApi.dashboard, refetchInterval: 60_000 });
  if (q.isPending) return <PageSpinner />;
  if (q.isError) return <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} />;
  const d = q.data;
  const trendTotal = d.salesTrend.reduce((n, p) => n + p.paidRevenue, 0);

  return (
    <AdminPage title="Dashboard" description="All figures are calculated live from orders and inventory.">
      {(d.awaitingVerification > 0 || d.pendingOrders > 0 || d.newEnquiries > 0) && (
        <div className="mb-6 flex flex-wrap gap-3">
          {d.pendingOrders > 0 && <Link to="/admin/orders?status=Pending" className="inline-flex items-center gap-2 rounded-theme border border-accent/50 bg-accent/5 px-4 py-2 text-sm"><Clock className="h-4 w-4 text-accent" aria-hidden /> {d.pendingOrders} order(s) to confirm</Link>}
          {d.awaitingVerification > 0 && <Link to="/admin/orders?paymentStatus=PendingVerification" className="inline-flex items-center gap-2 rounded-theme border border-accent/50 bg-accent/5 px-4 py-2 text-sm"><AlertTriangle className="h-4 w-4 text-accent" aria-hidden /> {d.awaitingVerification} payment(s) to verify</Link>}
          {d.newEnquiries > 0 && <Link to="/admin/enquiries" className="inline-flex items-center gap-2 rounded-theme border border-line bg-paper px-4 py-2 text-sm">{d.newEnquiries} new enquiry(ies)</Link>}
        </div>
      )}
      <RevenueTiles revenue={d.revenue} />
      <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatTile label="Total orders" value={d.totalOrders} hint="Excluding cancelled" />
        <StatTile label="Pending orders" value={d.pendingOrders} tone={d.pendingOrders > 0 ? 'warning' : undefined} />
        <StatTile label="Products" value={d.products.total} hint={`${d.products.draft} draft · ${d.products.archived} archived`} />
        <StatTile label="Available / Sold" value={<>{d.products.available} <span className="text-muted">/</span> {d.products.sold}</>} hint={`${d.products.reserved} reserved right now`} />
      </div>

      <div className="mt-6 grid gap-6 xl:grid-cols-[1.6fr_1fr]">
        <Panel title={`Paid revenue per day · last 30 days (${formatPrice(trendTotal)})`}>
          <BarChart title="Paid revenue per day, last 30 days" data={d.salesTrend.map((p) => ({
            label: formatDate(p.date), tick: shortDate.format(new Date(p.date)), value: p.paidRevenue, detail: `${p.orders} order${p.orders === 1 ? '' : 's'} placed`,
          }))} />
        </Panel>
        <Panel title="Low stock (multi-unit items)">
          {d.lowStock.length === 0 ? <p className="text-sm text-muted">No multi-unit items are running low. One-of-a-kind pieces show simply as available or sold.</p> : (
            <ul className="divide-y divide-line text-sm">
              {d.lowStock.map((p) => (
                <li key={p.id} className="flex justify-between py-2"><Link to={`/admin/products/${p.id}`} className="link-underline">{p.name}</Link><span className="text-muted">{p.quantityAvailable} left{p.quantityReserved > 0 && ` · ${p.quantityReserved} reserved`}</span></li>
              ))}
            </ul>
          )}
        </Panel>
      </div>

      <Panel title="Recent orders" className="mt-6" actions={<Link to="/admin/orders" className="text-sm link-underline">All orders</Link>}>
        <RecentOrdersTable orders={d.recentOrders} />
      </Panel>
    </AdminPage>
  );
}

function BreakdownTable({ title, rows, labels }: { title: string; rows: BreakdownRow[]; labels?: Record<string, string> }) {
  return (
    <Panel title={title}>
      {rows.length === 0 ? <p className="text-sm text-muted">No data for this period.</p> : (
        <table className="w-full text-sm">
          <thead><tr><th className="pb-2 text-left text-xs text-muted">Group</th><th className="pb-2 text-right text-xs text-muted">Orders</th><th className="pb-2 text-right text-xs text-muted">Amount</th></tr></thead>
          <tbody className="divide-y divide-line">{rows.map((r) => <tr key={r.label}><td className="py-2">{labels?.[r.label] ?? r.label}</td><td className="py-2 text-right">{r.orders}</td><td className="py-2 text-right">{formatPrice(r.amount)}</td></tr>)}</tbody>
        </table>
      )}
    </Panel>
  );
}

const iso = (d: Date) => d.toISOString().slice(0, 10);

export function ReportsPage() {
  const today = new Date();
  const [from, setFrom] = useState(iso(new Date(today.getTime() - 29 * 86400000)));
  const [to, setTo] = useState(iso(today));
  const [range, setRange] = useState({ from, to });
  const q = useQuery({ queryKey: ['admin', 'report', range], queryFn: () => adminApi.salesReport(range.from, range.to) });
  const preset = (days: number) => { const f = iso(new Date(today.getTime() - (days - 1) * 86400000)); setFrom(f); setTo(iso(today)); setRange({ from: f, to: iso(today) }); };

  const exportCsv = () => {
    if (!q.data) return;
    const rows = [['Date', 'Paid revenue (LKR)', 'Orders'], ...q.data.daily.map((d) => [d.date, d.paidRevenue.toFixed(2), String(d.orders)])];
    const blob = new Blob([rows.map((r) => r.join(',')).join('\n')], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `sales-${range.from}-to-${range.to}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  return (
    <AdminPage title="Reports" description="Sales by day, status, payment method and category. Business days follow Sri Lanka time."
      actions={<Button variant="outline" size="sm" onClick={exportCsv} disabled={!q.data}>Export CSV</Button>}>
      <form className="mb-6 flex flex-wrap items-end gap-3" onSubmit={(e) => { e.preventDefault(); setRange({ from, to }); }}>
        <label className="text-sm">From<Input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} className="mt-1 py-2" /></label>
        <label className="text-sm">To<Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} className="mt-1 py-2" /></label>
        <Button type="submit" size="sm">Apply</Button>
        {[7, 30, 90, 365].map((n) => <Button key={n} variant="ghost" size="sm" onClick={() => preset(n)}>{n === 365 ? '12 months' : `${n} days`}</Button>)}
      </form>
      {q.isPending ? <PageSpinner /> : q.isError ? <ErrorState message={errorMessage(q.error)} onRetry={() => q.refetch()} /> : (
        <div className="space-y-6">
          <RevenueTiles revenue={q.data.revenue} />
          <StatTile label="Average paid order value" value={formatPrice(q.data.averageOrderValue)} />
          <Panel title="Paid revenue per day">
            <BarChart title="Paid revenue per day" data={q.data.daily.map((p) => ({ label: formatDate(p.date), tick: shortDate.format(new Date(p.date)), value: p.paidRevenue, detail: `${p.orders} orders` }))} />
          </Panel>
          <div className="grid gap-6 lg:grid-cols-3">
            <BreakdownTable title="Orders by status" rows={q.data.byStatus} labels={orderStatusLabels} />
            <BreakdownTable title="Paid revenue by payment method" rows={q.data.byPaymentMethod} labels={paymentMethodLabels} />
            <BreakdownTable title="Paid revenue by category" rows={q.data.byCategory} />
          </div>
          <Panel title="Top products (paid orders)">
            {q.data.topProducts.length === 0 ? <p className="text-sm text-muted">No paid sales in this period.</p> : (
              <table className="w-full text-sm"><tbody className="divide-y divide-line">
                {q.data.topProducts.map((p) => <tr key={p.productId}><td className="py-2"><Link to={`/admin/products/${p.productId}`} className="link-underline">{p.name}</Link></td><td className="py-2 text-right text-muted">{p.quantity} sold</td><td className="py-2 text-right">{formatPrice(p.revenue)}</td></tr>)}
              </tbody></table>
            )}
          </Panel>
        </div>
      )}
    </AdminPage>
  );
}
