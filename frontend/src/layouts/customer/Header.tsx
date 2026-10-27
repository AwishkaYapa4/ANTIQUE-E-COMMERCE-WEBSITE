import { useEffect, useState, type FormEvent } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router';
import { ChevronDown, Heart, Menu, Search, ShoppingBag, User, X } from 'lucide-react';
import { useSiteConfig } from '@/features/site/SiteConfigProvider';
import { useAuth } from '@/features/auth/AuthProvider';
import { useCart } from '@/features/cart/useCart';
import { useWishlist } from '@/features/wishlist/useWishlist';
import { useUiStore } from '@/store/uiStore';
import { SmartLink } from '@/components/SmartLink';
import { Drawer } from '@/components/ui/Overlay';
import { Input } from '@/components/ui/Field';
import { Button } from '@/components/ui/Button';
import { cn } from '@/utils/format';

export function AnnouncementBar() {
  const { brand } = useSiteConfig();
  if (!brand.announcementEnabled || !brand.announcementText) return null;
  const text = <span className="tracking-wide">{brand.announcementText}</span>;
  return (
    <div className="bg-primary px-4 py-2 text-center text-xs text-button-ink sm:text-[0.8rem]">
      {brand.announcementLink ? <SmartLink href={brand.announcementLink} className="hover:underline">{text}</SmartLink> : text}
    </div>
  );
}

export function Logo({ className }: { className?: string }) {
  const { brand } = useSiteConfig();
  return (
    <Link to="/" className={cn('flex min-w-0 items-center gap-2', className)} aria-label={`${brand.storeName} home`}>
      {brand.logoUrl ? (
        <img src={brand.logoUrl} alt={brand.storeName} className="h-8 w-auto max-w-[140px] object-contain sm:h-11 sm:max-w-[180px]" />
      ) : (
        <span className="truncate font-heading text-[1.2rem] leading-none font-semibold tracking-tight text-primary sm:text-[1.7rem]">{brand.storeName}</span>
      )}
    </Link>
  );
}

function SearchForm({ onDone, autoFocus }: { onDone?: () => void; autoFocus?: boolean }) {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (!q.trim()) return;
    navigate(`/shop?q=${encodeURIComponent(q.trim())}`);
    setQ('');
    onDone?.();
  };
  return (
    <form role="search" onSubmit={submit} className="relative w-full">
      <label htmlFor="site-search" className="sr-only">Search antiques</label>
      <Input id="site-search" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Search clocks, teak, Victorianâ€¦" className="pr-10" autoFocus={autoFocus} />
      <button type="submit" className="absolute top-1/2 right-3 -translate-y-1/2 text-muted hover:text-ink" aria-label="Search">
        <Search className="h-4 w-4" />
      </button>
    </form>
  );
}

