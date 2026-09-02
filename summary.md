# Migration Summary: .NET Framework 4.8 → .NET 8

## Status
✅ **Build: SUCCEEDED — 0 errors, 4 NuGet advisory warnings (NU1901, low-severity, CDK only)**

## What Was Migrated

### Project Files
| Project | Before | After |
|---|---|---|
| Bookstore.Web | Old-style XML, .NET 4.8 | SDK-style (`Microsoft.NET.Sdk.Web`), `net8.0` |
| Bookstore.Data | SDK-style, `netstandard2.0` | SDK-style, `net8.0` |
| Bookstore.Domain | SDK-style, `netstandard2.0` | SDK-style, `net8.0` |
| Bookstore.Common | SDK-style, `netstandard2.0` | SDK-style, `net8.0` |

### Entity Framework 6 → EF Core 8
- `ApplicationDbContext`: replaced EF6 `DbModelBuilder` with EF Core `ModelBuilder`
  - `HasRequired(...).WillCascadeOnDelete(false)` → `HasOne(...).OnDelete(DeleteBehavior.NoAction)`
  - Constructor changed from `base(connectionString)` to `DbContextOptions<ApplicationDbContext>`
  - `HasColumnType("nvarchar").HasMaxLength(450)` → `HasColumnType("nvarchar(450)")`
  - `DatabaseGeneratedOption.Identity` → `ValueGeneratedOnAdd()`
  - `Database.SetInitializer` removed; replaced with `BookstoreDbInitializer.SeedAsync()`
- `PaginatedList<T>`: updated `using System.Data.Entity` → `using Microsoft.EntityFrameworkCore`
- All repositories: replaced EF6 nested `Select` includes with EF Core `ThenInclude`
  - e.g. `.Include(x => x.OrderItems.Select(y => y.Book))` → `.Include(x => x.OrderItems).ThenInclude(y => y.Book)`
  - String-based includes replaced with lambda expressions
  - `Task.Run(() => dbContext.X.Add(entity))` → `await dbContext.X.AddAsync(entity)`

### OWIN → ASP.NET Core
- `Startup.cs` (OWIN) + `Global.asax` + all `App_Start/*.cs` → **replaced by `Program.cs`**
- OWIN middleware pipeline → ASP.NET Core middleware pipeline
- `OwinMiddleware` → `IMiddleware` (`LocalAuthenticationMiddleware`)
- `IOwinContext` → `HttpContext`
- `HttpContext.Current` → scoped `HttpContext` parameter
- `Microsoft.Owin.Security.*` → `Microsoft.AspNetCore.Authentication.*`
- OWIN OpenIdConnect → `AddOpenIdConnect` with `OpenIdConnectEvents`

### System.Web.Mvc → Microsoft.AspNetCore.Mvc
- All controllers: `ActionResult` → `IActionResult`, `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc`
- `AuthorizeAttribute` global filter → ASP.NET Core `FallbackPolicy` (`RequireAuthenticatedUser`)
- `[RouteArea("Admin")]` → `[Area("Admin")]` on `AdminAreaControllerBase`
- Route registration → `app.MapAreaControllerRoute` + `app.MapControllerRoute` in `Program.cs`
- `AdminAreaRegistration` class no longer needed (area routing now declarative)

### Configuration Migration
- `Web.config` → `appsettings.json` + `appsettings.Development.json`
- `System.Configuration.ConfigurationManager` → `IConfiguration`
- `BookstoreConfiguration`: replaced lazy singleton with static `Initialize(IConfiguration)` method
- Config keys use `:` separator in JSON; translated to `/` for backward compatibility with existing code

### HTTP Helpers
- `HttpContextBase.GetShoppingCartCorrelationId()` → `HttpContext.GetShoppingCartCorrelationId()`
- `HttpCookie` → `Response.Cookies.Append(...)` with `CookieOptions`
- `IOwinRequestExtensions` → `HttpRequestExtensions` (removed Owin dependency)
- `HtmlHelper` → `IHtmlHelper` in `MvcHelpers.cs`

