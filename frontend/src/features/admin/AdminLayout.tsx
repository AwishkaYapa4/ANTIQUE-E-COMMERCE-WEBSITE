import { Suspense, useState, type ReactNode } from 'react';
import { Link, NavLink, Outlet, useLocation } from 'react-router';
import {
  BarChart3, Boxes, ExternalLink, FileText, FolderTree, Image, LayoutDashboard, LayoutTemplate, Layers, Menu, MessageSquare,
  Paintbrush, Package, ScrollText, Settings, ShoppingCart, Star, Tag, User, Users,
} from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/features/auth/AuthProvider';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import { adminApi } from '@/services/adminApi';
import { Drawer } from '@/components/ui/Overlay';
import { ErrorBoundary } from '@/components/ui/Misc';
import { PageSpinner } from '@/components/ui/Feedback';
import { cn } from '@/utils/format';

type Item = { to: string; label: string; icon: typeof LayoutDashboard; end?: boolean; badge?: number };

function useNavGroups(): { title: string; items: Item[] }[] {
  const dashboard = useQuery({ queryKey: ['admin', 'dashboard'], queryFn: adminApi.dashboard, staleTime: 60_000 });
  const d = dashboard.data;
  return [
    { title: 'Overview', items: [{ to: '/admin', label: 'Dashboard', icon: LayoutDashboard, end: true }, { to: '/admin/reports', label: 'Reports', icon: BarChart3 }] },
    {
      title: 'Catalog',
      items: [
        { to: '/admin/products', label: 'Products', icon: Package },
        { to: '/admin/categories', label: 'Categories', icon: FolderTree },
        { to: '/admin/collections', label: 'Collections', icon: Layers },
        { to: '/admin/inventory', label: 'Inventory', icon: Boxes, badge: d?.activeReservations || undefined },
        { to: '/admin/media', label: 'Media Library', icon: Image },
      ],
    },
    {
      title: 'Sales',
      items: [
        { to: '/admin/orders', label: 'Orders', icon: ShoppingCart, badge: (d?.pendingOrders ?? 0) + (d?.awaitingVerification ?? 0) || undefined },
        { to: '/admin/customers', label: 'Customers', icon: Users },
        { to: '/admin/promotions', label: 'Promotions', icon: Tag },
      ],
    },
    {
      title: 'Engagement',
      items: [
        { to: '/admin/enquiries', label: 'Enquiries', icon: MessageSquare, badge: d?.newEnquiries || undefined },
        { to: '/admin/reviews', label: 'Reviews', icon: Star, badge: d?.pendingReviews || undefined },
      ],
    },
    {
      title: 'Website',
      items: [
        { to: '/admin/customize', label: 'Website Customization', icon: Paintbrush },
        { to: '/admin/homepage', label: 'Homepage Builder', icon: LayoutTemplate },
        { to: '/admin/pages', label: 'Content Pages', icon: FileText },
      ],
    },
    {
      title: 'Settings',
      items: [
        { to: '/admin/settings', label: 'Store Settings', icon: Settings },
        { to: '/admin/audit', label: 'Audit Log', icon: ScrollText },
        { to: '/admin/profile', label: 'Admin Profile', icon: User },
      ],
    },
  ];
}

function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const groups = useNavGroups();
  return (
    <nav aria-label="Admin" className="flex flex-col gap-6">
      {groups.map((g) => (
        <div key={g.title}>
          <p className="mb-1.5 px-3 text-[0.68rem] font-semibold tracking-[0.18em] text-muted uppercase">{g.title}</p>
          <ul className="space-y-0.5">
            {g.items.map((item) => (
              <li key={item.to}>
                <NavLink to={item.to} end={item.end} onClick={onNavigate}
                  className={({ isActive }) => cn('flex items-center gap-3 rounded-theme px-3 py-2 text-sm transition-colors', isActive ? 'bg-primary text-button-ink' : 'text-ink hover:bg-surface')}>
                  <item.icon className="h-4 w-4 shrink-0" aria-hidden />
                  <span className="flex-1">{item.label}</span>
                  {item.badge ? <span className="rounded-full bg-accent px-1.5 text-[0.65rem] font-semibold text-paper">{item.badge}</span> : null}
                </NavLink>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </nav>
  );
}

export function AdminLayout({ children }: { children?: ReactNode }) {
  const [open, setOpen] = useState(false);
  const { brand } = useSiteConfig();
  const { me } = useAuth();
  const { pathname } = useLocation();
  return (
    <div className="min-h-screen bg-bg lg:grid lg:grid-cols-[260px_1fr]">
      <aside className="sticky top-0 hidden h-screen overflow-y-auto border-r border-line bg-paper px-3 py-5 lg:block">
        <Link to="/admin" className="mb-6 block px-3 font-heading text-2xl text-primary">{brand.storeName}<span className="block font-body text-[0.7rem] tracking-[0.2em] text-muted uppercase">Admin</span></Link>
        <Sidebar />
      </aside>
      <div className="min-w-0">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-line bg-paper/95 px-4 backdrop-blur sm:px-8">
          <button className="inline-flex h-9 w-9 items-center justify-center rounded-theme hover:bg-surface lg:hidden" onClick={() => setOpen(true)} aria-label="Open admin menu"><Menu className="h-5 w-5" /></button>
          <span className="font-heading text-xl lg:hidden">Admin</span>
          <div className="ml-auto flex items-center gap-4 text-sm">
            <Link to="/" target="_blank" className="inline-flex items-center gap-1 text-muted hover:text-ink">View store <ExternalLink className="h-3.5 w-3.5" aria-hidden /></Link>
            <span className="hidden text-muted sm:inline">{me?.email}</span>
          </div>
        </header>
        <main>
          <ErrorBoundary key={pathname}>
            <Suspense fallback={<PageSpinner />}>{children ?? <Outlet />}</Suspense>
          </ErrorBoundary>
        </main>
      </div>
      <Drawer open={open} onClose={() => setOpen(false)} title="Admin" side="left"><Sidebar onNavigate={() => setOpen(false)} /></Drawer>
    </div>
  );
}
