import { useState, type ReactNode } from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { MailCheck } from 'lucide-react';
import { toast } from 'sonner';
import { Seo } from '@/components/Seo';
import { Button } from '@/components/ui/Button';
import { Field, Input } from '@/components/ui/Field';
import { Notice } from '@/components/ui/Feedback';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import { authErrorMessage, useAuth } from './AuthProvider';

/** Only allow redirects back to our own site paths (prevents open-redirects). */
function safeRedirect(value: string | null): string {
  return value && value.startsWith('/') && !value.startsWith('//') ? value : '/account';
}

function AuthShell({ title, subtitle, children }: { title: string; subtitle?: ReactNode; children: ReactNode }) {
  const { brand } = useSiteConfig();
  const { isConfigured } = useAuth();
  return (
    <div className="container-page flex min-h-[70vh] items-center justify-center py-14">
      <Seo title={title} noIndex />
      <div className="w-full max-w-md">
        <p className="eyebrow text-center">{brand.storeName}</p>
        <h1 className="mt-3 text-center text-4xl">{title}</h1>
        {subtitle && <p className="mt-3 text-center text-muted">{subtitle}</p>}
        <div className="mt-8 rounded-theme border border-line bg-paper p-6 sm:p-8">
          {isConfigured ? children : (
            <Notice tone="danger" title="Sign-in is not configured">
              This site's Firebase Authentication settings have not been added yet. The store owner needs to set the VITE_FIREBASE_* values (see README).
            </Notice>
          )}
        </div>
      </div>
    </div>
  );
}

function GoogleButton({ onSuccess }: { onSuccess: () => void }) {
  const { signInWithGoogle } = useAuth();
  const [busy, setBusy] = useState(false);
  return (
    <Button variant="outline" className="w-full" loading={busy} onClick={async () => {
      setBusy(true);
      try { await signInWithGoogle(); onSuccess(); } catch (e) { toast.error(authErrorMessage(e)); } finally { setBusy(false); }
    }}>
      <svg viewBox="0 0 24 24" className="h-4 w-4" aria-hidden><path fill="#4285F4" d="M22.6 12.2c0-.7-.1-1.4-.2-2H12v3.8h5.9a5 5 0 0 1-2.2 3.3v2.7h3.6c2.1-1.9 3.3-4.8 3.3-7.8z"/><path fill="#34A853" d="M12 23c3 0 5.5-1 7.3-2.7l-3.6-2.7c-1 .7-2.2 1.1-3.7 1.1-2.9 0-5.3-1.9-6.2-4.5H2.1v2.8A11 11 0 0 0 12 23z"/><path fill="#FBBC05" d="M5.8 14.2a6.6 6.6 0 0 1 0-4.3V7.1H2.1a11 11 0 0 0 0 9.9l3.7-2.8z"/><path fill="#EA4335" d="M12 5.4c1.6 0 3.1.6 4.2 1.7l3.2-3.2A11 11 0 0 0 2.1 7.1l3.7 2.8C6.7 7.3 9.1 5.4 12 5.4z"/></svg>
      Continue with Google
    </Button>
  );
}

const Divider = () => <div className="my-6 flex items-center gap-3 text-xs text-muted"><span className="h-px flex-1 bg-line" />or<span className="h-px flex-1 bg-line" /></div>;

const loginSchema = z.object({ email: z.email('Enter a valid email address.'), password: z.string().min(1, 'Enter your password.') });

export function LoginPage() {
  const { signIn, isAuthenticated, initializing } = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const redirect = safeRedirect(params.get('redirect'));
  const [error, setError] = useState<string | null>(null);
  const form = useForm<z.infer<typeof loginSchema>>({ resolver: zodResolver(loginSchema) });

  if (!initializing && isAuthenticated) return <Navigate to={redirect} replace />;

  const submit = form.handleSubmit(async ({ email, password }) => {
    setError(null);
    try { await signIn(email, password); navigate(redirect, { replace: true }); } catch (e) { setError(authErrorMessage(e)); }
  });

  return (
    <AuthShell title="Welcome back" subtitle={<>New here? <Link to={`/register?redirect=${encodeURIComponent(redirect)}`} className="link-underline text-ink">Create an account</Link></>}>
      <GoogleButton onSuccess={() => navigate(redirect, { replace: true })} />
      <Divider />
      <form onSubmit={submit} className="space-y-4" noValidate>
        {error && <Notice tone="danger">{error}</Notice>}
        <Field label="Email" error={form.formState.errors.email?.message}>{(p) => <Input {...p} type="email" autoComplete="email" {...form.register('email')} />}</Field>
        <Field label="Password" error={form.formState.errors.password?.message}>{(p) => <Input {...p} type="password" autoComplete="current-password" {...form.register('password')} />}</Field>
        <div className="text-right text-sm"><Link to="/forgot-password" className="link-underline text-muted">Forgot password?</Link></div>
        <Button type="submit" className="w-full" size="lg" loading={form.formState.isSubmitting}>Sign in</Button>
      </form>
    </AuthShell>
  );
}

