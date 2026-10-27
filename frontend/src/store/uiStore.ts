import { create } from 'zustand';

interface UiState {
  cartOpen: boolean;
  quickViewSlug: string | null;
  setCartOpen: (open: boolean) => void;
  openQuickView: (slug: string | null) => void;
}

export const useUiStore = create<UiState>()((set) => ({
  cartOpen: false,
  quickViewSlug: null,
  setCartOpen: (cartOpen) => set({ cartOpen }),
  openQuickView: (quickViewSlug) => set({ quickViewSlug }),
}));
