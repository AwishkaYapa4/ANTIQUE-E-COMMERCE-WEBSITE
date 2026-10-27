import { forwardRef, useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import { cn } from '@/utils/format';

const control =
  'w-full rounded-theme border border-line bg-paper px-3.5 py-2.5 text-[0.95rem] text-ink placeholder:text-muted/70 ' +
  'transition-colors focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:bg-surface disabled:text-muted';

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { invalid?: boolean }>(
  function Input({ className, invalid, ...rest }, ref) {
    return <input ref={ref} className={cn(control, invalid && 'border-danger', className)} aria-invalid={invalid || undefined} {...rest} />;
  },
);

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement> & { invalid?: boolean }>(
  function Textarea({ className, invalid, rows = 4, ...rest }, ref) {
    return <textarea ref={ref} rows={rows} className={cn(control, 'resize-y', invalid && 'border-danger', className)} aria-invalid={invalid || undefined} {...rest} />;
  },
);

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement> & { invalid?: boolean }>(
  function Select({ className, invalid, children, ...rest }, ref) {
    return (
      <select ref={ref} className={cn(control, 'appearance-auto pr-8', invalid && 'border-danger', className)} aria-invalid={invalid || undefined} {...rest}>
        {children}
      </select>
    );
  },
);

export const Checkbox = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement> & { label: ReactNode; description?: ReactNode }>(
  function Checkbox({ label, description, className, id, ...rest }, ref) {
    const generated = useId();
    const inputId = id ?? generated;
    return (
      <label htmlFor={inputId} className={cn('flex cursor-pointer items-start gap-3 text-sm', className)}>
        <input ref={ref} id={inputId} type="checkbox" className="mt-0.5 h-4 w-4 shrink-0 accent-[var(--c-primary)]" {...rest} />
        <span>
          <span className="text-ink">{label}</span>
          {description && <span className="mt-0.5 block text-muted">{description}</span>}
        </span>
      </label>
    );
  },
);

interface FieldProps {
  label: ReactNode;
  error?: string;
  hint?: ReactNode;
  required?: boolean;
  className?: string;
  children: (props: { id: string; 'aria-describedby'?: string; invalid: boolean }) => ReactNode;
}

/** Label + control + hint/error, wired up for screen readers. */
export function Field({ label, error, hint, required, className, children }: FieldProps) {
  const id = useId();
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;
  return (
    <div className={cn('space-y-1.5', className)}>
      <label htmlFor={id} className="block text-sm font-medium text-ink">
        {label}
        {required && <span className="ml-0.5 text-danger" aria-hidden>*</span>}
      </label>
      {children({ id, 'aria-describedby': describedBy, invalid: Boolean(error) })}
      {error ? (
        <p id={`${id}-error`} className="text-sm text-danger" role="alert">{error}</p>
      ) : hint ? (
        <p id={`${id}-hint`} className="text-xs text-muted">{hint}</p>
      ) : null}
    </div>
  );
}

/** Hidden honeypot input: bots fill it, humans never see it. */
export function Honeypot({ register }: { register: object }) {
  return (
    <div aria-hidden className="absolute -left-[9999px] h-0 w-0 overflow-hidden">
      <label>
        Website
        <input type="text" tabIndex={-1} autoComplete="off" {...register} />
      </label>
    </div>
  );
}
