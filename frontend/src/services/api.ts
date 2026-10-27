import type * as T from '@/types/api';
import { del, get, http, patch, post, put } from './http';

export interface ProductQuery {
  q?: string;
  category?: string;
  collection?: string;
  minPrice?: number;
  maxPrice?: number;
  condition?: string[];
  era?: string[];
  material?: string[];
  ids?: number[];
  availability?: T.Availability;
  sort?: T.ProductSort;
  page?: number;
  pageSize?: number;
}

/** Public catalog and site content. */
export const catalogApi = {
  products: (q: ProductQuery) => get<T.Paged<T.ProductCard>>('/api/products', q),
  facets: (category?: string) => get<T.ProductFacets>('/api/products/facets', { category }),
  product: (slug: string) => get<T.ProductDetail>(`/api/products/${encodeURIComponent(slug)}`),
  related: (slug: string) => get<T.ProductCard[]>(`/api/products/${encodeURIComponent(slug)}/related`),
  reviews: (slug: string) => get<T.Review[]>(`/api/products/${encodeURIComponent(slug)}/reviews`),
  createReview: (slug: string, body: { rating: number; title: string | null; body: string }) =>
    post<T.Review>(`/api/products/${encodeURIComponent(slug)}/reviews`, body),
  categories: () => get<T.Category[]>('/api/categories'),
  category: (slug: string) => get<T.Category>(`/api/categories/${encodeURIComponent(slug)}`),
  collections: () => get<T.Collection[]>('/api/collections'),
  collection: (slug: string) => get<T.Collection>(`/api/collections/${encodeURIComponent(slug)}`),
};

export const siteApi = {
  settings: (preview = false) => get<T.SiteConfig>(preview ? '/api/admin/preview/site' : '/api/store/settings'),
  homepage: (preview = false) => get<T.Homepage>(preview ? '/api/admin/preview/homepage' : '/api/homepage'),
  page: (slug: string, preview = false) =>
    get<T.ContentPage>(preview ? `/api/admin/preview/pages/${slug}` : `/api/pages/${encodeURIComponent(slug)}`),
  shippingOptions: () => get<T.ShippingOptions>('/api/shipping/options'),
  contact: (body: EnquiryInput) => post<{ received: boolean; message: string }>('/api/contact', body),
  enquiry: (body: EnquiryInput) => post<{ received: boolean; message: string }>('/api/enquiries', body),
  subscribe: (email: string, source: string, website?: string) => post('/api/newsletter', { email, source, website }),
};

export interface EnquiryInput {
  productId?: number | null;
  name: string;
  email: string;
  phone?: string | null;
  subject?: string | null;
  message: string;
  website?: string;
  formStartedAt?: number;
}

export const meApi = {
  get: () => get<T.Me>('/api/me'),
  update: (body: { displayName: string | null; phoneNumber: string | null }) => put<T.Me>('/api/me', body),
  addresses: () => get<T.Address[]>('/api/me/addresses'),
  addAddress: (a: T.AddressInput) => post<T.Address[]>('/api/me/addresses', a),
  updateAddress: (id: number, a: T.AddressInput) => put<T.Address[]>(`/api/me/addresses/${id}`, a),
  deleteAddress: (id: number) => del<T.Address[]>(`/api/me/addresses/${id}`),
};

export const cartApi = {
  get: () => get<T.Cart>('/api/cart'),
  add: (productId: number, quantity = 1) => post<T.Cart>('/api/cart/items', { productId, quantity }),
  update: (id: number, quantity: number) => patch<T.Cart>(`/api/cart/items/${id}`, { quantity }),
  remove: (id: number) => del<T.Cart>(`/api/cart/items/${id}`),
  merge: (items: { productId: number; quantity: number }[]) => post<{ cart: T.Cart; skipped: string[] }>('/api/cart/merge', { items }),
};

export const wishlistApi = {
  get: () => get<T.WishlistItem[]>('/api/wishlist'),
  add: (productId: number) => post<T.WishlistItem[]>('/api/wishlist/items', { productId }),
  removeProduct: (productId: number) => del<T.WishlistItem[]>(`/api/wishlist/products/${productId}`),
  merge: (productIds: number[]) => post<T.WishlistItem[]>('/api/wishlist/merge', productIds),
};

export const checkoutApi = {
  quote: (body: { fulfillmentMethod: T.FulfillmentMethod; district: string | null; discountCode: string | null; paymentMethod: T.PaymentMethod | null }) =>
    post<T.CheckoutQuote>('/api/checkout/quote', body),
  reserve: () => post<T.CheckoutReservation>('/api/checkout/reservations'),
  release: () => del<void>('/api/checkout/reservations'),
  placeOrder: (body: T.PlaceOrderRequest) => post<T.Order>('/api/orders', body),
};

export const ordersApi = {
  mine: (page = 1) => get<T.Paged<T.OrderSummary>>('/api/orders/my', { page, pageSize: 10 }),
  get: (id: number) => get<T.Order>(`/api/orders/${id}`),
  cancel: (id: number, reason: string | null) => post<T.Order>(`/api/orders/${id}/cancel`, { reason }),
  submitPayment: (id: number, reference: string, notes: string | null, proof: File | null) => {
    const form = new FormData();
    form.append('reference', reference);
    if (notes) form.append('notes', notes);
    if (proof) form.append('proof', proof);
    return http.post<T.Order>(`/api/orders/${id}/payment`, form).then((r) => r.data);
  },
};
