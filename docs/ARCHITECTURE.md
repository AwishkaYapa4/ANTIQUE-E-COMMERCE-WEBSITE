# Architecture

## Overview

```
Browser (React SPA) ──HTTPS──▶ ASP.NET Core API ──▶ MySQL 8
      │                        │   ├─ Firebase Admin SDK (token verification)
      └─ Firebase Auth (sign-in, ID tokens)        ├─ Cloudinary / local disk (images)
                                                   └─ Background worker (reservation expiry, unpaid-transfer cancellation)
```

It is a **modular monolith**: one deployable API with clear internal layers, and no microservices.

| Layer | Responsibility | Depends on |
|---|---|---|
| Domain | Entities, enums, state machines (`OrderRules`), Sri Lanka districts | nothing |
| Application | Use cases (`CheckoutService`, `InventoryService`, …), DTOs, FluentValidation validators, ports (`IAppDbContext`, `IImageStorage`, `IHtmlSanitizer`, …) | Domain, EF Core abstractions |
| Infrastructure | `AppDbContext` + configurations, migrations, seeding, Firebase verifier, image processing/storage, audit logger, background worker | Application |
| Api | Controllers, Firebase auth handler, policies, error mapping, rate limiting, output caching, SEO head injection, sitemap/robots, operator CLI | Application, Infrastructure |

The frontend is organised by feature: `features/customer` (home, shop, product, checkout, account, pages), `features/admin`, `features/auth`, `features/cart`, `features/site` (theme/config) and `features/wishlist`. Shared UI lives in `components/ui`; API access is in `services/` (Axios with an ID-token interceptor and normalised `ApiError`).

## Key decisions

### Database providers
`Database:Provider` selects **SqlServer** (default), **MySql** or **Sqlite** (demos/tests only, schema created directly). Each real provider has its own migration set and history: SQL Server migrations live in `AntiqueStore.Migrations.SqlServer` (its own assembly, with a design-time factory), MySQL migrations stay in `AntiqueStore.Infrastructure`. After a model change, add a migration to **both**. `db migrate` refuses to run on a database that has tables but no migration history, instead of failing part-way. SQL Server integration tests (`SqlServerIntegrationTests`) run against throwaway `AntiqueStore_Test_*` databases and are skipped when no server is reachable (`ANTIQUESTORE_TEST_SQLSERVER` overrides the server).

### MySQL provider
Pomelo has no stable EF Core 10 release (its latest stable, 9.0.0, targets EF 9), so the project uses Oracle's **MySql.EntityFrameworkCore 10.0.x**, which targets EF Core 10. Card projections avoid `LATERAL`/`APPLY` SQL (images are loaded in a second query), which keeps queries portable. Long text columns use `LONGTEXT` (a model convention, skipped on SQL Server, which uses `nvarchar(n)`/`nvarchar(max)`) so no table exceeds InnoDB's 65,535-byte row limit for `VARCHAR`s.

### Identity vs. business data
Firebase only proves identity. On each request the API verifies the ID token, then finds or creates the `Users` row by Firebase UID. **Roles and permissions come only from MySQL.** New users are always `Customer`; admins are promoted by the CLI (`admin grant`). Permissions (`catalog.manage`, `orders.manage`, …) map from roles in one place (`Permissions.For`), so a future "Staff" role is a one-line change.

### Inventory model and checkout concurrency (the critical part)
Each product has three counters: `QuantityAvailable`, `QuantityReserved` and `QuantitySold`. The customer-facing state (`Draft`/`Available`/`Reserved`/`Sold`/`Archived`) is **derived**, never stored, so it can't drift from the counters. Publication (`Draft`/`Published`/`Archived`) is a separate field.

Every stock change is one **conditional atomic UPDATE**, e.g.:
```sql
UPDATE Products SET QuantityAvailable = QuantityAvailable - 1, QuantityReserved = QuantityReserved + 1
WHERE Id = @id AND Status = 'Published' AND QuantityAvailable >= 1
```
When two shoppers race for the last unit, the database row lock lets exactly one UPDATE affect a row. The loser affects zero rows and gets "no longer available". CHECK constraints (`QuantityAvailable >= 0` …) are a second line of defence.

