// Mirrors the ASP.NET Core DTOs (camelCase JSON, enums serialized as strings).

export type ProductCondition = 'Excellent' | 'VeryGood' | 'Good' | 'Fair' | 'Restored' | 'ForRestoration';
export type InventoryState = 'Draft' | 'Available' | 'Reserved' | 'Sold' | 'Archived';
export type ProductStatus = 'Draft' | 'Published' | 'Archived';
export type OrderStatus = 'Pending' | 'Confirmed' | 'Processing' | 'Shipped' | 'Delivered' | 'Cancelled';
export type PaymentStatus = 'Unpaid' | 'PendingVerification' | 'Paid' | 'Failed' | 'Refunded';
export type PaymentMethod = 'CashOnDelivery' | 'BankTransfer';
export type FulfillmentMethod = 'Delivery' | 'LocalPickup';
export type UserRole = 'Customer' | 'Admin';
export type ProductSort = 'Featured' | 'Newest' | 'PriceAsc' | 'PriceDesc' | 'RecentlyAdded';
export type Availability = 'All' | 'Available' | 'Sold';
export type ReviewStatus = 'Pending' | 'Approved' | 'Rejected';
export type EnquiryStatus = 'New' | 'InProgress' | 'Resolved' | 'Spam';
export type EnquiryType = 'Product' | 'Contact';
export type DiscountType = 'Percentage' | 'FixedAmount';
export type InventoryMovementType =
  | 'InitialStock' | 'Reserved' | 'ReservationReleased' | 'ReservationExpired' | 'Sold'
  | 'OrderCancelledRestock' | 'ManualAdjustment' | 'MarkedSold';

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ImageDto {
  url: string;
  alt: string | null;
  width: number | null;
  height: number | null;
  srcSet: string | null;
}

export interface ProductCard {
  id: number;
  slug: string;
  name: string;
  price: number;
  compareAtPrice: number | null;
  currency: string;
  categoryName: string | null;
  categorySlug: string | null;
  condition: ProductCondition;
  era: string | null;
  image: ImageDto | null;
  hoverImage: ImageDto | null;
  inventoryState: InventoryState;
  isFeatured: boolean;
  maxPerOrder: number;
}

export interface RefDto { id: number; name: string; slug: string }

export interface ConditionReport {
  overall: ProductCondition;
  notes: string | null;
  signsOfWear: string | null;
  scratches: string | null;
  damage: string | null;
  restoration: string | null;
  missingParts: string | null;
  functionalStatus: string | null;
}

export interface ProductDetail {
  id: number;
  slug: string;
  name: string;
  sku: string;
  price: number;
  compareAtPrice: number | null;
  currency: string;
  shortDescription: string | null;
  description: string | null;
  category: RefDto | null;
  collections: RefDto[];
  conditionReport: ConditionReport;
  material: string | null;
  color: string | null;
  dimensions: string | null;
  weightKg: number | null;
  era: string | null;
  approximateAge: string | null;
  provenance: string | null;
  hasProvenanceEvidence: boolean;
  provenanceEvidence: string | null;
  inventoryState: InventoryState;
  maxPerOrder: number;
  isDeliveryAvailable: boolean;
  images: ImageDto[];
  attributes: { name: string; value: string }[];
  seoTitle: string | null;
  seoDescription: string | null;
  publishedAt: string | null;
  reviews: { count: number; averageRating: number | null };
}

export interface Category {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  imageUrl: string | null;
  parentId: number | null;
  sortOrder: number;
  isVisible: boolean;
  isFeatured: boolean;
  productCount: number;
  seoTitle: string | null;
  seoDescription: string | null;
}

export interface Collection {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  imageUrl: string | null;
  sortOrder: number;
  isVisible: boolean;
  productCount: number;
  seoTitle: string | null;
  seoDescription: string | null;
}

export interface FacetValue { value: string; label: string; count: number }
export interface ProductFacets {
  categories: FacetValue[];
  conditions: FacetValue[];
  eras: FacetValue[];
  materials: FacetValue[];
  minPrice: number | null;
  maxPrice: number | null;
}

