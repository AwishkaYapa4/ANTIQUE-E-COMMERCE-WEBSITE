import { useCallback, useMemo } from 'react';
import { useSearchParams } from 'react-router';
import type { ProductQuery } from '@/services/api';
import type { Availability, ProductSort } from '@/types/api';

const SORTS: ProductSort[] = ['Featured', 'Newest', 'PriceAsc', 'PriceDesc', 'RecentlyAdded'];
const AVAILABILITY: Availability[] = ['All', 'Available', 'Sold'];
export const PAGE_SIZE = 24;

const num = (v: string | null) => (v && !Number.isNaN(Number(v)) && Number(v) >= 0 ? Number(v) : undefined);

/** Shop filters live in the URL so they survive reloads, back/forward and sharing. */
export function useShopFilters(fixed: { category?: string; collection?: string } = {}) {
  const [params, setParams] = useSearchParams();

  const query: ProductQuery = useMemo(() => {
    const sort = params.get('sort') as ProductSort | null;
    const availability = params.get('availability') as Availability | null;
    return {
      q: params.get('q') ?? undefined,
      category: fixed.category ?? params.get('category') ?? undefined,
      collection: fixed.collection ?? params.get('collection') ?? undefined,
      minPrice: num(params.get('minPrice')),
      maxPrice: num(params.get('maxPrice')),
      condition: params.getAll('condition'),
      era: params.getAll('era'),
      material: params.getAll('material'),
      availability: availability && AVAILABILITY.includes(availability) ? availability : 'All',
      sort: sort && SORTS.includes(sort) ? sort : 'Featured',
      page: Math.max(1, num(params.get('page')) ?? 1),
      pageSize: PAGE_SIZE,
    };
  }, [params, fixed.category, fixed.collection]);

  const update = useCallback(
    (mutate: (p: URLSearchParams) => void, resetPage = true) => {
      setParams((prev) => {
        const next = new URLSearchParams(prev);
        mutate(next);
        if (resetPage) next.delete('page');
        return next;
      }, { preventScrollReset: !resetPage });
    },
    [setParams],
  );

  const setValue = (key: string, value: string | number | undefined | null) =>
    update((p) => (value === undefined || value === null || value === '' ? p.delete(key) : p.set(key, String(value))));

  const toggleValue = (key: 'condition' | 'era' | 'material', value: string) =>
    update((p) => {
      const values = p.getAll(key);
      p.delete(key);
      (values.includes(value) ? values.filter((v) => v !== value) : [...values, value]).forEach((v) => p.append(key, v));
    });

  const reset = () => update((p) => ['q', 'category', 'collection', 'minPrice', 'maxPrice', 'condition', 'era', 'material', 'availability'].forEach((k) => p.delete(k)));

  const activeCount =
    (query.q ? 1 : 0) + (!fixed.category && query.category ? 1 : 0) + (query.minPrice != null ? 1 : 0) + (query.maxPrice != null ? 1 : 0) +
    query.condition!.length + query.era!.length + query.material!.length + (query.availability !== 'All' ? 1 : 0);

  return { query, setValue, toggleValue, reset, activeCount, setPage: (page: number) => update((p) => p.set('page', String(page)), false) };
}
