import type * as T from '@/types/api';
import { del, get, http, patch, post, put } from './http';

export interface AdminProductQuery {
  q?: string;
  status?: T.ProductStatus;
  inventory?: T.InventoryState;
  categoryId?: number;
  featured?: boolean;
  sort?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminOrderQuery {
  q?: string;
  status?: T.OrderStatus;
  paymentStatus?: T.PaymentStatus;
  paymentMethod?: T.PaymentMethod;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export const adminApi = {
  dashboard: () => get<T.Dashboard>('/api/admin/dashboard'),
  salesReport: (from?: string, to?: string) => get<T.SalesReport>('/api/admin/reports/sales', { from, to }),

  products: (q: AdminProductQuery) => get<T.Paged<T.AdminProductListItem>>('/api/admin/products', q),
  product: (id: number) => get<T.AdminProduct>(`/api/admin/products/${id}`),
  createProduct: (body: T.ProductUpsert) => post<T.AdminProduct>('/api/admin/products', body),
  updateProduct: (id: number, body: T.ProductUpsert) => put<T.AdminProduct>(`/api/admin/products/${id}`, body),
  deleteProduct: (id: number) => del<{ deleted: boolean; archived: boolean; message: string }>(`/api/admin/products/${id}`),
  duplicateProduct: (id: number) => post<T.AdminProduct>(`/api/admin/products/${id}/duplicate`),
  setProductStatus: (id: number, status: T.ProductStatus) => patch<T.AdminProduct>(`/api/admin/products/${id}/status`, { status }),
  setFeatured: (id: number, isFeatured: boolean) => patch<T.AdminProduct>(`/api/admin/products/${id}/featured`, { isFeatured }),
  adjustStock: (id: number, quantityAvailable: number, reason: string) =>
    post<T.AdminProduct>(`/api/admin/products/${id}/stock`, { quantityAvailable, reason }),
  markSold: (id: number, reason: string | null) => post<T.AdminProduct>(`/api/admin/products/${id}/mark-sold`, { reason }),
  movements: (id: number) => get<T.InventoryMovement[]>(`/api/admin/products/${id}/movements`),
  reservations: () => get<T.ActiveReservation[]>('/api/admin/inventory/reservations'),

  categories: () => get<T.Category[]>('/api/admin/categories'),
  saveCategory: (id: number | null, body: Record<string, unknown>) =>
    id ? put<T.Category>(`/api/admin/categories/${id}`, body) : post<T.Category>('/api/admin/categories', body),
  deleteCategory: (id: number) => del<void>(`/api/admin/categories/${id}`),
  reorderCategories: (ids: number[]) => put<void>('/api/admin/categories/order', { ids }),
  collections: () => get<T.Collection[]>('/api/admin/collections'),
  saveCollection: (id: number | null, body: Record<string, unknown>) =>
    id ? put<T.Collection>(`/api/admin/collections/${id}`, body) : post<T.Collection>('/api/admin/collections', body),
  deleteCollection: (id: number) => del<void>(`/api/admin/collections/${id}`),

  orders: (q: AdminOrderQuery) => get<T.Paged<T.AdminOrderListItem>>('/api/admin/orders', q),
  order: (id: number) => get<T.AdminOrder>(`/api/admin/orders/${id}`),
  updateOrderStatus: (id: number, body: { status: T.OrderStatus; note: string | null; restock?: boolean; trackingCarrier?: string | null; trackingNumber?: string | null; trackingUrl?: string | null; expectedVersion?: number }) =>
    patch<T.AdminOrder>(`/api/admin/orders/${id}/status`, body),
  updatePaymentStatus: (id: number, body: { status: T.PaymentStatus; note: string | null; reference?: string | null }) =>
    patch<T.AdminOrder>(`/api/admin/orders/${id}/payment`, body),
  recordRefund: (id: number, body: { amount: number; note: string | null; reference: string | null }) =>
    post<T.AdminOrder>(`/api/admin/orders/${id}/refunds`, body),
  updateOrderDetails: (id: number, body: { adminNotes: string | null; trackingCarrier: string | null; trackingNumber: string | null; trackingUrl: string | null }) =>
    patch<T.AdminOrder>(`/api/admin/orders/${id}`, body),
  /** Proofs are private: fetched with the admin's token, then shown as a blob URL. */
  proofBlob: async (orderId: number, proofId: number) =>
    (await http.get<Blob>(`/api/admin/orders/${orderId}/proofs/${proofId}`, { responseType: 'blob' })).data,

  customers: (q: { q?: string; page?: number; pageSize?: number }) => get<T.Paged<T.AdminCustomer>>('/api/admin/customers', q),
  customer: (id: number) => get<{ customer: T.AdminCustomer; addresses: T.Address[]; orders: T.AdminOrderListItem[] }>(`/api/admin/customers/${id}`),

  media: (q: { q?: string; folder?: string; unused?: boolean; page?: number; pageSize?: number }) => get<T.Paged<T.MediaAsset>>('/api/admin/media', q),
  uploadMedia: (file: File, folder: string, altText?: string) => {
    const form = new FormData();
    form.append('file', file);
    form.append('folder', folder);
    if (altText) form.append('altText', altText);
    return http.post<T.MediaAsset>('/api/admin/media', form).then((r) => r.data);
  },
  updateMedia: (id: number, body: { altText: string | null; title: string | null }) => patch<T.MediaAsset>(`/api/admin/media/${id}`, body),
  deleteMedia: (id: number) => del<void>(`/api/admin/media/${id}`),

  customization: () => get<T.AdminCustomization>('/api/admin/customization'),
  saveBrand: (b: T.BrandSettings) => put<T.AdminCustomization>('/api/admin/customization/brand', b),
  saveTheme: (t: T.Theme) => put<T.AdminCustomization>('/api/admin/customization/theme', t),
  saveNavigation: (menus: T.NavigationMenu[]) => put<T.AdminCustomization>('/api/admin/customization/navigation', { menus }),
  saveHomepage: (sections: { key: string | null; type: T.SectionType; isVisible: boolean; config: Record<string, unknown> }[]) =>
    put<T.AdminCustomization>('/api/admin/homepage/draft', { sections }),
  publish: () => post<T.AdminCustomization>('/api/admin/customization/publish'),
  discard: () => post<T.AdminCustomization>('/api/admin/customization/discard'),

  pages: () => get<T.AdminContentPage[]>('/api/admin/pages'),
  createPage: (body: { slug: string | null; title: string; body: string; seoTitle: string | null; seoDescription: string | null }) =>
    post<T.AdminContentPage>('/api/admin/pages', body),
  savePage: (id: number, body: { slug: string | null; title: string; body: string; seoTitle: string | null; seoDescription: string | null }) =>
    put<T.AdminContentPage>(`/api/admin/pages/${id}`, body),
  publishPage: (id: number) => post<T.AdminContentPage>(`/api/admin/pages/${id}/publish`),
  unpublishPage: (id: number) => post<T.AdminContentPage>(`/api/admin/pages/${id}/unpublish`),
  discardPage: (id: number) => post<T.AdminContentPage>(`/api/admin/pages/${id}/discard`),
  deletePage: (id: number) => del<void>(`/api/admin/pages/${id}`),

  settings: () => get<T.CommerceSettings>('/api/admin/settings'),
  saveSettings: (s: T.CommerceSettings) => put<T.CommerceSettings>('/api/admin/settings', s),
  zones: () => get<T.ShippingZone[]>('/api/admin/shipping/zones'),
  saveZone: (z: T.ShippingZone) => (z.id ? put<T.ShippingZone>(`/api/admin/shipping/zones/${z.id}`, z) : post<T.ShippingZone>('/api/admin/shipping/zones', z)),
  deleteZone: (id: number) => del<void>(`/api/admin/shipping/zones/${id}`),

  discounts: () => get<T.DiscountCode[]>('/api/admin/discounts'),
  saveDiscount: (id: number | null, body: Record<string, unknown>) =>
    id ? put<T.DiscountCode>(`/api/admin/discounts/${id}`, body) : post<T.DiscountCode>('/api/admin/discounts', body),
  deleteDiscount: (id: number) => del<{ deleted: boolean }>(`/api/admin/discounts/${id}`),

  reviews: (q: { status?: T.ReviewStatus; page?: number }) => get<T.Paged<T.AdminReview>>('/api/admin/reviews', q),
  moderateReview: (id: number, status: T.ReviewStatus) => patch<void>(`/api/admin/reviews/${id}`, { status }),
  deleteReview: (id: number) => del<void>(`/api/admin/reviews/${id}`),
  enquiries: (q: { status?: T.EnquiryStatus; type?: T.EnquiryType; q?: string; page?: number }) => get<T.Paged<T.Enquiry>>('/api/admin/enquiries', q),
  updateEnquiry: (id: number, body: { status: T.EnquiryStatus; adminNotes: string | null }) => patch<T.Enquiry>(`/api/admin/enquiries/${id}`, body),
  testimonials: () => get<T.AdminTestimonial[]>('/api/admin/testimonials'),
  saveTestimonial: (id: number | null, body: Omit<T.AdminTestimonial, 'id'>) =>
    id ? put<T.AdminTestimonial>(`/api/admin/testimonials/${id}`, body) : post<T.AdminTestimonial>('/api/admin/testimonials', body),
  deleteTestimonial: (id: number) => del<void>(`/api/admin/testimonials/${id}`),
  newsletter: (page = 1) => get<T.Paged<{ id: number; email: string; status: string; source: string | null; createdAt: string }>>('/api/admin/newsletter', { page }),
  auditLogs: (q: { page?: number; entityType?: string }) => get<T.Paged<T.AuditLog>>('/api/admin/audit-logs', q),
};
