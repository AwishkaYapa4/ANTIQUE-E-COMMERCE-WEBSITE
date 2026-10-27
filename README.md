# Antique Heritage Store

A single-vendor e-commerce platform for antique, vintage and one-of-a-kind items, built for the Sri Lankan market (LKR) with room to grow internationally.

- **Frontend:** React 19, Vite 7, TypeScript, Tailwind CSS 4, React Router 7, TanStack Query, Zustand, React Hook Form + Zod, Motion, Tiptap
- **Backend:** ASP.NET Core (.NET 10), EF Core 10 (SQL Server provider; Oracle `MySql.EntityFrameworkCore` for MySQL), FluentValidation, Serilog, OpenAPI + Scalar
- **Database:** Microsoft SQL Server by default (`Database:Provider=SqlServer`, migrations in `AntiqueStore.Migrations.SqlServer`), or MySQL 8 (`Database:Provider=MySql`, migrations in `AntiqueStore.Infrastructure`)
- **Auth:** Firebase Authentication (email/password, Google, password reset, email verification), verified server-side with the Firebase Admin SDK
- **Images:** Cloudinary (production) or local disk (development only)

| Document | Contents |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Architecture, folder layout, key design decisions, inventory concurrency model |
| [docs/API.md](docs/API.md) | REST endpoints, conventions, errors, pagination |
| [docs/SECURITY.md](docs/SECURITY.md) | Threat model and how each security requirement is met |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Production deployment, secrets, backups, monitoring |

---

## 1. What you need

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| Node.js | 22 or 24 | `node --version` |
| SQL Server | 2019+ (Developer/Express) | Default database; manage it with SSMS. See [docs/LOCAL-SETUP.md](docs/LOCAL-SETUP.md) |
| MySQL | 8.0+ | Optional alternative; Docker Compose provides MySQL 8.4 |
| Java | 11+ | Only for the optional local Firebase Auth Emulator |

### External services and credentials

None are committed to this repository. Use user-secrets, environment variables or your host's secret manager.

| Service | Needed for | Where it goes |
|---|---|---|
| Firebase project (web app + service account) | Real sign-in | Frontend `VITE_FIREBASE_*`; API `Firebase:ProjectId` + `Firebase:CredentialsJson` or `Firebase:CredentialsPath` |
| SQL Server or MySQL database | Data | `Database:Provider` + `ConnectionStrings:Default` |
| Cloudinary account (optional) | Production image hosting | `Storage:Provider=Cloudinary` + `Storage:Cloudinary*` |

Without Firebase credentials you can still run everything locally with the **Firebase Auth Emulator** (section 3b).

---

## 2. Quick start

**SQL Server + SSMS (default, no Docker):** follow [docs/LOCAL-SETUP.md](docs/LOCAL-SETUP.md).

**MySQL + real Firebase:**

```bash
# 1) Database
cp .env.example .env          # set MYSQL_ROOT_PASSWORD and MYSQL_PASSWORD
docker compose up -d mysql

# 2) API secrets (stored outside the repo)
cd backend/AntiqueStore.Api
dotnet user-secrets set "Database:Provider" "MySql"
dotnet user-secrets set "ConnectionStrings:Default" "server=127.0.0.1;port=3306;database=antique_store;user=antique_app;password=<MYSQL_PASSWORD>"
dotnet user-secrets set "Firebase:ProjectId" "<your-project-id>"
dotnet user-secrets set "Firebase:CredentialsPath" "C:/secure/firebase-service-account.json"

# 3) Apply migrations once (never automatic), then run the API
dotnet run -- db migrate
dotnet run                    # http://localhost:5080, API docs at http://localhost:5080/docs

# 4) Frontend
cd ../../frontend
cp .env.example .env.local    # fill in VITE_FIREBASE_* from Firebase console → Project settings → Web app
npm ci
npm run dev                   # http://localhost:5173 (proxies /api to the API)
```

In the Firebase console, enable the **Email/Password** and **Google** sign-in providers. Add `localhost` and your production domain to **Authorized domains**.

## 3. Other ways to run locally