Flow:
1. **Quote** (`POST /api/checkout/quote`): totals from current DB prices, server shipping rates and discount rules.
2. **Reserve** (`POST /api/checkout/reservations`): all-or-nothing hold of every cart item for `ReservationMinutes` (default 15). Restarting checkout replaces earlier holds instead of stacking them.
3. **Place order** (`POST /api/orders`): one transaction. It inserts the order (the unique `(UserId, IdempotencyKey)` index makes retries return the original order), consumes the reservations (`UPDATE … WHERE Status = 'Active'`, so a reservation can be used or released exactly once), re-acquires stock atomically if a hold had expired, moves units from reserved to sold, claims a discount use (`UsedCount < UsageLimit` in the WHERE clause), clears the cart and writes the inventory ledger. If anything fails, everything rolls back.
4. **Expiry**: a background worker releases expired holds. It is safe on multiple instances, because the status flip is conditional.
5. **Cancellation**: stock returns to sale only while the order is still with the shop (Pending/Confirmed/Processing), only once (`StockRestored`), and the admin can opt out (e.g. damaged item). Unpaid bank-transfer orders are auto-cancelled after the configurable payment window.

Every change is written to `InventoryMovements`. Admin corrections use compare-and-set on the previous value and are audit-logged.

### Payments
Payment status (`Unpaid` → `PendingVerification` → `Paid`/`Failed` → `Refunded`) is independent of order status. Cash on delivery and bank transfer are implemented behind `IPaymentMethodProvider`. A future gateway adds a provider plus a webhook endpoint that confirms payment server-to-server and idempotently by provider transaction id. Orders are never marked paid from a browser redirect. Bank transfers require an admin to verify the payment. Proofs are stored privately and streamed only to admins.

### Website customization (draft → preview → publish)
Brand/contact/footer (`StoreSettings`), theme tokens (`ThemeSettings`), menus (`NavigationMenus`) and homepage sections (`HomepageSections`) each have a **Draft** and a **Published** row. Admin edits touch only Draft rows. Publishing copies Draft → Published inside one transaction. Preview uses admin-only endpoints (`/api/admin/preview/*`), so customers can never load drafts. Section configs are validated against typed schemas per section type and re-serialised, which drops unknown fields. Links and image URLs must be site-relative or http(s), and rich text is sanitised. Content pages have their own draft/publish fields.

Theme colours and fonts become CSS variables at runtime. Fonts come from an approved list, and colours must be `#RRGGBB`, so nothing executable can be injected through settings.

### SEO for a Vite SPA
When ASP.NET serves the built SPA (`Frontend:DistPath`), it injects route-specific `<title>`, description, canonical, Open Graph and **JSON-LD Product** markup into `index.html` server-side. Crawlers and link previews therefore get correct metadata without running JavaScript. React 19 then manages metadata client-side and removes the server tags. `/sitemap.xml` and `/robots.txt` are generated from the database; admin, account, cart and checkout routes are `noindex`/disallowed.

### Performance
The design uses route-level code splitting (each admin area is its own chunk), vendor chunking, TanStack Query caching, 60-second output caching for public catalog/site endpoints (evicted on any admin write), database indexes on all filter/sort columns, pagination everywhere, and responsive WebP images (400/800/1600 px) with lazy loading.

## Data model (tables)
Users, UserAddresses, Categories, Collections, Products, ProductImages, ProductCollections, ProductAttributes, InventoryReservations, InventoryMovements, Carts, CartItems, Wishlists, WishlistItems, Orders, OrderItems, OrderStatusHistory, Payments, PaymentProofs, Reviews, Enquiries, Testimonials, NewsletterSubscribers, DiscountCodes, DiscountUsages, ShippingZones, ShippingZoneDistricts, ShippingRates, CommerceSettings, StoreSettings, ThemeSettings, NavigationMenus, HomepageSections, ContentPages, MediaAssets, AdminAuditLogs.

Money uses `decimal(18,2)` and all timestamps are UTC. Products referenced by orders are archived, never deleted (FK `RESTRICT`), and order items keep a snapshot of name, SKU, price and image.
