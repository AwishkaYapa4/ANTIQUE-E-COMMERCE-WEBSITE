import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import DOMPurify from 'dompurify';
import { Heart, Mail, MapPin, MessageCircle, Phone, ShoppingBag } from 'lucide-react';
import { siteApi } from '@/services/api';
import { ApiError, errorMessage } from '@/services/http';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import { useCart } from '@/features/cart/useCart';
import { useWishlist } from '@/features/wishlist/useWishlist';
import { useAuth } from '@/features/auth/AuthProvider';
import { catalogApi } from '@/services/api';
import { Seo } from '@/components/Seo';
import { ButtonLink, Button } from '@/components/ui/Button';
import { EmptyState, ErrorState, Notice, PageSpinner } from '@/components/ui/Feedback';
import { ProductGrid, ProductGridSkeleton } from '@/features/customer/product/ProductCard';
import { CartLineRow } from '@/layouts/customer/CartDrawer';
import { EnquiryForm } from '@/features/customer/product/EnquiryForm';
import { formatPrice } from '@/utils/format';

export function ContentPage() {
  const { slug = '' } = useParams();
  const { preview } = useSiteConfig();
  const page = useQuery({ queryKey: ['page', slug, preview], queryFn: () => siteApi.page(slug, preview) });
  if (page.isPending) return <PageSpinner />;
  if (page.isError) {
    return page.error instanceof ApiError && page.error.status === 404
      ? <NotFoundPage />
      : <ErrorState message={errorMessage(page.error)} onRetry={() => page.refetch()} />;
  }
  const p = page.data;
  return (
    <article className="container-page max-w-3xl py-14 sm:py-20">
      <Seo title={p.seoTitle ?? p.title} description={p.seoDescription} />
      <h1>{p.title}</h1>
      {/* Server-sanitized HTML, sanitized again client-side as defence in depth. */}
      <div className="prose-antique mt-8" dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(p.body) }} />
    </article>
  );
}

export function ContactPage() {
  const { brand } = useSiteConfig();
  const intro = useQuery({ queryKey: ['page', 'contact', false], queryFn: () => siteApi.page('contact'), retry: false });
  const whatsApp = brand.whatsAppNumber?.replace(/[^\d]/g, '');
  return (
    <div className="container-page py-14 sm:py-20">
      <Seo title="Contact us" description="Questions about a piece, delivery or visiting? Get in touch." />
      <div className="grid gap-14 lg:grid-cols-[1fr_1.2fr]">
        <div>
          <p className="eyebrow mb-3">Get in touch</p>
          <h1>Contact us</h1>
          {intro.data && <div className="prose-antique mt-6" dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(intro.data.body) }} />}
          <ul className="mt-8 space-y-4">
            {brand.contactPhone && <li className="flex gap-3"><Phone className="mt-0.5 h-5 w-5 text-accent" aria-hidden /><a href={`tel:${brand.contactPhone}`} className="link-underline">{brand.contactPhone}</a></li>}
            {brand.contactEmail && <li className="flex gap-3"><Mail className="mt-0.5 h-5 w-5 text-accent" aria-hidden /><a href={`mailto:${brand.contactEmail}`} className="link-underline">{brand.contactEmail}</a></li>}
            {brand.address && <li className="flex gap-3"><MapPin className="mt-0.5 h-5 w-5 text-accent" aria-hidden /><span className="whitespace-pre-line">{brand.address}</span></li>}
            {brand.openingHours && <li className="text-sm whitespace-pre-line text-muted">{brand.openingHours}</li>}
          </ul>
          {whatsApp && (
            <a href={`https://wa.me/${whatsApp}`} target="_blank" rel="noopener noreferrer" className="mt-6 inline-flex items-center gap-2 rounded-theme bg-[#25D366] px-5 py-3 text-sm font-medium text-white">
              <MessageCircle className="h-4 w-4" aria-hidden /> Chat on WhatsApp
            </a>
          )}
          {brand.socialLinks.length > 0 && (
            <ul className="mt-8 flex flex-wrap gap-4 text-sm">
              {brand.socialLinks.map((s) => <li key={s.url}><a href={s.url} target="_blank" rel="noopener noreferrer" className="capitalize link-underline">{s.platform}</a></li>)}
            </ul>
          )}
          {brand.mapEmbedUrl && (
            <iframe title="Store location map" src={brand.mapEmbedUrl} className="mt-8 h-64 w-full rounded-theme border border-line" loading="lazy" referrerPolicy="no-referrer-when-downgrade" sandbox="allow-scripts allow-same-origin allow-popups" />
          )}
        </div>
        <div className="rounded-theme border border-line bg-paper p-6 sm:p-10">
          <h2 className="mb-6 text-3xl">Send a message</h2>
          <EnquiryForm />
        </div>
      </div>
    </div>
  );
}

