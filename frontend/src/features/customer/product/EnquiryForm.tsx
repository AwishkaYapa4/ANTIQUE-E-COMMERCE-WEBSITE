import { useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { CheckCircle2 } from 'lucide-react';
import { siteApi } from '@/services/api';
import { ApiError, errorMessage } from '@/services/http';
import { useAuth } from '@/features/auth/AuthProvider';
import { Button } from '@/components/ui/Button';
import { Field, Honeypot, Input, Textarea } from '@/components/ui/Field';
import { Notice } from '@/components/ui/Feedback';

export const enquirySchema = z.object({
  name: z.string().trim().min(1, 'Please enter your name.').max(200),
  email: z.email('Enter a valid email address.'),
  phone: z.string().trim().max(20).regex(/^$|^\+?[0-9 ()-]{7,20}$/, 'Enter a valid phone number.').optional(),
  subject: z.string().trim().max(200).optional(),
  message: z.string().trim().min(10, 'Please write at least 10 characters.').max(4000),
  website: z.string().optional(),
});
export type EnquiryValues = z.infer<typeof enquirySchema>;

/** Used for both "Ask about this item" (productId set) and the general contact form. */
export function EnquiryForm({ productId, productName, onSent }: { productId?: number; productName?: string; onSent?: () => void }) {
  const { me } = useAuth();
  const startedAt = useRef(Date.now());
  const [sent, setSent] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<EnquiryValues>({
    resolver: zodResolver(enquirySchema),
    defaultValues: {
      name: me?.displayName ?? '', email: me?.email ?? '', phone: me?.phoneNumber ?? '',
      message: productName ? `Hello, I'm interested in "${productName}". ` : '',
    },
  });

  const submit = handleSubmit(async (values) => {
    setServerError(null);
    try {
      const body = { ...values, productId: productId ?? null, formStartedAt: startedAt.current };
      const result = productId ? await siteApi.enquiry(body) : await siteApi.contact(body);
      setSent(result.message);
      onSent?.();
    } catch (e) {
      if (e instanceof ApiError) {
        Object.entries(e.fieldErrors).forEach(([k, v]) => setError(k as keyof EnquiryValues, { message: v[0] }));
      }
      setServerError(errorMessage(e));
    }
  });

  if (sent) {
    return (
      <div className="flex flex-col items-center py-6 text-center" role="status">
        <CheckCircle2 className="h-10 w-10 text-success" aria-hidden />
        <p className="mt-3 text-lg">{sent}</p>
      </div>
    );
  }

  return (
    <form onSubmit={submit} className="relative space-y-4" noValidate>
      {serverError && <Notice tone="danger">{serverError}</Notice>}
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required error={errors.name?.message}>{(p) => <Input {...p} autoComplete="name" {...register('name')} />}</Field>
        <Field label="Email" required error={errors.email?.message}>{(p) => <Input {...p} type="email" autoComplete="email" {...register('email')} />}</Field>
      </div>
      <Field label="Phone (optional)" error={errors.phone?.message}>{(p) => <Input {...p} type="tel" autoComplete="tel" {...register('phone')} />}</Field>
      {!productId && <Field label="Subject" error={errors.subject?.message}>{(p) => <Input {...p} {...register('subject')} />}</Field>}
      <Field label="Message" required error={errors.message?.message}>{(p) => <Textarea {...p} rows={5} {...register('message')} />}</Field>
      <Honeypot register={register('website')} />
      <Button type="submit" loading={isSubmitting} className="w-full sm:w-auto">Send message</Button>
    </form>
  );
}
