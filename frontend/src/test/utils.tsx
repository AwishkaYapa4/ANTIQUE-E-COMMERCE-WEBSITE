import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import type { ImageDto, ProductCard, ProductDetail } from '@/types/api';

/** Exposes the current location to tests (MemoryRouter avoids jsdom/undici AbortSignal issues of data routers). */
const location = { current: { pathname: '/', search: '' } };
function LocationProbe() {
  const l = useLocation();
  location.current = { pathname: l.pathname, search: l.search };
  return null;
}

export function renderWithProviders(ui: ReactElement, { route = '/', path = '*' }: { route?: string; path?: string } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const result = render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[route]}>
        <LocationProbe />
        <Routes><Route path={path} element={ui} /></Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
  return { ...result, client, router: { get state() { return { location: location.current }; } } };
}

const img: ImageDto = { url: '/placeholders/product-clock.svg', alt: 'Clock', width: 1200, height: 1500, srcSet: null };

export function card(overrides: Partial<ProductCard> = {}): ProductCard {
  return {
    id: 1, slug: 'victorian-clock', name: 'Victorian Walnut Mantel Clock', price: 68000, compareAtPrice: null, currency: 'LKR',
    categoryName: 'Vintage Clocks', categorySlug: 'vintage-clocks', condition: 'VeryGood', era: 'Victorian', image: img, hoverImage: null,
    inventoryState: 'Available', isFeatured: true, maxPerOrder: 1, ...overrides,
  };
}

export function detail(overrides: Partial<ProductDetail> = {}): ProductDetail {
  return {
    id: 1, slug: 'victorian-clock', name: 'Victorian Walnut Mantel Clock', sku: 'EXAMPLE-002', price: 68000, compareAtPrice: 75000, currency: 'LKR',
    shortDescription: 'A handsome walnut-cased mantel clock.', description: '<p>Eight-day movement.</p><script>alert(1)</script>',
    category: { id: 2, name: 'Vintage Clocks', slug: 'vintage-clocks' }, collections: [],
    conditionReport: { overall: 'VeryGood', notes: 'Light wear', signsOfWear: 'Minor wear to the base', scratches: null, damage: null, restoration: null, missingParts: null, functionalStatus: 'Running' },
    material: 'Walnut', color: null, dimensions: 'W 30 × H 38 cm', weightKg: null, era: 'Victorian', approximateAge: 'Late 19th century',
    provenance: 'From a private collection in Kandy', hasProvenanceEvidence: false, provenanceEvidence: null,
    inventoryState: 'Available', maxPerOrder: 1, isDeliveryAvailable: true, images: [img], attributes: [], seoTitle: null, seoDescription: null,
    publishedAt: '2026-10-01T00:00:00Z', reviews: { count: 0, averageRating: null }, ...overrides,
  };
}
