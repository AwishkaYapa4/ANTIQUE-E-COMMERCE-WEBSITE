# Deployment

## Recommended topology
- **One container** (see `Dockerfile`): ASP.NET Core serves the API *and* the built React app, injecting SEO tags per page. It runs behind a TLS-terminating reverse proxy or load balancer (Azure App Service, AWS App Runner/ECS, DigitalOcean App Platform, a VPS with Caddy/Nginx…).
- **Managed MySQL 8** (Azure Database for MySQL, AWS RDS, DigitalOcean Managed MySQL) with automated daily backups and point-in-time recovery.
- **Cloudinary** for product images (`Storage:Provider=Cloudinary`). Local disk storage is for development only.
- **Firebase Authentication** project with your production domain in *Authorized domains*.

Prefer to host the frontend on a static host/CDN? Run `npm run build`, set `VITE_API_BASE_URL` to the API origin, add that origin to `Cors:AllowedOrigins`, and configure SPA fallback to `index.html`. Note that pure static hosting loses the server-side SEO tags. Keep the combined container if SEO matters.

## Configuration (environment variables)
| Variable | Example |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__Provider` | `MySql` for the MySQL deployment below (the default is now `SqlServer`) |
| `ConnectionStrings__Default` | `server=…;port=3306;database=antique_store;user=…;password=…;SslMode=Required` |
| `Firebase__ProjectId` | `my-shop-prod` |
| `Firebase__CredentialsJson` | service-account JSON (from your secret manager) |
| `Storage__Provider` / `Storage__Cloudinary*` | `Cloudinary`, cloud name, key, secret |
| `Seo__PublicBaseUrl` | `https://www.example.lk` |
| `ReverseProxy__TrustAllProxies` | `true` only when the app is reachable solely through your proxy |
| `Database__ApplyMigrationsOnStartup` | `false` in production (run migrations as a release step) |
| `Database__SeedExampleData` | must stay `false` in production |

Frontend build arguments: `VITE_FIREBASE_API_KEY`, `VITE_FIREBASE_AUTH_DOMAIN`, `VITE_FIREBASE_PROJECT_ID`, `VITE_FIREBASE_APP_ID`. These are public web config, not secrets.

## Release steps
1. Back up the database (or confirm a recent automated snapshot).
2. Build and push the image: `docker build -t antique-store:<version> --build-arg VITE_FIREBASE_API_KEY=… .`
3. Apply migrations from the new image: `docker run --rm -e ConnectionStrings__Default=… antique-store:<version> db migrate`. Migrations are additive; review generated SQL with `dotnet ef migrations script` for large changes.
4. Deploy the new image; wait for `GET /health/ready` (checks the database) to return 200.
5. First deployment only: seed essentials (`db seed`), sign in with the owner account, then `admin grant <email>`.

## Health, logging and monitoring
- `/health/live` (process up) and `/health/ready` (database reachable). The Docker `HEALTHCHECK` uses `--healthcheck`.
- Serilog writes **structured JSON to stdout** in production. Ship it to your platform's log service (Azure Monitor, CloudWatch, Grafana Loki…).
- Error monitoring: add Sentry/Application Insights by registering its Serilog sink and the frontend SDK in `ErrorBoundary.componentDidCatch` (hook point marked in code).
- The background worker (`Maintenance:*`) expires reservations and cancels overdue unpaid bank transfers every 60 seconds. It is safe to run on several instances.

## Backups and data
- Daily automated MySQL backups with 7–30 days retention; test restoring quarterly.
- Cloudinary keeps the original images. Payment proofs are stored on the private volume (`/app/App_Data`), so back up that volume too, or move proofs to a private bucket for multi-instance setups.

## Scaling notes
The app is stateless apart from local storage. For more than one instance, use Cloudinary for images and a shared/private object store for payment proofs. Output caching is in-memory per instance (60-second TTL), which is acceptable; use a distributed cache only if needed.
