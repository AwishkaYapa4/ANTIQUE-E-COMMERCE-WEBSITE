import { Suspense, useEffect } from 'react';
import { Outlet, ScrollRestoration, useLocation } from 'react-router';
import { ErrorBoundary } from '@/components/ui/Misc';
import { PageSpinner } from '@/components/ui/Feedback';
import { QuickViewModal } from '@/features/customer/product/QuickView';
import { AnnouncementBar, Header, PreviewBanner } from './Header';
import { Footer } from './Footer';
import { CartDrawer } from './CartDrawer';

export function CustomerLayout() {
  const { pathname } = useLocation();
  useEffect(() => {
    document.getElementById('main')?.focus({ preventScroll: true });
  }, [pathname]);
  return (
    <div className="flex min-h-screen flex-col">
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-[60] focus:bg-paper focus:px-4 focus:py-2">Skip to content</a>
      <PreviewBanner />
      <AnnouncementBar />
      <Header />
      <main id="main" tabIndex={-1} className="flex-1 outline-none">
        <ErrorBoundary key={pathname}>
          <Suspense fallback={<PageSpinner />}>
            <Outlet />
          </Suspense>
        </ErrorBoundary>
      </main>
      <Footer />
      <CartDrawer />
      <QuickViewModal />
      <ScrollRestoration />
    </div>
  );
}
