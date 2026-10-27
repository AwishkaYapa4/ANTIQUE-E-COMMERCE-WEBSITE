import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import type { ImageDto } from '@/types/api';

/**
 * Browser-only cart and wishlist for visitors who are not signed in. The snapshot fields are for display
 * only; prices and availability are always re-validated by the server when the cart is merged at sign-in.
 */
export interface GuestCartLine {
  productId: number;
  quantity: number;
  name: string;
  slug: string;
  price: number;
  image: ImageDto | null;
  maxPerOrder: number;
}

interface GuestState {
  cart: GuestCartLine[];
  wishlist: number[];
  addToCart: (line: GuestCartLine) => { ok: boolean; message?: string };
  setQuantity: (productId: number, quantity: number) => void;
  removeFromCart: (productId: number) => void;
  clearCart: () => void;
  toggleWishlist: (productId: number) => boolean;
  clearWishlist: () => void;
}

export const useGuestStore = create<GuestState>()(
  persist(
    (set, get) => ({
      cart: [],
      wishlist: [],
      addToCart: (line) => {
        const existing = get().cart.find((l) => l.productId === line.productId);
        if (existing) {
          if (existing.quantity + line.quantity > line.maxPerOrder) {
            return { ok: false, message: line.maxPerOrder === 1 ? 'This one-of-a-kind piece is already in your cart.' : `You can buy at most ${line.maxPerOrder}.` };
          }
          set({ cart: get().cart.map((l) => (l.productId === line.productId ? { ...l, quantity: l.quantity + line.quantity } : l)) });
        } else {
          set({ cart: [...get().cart, line].slice(0, 50) });
        }
        return { ok: true };
      },
      setQuantity: (productId, quantity) =>
        set({ cart: get().cart.map((l) => (l.productId === productId ? { ...l, quantity: Math.max(1, Math.min(quantity, l.maxPerOrder)) } : l)) }),
      removeFromCart: (productId) => set({ cart: get().cart.filter((l) => l.productId !== productId) }),
      clearCart: () => set({ cart: [] }),
      toggleWishlist: (productId) => {
        const has = get().wishlist.includes(productId);
        set({ wishlist: has ? get().wishlist.filter((id) => id !== productId) : [...get().wishlist, productId].slice(0, 100) });
        return !has;
      },
      clearWishlist: () => set({ wishlist: [] }),
    }),
    { name: 'antique-guest', storage: createJSONStorage(() => localStorage), version: 1 },
  ),
);
