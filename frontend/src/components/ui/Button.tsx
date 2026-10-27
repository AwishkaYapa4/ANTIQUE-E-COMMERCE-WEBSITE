import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { Link, type LinkProps } from 'react-router';
import { Loader2 } from 'lucide-react';
import { cn } from '@/utils/format';

type Variant = 'primary' | 'outline' | 'ghost' | 'accent' | 'danger' | 'link' | 'light';
type Size = 'sm' | 'md' | 'lg' | 'icon';

const base =
  'inline-flex items-center justify-center gap-2 font-medium tracking-wide transition-colors duration-200 ' +
  'disabled:cursor-not-allowed disabled:opacity-50 rounded-theme select-none whitespace-nowrap';

const variants: Record<Variant, string> = {
  primary: 'bg-button text-button-ink hover:bg-ink',
  outline: 'border border-primary text-primary hover:bg-primary hover:text-button-ink',
  ghost: 'text-ink hover:bg-surface',
  accent: 'bg-accent text-paper hover:bg-primary',
  danger: 'bg-danger text-paper hover:opacity-90',
  link: 'text-primary underline underline-offset-4 decoration-line hover:decoration-accent px-0',
  light: 'bg-paper text-ink hover:bg-surface',
};

const sizes: Record<Size, string> = {
  sm: 'h-9 px-3.5 text-sm',
  md: 'h-11 px-5 text-sm',
  lg: 'h-13 px-7 text-[0.95rem]',
  icon: 'h-10 w-10',
};

export function buttonClass(variant: Variant = 'primary', size: Size = 'md', className?: string) {
  return cn(base, variants[variant], variant === 'link' ? 'h-auto' : sizes[size], className);
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  icon?: ReactNode;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', size = 'md', loading, icon, className, children, disabled, type = 'button', ...rest },
  ref,
) {
  return (
    <button ref={ref} type={type} className={buttonClass(variant, size, className)} disabled={disabled || loading} aria-busy={loading || undefined} {...rest}>
      {loading ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : icon}
      {children}
    </button>
  );
});

export function ButtonLink({ variant = 'primary', size = 'md', className, ...rest }: LinkProps & { variant?: Variant; size?: Size }) {
  return <Link className={buttonClass(variant, size, className)} {...rest} />;
}