### 3a. No database server: SQLite demo database
For a quick look only (`run-backend.ps1` / `dev.ps1` do this). Real environments use SQL Server or MySQL with migrations.
```bash
cd backend/AntiqueStore.Api
$env:Database__Provider="Sqlite"; $env:ConnectionStrings__Default="Data Source=App_Data/dev.db"   # PowerShell
$env:Database__ApplyMigrationsOnStartup="true"; $env:Database__SeedExampleData="true"
dotnet run
```

### 3b. No Firebase project: Auth Emulator
```bash
npx firebase-tools@14 emulators:start --only auth --project demo-antique-store   # from the repo root (uses firebase.json)
# API:
$env:Firebase__ProjectId="demo-antique-store"; $env:Firebase__AuthEmulatorHost="127.0.0.1:9099"; dotnet run
# Frontend:
npm run dev -- --mode e2e     # uses frontend/.env.e2e (demo values, no secrets)
```
The emulator accepts unsigned tokens, so the API **refuses to start** if `Firebase:AuthEmulatorHost` is set outside Development/Testing.

## 4. Provisioning an admin (trusted process)

Admin rights are never granted from the browser and no credentials are seeded.

1. Sign in to the website once with the owner's account and verify its email.
2. On the server (or anywhere with the production configuration), run:
```bash
cd backend/AntiqueStore.Api
dotnet run -- admin grant owner@example.com      # also: admin revoke <email>, admin list
# In the container: docker compose exec app dotnet AntiqueStore.Api.dll admin grant owner@example.com
```
The change is written to the audit log. The admin dashboard is at `/admin`.

## 5. Database

```bash
# Apply migrations (production: run as a deployment step, after a backup)
dotnet run --project backend/AntiqueStore.Api -- db migrate
# Seed essential settings (idempotent), optionally with EXAMPLE products
dotnet run --project backend/AntiqueStore.Api -- db seed [--examples]
# Create a new migration after changing entities
cd backend && dotnet tool restore
dotnet ef migrations add <Name> --project AntiqueStore.Infrastructure --startup-project AntiqueStore.Infrastructure --output-dir Persistence/Migrations
```
Example seed data is clearly marked: SKUs start with `EXAMPLE-`, descriptions say "Example listing", and delivery rates are named "(example rate)". Delete or replace it before launch. No testimonials or reviews are seeded.

**Example photos:** the example products, categories and default homepage use real photographs from the [Cleveland Museum of Art Open Access](https://www.clevelandart.org/open-access) collection, released under **CC0** (free for any use, including commercial, with no attribution required). They live in `frontend/public/demo-photos/`, with the source of each image in `credits.json`. They show museum pieces, **not items you own**, so replace them with photos of your actual stock (Admin → Products → Add photos) before you start selling.

## 6. Tests and quality checks

```bash
# Backend: unit + integration tests (SQLite, real services, concurrency tests)
cd backend && dotnet test

# Frontend
cd frontend
npm run typecheck && npm run lint && npm test && npm run build

# End-to-end (starts the Auth Emulator, API with SQLite and Vite automatically)
npx playwright install chromium
npm run e2e
```

## 7. Project structure

```
backend/
  AntiqueStore.Domain/          entities, enums, order/payment state machines
  AntiqueStore.Application/     use-case services, DTOs, validators, ports (interfaces)
  AntiqueStore.Infrastructure/  EF Core (MySQL), migrations, seeding, Firebase, Cloudinary/local storage, background jobs
  AntiqueStore.Api/             controllers, auth handler, error handling, SEO rendering, CLI
  AntiqueStore.Tests/           xUnit tests (service-level + HTTP-level)
frontend/
  src/app, components, layouts, features/{customer,admin,auth,cart,site,wishlist}, services, store, types, styles, utils
  e2e/                          Playwright end-to-end tests
docs/                           architecture, API, security, deployment
docker-compose.yml, Dockerfile
```

## 8. Licensing note
Image processing uses SixLabors.ImageSharp 4, licensed under the Six Labors Split License. It is free for organisations with less than USD 1M annual gross revenue; check the terms for your business.
