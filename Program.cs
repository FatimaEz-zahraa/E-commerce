using AutoMapper;
using E_commerce.Data;
using E_commerce.Models.Entities;
using E_commerce.Models.Mapping;
using E_commerce.Services;
using E_commerce.Services.Cache;
using E_commerce.Services.External;
using E_commerce.Services.Implementations;
using E_commerce.Services.Interfaces;
using E_commerce.Services.Rag;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// =======================================
// COOKIES & SESSION
// =======================================
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = _ => false;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
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
// REDIS CACHE
// =======================================
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "ECommerce:";
});

// =======================================
// SERVICES
// =======================================
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<DataSeederService>();

// Product Service + Cache Redis
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.Decorate<IProductService, CachedProductService>();

builder.Services.AddHttpContextAccessor();

// =======================================
// SERVICES GEMINI - CONFIGURATION CORRECTE
// =======================================

// Configuration Gemini
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));

// HttpClient pour Gemini
builder.Services.AddHttpClient("Gemini", client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "ECommerce-App/1.0");
});

// Vérification et enregistrement du service Gemini
var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
var isGeminiEnabled = builder.Configuration.GetValue<bool>("Gemini:Enabled", true);

if (!string.IsNullOrEmpty(geminiApiKey) && isGeminiEnabled)
{
    builder.Services.AddSingleton<E_commerce.Services.GeminiService>(); // Singleton pour permettre l'utilisation par VectorProductIndexService
    Console.WriteLine("✅ Gemini Service activé");
}
else
{
    builder.Services.AddScoped<FallbackAssistantService>();
    Console.WriteLine("⚠️ Gemini Service désactivé - Fallback activé");
}

// Services RAG
builder.Services.AddScoped<ProductKnowledgeService>();
builder.Services.AddSingleton<VectorProductIndexService>(); // Singleton pour garder l'index en mémoire
builder.Services.AddScoped<IRagService, RagService>();

// =======================================
// IMAGES EXTERNES
// =======================================
builder.Services.AddHttpClient<ImageSearchService>();
builder.Services.AddHttpClient<ImageService>();
builder.Services.AddScoped<ProductImageUpdateService>();

// =======================================
// IDENTITY
// =======================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Ecommerce.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

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

builder.Services.AddControllers();

// =======================================
// CORS (DEV)
// =======================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("https://localhost:7058", "http://localhost:5066")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// =======================================
// BUILD
// =======================================
var app = builder.Build();

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

app.UseCookiePolicy();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
// Redirection de l'URL racine vers Products/Index
app.MapGet("/", () => Results.Redirect("/Products/Index"));
app.MapGet("/Index", () => Results.Redirect("/Products/Index"));
app.MapControllers(); // CEci mappe tous les contrôleurs API

// =======================================
// API CART (Endpoint minimal)
// =======================================
app.MapPost("/api/cart/add", async (
    HttpContext context,
    [FromServices] ICartService cartService) =>
{
    var data = await JsonSerializer.DeserializeAsync<CartAddRequest>(context.Request.Body);

    if (data == null)
        return Results.BadRequest("Invalid data");

    var userId = context.User.Identity?.IsAuthenticated == true
        ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        : null;

    await cartService.AddToCartAsync(userId, data.ProductId, data.Quantity);

    return Results.Ok(new { success = true });
});



// =======================================
// SEEDING & VECTOR INDEX BUILD
// =======================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeederService>();
        await seeder.SeedAsync(forceReseed: false);

        // Construire l'index vectoriel en arrière-plan
        var vectorIndex = scope.ServiceProvider.GetRequiredService<VectorProductIndexService>();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
        
        _ = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine("🚀 Construction de l'index vectoriel en arrière-plan...");
                var products = await productService.GetAllAsync();
                await vectorIndex.BuildIndexAsync(products, forceRebuild: false);
                Console.WriteLine("✅ Index vectoriel prêt!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur index vectoriel: {ex.Message}");
            }
        });
    }
}


// =======================================
app.Run();

// =======================================
// SUPPORT CLASSES
// =======================================
public record CartAddRequest(Guid ProductId, int Quantity = 1);

public class DummyEmailSender : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
        => Task.CompletedTask;
}

// Configuration Gemini
public class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-1.5-flash-latest"; // Modèle gratuit recommandé
    public decimal Temperature { get; set; } = 0.3m;
    public int MaxTokens { get; set; } = 1000;
    public bool Enabled { get; set; } = true;
}