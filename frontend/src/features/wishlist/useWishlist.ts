import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { wishlistApi } from '@/services/api';
import { errorMessage } from '@/services/http';
import { useAuth } from '@/features/auth/AuthProvider';
import { useGuestStore } from '@/store/guestStore';
import type { WishlistItem } from '@/types/api';

export function useWishlist() {
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const guestIds = useGuestStore((s) => s.wishlist);
  const toggleGuest = useGuestStore((s) => s.toggleWishlist);

  const query = useQuery({ queryKey: ['wishlist'], queryFn: wishlistApi.get, enabled: isAuthenticated, staleTime: 60_000 });
  const set = (items: WishlistItem[]) => queryClient.setQueryData(['wishlist'], items);
  const add = useMutation({ mutationFn: wishlistApi.add, onSuccess: set, onError: (e) => toast.error(errorMessage(e)) });
  const remove = useMutation({ mutationFn: wishlistApi.removeProduct, onSuccess: set, onError: (e) => toast.error(errorMessage(e)) });

  const ids = isAuthenticated ? (query.data ?? []).map((i) => i.product.id) : guestIds;
  const has = (productId: number) => ids.includes(productId);

  const toggle = (productId: number, name?: string) => {
    if (isAuthenticated) {
      if (has(productId)) remove.mutate(productId);
      else add.mutate(productId, { onSuccess: () => toast.success(name ? `Saved “${name}” to your wishlist` : 'Saved to wishlist') });
    } else {
      const added = toggleGuest(productId);
      if (added) toast.success('Saved to your wishlist on this device. Sign in to keep it across devices.');
    }
  };

  return { ids, has, toggle, items: query.data, isLoading: isAuthenticated && query.isPending, count: ids.length };
}
