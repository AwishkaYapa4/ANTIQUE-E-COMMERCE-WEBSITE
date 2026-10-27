import { Link, useNavigate } from 'react-router';
import { Minus, Plus, ShoppingBag, Trash2 } from 'lucide-react';
import { useCart, type CartLineView } from '@/features/cart/useCart';
import { useUiStore } from '@/store/uiStore';
import { Drawer } from '@/components/ui/Overlay';
import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/ui/Feedback';
import { SmartImage } from '@/components/ui/Misc';
import { formatPrice } from '@/utils/format';

export function CartLineRow({ line, compact = false }: { line: CartLineView; compact?: boolean }) {
  const { update, remove, isMutating } = useCart();
  const close = useUiStore((s) => s.setCartOpen);
  return (
    <li className="flex gap-4 py-4">
      <Link to={`/products/${line.slug}`} onClick={() => close(false)} className="w-20 shrink-0 overflow-hidden rounded-theme bg-surface sm:w-24">
        <SmartImage image={line.image} alt={line.name} ratio="4 / 5" className="h-full w-full" sizes="96px" />
      </Link>
      <div className="flex min-w-0 flex-1 flex-col">
        <div className="flex justify-between gap-3">
          <Link to={`/products/${line.slug}`} onClick={() => close(false)} className="font-heading text-lg leading-snug hover:text-accent">{line.name}</Link>
          <p className="shrink-0 text-sm">{formatPrice(line.lineTotal)}</p>
        </div>
        {line.issue && <p className="mt-1 text-sm text-danger" role="alert">{line.issue}</p>}
        <div className="mt-auto flex items-center justify-between pt-3">
          {line.maxQuantity > 1 ? (
            <div className="flex items-center rounded-theme border border-line">
              <button className="px-2.5 py-1.5 disabled:opacity-40" onClick={() => update(line, line.quantity - 1)} disabled={line.quantity <= 1 || isMutating} aria-label="Decrease quantity"><Minus className="h-3.5 w-3.5" /></button>
              <span className="w-8 text-center text-sm" aria-live="polite">{line.quantity}</span>
              <button className="px-2.5 py-1.5 disabled:opacity-40" onClick={() => update(line, line.quantity + 1)} disabled={line.quantity >= line.maxQuantity || isMutating} aria-label="Increase quantity"><Plus className="h-3.5 w-3.5" /></button>
            </div>
          ) : (
            <span className="text-xs text-muted">{compact ? 'One of a kind' : 'One-of-a-kind piece · quantity 1'}</span>
          )}
          <button className="inline-flex items-center gap-1 text-xs text-muted hover:text-danger" onClick={() => remove(line)} disabled={isMutating}>
            <Trash2 className="h-3.5 w-3.5" aria-hidden /> Remove
          </button>
        </div>
      </div>
    </li>
  );
}

export function CartDrawer() {
  const open = useUiStore((s) => s.cartOpen);
  const setOpen = useUiStore((s) => s.setCartOpen);
  const { lines, subtotal, hasIssues, isGuest } = useCart();
  const navigate = useNavigate();
  const go = (to: string) => {
    setOpen(false);
    navigate(to);
  };
  return (
    <Drawer
      open={open}
      onClose={() => setOpen(false)}
      title="Your cart"
      footer={
        lines.length > 0 && (
          <div className="space-y-3">
            <div className="flex justify-between text-base">
              <span>Subtotal</span>
              <span className="font-medium">{formatPrice(subtotal)}</span>
            </div>
            <p className="text-xs text-muted">Delivery and any discount are calculated at checkout.{isGuest && ' Prices are confirmed when you sign in.'}</p>
            <Button className="w-full" size="lg" disabled={hasIssues} onClick={() => go('/checkout')}>Checkout</Button>
            <Button className="w-full" variant="outline" onClick={() => go('/cart')}>View cart</Button>
          </div>
        )
      }
    >
      {lines.length === 0 ? (
        <EmptyState icon={<ShoppingBag className="h-8 w-8" />} title="Your cart is empty" description="Every piece is one of a kind — when you see something you love, add it before someone else does." action={<Button variant="outline" onClick={() => go('/shop')}>Browse the collection</Button>} />
      ) : (
        <ul className="divide-y divide-line">{lines.map((l) => <CartLineRow key={l.key} line={l} compact />)}</ul>
      )}
    </Drawer>
  );
}
