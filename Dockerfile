# ──────────────────────────────────────────────────────────────────────────────
# Base images are pinned to specific immutable digests for reproducible Fargate
# deployments.  Refresh digests when patching the .NET 8 runtime:
#   docker buildx imagetools inspect mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim
#   docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim
# ──────────────────────────────────────────────────────────────────────────────

# Build stage — SDK image pinned to .NET 8.0.30-bookworm-slim (June 2026)
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim@sha256:306301580fcaa5b445180e759db59309979002d1000669cb4cf58a567d0014bc AS build
WORKDIR /src

# Restore packages (layer-cached when only csproj files change)
COPY ["app/Bookstore.Common/Bookstore.Common.csproj", "app/Bookstore.Common/"]
COPY ["app/Bookstore.Domain/Bookstore.Domain.csproj",  "app/Bookstore.Domain/"]
COPY ["app/Bookstore.Data/Bookstore.Data.csproj",      "app/Bookstore.Data/"]
COPY ["app/Bookstore.Web/Bookstore.Web.csproj",        "app/Bookstore.Web/"]
RUN dotnet restore "app/Bookstore.Web/Bookstore.Web.csproj" -a amd64

# Copy source and build
COPY . .
WORKDIR /src/app/Bookstore.Web
RUN dotnet build "Bookstore.Web.csproj" -c Release -o /app/build -a amd64

# Publish stage
FROM build AS publish
RUN dotnet publish "Bookstore.Web.csproj" -c Release -o /app/publish --no-restore -a amd64

# Runtime stage — ASP.NET Core image pinned to .NET 8.0.30-bookworm-slim (June 2026)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim@sha256:fd7596eaea7ad453fe7ac16724a3c9ae36edcda894ba13743d6a5c83d6a3b36d AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Bookstore.Web.dll"]
