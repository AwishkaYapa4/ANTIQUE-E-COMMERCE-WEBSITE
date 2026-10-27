import { useState } from 'react';
import { Link } from 'react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { BadgeCheck } from 'lucide-react';
import { catalogApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import { useAuth } from '@/features/auth/AuthProvider';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import { Button } from '@/components/ui/Button';
import { Field, Input, Textarea } from '@/components/ui/Field';
import { Notice } from '@/components/ui/Feedback';
import { Stars } from '@/components/ui/Misc';
import { formatDate } from '@/utils/format';

const schema = z.object({
  rating: z.number().int().min(1, 'Choose a rating.').max(5),
  title: z.string().max(200).optional(),
  body: z.string().trim().min(10, 'Please write at least 10 characters.').max(2000),
});

export function Reviews({ slug }: { slug: string }) {
  const { commerce } = useSiteConfig();
  const { isAuthenticated } = useAuth();
  const [writing, setWriting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const reviews = useQuery({ queryKey: ['reviews', slug], queryFn: () => catalogApi.reviews(slug) });
  const form = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { rating: 0, title: '', body: '' } });
  const create = useMutation({
    mutationFn: (v: z.infer<typeof schema>) => catalogApi.createReview(slug, { rating: v.rating, title: v.title || null, body: v.body }),
    onSuccess: () => { setSubmitted(true); setWriting(false); },
  });

  if (!commerce.reviewsEnabled) return null;
  const list = reviews.data ?? [];
  const rating = form.watch('rating');

  return (
    <section className="border-t border-line py-14" aria-labelledby="reviews-heading">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 id="reviews-heading" className="text-3xl">Customer reviews</h2>
          <p className="mt-1 text-sm text-muted">Reviews are moderated. “Verified purchase” appears only when the reviewer bought this item.</p>
        </div>
        {!writing && !submitted && (isAuthenticated
          ? <Button variant="outline" size="sm" onClick={() => setWriting(true)}>Write a review</Button>
          : <Link to="/login" className="link-underline text-sm">Sign in to write a review</Link>)}
      </div>

      {submitted && <div className="mt-6"><Notice tone="success">Thank you! Your review will appear once it has been approved.</Notice></div>}

      {writing && (
        <form className="mt-8 max-w-xl space-y-4" onSubmit={form.handleSubmit((v) => create.mutate(v))} noValidate>
          {create.isError && <Notice tone="danger">{errorMessage(create.error)}</Notice>}
          <fieldset>
            <legend className="mb-2 text-sm font-medium">Your rating</legend>
            <div className="flex gap-1">
              {[1, 2, 3, 4, 5].map((n) => (
                <button key={n} type="button" onClick={() => form.setValue('rating', n, { shouldValidate: true })} aria-label={`${n} star${n > 1 ? 's' : ''}`} aria-pressed={rating === n}
                  className={n <= rating ? 'text-accent' : 'text-line'}>
                  <span className="text-2xl">★</span>
                </button>
              ))}
            </div>
            {form.formState.errors.rating && <p className="text-sm text-danger">{form.formState.errors.rating.message}</p>}
          </fieldset>
          <Field label="Title (optional)">{(p) => <Input {...p} {...form.register('title')} />}</Field>
          <Field label="Review" required error={form.formState.errors.body?.message}>{(p) => <Textarea {...p} {...form.register('body')} />}</Field>
          <div className="flex gap-2">
            <Button type="submit" loading={create.isPending}>Submit review</Button>
            <Button variant="ghost" onClick={() => setWriting(false)}>Cancel</Button>
          </div>
        </form>
      )}

      {list.length === 0 ? (
        <p className="mt-8 text-muted">No reviews yet.</p>
      ) : (
        <ul className="mt-8 divide-y divide-line">
          {list.map((r) => (
            <li key={r.id} className="py-6">
              <div className="flex flex-wrap items-center gap-3">
                <Stars value={r.rating} />
                {r.title && <span className="font-medium">{r.title}</span>}
              </div>
              <p className="mt-2 text-ink/85">{r.body}</p>
              <p className="mt-2 flex items-center gap-2 text-xs text-muted">
                {r.authorName} · {formatDate(r.createdAt)}
                {r.isVerifiedPurchase && <span className="inline-flex items-center gap-1 text-success"><BadgeCheck className="h-3.5 w-3.5" aria-hidden /> Verified purchase</span>}
              </p>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
