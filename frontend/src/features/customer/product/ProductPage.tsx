import { useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import DOMPurify from 'dompurify';
import { FileCheck2, Truck } from 'lucide-react';
import { catalogApi } from '@/services/api';
import { ApiError, errorMessage } from '@/services/http';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import type { ProductDetail } from '@/types/api';
import { Seo } from '@/components/Seo';
import { Modal } from '@/components/ui/Overlay';
import { EmptyState, ErrorState, InventoryBadge, Skeleton } from '@/components/ui/Feedback';
import { Accordion, Price } from '@/components/ui/Misc';
import { ButtonLink } from '@/components/ui/Button';
import { conditionDescriptions, conditionLabels } from '@/utils/format';
import { Gallery } from './Gallery';
import { PurchaseButtons } from './PurchaseButtons';
import { EnquiryForm } from './EnquiryForm';
import { Reviews } from './Reviews';
import { ProductGrid } from './ProductCard';

function Spec({ label, value }: { label: string; value: ReactNode }) {
  if (value == null || value === '') return null;
  return (
    <div className="grid grid-cols-[9rem_1fr] gap-3 border-b border-line py-2.5 text-sm last:border-0">
      <dt className="text-muted">{label}</dt>
      <dd className="text-ink">{value}</dd>
    </div>
  );
}

function ConditionReport({ product }: { product: ProductDetail }) {
  const r = product.conditionReport;
  const rows: [string, string | null][] = [
    ['Signs of wear', r.signsOfWear], ['Scratches', r.scratches], ['Damage', r.damage],
    ['Restoration', r.restoration], ['Missing parts', r.missingParts], ['Functional status', r.functionalStatus],
  ];
  return (
    <div className="rounded-theme border border-line bg-paper p-5">
      <div className="flex items-baseline justify-between gap-4">
        <h2 className="font-body text-xs font-semibold tracking-[0.18em] uppercase">Condition report</h2>
        <span className="font-heading text-xl text-primary">{conditionLabels[r.overall]}</span>
      </div>
      <p className="mt-1 text-sm text-muted">{conditionDescriptions[r.overall]}</p>
      {r.notes && <p className="mt-3 text-sm text-ink/85">{r.notes}</p>}
      <dl className="mt-3">
        {rows.map(([label, value]) => <Spec key={label} label={label} value={value ?? <span className="text-muted">None noted</span>} />)}
      </dl>
      <p className="mt-3 text-xs text-muted">Antique and vintage items show age-related wear. Please review all photographs and ask us if you'd like more detail.</p>
    </div>
  );
}

function ProductSkeleton() {
  return (
    <div className="container-page grid gap-10 py-10 lg:grid-cols-2">
      <Skeleton className="aspect-[4/5] w-full" />
      <div className="space-y-4"><Skeleton className="h-4 w-24" /><Skeleton className="h-12 w-3/4" /><Skeleton className="h-6 w-32" /><Skeleton className="h-40 w-full" /></div>
    </div>
  );
}

export default function ProductPage() {
  const { slug = '' } = useParams();
  const { commerce } = useSiteConfig();
  const [asking, setAsking] = useState(false);
  const product = useQuery({ queryKey: ['product', slug], queryFn: () => catalogApi.product(slug) });
  const related = useQuery({ queryKey: ['related', slug], queryFn: () => catalogApi.related(slug), enabled: product.isSuccess });

  if (product.isPending) return <ProductSkeleton />;
  if (product.isError) {
    if (product.error instanceof ApiError && product.error.status === 404) {
      return <EmptyState title="This piece is no longer listed" description="It may have been sold or removed." action={<ButtonLink to="/shop" variant="outline">Browse the collection</ButtonLink>} />;
    }
    return <ErrorState message={errorMessage(product.error)} onRetry={() => product.refetch()} />;
  }

  const p = product.data;
  const sold = p.inventoryState === 'Sold';
  const jsonLd = {
    '@context': 'https://schema.org', '@type': 'Product', name: p.name, sku: p.sku, description: p.shortDescription,
    image: p.images.map((i) => (i.url.startsWith('/') ? window.location.origin + i.url : i.url)), category: p.category?.name,
    offers: {
      '@type': 'Offer', priceCurrency: p.currency, price: p.price.toFixed(2), itemCondition: 'https://schema.org/UsedCondition',
      availability: p.inventoryState === 'Available' ? 'https://schema.org/InStock' : 'https://schema.org/SoldOut',
    },
  };

  return (
    <>
      <Seo title={p.seoTitle ?? p.name} description={p.seoDescription ?? p.shortDescription} image={p.images[0]?.url} type="product" jsonLd={jsonLd} />
      <div className="container-page py-6 sm:py-10">
        <nav aria-label="Breadcrumb" className="mb-6 text-xs text-muted">
          <ol className="flex flex-wrap gap-1.5">
            <li><Link to="/" className="hover:text-ink">Home</Link> /</li>
            <li><Link to="/shop" className="hover:text-ink">Shop</Link> /</li>
            {p.category && <li><Link to={`/category/${p.category.slug}`} className="hover:text-ink">{p.category.name}</Link> /</li>}
            <li aria-current="page" className="text-ink">{p.name}</li>
          </ol>
        </nav>

        <div className="grid gap-10 lg:grid-cols-[1.15fr_1fr] lg:gap-16">
          <Gallery images={p.images} name={p.name} sold={sold} />

          <div className="lg:sticky lg:top-28 lg:self-start">
            {p.category && <Link to={`/category/${p.category.slug}`} className="eyebrow">{p.category.name}</Link>}
            <h1 className="mt-3 text-4xl sm:text-5xl">{p.name}</h1>
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <Price price={p.price} compareAt={p.compareAtPrice} className="text-2xl" />
              <InventoryBadge state={p.inventoryState} />
            </div>
            {p.shortDescription && <p className="mt-5 leading-relaxed text-ink/85">{p.shortDescription}</p>}

            <dl className="mt-6">
              <Spec label="Condition" value={conditionLabels[p.conditionReport.overall]} />
              <Spec label="Era / style" value={p.era} />
              <Spec label="Approximate age" value={p.approximateAge} />
              <Spec label="Material" value={p.material} />
              <Spec label="Colour" value={p.color} />
              <Spec label="Dimensions" value={p.dimensions} />
              <Spec label="Weight" value={p.weightKg != null ? `${p.weightKg} kg` : null} />
              {p.attributes.map((a) => <Spec key={a.name} label={a.name} value={a.value} />)}
              <Spec label="SKU" value={<span className="font-mono text-xs">{p.sku}</span>} />
            </dl>

            <div className="mt-6"><PurchaseButtons product={p} onAsk={() => setAsking(true)} /></div>

            <p className="mt-5 flex items-start gap-2 text-sm text-muted">
              <Truck className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
              {p.isDeliveryAvailable ? 'Delivery available. Fees are calculated at checkout by district.' : 'Local pickup only — this piece is too large or fragile for courier delivery.'}
            </p>

            <div className="mt-8"><ConditionReport product={p} /></div>

            <div className="mt-6">
              {p.description && (
                <Accordion title="Detailed description" defaultOpen>
                  <div className="prose-antique" dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(p.description) }} />
                </Accordion>
              )}
              {(p.provenance || p.hasProvenanceEvidence) && (
                <Accordion title="Item history & provenance">
                  {p.provenance && <p className="whitespace-pre-line">{p.provenance}</p>}
                  {p.hasProvenanceEvidence && p.provenanceEvidence && (
                    <p className="mt-3 flex items-start gap-2 text-sm"><FileCheck2 className="mt-0.5 h-4 w-4 shrink-0 text-success" aria-hidden />
                      <span><strong className="font-medium">Documentation held:</strong> {p.provenanceEvidence}</span></p>
                  )}
                  {!p.hasProvenanceEvidence && <p className="mt-3 text-xs text-muted">History is as known to us; we do not hold documentation verifying it.</p>}
                </Accordion>
              )}
              <Accordion title="Delivery & returns">
                <p>{commerce.deliveryInstructions ?? 'Delivery options and fees for your district are shown at checkout before you pay.'}</p>
                {commerce.returnsSummary && <p className="mt-3">{commerce.returnsSummary}</p>}
                <p className="mt-3 text-sm"><Link to="/pages/shipping-policy" className="link-underline">Shipping policy</Link> · <Link to="/pages/return-policy" className="link-underline">Return policy</Link></p>
              </Accordion>
            </div>
          </div>
        </div>

        <Reviews slug={p.slug} />

        {related.data && related.data.length > 0 && (
          <section className="border-t border-line pt-14" aria-labelledby="related-heading">
            <h2 id="related-heading" className="mb-8 text-3xl">You may also like</h2>
            <ProductGrid products={related.data} />
          </section>
        )}
      </div>

      <Modal open={asking} onClose={() => setAsking(false)} title="Ask about this item">
        <p className="mb-5 text-sm text-muted">Questions about measurements, condition or delivery for “{p.name}”? We usually reply within one working day.</p>
        <EnquiryForm productId={p.id} productName={p.name} />
      </Modal>
    </>
  );
}