export function CartPage() {
  const { lines, subtotal, hasIssues, isGuest, isLoading } = useCart();
  const { commerce } = useSiteConfig();
  return (
    <div className="container-page py-12 sm:py-16">
      <Seo title="Your cart" noIndex />
      <h1>Your cart</h1>
      {isLoading ? <PageSpinner /> : lines.length === 0 ? (
        <EmptyState icon={<ShoppingBag className="h-8 w-8" />} title="Your cart is empty" description="Each piece is one of a kind. When something catches your eye, add it to your cart." action={<ButtonLink to="/shop">Browse the collection</ButtonLink>} />
      ) : (
        <div className="mt-10 grid gap-10 lg:grid-cols-[1fr_360px]">
          <ul className="divide-y divide-line border-y border-line">{lines.map((l) => <CartLineRow key={l.key} line={l} />)}</ul>
          <aside className="h-fit rounded-theme border border-line bg-paper p-6">
            <h2 className="font-body text-xs font-semibold tracking-[0.18em] uppercase">Summary</h2>
            <div className="mt-4 flex justify-between"><span>Subtotal</span><span className="font-medium">{formatPrice(subtotal)}</span></div>
            <p className="mt-2 text-sm text-muted">Delivery is calculated at checkout based on your district.
              {commerce.freeShippingThreshold && ` Free delivery on orders over ${formatPrice(commerce.freeShippingThreshold)}.`}</p>
            {hasIssues && <div className="mt-4"><Notice tone="danger">Some items are no longer available. Remove them to continue.</Notice></div>}
            {isGuest && <p className="mt-4 text-sm text-muted">You'll sign in or create an account at checkout; your cart comes with you.</p>}
            <ButtonLink to="/checkout" size="lg" className="mt-6 w-full" aria-disabled={hasIssues} onClick={(e) => hasIssues && e.preventDefault()}>Proceed to checkout</ButtonLink>
            <Link to="/shop" className="mt-4 block text-center text-sm link-underline">Continue shopping</Link>
          </aside>
        </div>
      )}
    </div>
  );
}

export function WishlistPage() {
  const { isAuthenticated } = useAuth();
  const wishlist = useWishlist();
  // Guests: the wishlist is a list of ids on this device; fetch current product cards for display.
  const guestProducts = useQuery({
    queryKey: ['guest-wishlist', wishlist.ids],
    queryFn: async () => (await catalogApi.products({ ids: wishlist.ids, pageSize: 100 })).items,
    enabled: !isAuthenticated && wishlist.ids.length > 0,
  });
  const products = isAuthenticated ? (wishlist.items ?? []).map((i) => i.product) : guestProducts.data ?? [];
  const loading = isAuthenticated ? wishlist.isLoading : guestProducts.isFetching;
  const cart = useCart();
  const available = products.filter((p) => p.inventoryState === 'Available' && !cart.contains(p.id));

  return (
    <div className="container-page py-12 sm:py-16">
      <Seo title="Wishlist" noIndex />
      <div className="flex flex-wrap items-end justify-between gap-4">
        <h1>Wishlist</h1>
        {available.length > 0 && <Button variant="outline" onClick={async () => { for (const p of available) await cart.add(p); }}>Move available pieces to cart</Button>}
      </div>
      {!isAuthenticated && wishlist.ids.length > 0 && <p className="mt-3 text-sm text-muted">Saved on this device. <Link to="/login" className="link-underline">Sign in</Link> to keep your wishlist across devices.</p>}
      <div className="mt-10">
        {loading ? <ProductGridSkeleton count={4} /> : products.length === 0 ? (
          <EmptyState icon={<Heart className="h-8 w-8" />} title="Nothing saved yet" description="Tap the heart on any piece to keep it here." action={<ButtonLink to="/shop" variant="outline">Discover pieces</ButtonLink>} />
        ) : <ProductGrid products={products} />}
      </div>
    </div>
  );
}

export function NotFoundPage() {
  return (
    <div className="container-page py-24 text-center">
      <Seo title="Page not found" noIndex />
      <p className="eyebrow">404</p>
      <h1 className="mt-3">This page has wandered off</h1>
      <p className="mx-auto mt-4 max-w-md text-muted">The page you were looking for doesn't exist or may have moved.</p>
      <div className="mt-8 flex justify-center gap-3"><ButtonLink to="/">Go home</ButtonLink><ButtonLink to="/shop" variant="outline">Shop</ButtonLink></div>
    </div>
  );
}
