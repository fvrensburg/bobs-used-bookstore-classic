# Build stage
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build
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

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Bookstore.Web.dll"]
