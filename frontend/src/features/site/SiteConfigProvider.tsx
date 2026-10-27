import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { siteApi } from '@/services/api';
import { useAuth } from '@/features/auth/AuthProvider';
import type { SiteConfig, Theme } from '@/types/api';

/** Used only until the API responds (or if it is unreachable), so the site never renders unstyled. */
export const fallbackConfig: SiteConfig = {
  brand: {
    storeName: 'Antique Heritage Store', tagline: 'Timeless pieces. Beautiful stories.', description: null, logoUrl: null, faviconUrl: null,
    contactEmail: null, contactPhone: null, whatsAppNumber: null, address: null, openingHours: null, mapEmbedUrl: null,
    socialLinks: [], announcementEnabled: false, announcementText: null, announcementLink: null, footerDescription: null,
    copyrightText: null, newsletterEnabled: false, seoDefaultTitle: null, seoDefaultDescription: null, defaultOgImageUrl: null,
  },
  theme: {
    backgroundColor: '#F8F5EF', surfaceColor: '#F0E8DC', textColor: '#29231E', mutedTextColor: '#77716B', primaryColor: '#45382B',
    accentColor: '#B08D57', secondaryColor: '#777660', borderColor: '#E5DED3', buttonColor: '#45382B', buttonTextColor: '#FFFFFF',
    successColor: '#47745B', errorColor: '#B84D4D', headingFont: 'Cormorant Garamond', bodyFont: 'Inter', baseFontSize: 16,
    headingScale: 125, borderRadius: 4,
  },
  navigation: [
    { location: 'main', title: 'Main', items: [{ label: 'Home', href: '/' }, { label: 'Shop', href: '/shop' }, { label: 'Contact', href: '/contact' }] },
    { location: 'footer-shop', title: 'Shop', items: [] },
    { location: 'footer-help', title: 'Help', items: [] },
  ],
  commerce: {
    currency: 'LKR', cashOnDeliveryEnabled: true, bankTransferEnabled: true, localPickupEnabled: false, freeShippingThreshold: null,
    reservationMinutes: 15, reviewsEnabled: true, deliveryInstructions: null, returnsSummary: null,
  },
  isPreview: false,
};

const PREVIEW_KEY = 'antique-preview';

/** Preview is requested with ?preview=draft and remembered for the tab. Only admins can actually load drafts. */
function usePreviewFlag() {
  const [flag, setFlag] = useState(() => {
    if (typeof window === 'undefined') return false;
    const params = new URLSearchParams(window.location.search);
    if (params.get('preview') === 'draft') sessionStorage.setItem(PREVIEW_KEY, '1');
    if (params.get('preview') === 'off') sessionStorage.removeItem(PREVIEW_KEY);
    return sessionStorage.getItem(PREVIEW_KEY) === '1';
  });
  const exit = () => {
    sessionStorage.removeItem(PREVIEW_KEY);
    setFlag(false);
  };
  return { flag, exit };
}

interface SiteContextValue extends SiteConfig {
  isLoading: boolean;
  preview: boolean;
  exitPreview: () => void;
  navigation: SiteConfig['navigation'];
  menu: (location: 'main' | 'footer-shop' | 'footer-help') => SiteConfig['navigation'][number];
}

const SiteContext = createContext<SiteContextValue | null>(null);

const fontParam = (f: string) => f.replaceAll(' ', '+');

export function applyTheme(theme: Theme) {
  const root = document.documentElement.style;
  const set = (name: string, value: string) => root.setProperty(name, value);
  set('--c-bg', theme.backgroundColor);
  set('--c-surface', theme.surfaceColor);
  set('--c-ink', theme.textColor);
  set('--c-muted', theme.mutedTextColor);
  set('--c-primary', theme.primaryColor);
  set('--c-accent', theme.accentColor);
  set('--c-secondary', theme.secondaryColor);
  set('--c-line', theme.borderColor);
  set('--c-button', theme.buttonColor);
  set('--c-button-ink', theme.buttonTextColor);
  set('--c-success', theme.successColor);
  set('--c-danger', theme.errorColor);
  set('--font-heading-family', `'${theme.headingFont}', Georgia, serif`);
  set('--font-body-family', `'${theme.bodyFont}', system-ui, sans-serif`);
  set('--base-size', `${theme.baseFontSize}px`);
  set('--scale', String(theme.headingScale / 100));
  set('--radius', `${theme.borderRadius}px`);

  // Fonts come from an approved list validated by the API, so building this URL is safe.
  const href = `https://fonts.googleapis.com/css2?family=${fontParam(theme.headingFont)}:wght@400;500;600;700&family=${fontParam(theme.bodyFont)}:wght@400;500;600&display=swap`;
  const link = document.getElementById('theme-fonts') as HTMLLinkElement | null;
  if (link && link.href !== href) link.href = href;
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme.backgroundColor);
}

export function SiteConfigProvider({ children }: { children: ReactNode }) {
  const { isAdmin, initializing } = useAuth();
  const { flag, exit } = usePreviewFlag();
  const preview = flag && isAdmin;

  const query = useQuery({
    queryKey: ['site', preview ? 'draft' : 'published'],
    queryFn: () => siteApi.settings(preview),
    enabled: !flag || !initializing,
    staleTime: preview ? 0 : 5 * 60_000,
  });
  const config = query.data ?? fallbackConfig;

  useEffect(() => applyTheme(config.theme), [config.theme]);
  useEffect(() => {
    if (!config.brand.faviconUrl) return;
    let link = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (!link) {
      link = document.createElement('link');
      link.rel = 'icon';
      document.head.appendChild(link);
    }
    link.href = config.brand.faviconUrl;
  }, [config.brand.faviconUrl]);

  const value: SiteContextValue = {
    ...config,
    isLoading: query.isPending,
    preview,
    exitPreview: exit,
    menu: (location) => config.navigation.find((m) => m.location === location) ?? { location, title: '', items: [] },
  };
  return <SiteContext.Provider value={value}>{children}</SiteContext.Provider>;
}

export function useSiteConfig() {
  const ctx = useContext(SiteContext);
  if (!ctx) throw new Error('useSiteConfig must be used inside SiteConfigProvider');
  return ctx;
}
