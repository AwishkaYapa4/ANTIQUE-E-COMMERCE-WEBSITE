# Single image: the ASP.NET Core API also serves the built React app, injecting per-page SEO tags
# (title, description, Open Graph, JSON-LD) into index.html so product/category pages are crawlable.

# ---------- 1. Frontend build ----------
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --ignore-scripts && npm rebuild esbuild
COPY frontend/ ./
# Public Firebase web config is baked into the static bundle (these values are not secrets).
ARG VITE_FIREBASE_API_KEY
ARG VITE_FIREBASE_AUTH_DOMAIN
ARG VITE_FIREBASE_PROJECT_ID
ARG VITE_FIREBASE_APP_ID
ENV VITE_FIREBASE_API_KEY=$VITE_FIREBASE_API_KEY \
    VITE_FIREBASE_AUTH_DOMAIN=$VITE_FIREBASE_AUTH_DOMAIN \
    VITE_FIREBASE_PROJECT_ID=$VITE_FIREBASE_PROJECT_ID \
    VITE_FIREBASE_APP_ID=$VITE_FIREBASE_APP_ID
RUN npx vite build

# ---------- 2. Backend build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src/backend
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/AntiqueStore.slnx ./
COPY backend/AntiqueStore.Domain/AntiqueStore.Domain.csproj AntiqueStore.Domain/
COPY backend/AntiqueStore.Application/AntiqueStore.Application.csproj AntiqueStore.Application/
COPY backend/AntiqueStore.Infrastructure/AntiqueStore.Infrastructure.csproj AntiqueStore.Infrastructure/
COPY backend/AntiqueStore.Api/AntiqueStore.Api.csproj AntiqueStore.Api/
RUN dotnet restore AntiqueStore.Api/AntiqueStore.Api.csproj
COPY backend/ ./
RUN dotnet publish AntiqueStore.Api/AntiqueStore.Api.csproj -c Release -o /app --no-restore

# ---------- 3. Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /src/frontend/dist ./spa
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Frontend__DistPath=spa \
    DOTNET_RUNNING_IN_CONTAINER=true
# Uploads (local storage) and private payment proofs live on a volume; use Cloudinary for images in production.
RUN mkdir -p /app/wwwroot/uploads /app/App_Data && chown -R app:app /app/wwwroot /app/App_Data
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD ["dotnet", "AntiqueStore.Api.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "AntiqueStore.Api.dll"]
