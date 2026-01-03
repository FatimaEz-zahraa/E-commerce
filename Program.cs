using AutoMapper;
using E_commerce.Data;
using E_commerce.Models.Entities;
using E_commerce.Models.Mapping;
using E_commerce.Services;
using E_commerce.Services.Cache;
using E_commerce.Services.External;
using E_commerce.Services.Implementations;
using E_commerce.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// =======================================
// CONFIGURATION DES COOKIES ET SESSION
// =======================================
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.Name = ".Ecommerce.Session";
});

// =======================================
// DATABASE
// =======================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// =======================================
// CACHE & HTTP CONTEXT
// =======================================
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

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
    options.SignIn.RequireConfirmedEmail = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// =======================================
// CONFIGURATION DES COOKIES D'AUTHENTIFICATION
// =======================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Ecommerce.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// =======================================
// SERVICES
// =======================================
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICartService, CartService>();

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
    options.Conventions.AllowAnonymousToPage("/TestCart");
    options.Conventions.AllowAnonymousToPage("/TestForm");
    options.Conventions.AuthorizeFolder("/Admin");
})
.AddRazorPagesOptions(options =>
{
    options.Conventions.AddPageRoute("/Cart/Index", "Cart/Index/{handler?}");
});

// =======================================
// CONTROLLERS POUR API
// =======================================
builder.Services.AddControllers();

// =======================================
// CORS POUR LE DÉVELOPPEMENT
// =======================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("https://localhost:7058", "http://localhost:5066")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// =======================================
// BUILD DE L'APPLICATION
// =======================================
var app = builder.Build();

// =======================================
// MIDDLEWARE POUR LOGS DU PANIER
// =======================================
app.Use(async (context, next) =>
{
    if (context.Request.Cookies.TryGetValue("ECOM_CART", out var cartCookie))
    {
        Console.WriteLine($"=== COOKIE PANIER DÉTECTÉ ===");
        Console.WriteLine($"URL: {context.Request.Path}");
        Console.WriteLine($"Method: {context.Request.Method}");
        Console.WriteLine($"Cookie taille: {cartCookie.Length} chars");

        if (cartCookie.Length < 1000)
        {
            Console.WriteLine($"Contenu: {cartCookie}");
        }
    }

    if (context.Request.Path.StartsWithSegments("/Cart/Index") &&
        context.Request.Method == "POST")
    {
        Console.WriteLine($"=== POST VERS CART ===");
        Console.WriteLine($"Query String: {context.Request.QueryString}");

        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        context.Request.Body.Position = 0;
        Console.WriteLine($"Body: {body}");
    }

    await next();
});

// =======================================
// MIDDLEWARE PIPELINE
// =======================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseCors("AllowLocalhost");
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// IMPORTANT: Ordre correct
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapRazorPages();
app.MapControllers();

// =======================================
// ENDPOINTS DE DEBUG
// =======================================

