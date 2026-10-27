# API reference

The live OpenAPI document is served in Development at `/openapi/v1.json`, with an interactive reference (Scalar) at `/docs`.

## Conventions
- JSON, camelCase; enums are strings (e.g. `"Available"`, `"CashOnDelivery"`).
- **Auth:** `Authorization: Bearer <Firebase ID token>`. Roles come from the database, never the request.
- **Pagination:** `?page=1&pageSize=24` (max 100). Response: `{ items, page, pageSize, totalCount, totalPages }`.
- **Errors:** RFC 7807 problem details with a stable `code`:

| Status | code | Meaning |
|---|---|---|
| 400 | `validation` | Field errors in `errors: { "address.city": ["…"] }` |
| 401 | `unauthenticated` | Missing/invalid/expired token |
| 403 | `forbidden` | Authenticated but not allowed (e.g. unverified email at checkout, non-admin) |
| 404 | `not_found` | Missing, or not yours (ownership is enforced in queries) |
| 409 | `conflict` | Concurrent modification (stale `version`) |
| 422 | e.g. `items_unavailable`, `invalid_transition`, `total_changed`, `payment_unavailable` | Business rule |
| 429 | `rate_limited` | Too many requests |

## Public
| Method | Path | Notes |
|---|---|---|
| GET | `/api/products` | Filters: `q, category, collection, minPrice, maxPrice, condition[], era[], material[], availability (All/Available/Sold), ids[], sort (Featured/Newest/RecentlyAdded/PriceAsc/PriceDesc), page, pageSize` |
| GET | `/api/products/facets?category=` | Filter counts and price range |
| GET | `/api/products/{slug}` | Detail incl. condition report |
| GET | `/api/products/{slug}/related` | |
| GET/POST | `/api/products/{slug}/reviews` | POST requires auth; reviews are moderated |
| GET | `/api/categories`, `/api/categories/{slug}` | |
| GET | `/api/collections`, `/api/collections/{slug}` | |
| GET | `/api/homepage` | Published sections with resolved products/categories/testimonials |
| GET | `/api/store/settings` | Published brand, theme, navigation, public commerce settings |
| GET | `/api/pages/{slug}` | Published content page |
| GET | `/api/shipping/options` | Deliverable districts, pickup info |
| POST | `/api/contact`, `/api/enquiries` | Rate-limited, honeypot (`website`) and timing (`formStartedAt`) spam checks |
| POST | `/api/newsletter`, `/api/newsletter/unsubscribe?token=` | |
| GET | `/sitemap.xml`, `/robots.txt`, `/health/live`, `/health/ready` | |

## Customer (auth required)
| Method | Path |
|---|---|
| GET/PUT | `/api/me` (only `displayName`, `phoneNumber` are editable) |
| GET/POST | `/api/me/addresses` · PUT/DELETE `/api/me/addresses/{id}` |
| GET | `/api/cart` · POST `/api/cart/items` · PATCH/DELETE `/api/cart/items/{id}` · POST `/api/cart/merge` |
| GET | `/api/wishlist` · POST `/api/wishlist/items` · DELETE `/api/wishlist/items/{id}` · DELETE `/api/wishlist/products/{productId}` · POST `/api/wishlist/merge` |
| POST | `/api/checkout/quote` |
| POST/DELETE | `/api/checkout/reservations` (verified email required) |
| POST | `/api/orders` (verified email; body includes `idempotencyKey`, optional `expectedTotal`) → 201, or 200 with the original order on retry |
| GET | `/api/orders/my`, `/api/orders/{id}`, `/api/orders/number/{orderNumber}` |
| POST | `/api/orders/{id}/cancel` (Pending + unpaid only) |
| POST | `/api/orders/{id}/payment` (multipart: `reference`, `notes?`, `proof?` JPG/PNG/WEBP/PDF ≤ 5 MB) |

## Admin (Admin role + permission policies)
| Area | Endpoints |
|---|---|
| Dashboard/reports | GET `/api/admin/dashboard`, `/api/admin/reports/sales?from&to` |
| Products | GET/POST `/api/admin/products` · GET/PUT/DELETE `/api/admin/products/{id}` (DELETE archives products with order history) · POST `{id}/duplicate`, `{id}/stock`, `{id}/mark-sold` · PATCH `{id}/status`, `{id}/featured` · GET `{id}/movements`, `/api/admin/inventory/reservations` |
| Categories/collections | CRUD `/api/admin/categories`, PUT `/api/admin/categories/order`, CRUD `/api/admin/collections` |
| Orders | GET `/api/admin/orders`, `/api/admin/orders/{id}` · PATCH `{id}/status`, `{id}/payment`, `{id}` (notes/tracking) · POST `{id}/refunds` · GET `{id}/proofs/{proofId}` |
| Customers | GET `/api/admin/customers`, `/api/admin/customers/{id}` |
| Media | GET/POST `/api/admin/media` (multipart `file`, `folder`, `altText`) · PATCH/DELETE `/api/admin/media/{id}` |
| Customization | GET `/api/admin/customization` · PUT `…/brand`, `…/theme`, `…/navigation` · GET `/api/admin/homepage` · PUT `/api/admin/homepage/draft` · POST `/api/admin/homepage/publish`, `/api/admin/customization/publish`, `/api/admin/customization/discard` |
| Preview | GET `/api/admin/preview/site`, `/homepage`, `/pages/{slug}` |
| Pages | CRUD `/api/admin/pages` · POST `{id}/publish`, `{id}/unpublish`, `{id}/discard` |
| Settings & shipping | GET/PUT `/api/admin/settings` · CRUD `/api/admin/shipping/zones` |
| Promotions | CRUD `/api/admin/discounts` |
| Engagement | `/api/admin/reviews`, `/api/admin/enquiries`, `/api/admin/testimonials`, `/api/admin/newsletter` |
| Audit | GET `/api/admin/audit-logs` |

## Homepage section types
`hero`, `featuredCategories`, `featuredProducts`, `newArrivals`, `collectionProducts`, `imageText`, `promoBanner`, `testimonials`, `newsletter`, `richText`, `eraShowcase`, `trustFeatures`. Schemas: `backend/AntiqueStore.Application/Customization/SectionConfigs.cs` (mirrored in `frontend/src/types/sections.ts`).
