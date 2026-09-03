# Migration Summary: .NET Framework 4.8 → .NET 8

## Status: ✅ Build Succeeds — 0 Errors, 0 Warnings | ✅ 20/20 Tests Pass

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Passed!  - Failed: 0, Passed: 20, Skipped: 0, Total: 20
```

---

## Changes Made (Latest Cycle)

### 1. @Html.Partial → `<partial>` tag helper

`Areas/Admin/Views/Offers/Index.cshtml`: Replaced both `@await Html.PartialAsync("_Paginator", Model)` calls with `<partial name="_Paginator" model="Model" />` — consistent with all other Admin views and eliminates the MVC1000 advisory pattern entirely across the solution.

### 2. Nullable reference types enabled on Bookstore.Domain and Bookstore.Data

Added `<Nullable>enable</Nullable>` to both `Bookstore.Domain/Bookstore.Domain.csproj` and `Bookstore.Data/Bookstore.Data.csproj`. The build produces **0 warnings** after resolving all CS8xxx diagnostics.

**Entity classes** — Used `#pragma warning disable CS8618` around EF Core parameterless constructors (same pattern as the pre-existing `Address.cs`) to keep required string columns non-nullable while satisfying the compiler. Navigation properties that can be unloaded are correctly typed as `T?`.

**Summary of changes across all projects:**

| Category | Fix |
|---|---|
| EF entity empty constructors (`Book`, `Offer`, `Order`, `OrderItem`, `ShoppingCart`, `ShoppingCartItem`, `ReferenceDataItem`) | Added `#pragma warning disable/restore CS8618` around EF parameterless constructors |
| Navigation properties (`Publisher`, `Genre`, `Condition`, `BookType`, `Customer`, `Address`, `Book`, `Order`, `ShoppingCart`) | Changed to `T?` |
| Optional string columns (`CoverImageUrl`, `Summary`, `FrontUrl`, `Comment`) | Changed to `string?` |
| Filter DTOs (`BookFilters.Name/Author`, `OfferFilters.BookName/Author`) | Changed to `string?` (optional search criteria) |
| `Entity.RowVersion` | Changed to `byte[]?` |
| `Customer` all string properties | Changed to `string?` (identity data from auth provider) |
| `DbSecrets` all string properties | Changed to `string?` (JSON-deserialized secrets config) |
| Repository "get by id/key" return types (`GetAsync`, `ListAsync` with sub) | Changed to `T?` throughout all interfaces and implementations |
| `IFileService.SaveAsync/DeleteAsync` | `Stream?`/`string?` parameters; `Task<string?>` return |
| `IImageValidationService.IsSafeAsync` | Changed to `Stream? image` |
| `BookstoreConfiguration.GetSetting/GetConnectionString` | Changed to `string?` return |
| `PaginatedList.source` field | Added `= null!` (EF parameterless constructor pattern) |
| Auth-identity DTO fields (`CustomerSub`, `Username`, `FirstName`, `LastName`, `BookName`, `Author`, `ISBN`) | Changed to `string?` in all affected DTOs |
| `CreateBookDto`/`UpdateBookDto` optional fields | `string? Summary`, `Stream? CoverImage`, `string? CoverImageFileName` |
| `CreateReferenceDataItemDto`/`UpdateReferenceDataItemDto` | `string? Text` |
| `BookResult.ErrorMessage` | Changed to `string?` |
| View model constructors with nullable entity params | Changed `ShoppingCart`, `Order` to `T?`; added null guards |
| View models accessing nullable nav props | Changed to `?.Text`, `?.FullName`, `?.AddressLine1`, `?.Price ?? 0m` etc. |
| `SearchDetailsViewModel` string properties | `PublisherName`, `GenreName`, `TypeName`, `ConditionName` changed to `string?` |
| LINQ filters on nullable strings | Used `filters.Name!` null-forgiving inside lambdas guarded by `IsNullOrWhiteSpace` |
| LINQ on nullable nav props after `Include()` | Used `x.Genre!.Text` null-forgiving pattern |
| Service null guards | Added `if (x == null) return;` in `AddressService`, `ReferenceDataService`, `OfferService`, `ShoppingCartService`, `OrderService` where entity lookups can legitimately return null |
| Controller null guards | Added `return NotFound()` guards in `OrdersController.Details`, `Admin/OrdersController.Details`, `Admin/ReferenceDataController.Update`, `AddressController.Update` |

