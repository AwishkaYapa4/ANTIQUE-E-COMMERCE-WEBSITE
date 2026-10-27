import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { detail, renderWithProviders } from '@/test/utils';
import { authState, siteState } from '@/test/mocks';
import { useGuestStore } from '@/store/guestStore';
import ProductPage from './ProductPage';

vi.mock('@/features/auth/AuthProvider', () => ({ useAuth: () => authState }));
vi.mock('@/features/site/SiteConfigProvider', async (orig) => ({ ...(await orig<object>()), useSiteConfig: () => siteState }));

const product = vi.fn();
vi.mock('@/services/api', async (orig) => ({
  ...(await orig<object>()),
  catalogApi: { product: (...a: unknown[]) => product(...a), related: vi.fn().mockResolvedValue([]), reviews: vi.fn().mockResolvedValue([]) },
}));

beforeEach(() => {
  product.mockReset();
  useGuestStore.setState({ cart: [], wishlist: [] });
});

describe('Product details', () => {
  it('shows price, condition report and sanitised description', async () => {
    product.mockResolvedValue(detail());
    renderWithProviders(<ProductPage />, { route: '/products/victorian-clock', path: '/products/:slug' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Victorian Walnut Mantel Clock' })).toBeInTheDocument();
    expect(screen.getByText('Condition report')).toBeInTheDocument();
    expect(screen.getByText('Minor wear to the base')).toBeInTheDocument();
    expect(screen.getAllByText('None noted').length).toBeGreaterThan(0);
    expect(screen.getByText('Eight-day movement.')).toBeInTheDocument();
    expect(document.querySelector('.prose-antique script')).toBeNull();
    expect(screen.getByText('EXAMPLE-002')).toBeInTheDocument();
  });

  it('never claims documented provenance without stored evidence', async () => {
    product.mockResolvedValue(detail());
    const user = userEvent.setup();
    renderWithProviders(<ProductPage />, { route: '/products/victorian-clock', path: '/products/:slug' });
    await user.click(await screen.findByRole('button', { name: /item history/i }));
    expect(screen.getByText(/we do not hold documentation verifying it/i)).toBeInTheDocument();
    expect(screen.queryByText(/documentation held/i)).not.toBeInTheDocument();
  });

  it('disables purchasing for sold items', async () => {
    product.mockResolvedValue(detail({ inventoryState: 'Sold' }));
    renderWithProviders(<ProductPage />, { route: '/products/victorian-clock', path: '/products/:slug' });
    expect(await screen.findByText(/found a new home/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add to cart/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /buy now/i })).not.toBeInTheDocument();
  });

  it('adds an available piece to the guest cart only once', async () => {
    product.mockResolvedValue(detail());
    const user = userEvent.setup();
    renderWithProviders(<ProductPage />, { route: '/products/victorian-clock', path: '/products/:slug' });

    await user.click(await screen.findByRole('button', { name: /add to cart/i }));
    expect(useGuestStore.getState().cart).toHaveLength(1);
    expect(await screen.findByRole('button', { name: /in your cart/i })).toBeDisabled();
  });
});
