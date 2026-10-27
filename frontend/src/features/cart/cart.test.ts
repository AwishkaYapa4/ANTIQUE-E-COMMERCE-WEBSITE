import { beforeEach, describe, expect, it } from 'vitest';
import { useGuestStore } from '@/store/guestStore';

const line = { productId: 7, quantity: 1, name: 'Brass Lantern', slug: 'brass-lantern', price: 32500, image: null, maxPerOrder: 1 };

describe('Guest cart', () => {
  beforeEach(() => useGuestStore.setState({ cart: [], wishlist: [] }));

  it('adds items and prevents adding a one-of-a-kind piece twice', () => {
    expect(useGuestStore.getState().addToCart(line).ok).toBe(true);
    const second = useGuestStore.getState().addToCart(line);
    expect(second.ok).toBe(false);
    expect(second.message).toMatch(/already in your cart/i);
    expect(useGuestStore.getState().cart).toHaveLength(1);
  });

  it('clamps quantity to the per-order maximum and removes lines', () => {
    useGuestStore.getState().addToCart({ ...line, productId: 8, maxPerOrder: 3 });
    useGuestStore.getState().setQuantity(8, 10);
    expect(useGuestStore.getState().cart[0].quantity).toBe(3);
    useGuestStore.getState().setQuantity(8, 0);
    expect(useGuestStore.getState().cart[0].quantity).toBe(1);
    useGuestStore.getState().removeFromCart(8);
    expect(useGuestStore.getState().cart).toHaveLength(0);
  });

  it('persists to localStorage so the cart survives a reload', () => {
    useGuestStore.getState().addToCart(line);
    expect(localStorage.getItem('antique-guest')).toContain('Brass Lantern');
  });

  it('toggles wishlist entries', () => {
    expect(useGuestStore.getState().toggleWishlist(5)).toBe(true);
    expect(useGuestStore.getState().wishlist).toEqual([5]);
    expect(useGuestStore.getState().toggleWishlist(5)).toBe(false);
    expect(useGuestStore.getState().wishlist).toEqual([]);
  });
});