export interface Review {
  id: number;
  rating: number;
  title: string | null;
  body: string;
  authorName: string;
  isVerifiedPurchase: boolean;
  createdAt: string;
}

// ---------- Site configuration ----------
export interface SocialLink { platform: string; url: string }
export interface BrandSettings {
  storeName: string;
  tagline: string | null;
  description: string | null;
  logoUrl: string | null;
  faviconUrl: string | null;
  contactEmail: string | null;
  contactPhone: string | null;
  whatsAppNumber: string | null;
  address: string | null;
  openingHours: string | null;
  mapEmbedUrl: string | null;
  socialLinks: SocialLink[];
  announcementEnabled: boolean;
  announcementText: string | null;
  announcementLink: string | null;
  footerDescription: string | null;
  copyrightText: string | null;
  newsletterEnabled: boolean;
  seoDefaultTitle: string | null;
  seoDefaultDescription: string | null;
  defaultOgImageUrl: string | null;
}

export interface Theme {
  backgroundColor: string;
  surfaceColor: string;
  textColor: string;
  mutedTextColor: string;
  primaryColor: string;
  accentColor: string;
  secondaryColor: string;
  borderColor: string;
  buttonColor: string;
  buttonTextColor: string;
  successColor: string;
  errorColor: string;
  headingFont: string;
  bodyFont: string;
  baseFontSize: number;
  headingScale: number;
  borderRadius: number;
}

export interface NavItem { label: string; href: string; children?: NavItem[] | null }
export interface NavigationMenu { location: 'main' | 'footer-shop' | 'footer-help'; title: string; items: NavItem[] }

export interface PublicCommerce {
  currency: string;
  cashOnDeliveryEnabled: boolean;
  bankTransferEnabled: boolean;
  localPickupEnabled: boolean;
  freeShippingThreshold: number | null;
  reservationMinutes: number;
  reviewsEnabled: boolean;
  deliveryInstructions: string | null;
  returnsSummary: string | null;
}

export interface SiteConfig {
  brand: BrandSettings;
  theme: Theme;
  navigation: NavigationMenu[];
  commerce: PublicCommerce;
  isPreview: boolean;
}

export type SectionType =
  | 'hero' | 'featuredCategories' | 'featuredProducts' | 'newArrivals' | 'collectionProducts' | 'imageText'
  | 'promoBanner' | 'testimonials' | 'newsletter' | 'richText' | 'eraShowcase' | 'trustFeatures';

export interface Testimonial { id: number; quote: string; authorName: string; authorLocation: string | null; rating: number | null }

export interface RenderedSection {
  key: string;
  type: SectionType;
  config: Record<string, unknown>;
  products: ProductCard[] | null;
  categories: Category[] | null;
  testimonials: Testimonial[] | null;
}

export interface Homepage { sections: RenderedSection[]; isPreview: boolean }

export interface ContentPage { slug: string; title: string; body: string; seoTitle: string | null; seoDescription: string | null; updatedAt: string | null }

// ---------- Customer ----------
export interface Me {
  id: number;
  email: string;
  emailVerified: boolean;
  displayName: string | null;
  phoneNumber: string | null;
  photoUrl: string | null;
  role: UserRole;
  createdAt: string;
  permissions: string[];
}

export interface Address {
  id: number;
  label: string;
  fullName: string;
  phone: string;
  line1: string;
  line2: string | null;
  city: string;
  district: string;
  postalCode: string | null;
  country: string;
  isDefault: boolean;
}
export type AddressInput = Omit<Address, 'id' | 'country'>;

export interface CartLine {
  id: number;
  productId: number;
  slug: string;
  name: string;
  sku: string;
  image: ImageDto | null;
  unitPrice: number;
  compareAtPrice: number | null;
  quantity: number;
  maxQuantity: number;
  lineTotal: number;
  inventoryState: InventoryState;
  isPurchasable: boolean;
  issue: string | null;
}
export interface Cart { items: CartLine[]; subtotal: number; itemCount: number; hasIssues: boolean; currency: string }

