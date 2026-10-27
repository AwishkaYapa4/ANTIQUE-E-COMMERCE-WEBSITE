import { useQuery } from '@tanstack/react-query';
import { siteApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import { ErrorState, Skeleton } from '@/components/ui/Feedback';
import { ProductGridSkeleton } from '@/features/customer/product/ProductCard';
import { Seo } from '@/components/Seo';
import { SectionRenderer } from './sections';

export default function HomePage() {
  const { preview, brand } = useSiteConfig();
  const query = useQuery({ queryKey: ['homepage', preview ? 'draft' : 'published'], queryFn: () => siteApi.homepage(preview), staleTime: preview ? 0 : 60_000 });

  return (
    <>
      <Seo description={brand.seoDefaultDescription ?? brand.description} />
      {query.isPending ? (
        <>
          <Skeleton className="h-[70vh] w-full rounded-none" />
          <div className="container-page py-20"><ProductGridSkeleton count={4} /></div>
        </>
      ) : query.isError ? (
        <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} />
      ) : (
        query.data.sections.map((section) => <SectionRenderer key={section.key} section={section} />)
      )}
    </>
  );
}
