# Local setup without Docker (SQL Server + SSMS + Firebase)

Runs the full store on Windows with a local SQL Server database you manage in SSMS, and sign-in through a real
Firebase project.

| Part | Address |
|---|---|
| Website | http://localhost:5173 |
| Admin panel | http://localhost:5173/admin |
| API / API docs | http://localhost:5080 · http://localhost:5080/docs |
| Database | SQL Server `localhost`, database `AntiqueStore` (Windows Authentication) |

The development settings are in `backend/AntiqueStore.Api/appsettings.Development.json`:

```json
"ConnectionStrings": { "Default": "Server=localhost;Database=AntiqueStore;Trusted_Connection=True;TrustServerCertificate=True;" },
"Database": { "Provider": "SqlServer", "ApplyMigrationsOnStartup": false, "SeedEssentials": true, "SeedExampleData": false }
```

Windows Authentication means no password is stored anywhere. Migrations are **never applied automatically**. You
apply them yourself (step 3).

## 1. Requirements

- .NET 10 SDK, Node.js 22+
- SQL Server 2019+ (Developer or Express) running as the default instance (`MSSQLSERVER`), and SSMS
- A Google account for Firebase

If you use a named instance (for example SQL Express), override the connection string with user-secrets rather
than editing the file:

```powershell
cd backend\AntiqueStore.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost\SQLEXPRESS;Database=AntiqueStore;Trusted_Connection=True;TrustServerCertificate=True;"
```

## 2. Firebase project

1. Go to https://console.firebase.google.com, then **Add project**.
2. **Build → Authentication → Get started → Sign-in method**. Enable **Email/Password** and **Google**.
3. **Authentication → Settings → Authorized domains**. Check that `localhost` is in the list.
4. **Project settings (gear) → General → Your apps → Web (`</>`)**. Register an app and keep the `firebaseConfig`
   values it shows.
5. **Project settings → Service accounts → Generate new private key**. Save the JSON file **outside this folder**,
   for example `C:\Users\<you>\secrets\antique-firebase.json`. Never commit it.

Frontend: replace everything in `frontend/.env.development.local` (git-ignored) with your web app values. Leave out
`VITE_FIREBASE_AUTH_EMULATOR_HOST`:

```
VITE_FIREBASE_API_KEY=<apiKey>
VITE_FIREBASE_AUTH_DOMAIN=<authDomain>
VITE_FIREBASE_PROJECT_ID=<projectId>
VITE_FIREBASE_APP_ID=<appId>
```

Backend (user-secrets, stored outside the repo):

```powershell
cd backend\AntiqueStore.Api
dotnet user-secrets set "Firebase:ProjectId" "<projectId>"
dotnet user-secrets set "Firebase:CredentialsPath" "C:\Users\<you>\secrets\antique-firebase.json"
```

## 3. Create the database and apply migrations

From `backend\AntiqueStore.Api`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run -- db migrate          # creates the AntiqueStore database (if missing) and applies the SQL Server migrations
```

Essential settings (store settings, theme, menus, homepage, content pages) are seeded automatically on the first
API start (`SeedEssentials: true`). To also load the example catalogue for testing, run `dotnet run -- db seed --examples`
once. It only runs when the products table is empty.

Alternative using the EF CLI (from `backend\`):

```powershell
dotnet tool restore
dotnet ef database update --project AntiqueStore.Migrations.SqlServer --startup-project AntiqueStore.Migrations.SqlServer
```

To review the SQL first: `dotnet ef migrations script --idempotent --project AntiqueStore.Migrations.SqlServer --startup-project AntiqueStore.Migrations.SqlServer -o antique-sqlserver.sql`

**If `db migrate` says "the database already contains tables but has no EF Core migration history":** the
`AntiqueStore` database was created without migrations, for example by an earlier setup. Nothing was changed.
Either back it up and delete it in SSMS (right-click → Delete), or set `ConnectionStrings:Default` to a different
database name, then run `db migrate` again.

## 4. Connect with SSMS

1. Open SSMS. In the **Connect to Server** dialog choose **Server type: Database Engine**.
2. **Server name:** `localhost` (or `localhost\SQLEXPRESS` for a named instance).
3. **Authentication:** Windows Authentication.
4. **Encryption:** Mandatory or Optional. Tick **Trust server certificate**.
5. Click **Connect**, then expand **Databases → AntiqueStore → Tables**. Press F5 to refresh if it's missing.
6. `dbo.__EFMigrationsHistory` lists the applied migrations. Data is in `dbo.Products`, `dbo.Users`, `dbo.Orders`, etc.

## 5. Run the website

From the project folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\run-local.ps1
```

This opens two windows. Wait for `Now listening on: http://localhost:5080`, then open http://localhost:5173.

Manually instead:

```powershell
# window 1
cd backend\AntiqueStore.Api; $env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --no-launch-profile --urls http://localhost:5080
# window 2
cd frontend; npm install; npm run dev
```

## 6. Become admin

1. On the website, **Register**, click the link in Firebase's verification email, then sign in once.
2. `powershell -ExecutionPolicy Bypass -File .\make-admin.ps1 you@example.com -SqlServer`
3. Sign out and back in, then open http://localhost:5173/admin. In SSMS: `SELECT Email, Role FROM Users;`

## Moving existing MySQL data to SQL Server (optional)

Nothing in the code reads or changes an existing MySQL database. If you have real data in MySQL to bring across:

1. Back up MySQL first: `mysqldump --single-transaction antique_store > antique_store.sql`.
2. Create an empty SQL Server schema with `dotnet run -- db migrate` (step 3).
3. Copy the data table by table. The schema and column names are the same on both providers. Use either:
   - **SQL Server Migration Assistant (SSMA) for MySQL**, data migration only, into the existing tables, or
   - the SSMS **Import Data** wizard with the MySQL ODBC driver.
4. Load parent tables before child tables (for example `Users`, `Categories`, `Products` before `Orders`,
   `OrderItems`, `Payments`). Turn `IDENTITY_INSERT` on for each table so the `Id` values are kept.
5. Check row counts on both sides, then sign in and spot-check orders in the admin panel.
6. Keep the MySQL database read-only until you're satisfied. Don't drop it as part of the migration.

## Troubleshooting

| Problem | Fix |
|---|---|
| `Firebase:ProjectId is not configured` | Run the `dotnet user-secrets set` commands in step 2. |
| `Invalid object name 'CommerceSettings'` on API start | Migrations haven't been applied. Run step 3. |
| `A network-related error ... SQL Server` | Start the **SQL Server (MSSQLSERVER)** service, or fix the server name (step 1). |
| `Login failed for user` | Your Windows user has no access to SQL Server. Add it in SSMS under Security → Logins. |
| Login popup says `auth/unauthorized-domain` | Add `localhost` under Authentication → Settings → Authorized domains. |
| "settings have not been added" on the sign-in page | Check `frontend/.env.development.local`, then restart the website window. |
| `admin grant` says the user was not found or not verified | Register, verify the email, then sign in once before running it. |