### 3. Legacy App_Start files deleted

The following files were excluded from compilation in the previous migration cycle and have now been **physically deleted** to reduce confusion for future developers:

| Deleted file | Reason |
|---|---|
| `App_Start/AuthenticationSetup.cs` | Superseded by auth setup in `Program.cs` |
| `App_Start/BundleConfig.cs` | Superseded by static file serving in `Program.cs` |
| `App_Start/ConfigurationSetup.cs` | Superseded by SSM config provider in `Program.cs` |
| `App_Start/DependencyInjectionSetup.cs` | Superseded by DI registrations in `Program.cs` |
| `App_Start/FilterConfig.cs` | Superseded by `AddControllersWithViews(opts.Filters.Add(...))` |
| `App_Start/RouteConfig.cs` | Superseded by `MapControllerRoute` in `Program.cs` |
| `Areas/Admin/AdminAreaRegistration.cs` | Superseded by `[Area("Admin")]` + area route in `Program.cs` |
| `Global.asax.cs` | Superseded by `Program.cs` |
| `Startup.cs` | OWIN startup, superseded by `Program.cs` |

`App_Start/LoggingSetup.cs` is **retained** — it is still compiled and called from `Program.cs` to configure NLog targets.

All now-unnecessary `<Compile Remove>` entries were removed from `Bookstore.Web.csproj`.

---

## Previous Changes (Initial Migration)

### Project Files

| File | Change |
|------|--------|
| `app/Bookstore.Web/Bookstore.Web.csproj` | SDK-style `Microsoft.NET.Sdk.Web`, `net8.0`, `Nullable=enable`. |
| `app/Bookstore.Data/Bookstore.Data.csproj` | `net8.0` + EF Core 8.0 + `Nullable=enable`. Magick.NET 14.16.0. |
| `app/Bookstore.Domain/Bookstore.Domain.csproj` | `net8.0` + `Nullable=enable`. |
| `app/Bookstore.Common/Bookstore.Common.csproj` | `net8.0`. |
| `app/Bookstore.Cdk/Bookstore.Cdk.csproj` | `Amazon.CDK.Lib` 2.200.0. |
| `app/Bookstore.Web.Tests/Bookstore.Web.Tests.csproj` | xUnit 2.6, WebApplicationFactory, EF InMemory, Moq. |
| `Dockerfile` (root) | Linux multi-stage .NET 8 build. |

### Key Migration Highlights

- `Global.asax` + OWIN `Startup.cs` → `Program.cs` (ASP.NET Core entry point)
- `Web.config` → `appsettings.json`
- EF6 (`System.Data.Entity`) → EF Core 8.0 (`Microsoft.EntityFrameworkCore`)
- `System.Web.Mvc` → `Microsoft.AspNetCore.Mvc` across all controllers and view models
- OWIN `OwinMiddleware` → ASP.NET Core `RequestDelegate` middleware
- `HttpPostedFileBase` → `IFormFile`
- `HttpCookie` → `CookieOptions`
- Local and Cognito (OpenIdConnect) authentication migrated
- Linux multi-stage Dockerfile
- CDK EcsStack updated to Linux containers
- All `@Html.Partial` → `<partial>` tag helpers / `@await Html.PartialAsync`

---

## Next Steps

1. **EF Core Migrations**: Run `dotnet ef migrations add InitialCreate` in `Bookstore.Data` for first deployment to a real SQL Server instance.
2. **Cognito HTTPS**: The ECS stack comment notes Cognito Hosted UI requires HTTPS. Add TLS termination (ACM cert + ALB HTTPS listener) before enabling Cognito auth on Fargate.
