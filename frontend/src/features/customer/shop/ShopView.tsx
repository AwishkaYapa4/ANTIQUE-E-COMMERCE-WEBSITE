import { useEffect, useState, type ReactNode } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { SlidersHorizontal, X } from 'lucide-react';
import { catalogApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import type { FacetValue, ProductCondition, ProductSort } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { Checkbox, Input, Select } from '@/components/ui/Field';
import { EmptyState, ErrorState } from '@/components/ui/Feedback';
import { Drawer } from '@/components/ui/Overlay';
import { Pagination } from '@/components/ui/Misc';
import { ProductGrid, ProductGridSkeleton } from '@/features/customer/product/ProductCard';
import { conditionLabels, formatPrice } from '@/utils/format';
import { useShopFilters } from './useShopFilters';

const sortLabels: Record<ProductSort, string> = {
  Featured: 'Recommended',
  Newest: 'Newest',
  RecentlyAdded: 'Recently added',
  PriceAsc: 'Price: low to high',
  PriceDesc: 'Price: high to low',
};

function FacetGroup({ title, children }: { title: string; children: ReactNode }) {
  return (
    <fieldset className="border-b border-line py-5 first:pt-0">
      <legend className="mb-3 text-xs font-semibold tracking-[0.18em] text-ink uppercase">{title}</legend>
      <div className="space-y-2.5">{children}</div>
    </fieldset>
  );
}

function PriceInputs({ min, max, onApply }: { min?: number; max?: number; onApply: (min?: number, max?: number) => void }) {
  const [lo, setLo] = useState(min?.toString() ?? '');
  const [hi, setHi] = useState(max?.toString() ?? '');
  useEffect(() => { setLo(min?.toString() ?? ''); setHi(max?.toString() ?? ''); }, [min, max]);
  const apply = () => onApply(lo ? Number(lo) : undefined, hi ? Number(hi) : undefined);
  return (
    <form className="flex items-center gap-2" onSubmit={(e) => { e.preventDefault(); apply(); }}>
      <label className="sr-only" htmlFor="price-min">Minimum price</label>
      <Input id="price-min" inputMode="numeric" placeholder="Min" value={lo} onChange={(e) => setLo(e.target.value.replace(/\D/g, ''))} onBlur={apply} className="py-2 text-sm" />
      <span className="text-muted">–</span>
      <label className="sr-only" htmlFor="price-max">Maximum price</label>
      <Input id="price-max" inputMode="numeric" placeholder="Max" value={hi} onChange={(e) => setHi(e.target.value.replace(/\D/g, ''))} onBlur={apply} className="py-2 text-sm" />
    </form>
  );
}

function Filters({ filters, hideCategory }: { filters: ReturnType<typeof useShopFilters>; hideCategory?: boolean }) {
  const { query, setValue, toggleValue } = filters;
  const facets = useQuery({ queryKey: ['facets', query.category ?? null], queryFn: () => catalogApi.facets(query.category), staleTime: 5 * 60_000 });
  const f = facets.data;
  const list = (key: 'condition' | 'era' | 'material', values: FacetValue[] | undefined, selected: string[]) =>
    (values ?? []).map((v) => (
      <Checkbox key={v.value} label={<>{key === 'condition' ? conditionLabels[v.value as ProductCondition] ?? v.label : v.label} <span className="text-muted">({v.count})</span></>}
        checked={selected.includes(v.value)} onChange={() => toggleValue(key, v.value)} />
    ));

  return (
    <div>
      {!hideCategory && (f?.categories.length ?? 0) > 0 && (
        <FacetGroup title="Category">
          <Checkbox label="All categories" checked={!query.category} onChange={() => setValue('category', null)} />
          {f!.categories.map((c) => (
            <Checkbox key={c.value} label={<>{c.label} <span className="text-muted">({c.count})</span></>} checked={query.category === c.value}
              onChange={() => setValue('category', query.category === c.value ? null : c.value)} />
          ))}
        </FacetGroup>
      )}
      <FacetGroup title="Availability">
        {(['All', 'Available', 'Sold'] as const).map((a) => (
          <label key={a} className="flex cursor-pointer items-center gap-3 text-sm">
            <input type="radio" name="availability" className="accent-[var(--c-primary)]" checked={query.availability === a} onChange={() => setValue('availability', a === 'All' ? null : a)} />
            {a === 'All' ? 'All pieces' : a === 'Available' ? 'Available now' : 'Recently sold'}
          </label>
        ))}
      </FacetGroup>
      <FacetGroup title="Price (LKR)">
        <PriceInputs min={query.minPrice} max={query.maxPrice} onApply={(min, max) => { setValue('minPrice', min); setValue('maxPrice', max); }} />
        {f?.minPrice != null && <p className="text-xs text-muted">From {formatPrice(f.minPrice)} to {formatPrice(f.maxPrice ?? 0)}</p>}
      </FacetGroup>
      {(f?.conditions.length ?? 0) > 0 && <FacetGroup title="Condition">{list('condition', f?.conditions, query.condition!)}</FacetGroup>}
      {(f?.eras.length ?? 0) > 0 && <FacetGroup title="Era / Style">{list('era', f?.eras, query.era!)}</FacetGroup>}
      {(f?.materials.length ?? 0) > 0 && <FacetGroup title="Material">{list('material', f?.materials, query.material!)}</FacetGroup>}
    </div>
  );
}

function ActiveFilters({ filters, hideCategory }: { filters: ReturnType<typeof useShopFilters>; hideCategory?: boolean }) {
  const { query, setValue, toggleValue, reset, activeCount } = filters;
  if (activeCount === 0) return null;
  const chip = (label: string, onRemove: () => void) => (
    <button key={label} type="button" onClick={onRemove} className="inline-flex items-center gap-1.5 rounded-full border border-line bg-paper px-3 py-1 text-xs hover:border-ink" aria-label={`Remove filter ${label}`}>
      {label} <X className="h-3 w-3" aria-hidden />
    </button>
  );
  return (
    <div className="mb-6 flex flex-wrap items-center gap-2" aria-label="Active filters">
      {query.q && chip(`“${query.q}”`, () => setValue('q', null))}
      {!hideCategory && query.category && chip(query.category.replaceAll('-', ' '), () => setValue('category', null))}
      {query.availability !== 'All' && chip(query.availability === 'Sold' ? 'Recently sold' : 'Available now', () => setValue('availability', null))}
      {query.minPrice != null && chip(`From ${formatPrice(query.minPrice)}`, () => setValue('minPrice', null))}
      {query.maxPrice != null && chip(`Up to ${formatPrice(query.maxPrice)}`, () => setValue('maxPrice', null))}
      {query.condition!.map((c) => chip(conditionLabels[c as ProductCondition] ?? c, () => toggleValue('condition', c)))}
      {query.era!.map((e) => chip(e, () => toggleValue('era', e)))}
      {query.material!.map((m) => chip(m, () => toggleValue('material', m)))}
      <button type="button" onClick={reset} className="ml-1 text-xs text-muted underline underline-offset-4 hover:text-ink">Clear all</button>
    </div>
  );
}

export function ShopView({ fixed = {}, header }: { fixed?: { category?: string; collection?: string }; header: ReactNode }) {
  const filters = useShopFilters(fixed);
  const { query } = filters;
  const [drawerOpen, setDrawerOpen] = useState(false);
  const products = useQuery({ queryKey: ['products', query], queryFn: () => catalogApi.products(query), placeholderData: keepPreviousData });
  const data = products.data;

  return (
    <div className="container-page pt-10 pb-8 sm:pt-14">
      {header}
      <div className="mt-10 grid gap-10 lg:grid-cols-[250px_1fr]">
        <aside className="hidden lg:block" aria-label="Filters"><Filters filters={filters} hideCategory={Boolean(fixed.category)} /></aside>

        <div>
          <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
            <Button variant="outline" size="sm" className="lg:hidden" onClick={() => setDrawerOpen(true)} icon={<SlidersHorizontal className="h-4 w-4" />}>
              Filters{filters.activeCount > 0 && ` (${filters.activeCount})`}
            </Button>
            <p className="text-sm text-muted" aria-live="polite">{data ? `${data.totalCount} ${data.totalCount === 1 ? 'piece' : 'pieces'}` : ' '}</p>
            <div className="flex items-center gap-2">
              <label htmlFor="sort" className="text-sm text-muted">Sort</label>
              <Select id="sort" value={query.sort} onChange={(e) => filters.setValue('sort', e.target.value === 'Featured' ? null : e.target.value)} className="w-auto py-2 text-sm">
                {Object.entries(sortLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </Select>
            </div>
          </div>
          <ActiveFilters filters={filters} hideCategory={Boolean(fixed.category)} />

          {products.isPending ? (
            <ProductGridSkeleton count={9} columns={3} />
          ) : products.isError ? (
            <ErrorState message={errorMessage(products.error)} onRetry={() => products.refetch()} />
          ) : data!.items.length === 0 ? (
            <EmptyState
              title="No pieces match these filters"
              description="Try removing a filter or widening the price range. New pieces arrive regularly."
              action={filters.activeCount > 0 ? <Button variant="outline" onClick={filters.reset}>Clear filters</Button> : undefined}
            />
          ) : (
            <div className={products.isPlaceholderData ? 'opacity-60 transition-opacity' : undefined}>
              <ProductGrid products={data!.items} columns={3} />
              <Pagination page={data!.page} totalPages={data!.totalPages} onChange={(p) => { filters.setPage(p); window.scrollTo({ top: 0, behavior: 'smooth' }); }} />
            </div>
          )}
        </div>
      </div>

      <Drawer open={drawerOpen} onClose={() => setDrawerOpen(false)} title="Filters" side="left"
        footer={<div className="flex gap-2"><Button variant="ghost" onClick={filters.reset}>Reset</Button><Button className="flex-1" onClick={() => setDrawerOpen(false)}>Show {data?.totalCount ?? ''} results</Button></div>}>
        <Filters filters={filters} hideCategory={Boolean(fixed.category)} />
      </Drawer>
    </div>
  );
}
