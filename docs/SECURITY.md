# Security

| Requirement | Implementation |
|---|---|
| Firebase ID token verification | `FirebaseAuthenticationHandler` → `FirebaseTokenVerifier` (Admin SDK: signature, issuer, audience, expiry). Invalid tokens → 401. |
| Never trust client user id / role | User id comes from the verified token; role from MySQL. `PUT /api/me` only binds `displayName`/`phoneNumber` (overposting test included). |
| Admin provisioning | CLI `admin grant <email>` (requires a verified email; audit-logged). No seeded admins or credentials. |
| Least privilege | Permission policies per admin area; customers get none. |
| Server-side authorization | Every admin controller has `[Authorize(Policy=…)]`; ownership is enforced inside queries (`WHERE UserId = @me`), so other users' orders return 404. |
| Verified email to buy | `VerifiedEmail` policy on reservations and order placement. |
| Input validation | FluentValidation on every request DTO (global filter) + Zod on the client. |
| SQL injection | EF Core parameterised queries only; no raw SQL with user input. |
| XSS | Admin rich text sanitised server-side (HtmlSanitizer allow-list: no scripts, styles, event handlers, iframes; only http(s)/mailto/tel URLs) and again client-side (DOMPurify). Section links/images must be relative or http(s). Theme colours must be hex; fonts come from an approved list. CSP header in production. |
| Secure uploads | Type allow-list, size limits, magic-byte checks, full decode + re-encode to WebP (strips EXIF/GPS and polyglot payloads), pixel-count bomb guard, random file names, admin-only. Payment proofs go to private storage, are served only to admins with `no-store` and a sandbox CSP. |
| CORS | Explicit allow-list (`Cors:AllowedOrigins`); same-origin in production by default. |
| HTTPS | HSTS + HTTPS redirection outside Development; `ForwardedHeaders` for TLS-terminating proxies. |
| Rate limiting | Global per user/IP; stricter for forms (6/min), checkout (40/min) and uploads; per-IP daily enquiry cap; honeypot + timing spam detection. |
| Safe errors | Problem details with generic 500 messages; details only in logs. |
| Audit logging | `AdminAuditLogs` for every admin mutation, inventory correction and role change. |
| Secrets | user-secrets / environment variables / secret manager. `.env*` git-ignored; only `.env.example` placeholders are committed. Service-account keys are never in frontend code. |
| Price manipulation | Totals, shipping and discounts are recalculated on the server at every step; `expectedTotal` stops the customer paying a changed amount; discount limits are enforced atomically. |
| Inventory integrity | Atomic conditional updates, transactions, CHECK constraints, single-use reservations, idempotent orders (see ARCHITECTURE.md). |
| Auth emulator | Accepts unsigned tokens, so the API refuses to start with `Firebase:AuthEmulatorHost` outside Development/Testing. |
| Indexing | Admin/account/checkout are `noindex` and disallowed in robots.txt. |

## Operational recommendations
- Restrict the MySQL user to the application database (no `SUPER`/`FILE`).
- Enable Firebase **email enumeration protection** and App Check if abuse appears.
- Rotate the Firebase service-account key and Cloudinary secret periodically.
- Keep `checkRevoked` off for performance; to revoke a session immediately, disable the user in Firebase and set `Users.IsDisabled = 1`.
