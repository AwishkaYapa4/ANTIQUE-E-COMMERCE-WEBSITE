import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye } from 'lucide-react';
import { toast } from 'sonner';
import { adminApi } from '@/services/adminApi';
import { errorMessage } from '@/services/http';
import type { AdminCustomization } from '@/types/api';
import { Button } from '@/components/ui/Button';
import { useConfirm } from '@/components/ui/Overlay';
import { formatDateTime } from '@/utils/format';

/**
 * Draft → preview → publish workflow shared by the customization pages. Preview opens the real storefront
 * with ?preview=draft; drafts are fetched from admin-only endpoints, so customers never see them.
 */
export function PublishBar({ data, path = '/' }: { data?: AdminCustomization; path?: string }) {
  const queryClient = useQueryClient();
  const confirm = useConfirm();
  const done = (d: AdminCustomization, message: string) => {
    queryClient.setQueryData(['admin', 'customization'], d);
    queryClient.invalidateQueries({ queryKey: ['site'] });
    queryClient.invalidateQueries({ queryKey: ['homepage'] });
    toast.success(message);
  };
  const publish = useMutation({ mutationFn: adminApi.publish, onSuccess: (d) => done(d, 'Published — your changes are live.'), onError: (e) => toast.error(errorMessage(e)) });
  const discard = useMutation({ mutationFn: adminApi.discard, onSuccess: (d) => done(d, 'Draft changes discarded.'), onError: (e) => toast.error(errorMessage(e)) });
  const pending = data?.hasUnpublishedChanges;

  return (
    <div className="sticky top-14 z-20 mb-6 flex flex-wrap items-center gap-3 rounded-theme border border-line bg-paper/95 px-4 py-3 backdrop-blur">
      <span className={`h-2.5 w-2.5 rounded-full ${pending ? 'bg-accent' : 'bg-success'}`} aria-hidden />
      <p className="text-sm">
        {pending ? 'You have unpublished changes. Customers still see the published version.' : 'Everything is published.'}
        {data?.lastPublishedAt && <span className="text-muted"> Last published {formatDateTime(data.lastPublishedAt)}.</span>}
      </p>
      <div className="ml-auto flex flex-wrap gap-2">
        <a href={`${path}?preview=draft`} target="_blank" rel="noopener noreferrer" className="inline-flex h-9 items-center gap-1.5 rounded-theme border border-line px-3 text-sm hover:bg-surface"><Eye className="h-4 w-4" aria-hidden /> Preview draft</a>
        <Button size="sm" variant="ghost" disabled={!pending} loading={discard.isPending}
          onClick={async () => { if (await confirm({ title: 'Discard draft changes?', message: 'The draft will be reset to what is currently published.', danger: true, confirmLabel: 'Discard' })) discard.mutate(); }}>Discard</Button>
        <Button size="sm" disabled={!pending} loading={publish.isPending}
          onClick={async () => { if (await confirm({ title: 'Publish changes?', message: 'Brand, theme, navigation and homepage drafts will go live for all visitors.', confirmLabel: 'Publish' })) publish.mutate(); }}>Publish</Button>
      </div>
    </div>
  );
}
