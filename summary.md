# Migration Summary: .NET Framework 4.8 → .NET 8

## Status: ✅ Build Succeeds — 0 Errors, 2 Warnings | ✅ 20/20 Tests Pass

```
Build succeeded.
    2 Warning(s)   (pre-existing Amazon.CDK.Lib NU1901 — unresolvable upstream)
    0 Error(s)

Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20
```

---

## Changes Made

### Project Files

| File | Change |
|------|--------|
| `app/Bookstore.Web/Bookstore.Web.csproj` | SDK-style `Microsoft.NET.Sdk.Web`, `net8.0`, `Nullable=enable`. Added `Amazon.Extensions.Configuration.SystemsManager 4.0.0` for native SSM integration. Excluded all legacy App_Start files, AssemblyInfo, Global.asax. |
| `app/Bookstore.Data/Bookstore.Data.csproj` | `net8.0` + EF Core 8.0. Magick.NET upgraded to 14.16.0. |
| `app/Bookstore.Domain/Bookstore.Domain.csproj` | `GenerateAssemblyInfo=false` to suppress duplicate attribute errors from existing `AssemblyInfo.cs`. |
| `app/Bookstore.Web.Tests/Bookstore.Web.Tests.csproj` | **New** — xUnit 2.6, WebApplicationFactory, EF InMemory, Moq. |
| `BobsBookstoreClassic.sln` | Updated Bookstore.Web GUID; added Bookstore.Web.Tests project. |
| `Dockerfile` (root) | **Replaced** .NET Framework 4.8 Windows Dockerfile with Linux multi-stage .NET 8 build. |

### New Files

| File | Purpose |
|------|---------|
| `app/Bookstore.Web/Program.cs` | ASP.NET Core entry point: SSM config provider, NLog, DI, EF Core, auth, static files, middleware pipeline, DB seeding. Exposes `public partial class Program` for WebApplicationFactory. |
| `app/Bookstore.Web/appsettings.json` | Settings replacing Web.config. |
| `app/Bookstore.Web/wwwroot/.gitkeep` | `wwwroot/` root; Content/ and Scripts/ served via additional PhysicalFileProviders. |
| `app/Bookstore.Web/Areas/Admin/Views/_ViewImports.cshtml` | Admin area namespace imports. |
| `app/Bookstore.Web.Tests/GlobalUsings.cs` | Global usings for test project. |
| `app/Bookstore.Web.Tests/InMemoryDbFixture.cs` | Shared in-memory EF Core fixture with seed helpers. |
| `app/Bookstore.Web.Tests/Repositories/ReferenceDataRepositoryTests.cs` | 4 repository integration tests. |
| `app/Bookstore.Web.Tests/Repositories/BookRepositoryTests.cs` | 4 repository integration tests (statistics, search, filtering). |
| `app/Bookstore.Web.Tests/Helpers/HelperTests.cs` | 5 unit tests for `ClaimsPrincipalExtensions` and `HttpContextExtensions`. |
| `app/Bookstore.Web.Tests/Middleware/LocalAuthenticationMiddlewareTests.cs` | 4 unit tests for `LocalAuthenticationMiddleware`. |
| `app/Bookstore.Web.Tests/Controllers/HomeControllerIntegrationTests.cs` | 3 WebApplicationFactory integration tests (full pipeline with in-memory DB). |

### Deleted Legacy Files

- `app/Bookstore.Web/Web.config`, `Web.Debug.config`, `Web.Release.config`
- `app/Bookstore.Web/Views/Web.config`
- `app/Bookstore.Web/Areas/Admin/Views/web.config`
- `app/Bookstore.Web/Global.asax`
- `app/Bookstore.Web/packages.config`
- `app/Bookstore.Data/App.config`

### Static Files

`UseStaticFiles()` is called three times in `Program.cs`:
1. Default call — serves `wwwroot/` (ASP.NET Core convention).
2. `/Content` → `Content/` (CSS, images, jQuery libs — original paths preserved in views).
3. `/Scripts` → `Scripts/` (legacy jQuery scripts referenced in `_Layout.cshtml`).

No view URLs were changed. `LocalFileService` saves uploaded images to `Content/Images/coverimages/`, matching where the seed cover images already live.

