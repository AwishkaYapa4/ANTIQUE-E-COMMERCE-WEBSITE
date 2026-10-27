import { StrictMode, Suspense } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router';
import '@/styles/index.css';
import { AppProviders, createQueryClient } from '@/app/providers';
import { router } from '@/app/router';
import { ErrorBoundary } from '@/components/ui/Misc';
import { PageSpinner } from '@/components/ui/Feedback';

const queryClient = createQueryClient();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <AppProviders queryClient={queryClient}>
        <Suspense fallback={<PageSpinner />}>
          <RouterProvider router={router} />
        </Suspense>
      </AppProviders>
    </ErrorBoundary>
  </StrictMode>,
);