### File Upload (Models)
- `HttpPostedFileBase CoverImage` → `IFormFile CoverImage`
- `file.InputStream` → `file.OpenReadStream()`
- `file.ContentLength` → `file.Length`
- `ImageTypesAttribute` and `MaxFileSizeAttribute` updated accordingly

### ViewModels
- `System.Web.Mvc.SelectListItem` → `Microsoft.AspNetCore.Mvc.Rendering.SelectListItem`
- Updated: `ResaleCreateViewModel`, `AddressCreateUpdateViewModel`, `InventoryCreateUpdateViewModel`, `InventoryIndexViewModel`, `OfferIndexViewModel`, `ReferenceDataCreateViewModel`

### Dependency Injection
- `Autofac.Integration.Mvc` + `Autofac.Integration.Owin` → `Autofac.Extensions.DependencyInjection`
- Registered via `builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())`
- `LocalAuthenticationMiddleware` registered as `IMiddleware` (supports scoped DI per request)

### Logging
- `NLog` + `NLog.AWS.Logger` configured via `Program.cs` using `builder.Host.UseNLog()`

### Views
- `Views/Web.config` and `Areas/Admin/Views/web.config` excluded from compilation (framework-specific)
- `Views/_ViewImports.cshtml` updated with correct ASP.NET Core namespaces and tag helpers
- `Areas/Admin/Views/_ViewImports.cshtml` created
- `Html.EnumDropDownListFor` calls replaced with `<select asp-for asp-items>` tag helpers

### Database Seeding
- `BookstoreDbInitializer` (EF6 `DropCreateDatabaseIfModelChanges`) → static `SeedAsync(ApplicationDbContext)` called at startup

### Packages Removed
- All `System.Web.*` packages
- `Autofac.Mvc5`, `Autofac.Owin`
- `Microsoft.Owin.*`
- `EntityFramework` 6.x
- `Microsoft.AspNet.Mvc`, `Microsoft.AspNet.Web.Optimization`
- `WebGrease`, `Antlr3.Runtime`
- `Microsoft.CodeDom.Providers.DotNetCompilerPlatform`

### Packages Added
- `Microsoft.EntityFrameworkCore.SqlServer` 8.0.14
- `Autofac.Extensions.DependencyInjection` 9.0.0
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` 8.0.15
- `NLog.Web.AspNetCore` 5.3.15

### Package Upgrades
- `Magick.NET-Q8-AnyCPU`: 14.6.0 → 14.16.0 (security vulnerability fixes)

## Next Steps

1. **Static files**: The app references `/Content/css/site.css` etc. Consider migrating static assets to `wwwroot/` directory. The `LocalFileService` now saves to `wwwroot/images/`.
2. **Database migrations**: EF Core migrations should be set up (`dotnet ef migrations add InitialCreate`). Currently uses `EnsureCreated()` which is suitable for development only.
3. **wwwroot setup**: Create a `wwwroot` directory and move CSS/JS/images from `Content/` and `Scripts/` to `wwwroot/`.
4. **Antiforgery tokens**: Review all POST forms for `@Html.AntiForgeryToken()` → tag helper `asp-antiforgery` on forms.
5. **Session state**: The app doesn't use session explicitly, but if needed, `app.UseSession()` should be added.
6. **NuGet advisory**: `Amazon.CDK.Lib 2.188.0` has a low-severity advisory (GHSA-464c-974j-9xm6). Consider updating CDK package when a patched version is available.
7. **Nullable reference types**: Nullable reference types are disabled (`<Nullable>disable</Nullable>`) to ease migration. Consider enabling them incrementally as a follow-up step.
8. **Amazon Cognito HTTPS**: The README notes that Cognito Hosted UI requires HTTPS (except localhost). Ensure HTTPS is configured in production.