### Secrets Management

Replaced the manual `ConfigurationSetup.ConfigureConfiguration()` SSM bootstrap with `Amazon.Extensions.Configuration.SystemsManager`. The provider:
- Runs only when `AWS_EXECUTION_ENV` is set **or** any service selector is `"aws"` — local runs skip it entirely.
- Maps SSM parameter `/{AppName}/Key/Sub` → `Key:Sub` in IConfiguration.
- `BookstoreConfiguration.Initialize()` then converts `:` separators to `/`, preserving the existing `GetSetting("Services/Authentication")` call pattern everywhere.
- Configured with `optional: true` and `reloadAfter: 5 minutes`.

`App_Start/ConfigurationSetup.cs` is now excluded from compilation.

### Dockerfile (root)

Replaced the `.NET Framework 4.8 mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2019` pipeline with a Linux multi-stage build:

```
mcr.microsoft.com/dotnet/sdk:8.0 → publish → mcr.microsoft.com/dotnet/aspnet:8.0
```

### CDK EcsStack

Changed `OperatingSystemFamily.WINDOWS_SERVER_2019_CORE` → `OperatingSystemFamily.LINUX`. Changed `ContainerImage.FromAsset(".\\\\"")` → `ContainerImage.FromAsset("./")` (cross-platform path).

### Nullable Reference Types

`<Nullable>enable</Nullable>` is set on `Bookstore.Web`. The codebase is clean — zero CS8xxx warnings after enabling.

### Global Authorization Filter

