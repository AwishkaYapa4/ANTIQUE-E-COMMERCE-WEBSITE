import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { card, renderWithProviders } from '@/test/utils';
import { authState, siteState } from '@/test/mocks';
import { ShopView } from './ShopView';

vi.mock('@/features/auth/AuthProvider', () => ({ useAuth: () => authState }));
vi.mock('@/features/site/SiteConfigProvider', async (orig) => ({ ...(await orig<object>()), useSiteConfig: () => siteState }));

const products = vi.fn();
const facets = vi.fn();
vi.mock('@/services/api', async (orig) => ({
  ...(await orig<object>()),
  catalogApi: { products: (...a: unknown[]) => products(...a), facets: (...a: unknown[]) => facets(...a) },
}));

beforeEach(() => {
  localStorage.clear();
  products.mockReset();
  facets.mockReset();
  facets.mockResolvedValue({
    categories: [{ value: 'vintage-clocks', label: 'Vintage Clocks', count: 3 }],
    conditions: [{ value: 'Good', label: 'Good', count: 2 }],
    eras: [{ value: 'Victorian', label: 'Victorian', count: 2 }],
    materials: [{ value: 'Teak', label: 'Teak', count: 1 }],
    minPrice: 9500, maxPrice: 185000,
  });
});

describe('Product listing', () => {
  it('renders products with price, category and a Sold badge', async () => {
    products.mockResolvedValue({ items: [card(), card({ id: 2, slug: 'sold-chair', name: 'Teak Chair', inventoryState: 'Sold' })], page: 1, pageSize: 24, totalCount: 2, totalPages: 1 });
    renderWithProviders(<ShopView header={<h1>Shop</h1>} />, { route: '/shop' });

    expect(await screen.findByText('Victorian Walnut Mantel Clock')).toBeInTheDocument();
    expect(screen.getByText('2 pieces')).toBeInTheDocument();
    const soldCard = screen.getByText('Teak Chair').closest('article')!;
    expect(within(soldCard).getByText('Sold')).toBeInTheDocument();
    expect(within(soldCard).queryByRole('button', { name: /add to cart/i })).not.toBeInTheDocument();
  });

  it('shows a helpful empty state when nothing matches', async () => {
    products.mockResolvedValue({ items: [], page: 1, pageSize: 24, totalCount: 0, totalPages: 0 });
    renderWithProviders(<ShopView header={null} />, { route: '/shop?era=Victorian' });
    expect(await screen.findByText(/no pieces match these filters/i)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /clear/i }).length).toBeGreaterThan(0);
  });

  it('applies filters to the API query and keeps them in the URL', async () => {
    products.mockResolvedValue({ items: [card()], page: 1, pageSize: 24, totalCount: 1, totalPages: 1 });
    const user = userEvent.setup();
    const { router } = renderWithProviders(<ShopView header={null} />, { route: '/shop' });

    const aside = await screen.findByRole('complementary', { name: 'Filters' });
    await user.click(await within(aside).findByRole('checkbox', { name: /victorian/i }));

    await waitFor(() => expect(router.state.location.search).toContain('era=Victorian'));
    await waitFor(() => expect(products).toHaveBeenLastCalledWith(expect.objectContaining({ era: ['Victorian'], page: 1 })));
    expect(screen.getByRole('button', { name: /remove filter victorian/i })).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Sort'), 'PriceAsc');
    await waitFor(() => expect(router.state.location.search).toContain('sort=PriceAsc'));
    await waitFor(() => expect(products).toHaveBeenLastCalledWith(expect.objectContaining({ sort: 'PriceAsc', era: ['Victorian'] })));
  });

  it('reads initial filters from the URL', async () => {
    products.mockResolvedValue({ items: [], page: 1, pageSize: 24, totalCount: 0, totalPages: 0 });
    renderWithProviders(<ShopView header={null} />, { route: '/shop?minPrice=10000&maxPrice=50000&availability=Available&condition=Good&page=2' });
    await waitFor(() => expect(products).toHaveBeenCalledWith(expect.objectContaining({ minPrice: 10000, maxPrice: 50000, availability: 'Available', condition: ['Good'], page: 2 })));
  });
});