const registerSchema = z.object({
  name: z.string().trim().min(1, 'Enter your name.').max(100),
  email: z.email('Enter a valid email address.'),
  password: z.string().min(8, 'Use at least 8 characters.').regex(/[A-Za-z]/, 'Include at least one letter.').regex(/\d/, 'Include at least one number.'),
  confirm: z.string(),
}).refine((v) => v.password === v.confirm, { path: ['confirm'], message: 'Passwords do not match.' });

export function RegisterPage() {
  const { register: registerUser, isAuthenticated, initializing } = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const redirect = safeRedirect(params.get('redirect'));
  const [error, setError] = useState<string | null>(null);
  const form = useForm<z.infer<typeof registerSchema>>({ resolver: zodResolver(registerSchema) });

  if (!initializing && isAuthenticated && !form.formState.isSubmitSuccessful) return <Navigate to={redirect} replace />;

  const submit = form.handleSubmit(async ({ name, email, password }) => {
    setError(null);
    try {
      await registerUser(name, email, password);
      navigate(`/verify-email?redirect=${encodeURIComponent(redirect)}`, { replace: true });
    } catch (e) { setError(authErrorMessage(e)); }
  });

  return (
    <AuthShell title="Create an account" subtitle={<>Already registered? <Link to={`/login?redirect=${encodeURIComponent(redirect)}`} className="link-underline text-ink">Sign in</Link></>}>
      <GoogleButton onSuccess={() => navigate(redirect, { replace: true })} />
      <Divider />
      <form onSubmit={submit} className="space-y-4" noValidate>
        {error && <Notice tone="danger">{error}</Notice>}
        <Field label="Full name" error={form.formState.errors.name?.message}>{(p) => <Input {...p} autoComplete="name" {...form.register('name')} />}</Field>
        <Field label="Email" error={form.formState.errors.email?.message}>{(p) => <Input {...p} type="email" autoComplete="email" {...form.register('email')} />}</Field>
        <Field label="Password" hint="At least 8 characters with a letter and a number." error={form.formState.errors.password?.message}>{(p) => <Input {...p} type="password" autoComplete="new-password" {...form.register('password')} />}</Field>
        <Field label="Confirm password" error={form.formState.errors.confirm?.message}>{(p) => <Input {...p} type="password" autoComplete="new-password" {...form.register('confirm')} />}</Field>
        <Button type="submit" className="w-full" size="lg" loading={form.formState.isSubmitting}>Create account</Button>
        <p className="text-xs text-muted">By creating an account you agree to our <Link to="/pages/terms" className="link-underline">terms</Link> and <Link to="/pages/privacy-policy" className="link-underline">privacy policy</Link>.</p>
      </form>
    </AuthShell>
  );
}

export function ForgotPasswordPage() {
  const { resetPassword } = useAuth();
  const [sent, setSent] = useState(false);
  const form = useForm<{ email: string }>({ resolver: zodResolver(z.object({ email: z.email('Enter a valid email address.') })) });
  const submit = form.handleSubmit(async ({ email }) => {
    try { await resetPassword(email); } catch { /* do not reveal whether the account exists */ }
    setSent(true);
  });
  return (
    <AuthShell title="Reset your password" subtitle="We'll email you a link to choose a new password.">
      {sent ? (
        <Notice tone="success">If an account exists for that email, a reset link is on its way. Check your inbox (and spam folder).</Notice>
      ) : (
        <form onSubmit={submit} className="space-y-4" noValidate>
          <Field label="Email" error={form.formState.errors.email?.message}>{(p) => <Input {...p} type="email" autoComplete="email" {...form.register('email')} />}</Field>
          <Button type="submit" className="w-full" loading={form.formState.isSubmitting}>Send reset link</Button>
        </form>
      )}
      <p className="mt-6 text-center text-sm"><Link to="/login" className="link-underline">Back to sign in</Link></p>
    </AuthShell>
  );
}

export function VerifyEmailPage() {
  const { firebaseUser, resendVerification, refreshUser } = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [checking, setChecking] = useState(false);
  if (!firebaseUser) return <Navigate to="/login" replace />;
  if (firebaseUser.emailVerified) return <Navigate to={safeRedirect(params.get('redirect'))} replace />;
  return (
    <AuthShell title="Verify your email" subtitle={<>We sent a link to <strong className="text-ink">{firebaseUser.email}</strong>.</>}>
      <div className="flex flex-col items-center text-center">
        <MailCheck className="h-10 w-10 text-accent" aria-hidden />
        <p className="mt-4 text-sm text-muted">Click the link in the email, then return here. You need a verified email to place orders.</p>
        <Button className="mt-6 w-full" loading={checking} onClick={async () => {
          setChecking(true);
          const ok = await refreshUser().finally(() => setChecking(false));
          if (ok) { toast.success('Email verified â€” thank you!'); navigate(safeRedirect(params.get('redirect')), { replace: true }); }
          else toast.info("We haven't seen the verification yet. Please click the link in your email.");
        }}>I've verified my email</Button>
        <Button variant="link" className="mt-4" onClick={async () => { await resendVerification(); toast.success('Verification email sent again.'); }}>Resend email</Button>
      </div>
    </AuthShell>
  );
}