// Test API pour ajouter au panier
app.MapPost("/api/cart/add", async (HttpContext context, [FromServices] ICartService cartService) =>
{
    try
    {
        Console.WriteLine("=== API CART ADD ===");

        // Lire le body
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        Console.WriteLine($"Body: {body}");

        // Parser JSON
        var data = JsonSerializer.Deserialize<CartAddRequest>(body);

        if (data == null)
            return Results.BadRequest("Données invalides");

        // Récupérer userId
        var userId = context.User.Identity?.IsAuthenticated == true
            ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        Console.WriteLine($"API - UserId: {userId ?? "Guest"}, ProductId: {data.ProductId}, Quantity: {data.Quantity}");

        // Ajouter au panier
        await cartService.AddToCartAsync(userId, data.ProductId, data.Quantity);

        return Results.Ok(new
        {
            success = true,
            message = "Produit ajouté au panier",
            productId = data.ProductId
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"API Cart Add Error: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

// Routes disponibles
app.MapGet("/debug/routes", () =>
{
    var routes = new List<string>();

    routes.Add("=== PAGES DISPONIBLES ===");
    routes.Add("GET  /Products/Index");
    routes.Add("GET  /Products/Details/{id}");
    routes.Add("POST /Products/Details/{id}?handler=AddToCart");
    routes.Add("GET  /Cart/Index");
    routes.Add("POST /Cart/Index?handler=AddItem");
    routes.Add("GET  /TestCart");
    routes.Add("GET  /TestForm");
    routes.Add("");
    routes.Add("=== API ENDPOINTS ===");
    routes.Add("POST /api/cart/add");
    routes.Add("GET  /debug/routes");
    routes.Add("GET  /debug/cookie");

    return string.Join("\n", routes);
});

// Debug cookie
app.MapGet("/debug/cookie", (HttpContext context) =>
{
    var result = new
    {
        HasCookie = context.Request.Cookies.ContainsKey("ECOM_CART"),
        CookieValue = context.Request.Cookies["ECOM_CART"],
        CookieLength = context.Request.Cookies["ECOM_CART"]?.Length ?? 0,
        AllCookies = context.Request.Cookies.Keys.ToList()
    };

    return Results.Json(result);
});

// Test simple POST
app.MapPost("/test/post", (HttpContext context) =>
{
    Console.WriteLine("=== TEST POST RECEIVED ===");
    Console.WriteLine($"Headers: {string.Join(", ", context.Request.Headers.Keys)}");

    return Results.Ok(new
    {
        success = true,
        message = "POST reçu avec succès",
        timestamp = DateTime.UtcNow
    });
});

// =======================================
// TEST DE BASE DE DONNÉES AU DÉMARRAGE
// =======================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var productCount = await dbContext.Products.CountAsync(p => p.IsActive);
        Console.WriteLine($"✅ Base de données connectée - Produits actifs: {productCount}");

        var testProduct = await dbContext.Products
            .Where(p => p.IsActive && p.StockQuantity > 0)
            .Select(p => new { p.Id, p.Name, p.Price })
            .FirstOrDefaultAsync();

        if (testProduct != null)
        {
            Console.WriteLine($"✅ Produit de test disponible: {testProduct.Name} ({testProduct.Price:C})");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erreur base de données: {ex.Message}");
    }
}

// =======================================
// MESSAGE DE DÉMARRAGE
// =======================================
Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("🚀 APPLICATION DÉMARRÉE AVEC SUCCÈS");
Console.WriteLine(new string('=', 60));
Console.WriteLine("📋 URLs disponibles:");
Console.WriteLine($"  • Interface principale: https://localhost:7058");
Console.WriteLine($"  • Produits: https://localhost:7058/Products/Index");
Console.WriteLine($"  • Panier: https://localhost:7058/Cart/Index");
Console.WriteLine($"  • Test Cart: https://localhost:7058/TestCart");
Console.WriteLine($"  • Test Form: https://localhost:7058/TestForm");
Console.WriteLine($"  • Debug Routes: https://localhost:7058/debug/routes");
Console.WriteLine($"  • Debug Cookie: https://localhost:7058/debug/cookie");
Console.WriteLine($"  • API Test: https://localhost:7058/api/cart/add");
Console.WriteLine("\n🛒 POUR TESTER LE PANIER:");
Console.WriteLine($"  1. Allez sur /TestForm pour tester les formulaires");
Console.WriteLine($"  2. Utilisez /debug/cookie pour vérifier les cookies");
Console.WriteLine($"  3. Testez avec l'API: POST /api/cart/add");
Console.WriteLine(new string('=', 60));
Console.WriteLine("\n⚠️  Messages d'avertissement Identity (normaux):");
Console.WriteLine("   - 'PreferredBrands/PreferredCategories' sans ValueComparer");
Console.WriteLine("   - Ces warnings n'affectent pas le fonctionnement du panier");
Console.WriteLine(new string('=', 60));

app.Run();

// =======================================
// CLASSES AUXILIAIRES APRÈS app.Run()
// =======================================

// Modèle pour la requête API
public record CartAddRequest(Guid ProductId, int Quantity = 1);

// Service email factice
public class DummyEmailSender : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        Console.WriteLine($"📧 Email simulé envoyé à {email}: {subject}");
        return Task.CompletedTask;
    }
}