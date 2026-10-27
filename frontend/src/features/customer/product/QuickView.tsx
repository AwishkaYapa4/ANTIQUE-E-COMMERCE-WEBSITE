import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { catalogApi } from '@/services/api';
import { useUiStore } from '@/store/uiStore';
import { Modal } from '@/components/ui/Overlay';
import { ErrorState, InventoryBadge, PageSpinner } from '@/components/ui/Feedback';
import { Price, SmartImage } from '@/components/ui/Misc';
import { errorMessage } from '@/services/http';
import { conditionLabels } from '@/utils/format';
import { PurchaseButtons } from './PurchaseButtons';

export function QuickViewModal() {
  const slug = useUiStore((s) => s.quickViewSlug);
  const close = () => useUiStore.getState().openQuickView(null);
  const query = useQuery({ queryKey: ['product', slug], queryFn: () => catalogApi.product(slug!), enabled: Boolean(slug) });
  const p = query.data;

  return (
    <Modal open={Boolean(slug)} onClose={close} title="Quick view" size="lg">
      {query.isPending ? (
        <PageSpinner />
      ) : query.isError ? (
        <ErrorState message={errorMessage(query.error)} onRetry={() => query.refetch()} />
      ) : p ? (
        <div className="grid gap-6 sm:grid-cols-2">
          <SmartImage image={p.images[0]} alt={p.images[0]?.alt ?? p.name} ratio="4 / 5" className="w-full rounded-theme" sizes="(min-width: 640px) 380px, 90vw" />
          <div className="flex flex-col">
            <p className="eyebrow">{p.category?.name}</p>
            <h3 className="mt-2 text-3xl">{p.name}</h3>
            <Price price={p.price} compareAt={p.compareAtPrice} className="mt-3 text-xl" />
            <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-muted">
              <InventoryBadge state={p.inventoryState} />
              <span>Condition: {conditionLabels[p.conditionReport.overall]}</span>
              {p.era && <span>· {p.era}</span>}
            </div>
            {p.shortDescription && <p className="mt-4 text-[0.95rem] text-ink/85">{p.shortDescription}</p>}
            <div className="mt-6"><PurchaseButtons product={p} onDone={close} /></div>
            <Link to={`/products/${p.slug}`} onClick={close} className="link-underline mt-5 text-sm">See full details & condition report</Link>
          </div>
        </div>
      ) : null}
    </Modal>
  );
}
