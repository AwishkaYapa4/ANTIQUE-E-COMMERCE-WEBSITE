import { Link, useParams, useSearchParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import { Seo } from '@/components/Seo';
import { EmptyState, ErrorState, PageSpinner } from '@/components/ui/Feedback';
import { SmartImage } from '@/components/ui/Misc';
import { ApiError } from '@/services/http';
import { ShopView } from './ShopView';

function PageHeader({ eyebrow, title, description }: { eyebrow?: string; title: string; description?: string | null }) {
  return (
    <header className="max-w-3xl">
      {eyebrow && <p className="eyebrow mb-3">{eyebrow}</p>}
      <h1>{title}</h1>
      {description && <p className="mt-4 text-lg text-muted">{description}</p>}
    </header>
  );
}

export function ShopPage() {
  const [params] = useSearchParams();
  const q = params.get('q');
  return (
    <>
      <Seo title={q ? `Search: ${q}` : 'Shop antiques & vintage'} description="Browse one-of-a-kind antique furniture, clocks, art, lighting and collectibles." />
      <ShopView header={<PageHeader eyebrow="The collection" title={q ? `Results for “${q}”` : 'Shop all pieces'} description={q ? null : 'Every item is individually selected, photographed and described with an honest condition report.'} />} />
    </>
  );
}

export function CategoryPage() {
  const { slug = '' } = useParams();
  const category = useQuery({ queryKey: ['category', slug], queryFn: () => catalogApi.category(slug) });
  if (category.isPending) return <PageSpinner />;
  if (category.isError) {
    return category.error instanceof ApiError && category.error.status === 404
      ? <EmptyState title="Category not found" description="It may have been renamed." action={<Link to="/shop" className="link-underline">Browse all pieces</Link>} />
      : <ErrorState message={errorMessage(category.error)} onRetry={() => category.refetch()} />;
  }
  const c = category.data;
  return (
    <>
      <Seo title={c.seoTitle ?? c.name} description={c.seoDescription ?? c.description} image={c.imageUrl} />
      <ShopView fixed={{ category: slug }} header={<PageHeader eyebrow="Category" title={c.name} description={c.description} />} />
    </>
  );
}

export function CollectionPage() {
  const { slug = '' } = useParams();
  const collection = useQuery({ queryKey: ['collection', slug], queryFn: () => catalogApi.collection(slug) });
  if (collection.isPending) return <PageSpinner />;
  if (collection.isError) return <ErrorState message={errorMessage(collection.error)} onRetry={() => collection.refetch()} />;
  const c = collection.data;
  return (
    <>
      <Seo title={c.seoTitle ?? c.name} description={c.seoDescription ?? c.description} image={c.imageUrl} />
      <ShopView fixed={{ collection: slug }} header={<PageHeader eyebrow="Collection" title={c.name} description={c.description} />} />
    </>
  );
}

export function CollectionsPage() {
  const collections = useQuery({ queryKey: ['collections'], queryFn: catalogApi.collections, staleTime: 5 * 60_000 });
  const categories = useQuery({ queryKey: ['categories'], queryFn: catalogApi.categories, staleTime: 5 * 60_000 });
  return (
    <div className="container-page py-14">
      <Seo title="Collections" description="Curated collections and categories of antiques and vintage pieces." />
      <PageHeader eyebrow="Explore" title="Collections" description="Curated groupings of pieces that belong together." />
      {collections.isPending ? <PageSpinner /> : collections.isError ? <ErrorState message={errorMessage(collections.error)} /> : (
        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {collections.data.map((c) => (
            <Link key={c.id} to={`/collections/${c.slug}`} className="group block">
              <div className="overflow-hidden rounded-theme bg-surface">
                <SmartImage src={c.imageUrl} alt={c.name} ratio="4 / 3" className="h-full w-full transition-transform duration-700 group-hover:scale-105" sizes="(min-width: 1024px) 33vw, 100vw" />
              </div>
              <h2 className="mt-4 text-3xl group-hover:text-accent">{c.name}</h2>
              <p className="mt-1 text-sm text-muted">{c.productCount} pieces{c.description && ` · ${c.description}`}</p>
            </Link>
          ))}
          {collections.data.length === 0 && <EmptyState title="No collections yet" />}
        </div>
      )}
      {categories.data && categories.data.length > 0 && (
        <section className="mt-20">
          <h2 className="mb-8">Browse by category</h2>
          <ul className="grid gap-x-8 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
            {categories.data.filter((c) => c.parentId == null).map((c) => (
              <li key={c.id} className="border-b border-line pb-3">
                <Link to={`/category/${c.slug}`} className="flex justify-between font-heading text-xl hover:text-accent">{c.name}<span className="font-body text-sm text-muted">{c.productCount}</span></Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
