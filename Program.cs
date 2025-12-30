using AutoMapper;
using E_commerce.Data;
using E_commerce.Models.Entities;
using E_commerce.Models.Mapping;
using E_commerce.Services;
using E_commerce.Services.Cache;
using E_commerce.Services.External;
using E_commerce.Services.Implementations;
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// =======================================
// DATABASE
// =======================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// =======================================
// CACHE
// =======================================
builder.Services.AddMemoryCache();

// =======================================
// IDENTITY
// =======================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// =======================================
// SERVICES
// =======================================

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

// Review Service
builder.Services.AddScoped<IReviewService, ReviewService>();

// Cart Service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IHttpCartService, HttpCartService>();

// Product Service avec cache
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<IProductService>(provider =>
{
    var inner = provider.GetRequiredService<ProductService>();
    var cache = provider.GetRequiredService<IMemoryCache>();
    return new CachedProductService(inner, cache);
});

// IMAGES EXTERNES
builder.Services.AddHttpClient<ImageSearchService>();
builder.Services.AddScoped<ProductImageUpdateService>();
builder.Services.AddHttpClient<ImageService>();

// =======================================
// EMAIL
// =======================================
builder.Services.AddSingleton<IEmailSender, DummyEmailSender>();

// =======================================
// RAZOR PAGES
// =======================================
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Cart/Index");
    options.Conventions.AllowAnonymousToPage("/Products/Index");
    options.Conventions.AllowAnonymousToPage("/Products/Details");
    options.Conventions.AuthorizeFolder("/Admin");
});

// =======================================
// BUILD
// =======================================
var app = builder.Build();

// =======================================
// MIDDLEWARE
// =======================================
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// =======================================
// 🔥 MISE À JOUR DES IMAGES (UNE SEULE FOIS)
// =======================================
/*using (var scope = app.Services.CreateScope())
{
    try
    {
        var updater = scope.ServiceProvider
            .GetRequiredService<ProductImageUpdateService>();

        await updater.UpdateAllImagesAsync();

        Console.WriteLine("✅ Images produits mises à jour avec succès");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erreur MAJ images : {ex.Message}");
    }
}*/

// =======================================
// RUN
// =======================================
Console.WriteLine("🚀 Application démarrée");
app.Run();

// =======================================
// CLASSE EMAIL SECOURS
// =======================================
public class DummyEmailSender : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        return Task.CompletedTask;
    }
}
