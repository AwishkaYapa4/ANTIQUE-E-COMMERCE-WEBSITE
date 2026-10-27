import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'sonner';
import { ApiError } from '@/services/http';
import { AuthProvider } from '@/features/auth/AuthProvider';
import { SiteConfigProvider } from '@/features/site/SiteConfigProvider';
import { ConfirmProvider } from '@/components/ui/Overlay';

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // Don't retry client errors (404/403/422…); retry transient failures twice.
        retry: (count, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && count < 2,
      },
    },
  });
}

export function AppProviders({ children, queryClient }: { children: ReactNode; queryClient: QueryClient }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <SiteConfigProvider>
          <ConfirmProvider>
            {children}
            <Toaster position="bottom-right" toastOptions={{ classNames: { toast: 'font-body' } }} richColors closeButton />
          </ConfirmProvider>
        </SiteConfigProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
