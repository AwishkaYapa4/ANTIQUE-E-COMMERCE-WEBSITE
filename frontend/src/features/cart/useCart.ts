import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { cartApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import { useAuth } from '@/features/auth/AuthProvider';
import { useGuestStore } from '@/store/guestStore';
import { useUiStore } from '@/store/uiStore';
import type { Cart, ImageDto, InventoryState } from '@/types/api';

export interface CartLineView {
  key: number;
  productId: number;
  slug: string;
  name: string;
  image: ImageDto | null;
  unitPrice: number;
  quantity: number;
  maxQuantity: number;
  lineTotal: number;
  isPurchasable: boolean;
  issue: string | null;
  inventoryState: InventoryState | null;
}

export interface AddableProduct {
  id: number;
  slug: string;
  name: string;
  price: number;
  maxPerOrder: number;
  inventoryState: InventoryState;
  image?: ImageDto | null;
  images?: ImageDto[];
}

/**
 * One cart API for the UI. Signed-in shoppers use the server cart (prices/availability always fresh);
 * guests use a browser cart whose prices are re-validated on sign-in and checkout.
 */
export function useCart() {
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const guest = useGuestStore();
  const openCart = useUiStore((s) => s.setCartOpen);

  const serverCart = useQuery({ queryKey: ['cart'], queryFn: cartApi.get, enabled: isAuthenticated, staleTime: 30_000 });

  const onCart = (cart: Cart) => queryClient.setQueryData(['cart'], cart);
  const onError = (e: unknown) => toast.error(errorMessage(e));

  const addMutation = useMutation({ mutationFn: (v: { productId: number; quantity: number }) => cartApi.add(v.productId, v.quantity), onSuccess: onCart, onError });
  const updateMutation = useMutation({ mutationFn: (v: { id: number; quantity: number }) => cartApi.update(v.id, v.quantity), onSuccess: onCart, onError });
  const removeMutation = useMutation({ mutationFn: (id: number) => cartApi.remove(id), onSuccess: onCart, onError });

  const lines: CartLineView[] = isAuthenticated
    ? (serverCart.data?.items ?? []).map((l) => ({
        key: l.id, productId: l.productId, slug: l.slug, name: l.name, image: l.image, unitPrice: l.unitPrice, quantity: l.quantity,
        maxQuantity: l.maxQuantity, lineTotal: l.lineTotal, isPurchasable: l.isPurchasable, issue: l.issue, inventoryState: l.inventoryState,
      }))
    : guest.cart.map((l) => ({
        key: l.productId, productId: l.productId, slug: l.slug, name: l.name, image: l.image, unitPrice: l.price, quantity: l.quantity,
        maxQuantity: l.maxPerOrder, lineTotal: l.price * l.quantity, isPurchasable: true, issue: null, inventoryState: null,
      }));

  const add = async (product: AddableProduct, quantity = 1) => {
    if (product.inventoryState !== 'Available') {
      toast.error(product.inventoryState === 'Reserved' ? 'This piece is reserved by another customer at checkout.' : 'Sorry, this piece has been sold.');
      return false;
    }
    if (isAuthenticated) {
      try {
        await addMutation.mutateAsync({ productId: product.id, quantity });
      } catch {
        return false;
      }
    } else {
      const result = guest.addToCart({
        productId: product.id, quantity, name: product.name, slug: product.slug, price: product.price,
        image: product.image ?? product.images?.[0] ?? null, maxPerOrder: product.maxPerOrder,
      });
      if (!result.ok) {
        toast.info(result.message);
        return false;
      }
    }
    toast.success(`Added “${product.name}” to your cart`);
    openCart(true);
    return true;
  };

  return {
    lines,
    count: lines.reduce((n, l) => n + l.quantity, 0),
    subtotal: isAuthenticated ? serverCart.data?.subtotal ?? 0 : lines.reduce((n, l) => n + l.lineTotal, 0),
    hasIssues: lines.some((l) => !l.isPurchasable),
    isGuest: !isAuthenticated,
    isLoading: isAuthenticated && serverCart.isPending,
    isMutating: addMutation.isPending || updateMutation.isPending || removeMutation.isPending,
    contains: (productId: number) => lines.some((l) => l.productId === productId),
    add,
    update: (line: CartLineView, quantity: number) =>
      isAuthenticated ? updateMutation.mutate({ id: line.key, quantity }) : guest.setQuantity(line.productId, quantity),
    remove: (line: CartLineView) => (isAuthenticated ? removeMutation.mutate(line.key) : guest.removeFromCart(line.productId)),
  };
}