export interface WishlistItem { id: number; product: ProductCard; addedAt: string }

export interface PaymentMethodOption { method: PaymentMethod; name: string; description: string; isAvailable: boolean; unavailableReason: string | null }

export interface CheckoutQuote {
  lines: CartLine[];
  subtotal: number;
  discount: number;
  discountCode: string | null;
  discountError: string | null;
  shipping: { isAvailable: boolean; fee: number; rateName: string | null; estimatedDelivery: string | null; unavailableReason: string | null };
  total: number;
  currency: string;
  paymentMethods: PaymentMethodOption[];
  canPlaceOrder: boolean;
  issues: string[];
}

export interface CheckoutReservation { checkoutToken: string; expiresAt: string; items: { productId: number; name: string; quantity: number }[] }

export interface ShippingOptions {
  deliverableDistricts: string[];
  localPickupEnabled: boolean;
  localPickupAddress: string | null;
  localPickupInstructions: string | null;
  deliveryInstructions: string | null;
  freeShippingThreshold: number | null;
}

export interface ContactInfo { fullName: string; email: string; phone: string }
export interface DeliveryAddress { line1: string; line2: string | null; city: string; district: string; postalCode: string | null }

export interface PlaceOrderRequest {
  idempotencyKey: string;
  checkoutToken: string | null;
  contact: ContactInfo;
  fulfillmentMethod: FulfillmentMethod;
  address: DeliveryAddress | null;
  paymentMethod: PaymentMethod;
  discountCode: string | null;
  notes: string | null;
  saveAddress: boolean;
  expectedTotal: number | null;
}

export interface PaymentInstructions {
  method: PaymentMethod;
  summary: string;
  bankDetails: { bankName: string | null; accountName: string | null; accountNumber: string | null; branch: string | null } | null;
  instructions: string | null;
  paymentDueAt: string | null;
  requiresProof: boolean;
  redirectUrl: string | null;
}

export interface OrderItem { productId: number; productName: string; productSlug: string; sku: string; imageUrl: string | null; unitPrice: number; quantity: number; lineTotal: number }

export interface Order {
  id: number;
  orderNumber: string;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
  fulfillmentMethod: FulfillmentMethod;
  contact: ContactInfo;
  address: DeliveryAddress | null;
  notes: string | null;
  items: OrderItem[];
  subtotal: number;
  discountTotal: number;
  discountCode: string | null;
  shippingTotal: number;
  shippingRateName: string | null;
  total: number;
  refundedTotal: number;
  currency: string;
  trackingCarrier: string | null;
  trackingNumber: string | null;
  trackingUrl: string | null;
  placedAt: string;
  history: { status: OrderStatus | null; paymentStatus: PaymentStatus | null; note: string | null; createdAt: string }[];
  paymentInstructions: PaymentInstructions | null;
  canCancel: boolean;
  canSubmitPayment: boolean;
  cancellationReason: string | null;
}

export interface OrderSummary {
  id: number;
  orderNumber: string;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
  total: number;
  currency: string;
  itemCount: number;
  firstItemImage: string | null;
  placedAt: string;
}

// ---------- Admin ----------
export interface AdminProductListItem {
  id: number;
  name: string;
  sku: string;
  slug: string;
  price: number;
  compareAtPrice: number | null;
  status: ProductStatus;
  inventoryState: InventoryState;
  quantityAvailable: number;
  quantityReserved: number;
  quantitySold: number;
  categoryName: string | null;
  imageUrl: string | null;
  isFeatured: boolean;
  updatedAt: string;
}

export interface ProductImageInput { mediaAssetId: number | null; url: string | null; altText: string | null }

