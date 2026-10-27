import type { ReactNode } from 'react';
import { AlertTriangle, Loader2 } from 'lucide-react';
import type { InventoryState, OrderStatus, PaymentStatus } from '@/types/api';
import { cn, inventoryLabels, orderStatusLabels, paymentStatusLabels } from '@/utils/format';
import { Button } from './Button';

export function Spinner({ className, label = 'Loading' }: { className?: string; label?: string }) {
  return (
    <span role="status" className={cn('inline-flex items-center gap-2 text-muted', className)}>
      <Loader2 className="h-5 w-5 animate-spin" aria-hidden />
      <span className="sr-only">{label}</span>
    </span>
  );
}

export function PageSpinner() {
  return (
    <div className="flex min-h-[40vh] items-center justify-center">
      <Spinner />
    </div>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('animate-pulse rounded-theme bg-surface', className)} aria-hidden />;
}

export function EmptyState({ icon, title, description, action }: { icon?: ReactNode; title: string; description?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center px-6 py-16 text-center">
      {icon && <div className="mb-4 text-accent">{icon}</div>}
      <h3 className="text-2xl">{title}</h3>
      {description && <p className="mt-2 max-w-md text-muted">{description}</p>}
      {action && <div className="mt-6">{action}</div>}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-center justify-center px-6 py-14 text-center">
      <AlertTriangle className="mb-3 h-8 w-8 text-danger" aria-hidden />
      <p className="max-w-md text-ink">{message}</p>
      {onRetry && (
        <Button variant="outline" size="sm" className="mt-5" onClick={onRetry}>
          Try again
        </Button>
      )}
    </div>
  );
}

type Tone = 'neutral' | 'accent' | 'success' | 'danger' | 'dark' | 'warning';
const tones: Record<Tone, string> = {
  neutral: 'bg-surface text-ink border-line',
  accent: 'bg-accent/10 text-primary border-accent/40',
  success: 'bg-success/10 text-success border-success/30',
  danger: 'bg-danger/10 text-danger border-danger/30',
  warning: 'bg-accent/15 text-primary border-accent/50',
  dark: 'bg-primary text-paper border-primary',
};

export function Badge({ tone = 'neutral', children, className }: { tone?: Tone; children: ReactNode; className?: string }) {
  return (
    <span className={cn('inline-flex items-center rounded-theme border px-2 py-0.5 text-[0.7rem] font-medium tracking-wide uppercase', tones[tone], className)}>
      {children}
    </span>
  );
}

const inventoryTone: Record<InventoryState, Tone> = { Available: 'success', Reserved: 'warning', Sold: 'dark', Draft: 'neutral', Archived: 'neutral' };
export const InventoryBadge = ({ state }: { state: InventoryState }) => <Badge tone={inventoryTone[state]}>{inventoryLabels[state]}</Badge>;

const orderTone: Record<OrderStatus, Tone> = { Pending: 'warning', Confirmed: 'accent', Processing: 'accent', Shipped: 'accent', Delivered: 'success', Cancelled: 'neutral' };
export const OrderStatusBadge = ({ status }: { status: OrderStatus }) => <Badge tone={orderTone[status]}>{orderStatusLabels[status]}</Badge>;

const paymentTone: Record<PaymentStatus, Tone> = { Unpaid: 'neutral', PendingVerification: 'warning', Paid: 'success', Failed: 'danger', Refunded: 'neutral' };
export const PaymentStatusBadge = ({ status }: { status: PaymentStatus }) => <Badge tone={paymentTone[status]}>{paymentStatusLabels[status]}</Badge>;

export function Notice({ tone = 'accent', title, children }: { tone?: 'accent' | 'danger' | 'success'; title?: string; children: ReactNode }) {
  const cls = { accent: 'border-accent/40 bg-accent/5', danger: 'border-danger/40 bg-danger/5', success: 'border-success/40 bg-success/5' }[tone];
  return (
    <div className={cn('rounded-theme border px-4 py-3 text-sm', cls)} role={tone === 'danger' ? 'alert' : 'status'}>
      {title && <p className="mb-0.5 font-medium text-ink">{title}</p>}
      <div className="text-ink/85">{children}</div>
    </div>
  );
}