Fixed `options.Filters.Add(typeof(AuthorizeAttribute))` (which threw at runtime because `AuthorizeAttribute` doesn't implement `IFilterMetadata` directly) → `options.Filters.Add(new AuthorizeFilter())` from `Microsoft.AspNetCore.Mvc.Authorization`.

---

## Remaining Warnings (Cannot Be Resolved Without Upstream Fix)

| Warning | Location | Reason |
|---------|----------|--------|
| `NU1901: Amazon.CDK.Lib 2.188.0 (GHSA-464c-974j-9xm6)` | `Bookstore.Cdk` | Affects all published CDK.Lib versions. Pre-existing; not related to the migration. Monitor AWS CDK for a patched release. |

---

## Next Steps

1. **EF Core Migrations**: Run `dotnet ef migrations add InitialCreate` in `Bookstore.Data` for the first deployment to a real SQL Server instance.
2. **Cognito HTTPS**: The ECS stack comment notes Cognito Hosted UI requires HTTPS. Add a TLS termination (ACM cert + ALB HTTPS listener) to the CDK stack before enabling Cognito auth on Fargate.
3. **CDK NU1901**: Monitor `Amazon.CDK.Lib` releases for a patch to GHSA-464c-974j-9xm6.
4. **`@Html.Partial` → tag helpers**: Admin views use `@Html.Partial("_Paginator")` (MVC1000 advisory). Replace with `<partial name="_Paginator" />` when convenient.


## Status: ✅ Build Succeeds — 0 Errors, 2 Warnings

```
Build succeeded.
    2 Warning(s)
    0 Error(s)
```

---

## Changes Made

### Project Files

| File | Change |
|------|--------|
| `app/Bookstore.Web/Bookstore.Web.csproj` | Replaced legacy MSBuild format with SDK-style `Microsoft.NET.Sdk.Web`, targeting `net8.0`. Removed all old package/assembly references. Added modern NuGet packages. Excluded legacy App_Start files (BundleConfig, FilterConfig, RouteConfig, AuthenticationSetup, DependencyInjectionSetup, AdminAreaRegistration, AssemblyInfo, Global.asax). |
| `app/Bookstore.Data/Bookstore.Data.csproj` | Updated from `netstandard2.0` + EF6 to `net8.0` + EF Core 8.0. Replaced EntityFramework 6.5.1 with Microsoft.EntityFrameworkCore 8.0.0 + SqlServer provider. Upgraded Magick.NET-Q8-AnyCPU from 14.6.0 to 14.16.0 (security fix). |
| `app/Bookstore.Domain/Bookstore.Domain.csproj` | Added `GenerateAssemblyInfo=false` to suppress duplicate attribute error from existing `Properties/AssemblyInfo.cs`. |
| `BobsBookstoreClassic.sln` | Updated Bookstore.Web project type GUID from legacy web GUID to SDK-style GUID. |

### New Files Created

| File | Purpose |
|------|---------|
| `app/Bookstore.Web/Program.cs` | ASP.NET Core entry point replacing Global.asax + OWIN Startup. Configures services, EF Core, authentication (local or Cognito), DI, middleware pipeline, and database seeding. |
| `app/Bookstore.Web/appsettings.json` | Configuration file replacing Web.config. Contains connection strings, service selectors (local/aws), and Cognito settings. |
| `app/Bookstore.Web/Areas/Admin/Views/_ViewImports.cshtml` | Admin area Razor view imports with necessary namespaces for `RouteValueDictionary` and HTML helper extensions. |

### Data Layer (Bookstore.Data)

- **`ApplicationDbContext.cs`**: Migrated from EF6 to EF Core. Changed constructor to accept `DbContextOptions<ApplicationDbContext>`. Replaced `DbModelBuilder` (EF6) with `ModelBuilder` (EF Core). Updated all relationship configurations: `HasRequired → HasOne`, `WillCascadeOnDelete → OnDelete(DeleteBehavior.Restrict)`. Removed `PluralizingTableNameConvention` (EF Core uses singular names by default). Replaced `HasDatabaseGeneratedOption` with `ValueGeneratedOnAdd`.
- **`BookstoreDbInitializer.cs`**: Replaced `DropCreateDatabaseIfModelChanges<T>` with `BookstoreDbSeeder` — a safe static seeder that calls `EnsureCreatedAsync()` and seeds only if tables are empty. Called from `Program.cs`.
- **`BookstoreConfiguration.cs`**: Replaced `System.Configuration.ConfigurationManager` with `IConfiguration`. Added `Initialize(IConfiguration)` method that flattens IConfiguration keys (`:` separator → `/` separator to match existing call sites). Preserves backward-compatible `GetSetting("Services/Authentication")` API.
- **`PaginatedList.cs`**: Replaced `using System.Data.Entity` with `using Microsoft.EntityFrameworkCore`.
- **All repositories** (`BookRepository`, `OrderRepository`, `ShoppingCartRepository`, `OfferRepository`, `AddressRepository`, `CustomerRepository`, `ReferenceDataRepository`):
  - Replaced `using System.Data.Entity` with `using Microsoft.EntityFrameworkCore`
  - Changed `await Task.Run(() => dbSet.Add(entity))` to `await dbSet.AddAsync(entity)`
  - Fixed EF6 include chaining: `.Include(x => x.Items.Select(y => y.Child))` → `.Include(x => x.Items).ThenInclude(y => y.Child)`
  - `OrderRepository.ListBestSellingBooksAsync`: Rewrote as two queries (grouped BookId lookup, then Book fetch) to avoid EF Core translation issues with `x.FirstOrDefault().Book` inside GroupBy Select.
  - Removed `Amazon.Auth.AccessControlPolicy` import from OfferRepository.

### Web Layer (Bookstore.Web)

- **All controllers**: Replaced `using System.Web.Mvc` with `using Microsoft.AspNetCore.Mvc`. Added `using Microsoft.AspNetCore.Authorization` where `[AllowAnonymous]` is used.
- **`AuthenticationController`**: Rewrote cookie operations using `Response.Cookies.Append` with expiry `CookieOptions` instead of `HttpCookie`.
- **`Areas/Admin/Controllers/AdminAreaControllerBase`**: Replaced `[RouteArea("Admin")]` with `[Area("Admin")]` for ASP.NET Core area routing.
- **`Areas/Admin/Controllers/ErrorController`**: Cleaned up commented-out ASP.NET Core code.
- **`Helpers/LocalAuthenticationMiddleware.cs`**: Rewrote from OWIN `OwinMiddleware` to ASP.NET Core `RequestDelegate` pattern. Uses `ICustomerService` injected via method parameter (scoped service from pipeline).
- **`Helpers/HttpContextExtensions.cs`**: Replaced `HttpContextBase` with `HttpContext`. Replaced `HttpCookie` + `Response.Cookies.Add` with `Response.Cookies.Append(key, value, CookieOptions)`.
- **`Helpers/IOwinRequestExtensions.cs`**: Replaced OWIN `IOwinRequest` extension with `HttpRequest` extension (`HttpRequestExtensions`).
- **`Helpers/MvcHelpers.cs`**: Replaced `HtmlHelper` with `IHtmlHelper` for ASP.NET Core compatibility.
- **`Helpers/ControllerExtensions.cs`**: Updated namespace to `Microsoft.AspNetCore.Mvc`.
- **`Helpers/ImageTypesAttribute.cs`**: Replaced `HttpPostedFileBase` with `IFormFile`.
- **`Helpers/MaxFileSizeAttribute.cs`**: Replaced `HttpPostedFileBase` with `IFormFile`; used `file.Length` instead of `file.ContentLength`.
- **`Areas/Admin/Models/Inventory/InventoryCreateUpdateViewModel.cs`**: Replaced `HttpPostedFileBase CoverImage` with `IFormFile CoverImage`.
- **`Areas/Admin/Controllers/InventoryController.cs`**: Changed `model.CoverImage?.InputStream` to `model.CoverImage?.OpenReadStream()`.
- **All ViewModel files** using `SelectListItem`: Replaced `using System.Web.Mvc` with `using Microsoft.AspNetCore.Mvc.Rendering`.
- **`App_Start/ConfigurationSetup.cs`**: Replaced synchronous AWS SSM calls (`client.GetParameter`, `client.GetParametersByPath`) with async calls using `.GetAwaiter().GetResult()` since SSM .NET SDK no longer exposes synchronous overloads.
- **`App_Start/LoggingSetup.cs`**: Replaced `DebuggerTarget` with `ConsoleTarget` (DebuggerTarget not suitable for container/Linux).
- **`Views/_ViewImports.cshtml`**: Added `@using Microsoft.AspNetCore.Mvc.Rendering` and `@using Microsoft.AspNetCore.Routing`.
- **`Areas/Admin/Views/Orders/Index.cshtml`**: Replaced `@Html.EnumDropDownListFor` with `@Html.DropDownListFor` + `Html.GetEnumSelectList<OrderStatus>()`.
- **`Areas/Admin/Views/Offers/Index.cshtml`**: Same fix as above for `OfferStatus`.

---

## Remaining Warnings (Cannot Be Resolved)

| Warning | Location | Reason |
|---------|----------|--------|
| `NU1901: Amazon.CDK.Lib 2.188.0 low severity vulnerability (GHSA-464c-974j-9xm6)` | `Bookstore.Cdk/Bookstore.Cdk.csproj` | Affects all published versions of `Amazon.CDK.Lib`. Upgrading to 2.189.0 does not resolve it. This is a pre-existing CDK library issue not related to the application migration. A fix requires an upstream patch from AWS. |

---

## Next Steps

1. **Database migration**: Run `dotnet ef migrations add InitialCreate` and `dotnet ef database update` in the `Bookstore.Data` project to generate EF Core migrations for the first deployment.
2. **Static files path**: The `LocalFileService` now saves images to `wwwroot/Content/images/coverimages/`. Verify the `Content` directory in `wwwroot` contains the original CSS/image assets (they were previously served from `Content/` root). Consider symlinking or copying static files to `wwwroot/`.
3. **Dockerfile**: Update `Dockerfile` to use `mcr.microsoft.com/dotnet/aspnet:8.0` as the base runtime image and `mcr.microsoft.com/dotnet/sdk:8.0` for the build stage instead of Windows containers.
4. **CDK stack**: The `BobsUsedBooksClassicECS` CDK stack references a Windows container Dockerfile. Update to use the Linux .NET 8 image.
5. **Amazon.CDK.Lib vulnerability (GHSA-464c-974j-9xm6)**: Monitor AWS CDK releases for a patched version.
6. **`@Html.Partial` usage**: Some admin views use `@Html.Partial("_Paginator")`. ASP.NET Core recommends replacing these with `<partial name="_Paginator" />` tag helper or `@await Html.PartialAsync(...)` to avoid potential deadlock warnings. Not a breaking issue but worth addressing.
