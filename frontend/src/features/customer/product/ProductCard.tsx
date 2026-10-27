import { memo } from 'react';
import { Link } from 'react-router';
import { Eye, Heart } from 'lucide-react';
import type { ProductCard as Card } from '@/types/api';
import { useWishlist } from '@/features/wishlist/useWishlist';
import { useCart } from '@/features/cart/useCart';
import { useUiStore } from '@/store/uiStore';
import { Price, SmartImage } from '@/components/ui/Misc';
import { Skeleton } from '@/components/ui/Feedback';
import { cn, conditionLabels } from '@/utils/format';

export const ProductCard = memo(function ProductCard({ product, priority }: { product: Card; priority?: boolean }) {
  const wishlist = useWishlist();
  const cart = useCart();
  const openQuickView = useUiStore((s) => s.openQuickView);
  const saved = wishlist.has(product.id);
  const sold = product.inventoryState === 'Sold';
  const reserved = product.inventoryState === 'Reserved';
  const inCart = cart.contains(product.id);

  return (
    <article className="group relative flex flex-col">
      <div className="relative overflow-hidden rounded-theme bg-surface">
        <Link to={`/products/${product.slug}`} aria-label={product.name} className="block">
          <SmartImage
            image={product.image}
            alt={product.image?.alt ?? product.name}
            ratio="4 / 5"
            priority={priority}
            sizes="(min-width: 1024px) 25vw, (min-width: 640px) 33vw, 50vw"
            className={cn('h-full w-full transition-transform duration-700 group-hover:scale-[1.03]', sold && 'opacity-70 grayscale-[30%]')}
          />
          {product.hoverImage && !sold && (
            <SmartImage
              image={product.hoverImage}
              alt=""
              ratio="4 / 5"
              sizes="(min-width: 1024px) 25vw, 50vw"
              className="absolute inset-0 h-full w-full opacity-0 transition-opacity duration-500 group-hover:opacity-100"
            />
          )}
        </Link>

        <div className="pointer-events-none absolute top-3 left-3 flex flex-col gap-1.5">
          {sold && <span className="rounded-theme bg-primary px-2.5 py-1 text-[0.68rem] font-semibold tracking-[0.15em] text-button-ink uppercase">Sold</span>}
          {reserved && <span className="rounded-theme bg-paper/95 px-2.5 py-1 text-[0.68rem] font-semibold tracking-[0.15em] text-primary uppercase">Reserved</span>}
          {!sold && product.compareAtPrice != null && product.compareAtPrice > product.price && (
            <span className="rounded-theme bg-danger px-2.5 py-1 text-[0.68rem] font-semibold tracking-[0.15em] text-paper uppercase">Sale</span>
          )}
        </div>

        <button
          type="button"
          onClick={() => wishlist.toggle(product.id, product.name)}
          className="absolute top-3 right-3 inline-flex h-9 w-9 items-center justify-center rounded-full bg-paper/90 text-ink shadow-sm transition hover:bg-paper"
          aria-label={saved ? `Remove ${product.name} from wishlist` : `Save ${product.name} to wishlist`}
          aria-pressed={saved}
        >
          <Heart className={cn('h-4 w-4', saved && 'fill-danger text-danger')} />
        </button>

        <div className="absolute inset-x-3 bottom-3 flex gap-2 opacity-100 transition-all duration-300 md:translate-y-2 md:opacity-0 md:group-focus-within:translate-y-0 md:group-focus-within:opacity-100 md:group-hover:translate-y-0 md:group-hover:opacity-100">
          {!sold && !reserved && (
            <button
              type="button"
              disabled={inCart}
              onClick={() => cart.add(product)}
              className="flex-1 rounded-theme bg-button py-2.5 text-xs font-medium tracking-wider text-button-ink uppercase transition hover:bg-ink disabled:bg-paper/95 disabled:text-ink"
            >
              {inCart ? 'In your cart' : 'Add to cart'}
            </button>
          )}
          <button
            type="button"
            onClick={() => openQuickView(product.slug)}
            className={cn('inline-flex items-center justify-center gap-1.5 rounded-theme bg-paper/95 px-3 py-2.5 text-xs font-medium text-ink transition hover:bg-paper', (sold || reserved) && 'flex-1')}
            aria-label={`Quick view ${product.name}`}
          >
            <Eye className="h-4 w-4" aria-hidden />
            <span className={cn(!(sold || reserved) && 'sr-only')}>Quick view</span>
          </button>
        </div>
      </div>

      <div className="mt-3.5 flex flex-1 flex-col gap-1">
        <p className="text-[0.7rem] tracking-[0.15em] text-muted uppercase">
          {[product.categoryName, conditionLabels[product.condition]].filter(Boolean).join(' · ')}
        </p>
        <h3 className="font-heading text-[1.25rem] leading-snug font-medium">
          <Link to={`/products/${product.slug}`} className="hover:text-accent">{product.name}</Link>
        </h3>
        <Price price={product.price} compareAt={product.compareAtPrice} className={cn('text-[0.95rem]', sold && 'text-muted')} />
      </div>
    </article>
  );
});

export function ProductGrid({ products, columns = 4 }: { products: Card[]; columns?: 3 | 4 }) {
  return (
    <div className={cn('grid grid-cols-2 gap-x-4 gap-y-10 sm:gap-x-6', columns === 4 ? 'md:grid-cols-3 lg:grid-cols-4' : 'md:grid-cols-3')}>
      {products.map((p, i) => <ProductCard key={p.id} product={p} priority={i < 4} />)}
    </div>
  );
}

export function ProductGridSkeleton({ count = 8, columns = 4 }: { count?: number; columns?: 3 | 4 }) {
  return (
    <div className={cn('grid grid-cols-2 gap-x-4 gap-y-10 sm:gap-x-6', columns === 4 ? 'md:grid-cols-3 lg:grid-cols-4' : 'md:grid-cols-3')} aria-busy="true" aria-label="Loading products">
      {Array.from({ length: count }, (_, i) => (
        <div key={i}>
          <Skeleton className="aspect-[4/5] w-full" />
          <Skeleton className="mt-4 h-3 w-1/3" />
          <Skeleton className="mt-2 h-5 w-3/4" />
          <Skeleton className="mt-2 h-4 w-1/4" />
        </div>
      ))}
    </div>
  );
}
