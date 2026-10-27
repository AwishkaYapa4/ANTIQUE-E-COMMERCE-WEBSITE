import { useNavigate } from 'react-router';
import { Heart } from 'lucide-react';
import type { ProductDetail } from '@/types/api';
import { useCart } from '@/features/cart/useCart';
import { useWishlist } from '@/features/wishlist/useWishlist';
import { useUiStore } from '@/store/uiStore';
import { Button } from '@/components/ui/Button';
import { cn } from '@/utils/format';

/** Add to cart / Buy now / Wishlist. Purchasing is disabled for sold or reserved pieces. */
export function PurchaseButtons({ product, onDone, onAsk }: { product: ProductDetail; onDone?: () => void; onAsk?: () => void }) {
  const cart = useCart();
  const wishlist = useWishlist();
  const navigate = useNavigate();
  const saved = wishlist.has(product.id);
  const available = product.inventoryState === 'Available';
  const inCart = cart.contains(product.id);

  const buyNow = async () => {
    if (inCart || (await cart.add(product))) {
      useUiStore.getState().setCartOpen(false);
      onDone?.();
      navigate('/checkout');
    }
  };

  return (
    <div className="space-y-3">
      {available ? (
        <div className="grid gap-2 sm:grid-cols-2">
          <Button size="lg" onClick={async () => { if (await cart.add(product)) onDone?.(); }} disabled={inCart} loading={cart.isMutating}>
            {inCart ? 'In your cart' : 'Add to cart'}
          </Button>
          <Button size="lg" variant="outline" onClick={buyNow}>Buy now</Button>
        </div>
      ) : (
        <div className="rounded-theme border border-line bg-surface px-4 py-3 text-sm" role="status">
          {product.inventoryState === 'Reserved'
            ? 'This piece is currently reserved by another customer at checkout. If they do not complete their purchase it will become available again shortly.'
            : 'This piece has found a new home. Ask us about similar items — we often source comparable pieces.'}
        </div>
      )}
      <div className="flex flex-wrap gap-2">
        <Button variant="ghost" size="sm" onClick={() => wishlist.toggle(product.id, product.name)} aria-pressed={saved}>
          <Heart className={cn('h-4 w-4', saved && 'fill-danger text-danger')} aria-hidden />
          {saved ? 'Saved to wishlist' : 'Add to wishlist'}
        </Button>
        {onAsk && <Button variant="ghost" size="sm" onClick={onAsk}>Ask about this item</Button>}
      </div>
    </div>
  );
}