export function Header() {
  const { menu } = useSiteConfig();
  const { isAuthenticated, isAdmin } = useAuth();
  const { count } = useCart();
  const wishlist = useWishlist();
  const setCartOpen = useUiStore((s) => s.setCartOpen);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const location = useLocation();
  const items = menu('main').items;

  useEffect(() => {
    setMobileOpen(false);
    setSearchOpen(false);
  }, [location.pathname, location.search]);

  const iconBtn = 'relative inline-flex h-9 w-9 shrink-0 sm:h-10 sm:w-10 items-center justify-center rounded-theme text-ink transition-colors hover:bg-surface';

  return (
    <header className="sticky top-0 z-40 border-b border-line bg-bg/95 backdrop-blur supports-[backdrop-filter]:bg-bg/85">
      <div className="container-page flex h-16 items-center gap-2 sm:h-20 sm:gap-3">
        <button className={cn(iconBtn, 'lg:hidden')} onClick={() => setMobileOpen(true)} aria-label="Open menu">
          <Menu className="h-5 w-5" />
        </button>
        <Logo />

        <nav className="ml-8 hidden flex-1 items-center gap-1 lg:flex" aria-label="Main">
          {items.map((item) =>
            item.children?.length ? (
              <div key={item.label} className="group relative">
                <SmartLink href={item.href} className="inline-flex items-center gap-1 px-3 py-2 text-sm tracking-wide text-ink hover:text-accent">
                  {item.label}
                  <ChevronDown className="h-3.5 w-3.5" aria-hidden />
                </SmartLink>
                <div className="invisible absolute top-full left-0 z-50 min-w-52 rounded-theme border border-line bg-paper py-2 opacity-0 shadow-lg transition-all group-focus-within:visible group-focus-within:opacity-100 group-hover:visible group-hover:opacity-100">
                  {item.children.map((child) => (
                    <SmartLink key={child.label} href={child.href} className="block px-4 py-2 text-sm hover:bg-surface">{child.label}</SmartLink>
                  ))}
                </div>
              </div>
            ) : item.href.startsWith('/') && !item.href.includes('?') ? (
              <NavLink
                key={item.label}
                to={item.href}
                end={item.href === '/'}
                className={({ isActive }) => cn('px-3 py-2 text-sm tracking-wide transition-colors hover:text-accent', isActive ? 'text-accent' : 'text-ink')}
              >
                {item.label}
              </NavLink>
            ) : (
              <SmartLink key={item.label} href={item.href} className="px-3 py-2 text-sm tracking-wide text-ink hover:text-accent">{item.label}</SmartLink>
            ),
          )}
        </nav>

        <div className="ml-auto flex shrink-0 items-center gap-0.5">
          <span className="mr-2 hidden rounded-theme border border-line px-2 py-1 text-[0.7rem] font-medium tracking-wider text-muted sm:inline" title="Prices shown in Sri Lankan Rupees">
            LKR
          </span>
          <button className={iconBtn} onClick={() => setSearchOpen((o) => !o)} aria-label="Search" aria-expanded={searchOpen}>
            {searchOpen ? <X className="h-5 w-5" /> : <Search className="h-5 w-5" />}
          </button>
          <Link to={isAuthenticated ? (isAdmin ? '/account' : '/account') : '/login'} className={cn(iconBtn, 'hidden sm:inline-flex')} aria-label="Account">
            <User className="h-5 w-5" />
          </Link>
          <Link to="/wishlist" className={iconBtn} aria-label={`Wishlist, ${wishlist.count} items`}>
            <Heart className="h-5 w-5" />
            {wishlist.count > 0 && <span className="absolute top-1 right-1 h-2 w-2 rounded-full bg-accent" aria-hidden />}
          </Link>
          <button className={iconBtn} onClick={() => setCartOpen(true)} aria-label={`Cart, ${count} items`}>
            <ShoppingBag className="h-5 w-5" />
            {count > 0 && (
              <span className="absolute -top-0.5 -right-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-primary px-1 text-[0.65rem] font-semibold text-button-ink">
                {count}
              </span>
            )}
          </button>
        </div>
      </div>

      {searchOpen && (
        <div className="border-t border-line bg-bg">
          <div className="container-page py-4">
            <div className="mx-auto max-w-xl"><SearchForm autoFocus onDone={() => setSearchOpen(false)} /></div>
          </div>
        </div>
      )}

      <Drawer open={mobileOpen} onClose={() => setMobileOpen(false)} title="Menu" side="left">
        <div className="mb-6"><SearchForm onDone={() => setMobileOpen(false)} /></div>
        <nav className="flex flex-col" aria-label="Mobile">
          {items.map((item) => (
            <div key={item.label} className="border-b border-line">
              <SmartLink href={item.href} className="block py-3.5 font-heading text-2xl" onClick={() => setMobileOpen(false)}>{item.label}</SmartLink>
              {item.children?.map((child) => (
                <SmartLink key={child.label} href={child.href} className="block py-2 pl-4 text-sm text-muted" onClick={() => setMobileOpen(false)}>
                  {child.label}
                </SmartLink>
              ))}
            </div>
          ))}
        </nav>
        <div className="mt-8 flex flex-col gap-2">
          <Link to={isAuthenticated ? '/account' : '/login'} className="flex items-center gap-2 py-2 text-sm"><User className="h-4 w-4" /> {isAuthenticated ? 'My account' : 'Sign in / Register'}</Link>
          <Link to="/wishlist" className="flex items-center gap-2 py-2 text-sm"><Heart className="h-4 w-4" /> Wishlist</Link>
          {isAdmin && <Link to="/admin" className="flex items-center gap-2 py-2 text-sm text-accent">Store admin</Link>}
          <p className="mt-4 text-xs text-muted">Prices in Sri Lankan Rupees (LKR)</p>
        </div>
      </Drawer>
    </header>
  );
}

export function PreviewBanner() {
  const { preview, exitPreview } = useSiteConfig();
  if (!preview) return null;
  return (
    <div className="sticky top-0 z-50 flex flex-wrap items-center justify-center gap-3 bg-accent px-4 py-2 text-sm text-paper">
      <span>You are previewing unpublished changes. Customers still see the published site.</span>
      <Button size="sm" variant="light" onClick={() => { exitPreview(); window.location.reload(); }}>Exit preview</Button>
      <Link to="/admin/customize" className="underline">Back to editor</Link>
    </div>
  );
}
