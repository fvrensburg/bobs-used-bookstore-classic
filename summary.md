# Migration Summary: .NET Framework 4.8 → .NET 8

## Status
✅ **Build succeeded** — `dotnet build BobsBookstoreClassic.sln` exits with code 0  
✅ **Zero errors, zero warnings** across all 5 projects

---

## Projects Migrated

| Project | Before | After |
|---------|--------|-------|
| `Bookstore.Web` | .NET Framework 4.8 (non-SDK csproj) | `net8.0` (SDK-style, `Microsoft.NET.Sdk.Web`) |
| `Bookstore.Data` | `netstandard2.0` (EF6) | `net8.0` (EF Core 8) |
| `Bookstore.Domain` | `netstandard2.0` | `netstandard2.0` (unchanged) |
| `Bookstore.Common` | `netstandard2.0` | `netstandard2.0` (unchanged) |
| `Bookstore.Cdk` | `net8.0` | `net8.0` (updated CDK packages) |

---

## Key Changes

### Project Files
- Converted `Bookstore.Web.csproj` from legacy verbose format to SDK-style (`Microsoft.NET.Sdk.Web`)
- Replaced `packages.config` + `<Reference>` items with `<PackageReference>` inline
- Removed all `System.Web.*`, OWIN, MVC5, EntityFramework 6, and WebGrease references
- Updated `Bookstore.Data.csproj` from EF6 → EF Core 8 (`Microsoft.EntityFrameworkCore.SqlServer`)

### Application Startup
- Created `Program.cs` — replaces `Global.asax.cs`, `Startup.cs` (OWIN), and all `App_Start/` files
- `RouteConfig`, `FilterConfig`, `BundleConfig`, `DependencyInjectionSetup`, `AuthenticationSetup`, `ConfigurationSetup`, `LoggingSetup` all migrated into `Program.cs`
- AWS SSM Parameter Store loading moved to a static helper at startup

### Configuration
- Created `appsettings.json` from `Web.config`
- `BookstoreConfiguration` re-implemented to bootstrap from `IConfiguration` instead of `ConfigurationManager`
- Connection strings now in `appsettings.json` `ConnectionStrings` section

### Entity Framework: EF6 → EF Core 8
- `ApplicationDbContext` rewritten: constructor uses `DbContextOptions<T>`, `OnModelCreating` uses EF Core Fluent API
- `Database.SetInitializer` + `DropCreateDatabaseIfModelChanges` removed
- Seed data moved to `HasData()` in `OnModelCreating`
- `PaginatedList<T>` updated from `System.Data.Entity` → `Microsoft.EntityFrameworkCore` (`CountAsync`, `ToListAsync`)
- All repositories updated: `System.Data.Entity` → `Microsoft.EntityFrameworkCore`, nested `Include().Select()` → `ThenInclude()`
- `Task.Run(() => dbContext.X.Add(...))` replaced with `await dbContext.X.AddAsync(...)`

### Authentication
- OWIN middleware (`Microsoft.Owin.Security.Cookies` + `Microsoft.Owin.Security.OpenIdConnect`) replaced with ASP.NET Core Authentication
- `LocalAuthenticationMiddleware` rewritten as `IMiddleware` (ASP.NET Core) using `HttpContext.SignInAsync`
- Amazon Cognito OIDC configured via `AddOpenIdConnect`

### Dependency Injection
- Autofac (`Autofac.Integration.Mvc`) replaced with built-in `Microsoft.Extensions.DependencyInjection`
- All services registered with `AddScoped<>`, `AddSingleton<>` patterns
- `InstancePerRequest` → `AddScoped`

### MVC Migration
- `System.Web.Mvc` → `Microsoft.AspNetCore.Mvc` across all controllers
- `ActionResult` return types updated to `IActionResult`
- `[RouteArea]` replaced with `[Area]` attribute on `AdminAreaControllerBase`
- `AdminAreaRegistration.RegisterArea()` removed — routes configured in `Program.cs`
- `HttpContext.GetShoppingCartCorrelationId()` updated from `HttpContextBase` to `Microsoft.AspNetCore.Http.HttpContext`

### Helpers and Models
- `LocalAuthenticationMiddleware`: OWIN → ASP.NET Core `IMiddleware`
- `HttpContextExtensions`: `HttpContextBase`/`HttpCookie` → `Microsoft.AspNetCore.Http.HttpContext`/`CookieOptions`
- `IOwinRequestExtensions`: OWIN → `HttpRequest` extension
- `ControllerExtensions`: updated `Controller` reference
- `ImageTypesAttribute` / `MaxFileSizeAttribute`: `HttpPostedFileBase` → `IFormFile`
- `MvcHelpers`: `HtmlHelper` → `IHtmlHelper`
- All ViewModels with `SelectListItem`: `System.Web.Mvc` → `Microsoft.AspNetCore.Mvc.Rendering`
- `InventoryCreateUpdateViewModel`: `HttpPostedFileBase CoverImage` → `IFormFile CoverImage`
- `InventoryController`: `model.CoverImage?.InputStream` → `model.CoverImage?.OpenReadStream()`

### Views
- `_ViewImports.cshtml` updated with all required `@using` namespaces
- Created `Areas/Admin/Views/_ViewImports.cshtml` (was missing)
- `Html.EnumDropDownListFor()` (MVC5-only) replaced with `Html.DropDownListFor()` + `Html.GetEnumSelectList<T>()`
- Added `Microsoft.AspNetCore.Routing` using for `RouteValueDictionary` in Admin views

### NuGet Packages
- Upgraded `Magick.NET-Q8-AnyCPU` from `14.6.0` → `14.16.0` (resolved ~320 NU19xx vulnerability warnings)
- Upgraded `Amazon.CDK.Lib` from `2.188.0` → `2.268.0` (resolved NU1901 advisory)
- Updated `Constructs` from `10.4.2` → `10.5.0` (required by updated CDK.Lib)
- Fixed `NLog.Web.AspNetCore` version pin from `5.3.16` → `5.4.0` (resolved NU1603)

### Logging
- NLog configuration via `NLog.Web.AspNetCore` (`UseNLog()` in `Program.cs`)
- `AWS.Logger.NLog` retained for CloudWatch logging

---

## Next Steps

1. **Database migration**: Run `dotnet ef migrations add InitialCreate` and `dotnet ef database update` to create/update the database schema after the EF6 → EF Core change. The seed data structure changed to use `HasData()`.

2. **Static files path**: `LocalFileService` now uses `IWebHostEnvironment.WebRootPath` (`wwwroot/`). Ensure the `wwwroot/` directory exists and contains the static content (CSS, images). Previously content was under `Content/`.

3. **Dockerfile**: The existing Windows-container Dockerfile should be updated to use the Linux ASP.NET Core base image: `mcr.microsoft.com/dotnet/aspnet:8.0`.

4. **AssemblyInfo cleanup**: `Properties/AssemblyInfo.cs` files in `Bookstore.Data` and `Bookstore.Domain` contain attributes now auto-generated by the SDK. They are excluded from compilation via `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` — consider deleting them for cleanliness.

5. **Amazon Cognito HTTPS**: The OIDC configuration uses `signin-oidc` as the callback. When deployed, the redirect URI must match what is registered in the Cognito User Pool App Client settings.

6. **CDK stack**: The `BobsUsedBooksClassicECS` CDK stack should be reviewed — the `EcsStack.cs` still references Windows containers. Update the Docker base image to a Linux .NET 8 image for the modernized application.

7. **Views/Web.config**: The legacy `Views/Web.config` and `Areas/Admin/Views/web.config` files are excluded from compilation but still exist on disk. They can be deleted safely.