export interface ProductUpsert {
  name: string;
  slug: string | null;
  sku: string;
  shortDescription: string | null;
  description: string | null;
  price: number;
  compareAtPrice: number | null;
  categoryId: number | null;
  collectionIds: number[];
  condition: ProductCondition;
  conditionNotes: string | null;
  signsOfWear: string | null;
  scratches: string | null;
  damage: string | null;
  restorationHistory: string | null;
  missingParts: string | null;
  functionalStatus: string | null;
  material: string | null;
  color: string | null;
  dimensions: string | null;
  weightKg: number | null;
  era: string | null;
  approximateAge: string | null;
  provenance: string | null;
  hasProvenanceEvidence: boolean;
  provenanceEvidence: string | null;
  initialQuantity: number;
  maxPerOrder: number;
  status: ProductStatus;
  isFeatured: boolean;
  isDeliveryAvailable: boolean;
  showWhenSold: boolean;
  seoTitle: string | null;
  seoDescription: string | null;
  images: ProductImageInput[];
  attributes: { name: string; value: string }[];
  version: number | null;
}

export interface AdminProduct extends Omit<ProductUpsert, 'images' | 'initialQuantity' | 'version'> {
  id: number;
  slug: string;
  quantityAvailable: number;
  quantityReserved: number;
  quantitySold: number;
  inventoryState: InventoryState;
  images: { id: number; mediaAssetId: number | null; url: string; altText: string | null; sortOrder: number; width: number | null; height: number | null }[];
  version: number;
  createdAt: string;
  updatedAt: string;
  publishedAt: string | null;
  hasOrders: boolean;
}

export interface InventoryMovement {
  id: number;
  type: InventoryMovementType;
  availableChange: number;
  reservedChange: number;
  soldChange: number;
  reason: string | null;
  orderId: number | null;
  orderNumber: string | null;
  userName: string | null;
  createdAt: string;
}

export interface ActiveReservation { id: number; productId: number; productName: string; quantity: number; customerEmail: string; expiresAt: string }

export interface AdminOrderListItem {
  id: number;
  orderNumber: string;
  customerName: string;
  customerEmail: string;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
  fulfillmentMethod: FulfillmentMethod;
  total: number;
  itemCount: number;
  placedAt: string;
}

export interface PaymentRecord {
  id: number;
  kind: 'Charge' | 'Refund';
  method: PaymentMethod;
  status: 'Pending' | 'Succeeded' | 'Failed';
  amount: number;
  reference: string | null;
  notes: string | null;
  verifiedAt: string | null;
  verifiedBy: string | null;
  createdAt: string;
  proofs: { id: number; fileName: string; contentType: string; sizeBytes: number; uploadedAt: string }[];
}

export interface AdminOrder {
  order: Order;
  userId: number;
  customerAccountEmail: string | null;
  customerOrderCount: number;
  adminNotes: string | null;
  version: number;
  paymentDueAt: string | null;
  stockRestored: boolean;
  payments: PaymentRecord[];
  history: {
    fromStatus: OrderStatus | null; toStatus: OrderStatus | null; fromPaymentStatus: PaymentStatus | null;
    toPaymentStatus: PaymentStatus | null; note: string | null; changedBy: string | null; visibleToCustomer: boolean; createdAt: string;
  }[];
  nextStatuses: OrderStatus[];
  nextPaymentStatuses: PaymentStatus[];
}

