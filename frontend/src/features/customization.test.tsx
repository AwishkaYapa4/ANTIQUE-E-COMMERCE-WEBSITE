import { screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders, card } from '@/test/utils';
import { authState, siteState } from '@/test/mocks';
import { applyTheme, fallbackConfig } from '@/features/site/SiteConfigProvider';
import { SectionRenderer } from '@/features/customer/home/sections';
import type { RenderedSection } from '@/types/api';

vi.mock('@/features/auth/AuthProvider', () => ({ useAuth: () => authState }));
vi.mock('@/features/site/SiteConfigProvider', async (orig) => ({ ...(await orig<object>()), useSiteConfig: () => siteState }));

const section = (type: RenderedSection['type'], config: Record<string, unknown>, extra: Partial<RenderedSection> = {}): RenderedSection =>
  ({ key: type, type, config, products: null, categories: null, testimonials: null, ...extra });

describe('Theme settings', () => {
  it('applies admin colours, fonts and sizes as CSS variables', () => {
    applyTheme({ ...fallbackConfig.theme, accentColor: '#123456', headingFont: 'Playfair Display', baseFontSize: 18, borderRadius: 8 });
    const style = document.documentElement.style;
    expect(style.getPropertyValue('--c-accent')).toBe('#123456');
    expect(style.getPropertyValue('--font-heading-family')).toContain('Playfair Display');
    expect(style.getPropertyValue('--base-size')).toBe('18px');
    expect(style.getPropertyValue('--radius')).toBe('8px');
  });
});

describe('Homepage sections render from saved configuration', () => {
  it('renders a hero with the configured headline and buttons', () => {
    renderWithProviders(<SectionRenderer section={section('hero', {
      imageUrl: null, title: 'Spring Collection Arrives', subtitle: 'Fresh finds', eyebrow: null,
      primaryCta: { label: 'Shop now', href: '/shop' }, secondaryCta: null, alignment: 'center', height: 'medium', overlayOpacity: 20,
    })} />);
    expect(screen.getByRole('heading', { level: 1, name: 'Spring Collection Arrives' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Shop now' })).toHaveAttribute('href', '/shop');
  });

  it('renders a product section with its products', () => {
    renderWithProviders(<SectionRenderer section={section('featuredProducts', { title: 'Featured Pieces', subtitle: null, productIds: null, limit: 8, cta: null }, { products: [card()] })} />);
    expect(screen.getByRole('heading', { name: 'Featured Pieces' })).toBeInTheDocument();
    expect(screen.getByText('Victorian Walnut Mantel Clock')).toBeInTheDocument();
  });

  it('hides testimonials entirely when there are none (no placeholder reviews)', () => {
    const { container } = renderWithProviders(<SectionRenderer section={section('testimonials', { title: 'Kind Words', subtitle: null, limit: 6 }, { testimonials: [] })} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('sanitises rich text sections in the browser as well', () => {
    renderWithProviders(<SectionRenderer section={section('richText', { title: 'Note', html: '<p>Hello</p><img src=x onerror="alert(1)"><script>bad()</script>', width: 'narrow' })} />);
    expect(screen.getByText('Hello')).toBeInTheDocument();
    expect(document.querySelector('.prose-antique script')).toBeNull();
    expect(document.querySelector('.prose-antique img')?.getAttribute('onerror')).toBeNull();
  });

  it('ignores unknown section types', () => {
    const { container } = renderWithProviders(<SectionRenderer section={section('mystery' as never, {})} />);
    expect(container).toBeEmptyDOMElement();
  });
});
