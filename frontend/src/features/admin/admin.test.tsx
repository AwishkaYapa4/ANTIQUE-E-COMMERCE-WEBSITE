import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '@/test/utils';
import { authState, siteState } from '@/test/mocks';
import type { CommerceSettings } from '@/types/api';
import { ProductEditorPage } from './pages/ProductPages';
import { StoreSettingsPage } from './pages/SettingsPages';

vi.mock('@/features/auth/AuthProvider', () => ({ useAuth: () => ({ ...authState, isAuthenticated: true, isAdmin: true }) }));
vi.mock('@/features/site/SiteConfigProvider', async (orig) => ({ ...(await orig<object>()), useSiteConfig: () => siteState }));

const api = vi.hoisted(() => ({
  categories: vi.fn(), collections: vi.fn(), createProduct: vi.fn(), settings: vi.fn(), saveSettings: vi.fn(), zones: vi.fn(),
}));
vi.mock('@/services/adminApi', () => ({ adminApi: api }));

const settings: CommerceSettings = {
  orderNumberPrefix: 'AH', reservationMinutes: 15, bankTransferPaymentWindowHours: 48, freeShippingThreshold: null, localPickupEnabled: false,
  localPickupAddress: null, localPickupInstructions: null, deliveryInstructions: null, cashOnDeliveryEnabled: true, cashOnDeliveryMaxOrderValue: null,
  bankTransferEnabled: false, bankName: null, bankAccountName: null, bankAccountNumber: null, bankBranch: null, bankTransferInstructions: null,
  reviewsEnabled: true, returnsSummary: null, currency: 'LKR',
};

beforeEach(() => {
  Object.values(api).forEach((f) => f.mockReset());
  api.categories.mockResolvedValue([{ id: 3, name: 'Antique Furniture', slug: 'antique-furniture', parentId: null }]);
  api.collections.mockResolvedValue([{ id: 9, name: 'Rare Finds', slug: 'rare-finds' }]);
  api.zones.mockResolvedValue([]);
});

describe('Admin product creation', () => {
  it('validates required fields before calling the API', async () => {
    const user = userEvent.setup();
    renderWithProviders(<ProductEditorPage />, { route: '/admin/products/new', path: '/admin/products/new' });
    await user.click(await screen.findByRole('button', { name: 'Create product' }));
    expect(await screen.findByText('Product name is required.')).toBeInTheDocument();
    expect(screen.getByText('SKU is required.')).toBeInTheDocument();
    expect(api.createProduct).not.toHaveBeenCalled();
  });

  it('submits a new antique with category, collection and condition report', async () => {
    api.createProduct.mockResolvedValue({ id: 42, name: 'Teak Chair' });
    const user = userEvent.setup();
    renderWithProviders(<ProductEditorPage />, { route: '/admin/products/new', path: '/admin/products/new' });

    await user.type(await screen.findByLabelText(/product name/i), 'Colonial Teak Chair');
    await user.type(screen.getByLabelText(/^sku/i), 'FUR-0101');
    await user.type(screen.getByLabelText(/^price/i), '185000');
    await user.type(screen.getByLabelText(/signs of wear/i), 'Softened edges on the arms');
    await user.selectOptions(await screen.findByLabelText('Category'), '3');
    await user.click(await screen.findByRole('checkbox', { name: 'Rare Finds' }));
    await user.click(screen.getByRole('button', { name: 'Create product' }));

    await waitFor(() => expect(api.createProduct).toHaveBeenCalledTimes(1));
    expect(api.createProduct).toHaveBeenCalledWith(expect.objectContaining({
      name: 'Colonial Teak Chair', sku: 'FUR-0101', price: 185000, categoryId: 3, collectionIds: [9], signsOfWear: 'Softened edges on the arms',
      status: 'Draft', compareAtPrice: null, initialQuantity: 1, images: [],
    }));
  });
});

describe('Admin store settings', () => {
  it('saves operational settings including enabling local pickup', async () => {
    api.settings.mockResolvedValue(settings);
    api.saveSettings.mockImplementation(async (s: CommerceSettings) => s);
    const user = userEvent.setup();
    renderWithProviders(<StoreSettingsPage />, { route: '/admin/settings' });

    await user.click(await screen.findByRole('checkbox', { name: /offer local pickup/i }));
    await user.type(await screen.findByLabelText(/pickup address/i), '12 Temple Road, Kandy');
    const threshold = screen.getByLabelText(/free delivery threshold/i);
    await user.type(threshold, '100000');
    await user.click(screen.getByRole('button', { name: 'Save settings' }));

    await waitFor(() => expect(api.saveSettings).toHaveBeenCalled());
    expect(api.saveSettings.mock.calls[0][0]).toEqual(expect.objectContaining({
      localPickupEnabled: true, localPickupAddress: '12 Temple Road, Kandy', freeShippingThreshold: 100000, reservationMinutes: 15,
    }));
  });
});