export interface AdminCustomer {
  id: number;
  email: string;
  displayName: string | null;
  phoneNumber: string | null;
  role: UserRole;
  emailVerified: boolean;
  isDisabled: boolean;
  orderCount: number;
  paidTotal: number;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface MediaAsset {
  id: number;
  url: string;
  srcSet: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  width: number;
  height: number;
  altText: string | null;
  title: string | null;
  folder: string;
  createdAt: string;
  isInUse: boolean;
}

export interface HomepageSectionDto { key: string; type: SectionType; sortOrder: number; isVisible: boolean; config: Record<string, unknown> }

export interface AdminCustomization {
  brand: BrandSettings;
  theme: Theme;
  navigation: NavigationMenu[];
  sections: HomepageSectionDto[];
  hasUnpublishedChanges: boolean;
  lastPublishedAt: string | null;
  headingFonts: string[];
  bodyFonts: string[];
  sectionTypes: SectionType[];
}

export interface AdminContentPage {
  id: number;
  slug: string;
  title: string;
  draftTitle: string;
  draftBody: string;
  isPublished: boolean;
  hasUnpublishedChanges: boolean;
  isSystem: boolean;
  seoTitle: string | null;
  seoDescription: string | null;
  publishedAt: string | null;
  updatedAt: string;
}

export interface DiscountCode {
  id: number;
  code: string;
  description: string | null;
  type: DiscountType;
  value: number;
  minOrderSubtotal: number | null;
  maxDiscountAmount: number | null;
  startsAt: string | null;
  endsAt: string | null;
  usageLimit: number | null;
  usageLimitPerCustomer: number | null;
  usedCount: number;
  isActive: boolean;
  createdAt: string;
}

export interface ShippingZone {
  id: number | null;
  name: string;
  isActive: boolean;
  cashOnDeliveryAvailable: boolean;
  estimatedDelivery: string | null;
  sortOrder: number;
  districts: string[];
  rates: { id: number | null; name: string; fee: number; minSubtotal: number | null; maxSubtotal: number | null; isActive: boolean }[];
}

export interface CommerceSettings {
  orderNumberPrefix: string;
  reservationMinutes: number;
  bankTransferPaymentWindowHours: number;
  freeShippingThreshold: number | null;
  localPickupEnabled: boolean;
  localPickupAddress: string | null;
  localPickupInstructions: string | null;
  deliveryInstructions: string | null;
  cashOnDeliveryEnabled: boolean;
  cashOnDeliveryMaxOrderValue: number | null;
  bankTransferEnabled: boolean;
  bankName: string | null;
  bankAccountName: string | null;
  bankAccountNumber: string | null;
  bankBranch: string | null;
  bankTransferInstructions: string | null;
  reviewsEnabled: boolean;
  returnsSummary: string | null;
  currency: string;
}

export interface Enquiry {
  id: number;
  type: EnquiryType;
  productId: number | null;
  productName: string | null;
  productSlug: string | null;
  name: string;
  email: string;
  phone: string | null;
  subject: string | null;
  message: string;
  status: EnquiryStatus;
  adminNotes: string | null;
  createdAt: string;
}

export interface AdminReview {
  id: number;
  productId: number;
  productName: string;
  productSlug: string;
  rating: number;
  title: string | null;
  body: string;
  authorName: string;
  authorEmail: string;
  isVerifiedPurchase: boolean;
  status: ReviewStatus;
  createdAt: string;
}

export interface AdminTestimonial { id: number; quote: string; authorName: string; authorLocation: string | null; rating: number | null; isPublished: boolean; sortOrder: number }

export interface TrendPoint { date: string; paidRevenue: number; orders: number }
export interface RevenueBreakdown { netPaid: number; outstanding: number; cancelled: number; refunded: number; paidOrders: number }

export interface Dashboard {
  revenue: RevenueBreakdown;
  totalOrders: number;
  pendingOrders: number;
  awaitingVerification: number;
  products: { total: number; available: number; reserved: number; sold: number; draft: number; archived: number };
  recentOrders: AdminOrderListItem[];
  lowStock: { id: number; name: string; sku: string; quantityAvailable: number; quantityReserved: number }[];
  salesTrend: TrendPoint[];
  newEnquiries: number;
  pendingReviews: number;
  activeReservations: number;
  currency: string;
}

export interface BreakdownRow { label: string; orders: number; amount: number }
export interface SalesReport {
  from: string;
  to: string;
  revenue: RevenueBreakdown;
  daily: TrendPoint[];
  byStatus: BreakdownRow[];
  byPaymentMethod: BreakdownRow[];
  byCategory: BreakdownRow[];
  topProducts: { productId: number; name: string; quantity: number; revenue: number }[];
  averageOrderValue: number;
}

export interface AuditLog { id: number; userEmail: string | null; action: string; entityType: string; entityId: string | null; summary: string | null; createdAt: string }

export interface ProblemDetails {
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}
