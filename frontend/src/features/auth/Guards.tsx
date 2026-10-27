import type { ReactNode } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { PageSpinner } from '@/components/ui/Feedback';
import { useAuth } from './AuthProvider';

/** Route guard: signed-in customers only. Real enforcement is server-side; this is UX. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated, initializing } = useAuth();
  const location = useLocation();
  if (initializing) return <PageSpinner />;
  if (!isAuthenticated) return <Navigate to={`/login?redirect=${encodeURIComponent(location.pathname + location.search)}`} replace />;
  return <>{children}</>;
}

/** Hides the admin UI from non-admins. Every admin API call is independently authorized by the backend. */
export function RequireAdmin({ children }: { children: ReactNode }) {
  const { isAuthenticated, isAdmin, initializing } = useAuth();
  const location = useLocation();
  if (initializing) return <PageSpinner />;
  if (!isAuthenticated) return <Navigate to={`/login?redirect=${encodeURIComponent(location.pathname)}`} replace />;
  if (!isAdmin) {
    return (
      <div className="container-page py-24 text-center">
        <h1 className="text-4xl">Access restricted</h1>
        <p className="mt-3 text-muted">Your account doesn't have access to the store admin. Admin access is granted by the store owner on the server.</p>
        <Link to="/" className="link-underline mt-6 inline-block">Back to the shop</Link>
      </div>
    );
  }
  return <>{children}</>;
}
