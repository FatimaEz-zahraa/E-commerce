using E_commerce.Data;
using E_commerce.Models.Entities;
using E_commerce.Services.External;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace E_commerce.Services
{
    public interface IDataSeederService
    {
        Task SeedAsync(int userCount = 1000, int productsPerCategory = 100, int maxReviewsPerProduct = 20);
    }

    public class DataSeederService : IDataSeederService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<DataSeederService> _logger;
        private readonly IGeminiService? _geminiService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Random _random = new();

        private readonly List<string> _categories = new()
        {
            "Électronique", "Informatique", "Téléphonie", "Maison & Jardin",
            "Mode & Vêtements", "Sport & Loisirs", "Beauté & Santé",
            "Livres & Médias", "Jouets & Jeux", "Automobile"
        };

        // Dictionnaires pour les images par catégorie
        private readonly Dictionary<string, List<string>> _categoryImageTemplates = new()
        {
            ["Électronique"] = new()
            {
                "electronics-phone", "electronics-headphones", "electronics-watch",
                "electronics-tablet", "electronics-speaker", "electronics-camera",
                "electronics-drone", "electronics-gaming", "electronics-vr"
            },
            ["Informatique"] = new()
            {
                "computer-laptop", "computer-mouse", "computer-keyboard",
                "computer-monitor", "computer-printer", "computer-tablet",
                "computer-server", "computer-router", "computer-webcam"
            },
            ["Mode & Vêtements"] = new()
            {
                "fashion-shirt", "fashion-jeans", "fashion-dress",
                "fashion-shoes", "fashion-jacket", "fashion-handbag",
                "fashion-hat", "fashion-glasses", "fashion-jewelry"
            },
            ["Maison & Jardin"] = new()
            {
                "home-furniture", "home-decor", "home-kitchen",
                "home-garden", "home-lighting", "home-bedroom",
                "home-bathroom", "home-tools", "home-cleaning"
            },
            ["Sport & Loisirs"] = new()
            {
                "sport-fitness", "sport-running", "sport-yoga",
                "sport-cycling", "sport-camping", "sport-swimming",
                "sport-tennis", "sport-golf", "sport-skiing"
            },
            ["Beauté & Santé"] = new()
            {
                "beauty-skincare", "beauty-makeup", "beauty-hair",
                "beauty-perfume", "health-vitamins", "health-fitness",
                "health-medical", "health-supplements", "beauty-bath"
            },
            ["Livres & Médias"] = new()
            {
                "media-books", "media-movies", "media-music",
                "media-games", "media-magazines", "media-ebooks",
                "media-audiobooks", "media-instruments", "media-art"
            },
            ["Jouets & Jeux"] = new()
            {
                "toys-actionfigures", "toys-dolls", "toys-puzzles",
                "toys-boardgames", "toys-construction", "toys-educational",
                "toys-outdoor", "toys-vehicles", "toys-stuffed"
            },
            ["Automobile"] = new()
            {
                "auto-car", "auto-motorcycle", "auto-bicycle",
                "auto-parts", "auto-accessories", "auto-tools",
                "auto-care", "auto-electronics", "auto-safety"
            },
            ["Téléphonie"] = new()
            {
                "phone-smartphone", "phone-case", "phone-charger",
                "phone-earbuds", "phone-smartwatch", "phone-tablet",
                "phone-accessories", "phone-screen", "phone-battery"
            }
        };

        // Dictionnaire de marques par catégorie
        private readonly Dictionary<string, List<string>> _brandsByCategory = new()
        {
            ["Électronique"] = new() { "Samsung", "Apple", "Sony", "LG", "Xiaomi", "Huawei", "Google", "Bose", "JBL", "Philips" },
            ["Informatique"] = new() { "Dell", "HP", "Lenovo", "Asus", "Acer", "MSI", "Microsoft", "Apple", "Razer", "Logitech" },
            ["Mode & Vêtements"] = new() { "Zara", "H&M", "Levi's", "Nike", "Adidas", "Puma", "Gucci", "Louis Vuitton", "Chanel", "Prada" },
            ["Maison & Jardin"] = new() { "IKEA", "Home Depot", "Leroy Merlin", "Bosch", "Black & Decker", "Dyson", "Philips", "Samsung", "LG", "Whirlpool" },
            ["Sport & Loisirs"] = new() { "Nike", "Adidas", "Puma", "Reebok", "Under Armour", "Decathlon", "Wilson", "Spalding", "Callaway", "Titleist" },
            ["Beauté & Santé"] = new() { "L'Oréal", "Estée Lauder", "Chanel", "Dior", "Nivea", "Neutrogena", "Pantene", "Garnier", "Maybelline", "Clinique" },
            ["Livres & Médias"] = new() { "Penguin", "HarperCollins", "Simon & Schuster", "Disney", "Warner Bros", "Sony Pictures", "Universal", "Microsoft", "Nintendo", "Sony" },
            ["Jouets & Jeux"] = new() { "LEGO", "Hasbro", "Mattel", "Nintendo", "Sony", "Microsoft", "Bandai", "Playmobil", "Fisher-Price", "VTech" },
            ["Automobile"] = new() { "Toyota", "Ford", "BMW", "Mercedes", "Audi", "Tesla", "Honda", "Hyundai", "Volkswagen", "Peugeot" },
            ["Téléphonie"] = new() { "Apple", "Samsung", "Google", "Xiaomi", "OnePlus", "Motorola", "Nokia", "Sony", "LG", "Huawei" }
        };

        public DataSeederService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<DataSeederService> logger,
            IServiceScopeFactory scopeFactory,
            IGeminiService? geminiService = null)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _scopeFactory = scopeFactory;
            _geminiService = geminiService;
        }

        public async Task SeedAsync(int userCount = 1000, int productsPerCategory = 100, int maxReviewsPerProduct = 20)
        {
            _logger.LogInformation("🚀 Vérification du seeding...");

            // 1️⃣ Rôles (safe)
            await SeedRolesAsync();

            // 2️⃣ Utilisateurs
            List<ApplicationUser> users;
            if (!_context.Users.Any())
            {
                _logger.LogInformation("👤 Aucun utilisateur → seeding users");
                users = await SeedUsersAsync(userCount);
            }
            else
            {
                _logger.LogInformation("✅ Utilisateurs déjà présents → skip");
                users = await _context.Users.ToListAsync();
            }

            // 3️⃣ Produits
            List<Product> products;
            if (!_context.Products.Any())
            {
                _logger.LogInformation("📦 Aucun produit → seeding produits");
                products = await SeedProductsAsync(productsPerCategory);
            }
            else
            {
                _logger.LogInformation("✅ Produits déjà présents → skip");
                products = await _context.Products.ToListAsync();
            }

            // 4️⃣ Commandes
            if (!_context.Orders.Any())
            {
                _logger.LogInformation("🧾 Seeding commandes");
                await SeedOrdersAsync(users, products);
            }
            else
            {
                _logger.LogInformation("✅ Commandes déjà présentes → skip");
            }

            // 5️⃣ Wishlists
            if (!_context.WishlistItems.Any())
            {
                await SeedWishlistsAsync(users, products);
            }

            // 6️⃣ Reviews
            if (!_context.Reviews.Any())
            {
                await SeedReviewsAsync(users, products, maxReviewsPerProduct);
            }

            // 7️⃣ Carts
            if (!_context.Carts.Any())
            {
                await SeedCartsAsync(users, products);
            }

            // 8️⃣ Search history
            if (!_context.UserSearchHistory.Any())
            {
                await SeedSearchHistoryAsync(users);
            }

            _logger.LogInformation("🎉 Seeding vérifié / complété avec succès");
        }


        private async Task SeedRolesAsync()
        {
            var roles = new[] { "Admin", "Customer", "Premium", "Moderator", "VIP" };
            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                    _logger.LogDebug($"Rôle créé: {role}");
                }
            }
        }
        

        private async Task<List<ApplicationUser>> SeedUsersAsync(int count)
        {
            var users = new List<ApplicationUser>();
            var frenchCities = new[] { "Paris", "Lyon", "Marseille", "Toulouse", "Nice", "Nantes", "Strasbourg", "Montpellier", "Bordeaux", "Lille" };

            // Créer l'admin dans le scope principal
            var admin = await CreateAdminUser();
            users.Add(admin);

            // Créer les utilisateurs normaux
            for (int i = 1; i <= count; i++)
            {
                try
                {
                    // Créer un scope pour chaque utilisateur ou par batch
                    using var scope = _scopeFactory.CreateScope();
                    var scopedContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var scopedUserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var scopedRoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                    var user = CreateRandomUser(i, frenchCities);
                    var result = await scopedUserManager.CreateAsync(user, "Password123!");

                    if (result.Succeeded)
                    {
                        await scopedUserManager.AddToRoleAsync(user, "Customer");

                        if (user.AccountTier >= AccountTier.Premium)
                        {
                            await scopedUserManager.AddToRoleAsync(user, "Premium");
                        }

                        if (user.AccountTier >= AccountTier.VIP)
                        {
                            await scopedUserManager.AddToRoleAsync(user, "VIP");
                        }

                        users.Add(user);

                        // Sauvegarder périodiquement
                        if (i % 100 == 0)
                        {
                            await scopedContext.SaveChangesAsync();
                            _logger.LogDebug($"Création utilisateur: {i}/{count}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Erreur création utilisateur {i}: {ex.Message}");
                }
            }

            return users;
        }

        private ApplicationUser CreateRandomUser(int index, string[] cities)
        {
            var firstName = GetRandomFirstName();
            var lastName = GetRandomLastName();
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}{index}@example.com";
            var userName = $"{firstName.ToLower()}.{lastName.ToLower()}{index}";

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = _random.Next(100) < 90,
                PhoneNumber = $"+33{_random.Next(100000000, 999999999):D9}",
                DateOfBirth = DateTime.UtcNow.AddYears(-_random.Next(18, 70)),
                Gender = (Gender)_random.Next(0, 4),
                SubscribeToNewsletter = _random.Next(100) < 60,
                AcceptMarketingEmails = _random.Next(100) < 50,
                PreferredLanguage = _random.Next(100) < 80 ? "fr-FR" : "en-US",
                PreferredCurrency = "EUR",
                CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(1, 730)),
                LastLogin = DateTime.UtcNow.AddDays(-_random.Next(0, 30)),
                AccountStatus = AccountStatus.Active,
                AccountTier = GetRandomAccountTier(),
                LoyaltyPoints = _random.Next(0, 5000),
                TotalAmountSpent = _random.Next(0, 10000),
                TotalOrders = _random.Next(0, 100),
                CustomerType = _random.Next(100) < 5 ? CustomerType.Business : CustomerType.Consumer,
                CompanyName = _random.Next(100) < 5 ? GetRandomCompanyName() : null,
                AverageBudget = _random.Next(100) < 30 ? _random.Next(50, 500) : (decimal?)null,
                RegistrationSource = GetRandomRegistrationSource()
            };

            // Convertir les listes en JSON ou string séparé par des virgules
            user.PreferredCategories = GetRandomCategories(3);
            user.PreferredBrands = GetRandomBrands(5);

            // Adresse de livraison
            if (_random.Next(100) < 80)
            {
                user.ShippingAddress = new Address
                {
                    Street = $"{_random.Next(1, 200)} {GetRandomStreetName()}",
                    City = cities[_random.Next(cities.Length)],
                    PostalCode = $"{_random.Next(10000, 99999)}",
                    Country = "France",
                    IsDefault = true,
                    Label = "Maison",
                    PhoneNumber = user.PhoneNumber,
                    RecipientName = $"{user.FirstName} {user.LastName}"
                };
            }

            return user;
        }

        private async Task<ApplicationUser> CreateAdminUser()
        {
            var adminEmail = "fatimaezzahraahajri1@gmail.com";
            var admin = await _userManager.FindByEmailAsync(adminEmail);

            if (admin != null)
            {
                _logger.LogInformation("✅ Admin déjà existant → skip");
                return admin;
            }

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = "admin",
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "System",
                    EmailConfirmed = true,
                    PhoneNumber = "+33123456789",
                    DateOfBirth = new DateTime(1980, 1, 1),
                    Gender = Gender.Male,
                    SubscribeToNewsletter = false,
                    AcceptMarketingEmails = false,
                    PreferredLanguage = "fr-FR",
                    PreferredCurrency = "EUR",
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow,
                    AccountStatus = AccountStatus.Active,
                    AccountTier = AccountTier.VIP,
                    LoyaltyPoints = 10000,
                    TotalAmountSpent = 0,
                    TotalOrders = 0,
                    CustomerType = CustomerType.Consumer,
                    PreferredCategories = new List<string> { "Électronique", "Informatique" },
                    PreferredBrands = new List<string> { "Apple", "Samsung", "Microsoft" },
                    ShippingAddress = new Address
                    {
                        Street = "123 Rue de l'Administration",
                        City = "Paris",
                        PostalCode = "75000",
                        Country = "France",
                        IsDefault = true,
                        Label = "Bureau",
                        RecipientName = "Administrateur System"
                    }
                };

                var result = await _userManager.CreateAsync(admin, "Admin123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(admin, "Admin");
                    await _userManager.AddToRoleAsync(admin, "Premium");
                    await _userManager.AddToRoleAsync(admin, "VIP");
                }
            }

            return admin!;
        }

        private async Task<List<Product>> SeedProductsAsync(int productsPerCategory)
        {
            var products = new List<Product>();
            int geminiCallCount = 0;
            const int MAX_GEMINI_CALLS = 50;
            if (_context.Products.Any())
            {
                _logger.LogInformation("📦 Produits déjà seedés → arrêt");
                return await _context.Products.ToListAsync();
            }


            // Créer les produits par batch
            foreach (var category in _categories)
            {
                for (int i = 0; i < productsPerCategory; i++)
                {
                    // Vérifier si on peut appeler Gemini
                    bool canCallGemini = _geminiService != null &&
                                       geminiCallCount < MAX_GEMINI_CALLS &&
                                       _random.Next(100) < 20;

                    var product = await CreateProductAsync(category, i, canCallGemini);
                    products.Add(product);

                    if (canCallGemini)
                    {
                        geminiCallCount++;
                    }

                    // Sauvegarder par batch de 100
                    if (products.Count % 100 == 0)
                    {
                        await _context.Products.AddRangeAsync(products);
                        await _context.SaveChangesAsync();
                        _logger.LogDebug($"Produits créés: {products.Count}/{_categories.Count * productsPerCategory}");
                        products.Clear();
                    }
                }
            }

            // Sauvegarder les produits restants
            if (products.Any())
            {
                await _context.Products.AddRangeAsync(products);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation($"🤖 {geminiCallCount} descriptions générées par Gemini");
            return await _context.Products.ToListAsync();
        }

        private string GetRandomBrand(string category)
        {
            if (_brandsByCategory.ContainsKey(category))
            {
                var brands = _brandsByCategory[category];
                return brands[_random.Next(brands.Count)];
            }

            // Fallback si la catégorie n'est pas trouvée
            var allBrands = _brandsByCategory.Values.SelectMany(x => x).Distinct().ToList();
            return allBrands[_random.Next(allBrands.Count)];
        }

        private async Task<Product> CreateProductAsync(string category, int index, bool canCallGemini)
        {
            var template = GetProductTemplate(category);
            var brand = GetRandomBrand(category);
            var productName = $"{brand} {template.name} {index + 1}";

            // Générer la description
            string description;
            if (canCallGemini && _geminiService != null)
            {
                try
                {
                    description = await _geminiService.GenerateProductDescriptionAsync(category, productName);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Gemini échoué: {ex.Message}");
                    description = $"{template.description}. {GetRandomFeatures(category)}";
                }
            }
            else
            {
                description = $"{template.description}. {GetRandomFeatures(category)}";
            }

            // Générer l'URL d'image
            var imageUrl = GenerateProductImageUrl(category, brand, productName, index);

            return new Product
            {
                Id = Guid.NewGuid(),
                Name = productName,
                Description = description,
                Price = Math.Round(template.minPrice + (decimal)_random.NextDouble() * (template.maxPrice - template.minPrice), 2),
                StockQuantity = _random.Next(10, 1000),
                Category = category,
                Brand = brand,
                ImageUrl = imageUrl,
                Rating = Math.Round((decimal)_random.NextDouble() * 4 + 1, 1),
                CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(1, 730)),
                IsActive = true
            };
        }

        private string GenerateProductImageUrl(string category, string brand, string productName, int index)
        {
            productName = productName.ToLower();

            if (productName.Contains("smartphone") || productName.Contains("phone"))
                return "/images/products/electronics/smartphone.jpg";

            if (productName.Contains("laptop") || productName.Contains("ordinateur"))
                return "/images/products/computer/laptop.jpg";

            if (productName.Contains("chaussure") || productName.Contains("sport"))
                return "/images/products/fashion/shoes.jpg";

            if (productName.Contains("jeans"))
                return "/images/products/fashion/jeans.jpg";

            if (productName.Contains("canapé"))
                return "/images/products/home/sofa.jpg";

            if (productName.Contains("parfum"))
                return "/images/products/beauty/perfume.jpg";

            return "/images/products/default.jpg";
        }


        private async Task<List<Order>> SeedOrdersAsync(List<ApplicationUser> users, List<Product> products)
        {
            var orders = new List<Order>();
            var usersWithOrders = users.Where(_ => _random.Next(100) < 70).ToList();

            foreach (var user in usersWithOrders)
            {
                int orderCount = _random.Next(1, 11);
                for (int i = 0; i < orderCount; i++)
                {
                    var order = await CreateOrderAsync(user, products);
                    orders.Add(order);
                }

                // Mettre à jour les stats utilisateur
                user.TotalOrders = orderCount;
                user.TotalAmountSpent = orders.Where(o => o.UserId == user.Id).Sum(o => o.TotalAmount);
                user.LoyaltyPoints = (int)(user.TotalAmountSpent / 10);
            }

            // Sauvegarder par batch
            var batchSize = 50;
            for (int i = 0; i < orders.Count; i += batchSize)
            {
                var batch = orders.Skip(i).Take(batchSize).ToList();
                await _context.Orders.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            return orders;
        }

        private async Task<Order> CreateOrderAsync(ApplicationUser user, List<Product> products)
        {
            var orderDate = DateTime.UtcNow.AddDays(-_random.Next(1, 365));
            var orderItems = new List<OrderItem>();

            int itemCount = _random.Next(1, 9);
            var selectedProducts = products.OrderBy(_ => _random.Next()).Take(itemCount).ToList();

            decimal subtotal = 0;
            foreach (var product in selectedProducts)
            {
                int quantity = _random.Next(1, 4);
                decimal totalPrice = product.Price * quantity;
                subtotal += totalPrice;

                orderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = quantity,
                    TotalPrice = totalPrice,
                    ProductCategory = product.Category,
                    ProductBrand = product.Brand,
                    ProductRating = product.Rating
                });
            }

            var shippingCost = _random.Next(0, 20);
            var tax = Math.Round(subtotal * 0.2m, 2);
            var totalAmount = subtotal + shippingCost + tax;

            var order = new Order
            {
                UserId = user.Id,
                OrderNumber = $"ORD-{orderDate:yyyyMMdd}-{_random.Next(10000, 99999)}",
                OrderDate = orderDate,
                Status = GetRandomOrderStatus(orderDate),
                Subtotal = subtotal,
                ShippingCost = shippingCost,
                Tax = tax,
                TotalAmount = totalAmount,
                ShippingAddress = user.ShippingAddress ?? new Address(),
                PaymentMethod = GetRandomPaymentMethod(),
                PaymentStatus = PaymentStatus.Paid,
                ShippingMethod = GetRandomShippingMethod(),
                TrackingNumber = _random.Next(100) < 70 ? $"TRACK{_random.Next(100000000, 999999999)}" : null,
                ShippedDate = GetRandomShippedDate(orderDate),
                DeliveredDate = GetRandomDeliveredDate(orderDate),
                Notes = _random.Next(100) < 20 ? "Livraison rapide demandée" : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Ajouter l'ordre d'abord pour avoir un ID
            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();

            // Ajouter les OrderItems avec l'ID de l'ordre
            foreach (var item in orderItems)
            {
                item.OrderId = order.Id;
            }
            await _context.OrderItems.AddRangeAsync(orderItems);
            await _context.SaveChangesAsync();

            return order;
        }

        private async Task<List<WishlistItem>> SeedWishlistsAsync(List<ApplicationUser> users, List<Product> products)
        {
            var wishlistItems = new List<WishlistItem>();
            var usersWithWishlist = users.Where(_ => _random.Next(100) < 60).ToList();

            foreach (var user in usersWithWishlist)
            {
                int wishlistItemCount = _random.Next(1, 20);
                var selectedProducts = products.OrderBy(_ => _random.Next()).Take(wishlistItemCount).ToList();

                foreach (var product in selectedProducts)
                {
                    var wishlistItem = new WishlistItem
                    {
                        UserId = user.Id,
                        ProductId = product.Id,
                        AddedDate = DateTime.UtcNow.AddDays(-_random.Next(0, 180)),
                        IsActive = true,
                        Priority = _random.Next(100) < 30 ? _random.Next(1, 6) : null,
                        Notes = _random.Next(100) < 20 ? "À acheter prochainement" : null,
                        LastViewed = _random.Next(100) < 50 ? DateTime.UtcNow.AddDays(-_random.Next(0, 30)) : (DateTime?)null,
                        ViewCount = _random.Next(0, 10)
                    };
                    wishlistItems.Add(wishlistItem);
                }
            }

            // Sauvegarder par batch
            var batchSize = 100;
            for (int i = 0; i < wishlistItems.Count; i += batchSize)
            {
                var batch = wishlistItems.Skip(i).Take(batchSize).ToList();
                await _context.WishlistItems.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            return wishlistItems;
        }

        private async Task<List<Review>> SeedReviewsAsync(List<ApplicationUser> users, List<Product> products, int maxReviewsPerProduct)
        {
            var reviews = new List<Review>();

            foreach (var product in products)
            {
                int reviewCount = _random.Next(0, maxReviewsPerProduct + 1);
                var reviewers = users.OrderBy(_ => _random.Next()).Take(reviewCount).ToList();

                foreach (var user in reviewers)
                {
                    var review = new Review
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        UserId = user.Id,
                        Rating = GetRandomRating(product.Rating),
                        Comment = GetRandomReviewComment(),
                        CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(0, 365)),
                        IsVerifiedPurchase = _random.Next(100) < 70,
                        UpdatedAt = _random.Next(100) < 20 ? DateTime.UtcNow.AddDays(-_random.Next(0, 30)) : (DateTime?)null
                    };
                    reviews.Add(review);
                }
            }

            // Sauvegarder par batch
            var batchSize = 200;
            for (int i = 0; i < reviews.Count; i += batchSize)
            {
                var batch = reviews.Skip(i).Take(batchSize).ToList();
                await _context.Reviews.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            return reviews;
        }

        private async Task<List<Cart>> SeedCartsAsync(List<ApplicationUser> users, List<Product> products)
        {
            var carts = new List<Cart>();
            var usersWithCart = users.Where(_ => _random.Next(100) < 40).ToList();

            foreach (var user in usersWithCart)
            {
                var cart = new Cart
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(0, 30)),
                    UpdatedAt = DateTime.UtcNow.AddHours(-_random.Next(0, 24))
                };
                carts.Add(cart);

                // Ajouter des items au panier
                int itemCount = _random.Next(1, 6);
                var selectedProducts = products.OrderBy(_ => _random.Next()).Take(itemCount).ToList();

                foreach (var product in selectedProducts)
                {
                    var cartItem = new CartItem
                    {
                        Id = Guid.NewGuid(),
                        CartId = cart.Id,
                        ProductId = product.Id,
                        Quantity = _random.Next(1, 4),
                        AddedAt = DateTime.UtcNow.AddHours(-_random.Next(0, 24)),
                        UpdatedAt = DateTime.UtcNow.AddHours(-_random.Next(0, 24))
                    };
                    await _context.CartItems.AddAsync(cartItem);
                }
            }

            await _context.Carts.AddRangeAsync(carts);
            await _context.SaveChangesAsync();
            return carts;
        }

        private async Task<List<UserSearchHistory>> SeedSearchHistoryAsync(List<ApplicationUser> users)
        {
            var searchHistory = new List<UserSearchHistory>();
            var usersWithHistory = users.Where(_ => _random.Next(100) < 80).ToList();

            foreach (var user in usersWithHistory)
            {
                int searchCount = _random.Next(1, 15);
                for (int i = 0; i < searchCount; i++)
                {
                    var search = new UserSearchHistory
                    {
                        UserId = user.Id,
                        SearchQuery = GetRandomSearchQuery(),
                        Category = _random.Next(100) < 60 ? _categories[_random.Next(_categories.Count)] : string.Empty,
                        SearchedAt = DateTime.UtcNow.AddDays(-_random.Next(0, 90)),
                        ResultCount = _random.Next(0, 500),
                        ClickedResult = _random.Next(100) < 70
                    };
                    searchHistory.Add(search);
                }
            }

            // Sauvegarder par batch
            var batchSize = 300;
            for (int i = 0; i < searchHistory.Count; i += batchSize)
            {
                var batch = searchHistory.Skip(i).Take(batchSize).ToList();
                await _context.UserSearchHistory.AddRangeAsync(batch);
                await _context.SaveChangesAsync();
            }

            return searchHistory;
        }

        // Helper methods
        private string GetRandomFirstName()
        {
            var firstNames = new[] { "Jean", "Marie", "Pierre", "Julie", "Thomas", "Sophie", "David", "Laura", "Nicolas", "Sarah", "Alexandre", "Emma", "Antoine", "Chloé", "Maxime", "Léa" };
            return firstNames[_random.Next(firstNames.Length)];
        }

        private string GetRandomLastName()
        {
            var lastNames = new[] { "Martin", "Bernard", "Dubois", "Thomas", "Robert", "Richard", "Petit", "Durand", "Leroy", "Moreau", "Simon", "Laurent", "Lefebvre", "Michel", "Garcia" };
            return lastNames[_random.Next(lastNames.Length)];
        }

        private string GetRandomCompanyName()
        {
            var companies = new[] { "TechnoCorp", "Global Solutions", "Innovatech", "ProService", "BusinessPlus", "Elite Group", "Prime Systems", "Future Tech", "Alpha Industries", "Omega Corp" };
            return companies[_random.Next(companies.Length)];
        }

        private AccountTier GetRandomAccountTier()
        {
            var weights = new[] { 70, 20, 8, 2 }; // Standard, Premium, Gold, Platinum, VIP
            var cumulative = 0;
            var randomValue = _random.Next(100);

            var tiers = new[] { AccountTier.Standard, AccountTier.Premium, AccountTier.Gold, AccountTier.Platinum };

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (randomValue < cumulative)
                    return tiers[i];
            }
            return AccountTier.Standard;
        }

        private List<string> GetRandomCategories(int count)
        {
            return _categories.OrderBy(_ => _random.Next()).Take(count).ToList();
        }

        private List<string> GetRandomBrands(int count)
        {
            var allBrands = _brandsByCategory.Values.SelectMany(x => x).Distinct().ToList();
            return allBrands.OrderBy(_ => _random.Next()).Take(count).ToList();
        }

        private string GetRandomRegistrationSource()
        {
            var sources = new[] { "Website", "MobileApp", "SocialMedia", "Email", "Referral" };
            return sources[_random.Next(sources.Length)];
        }

        private (string name, string description, decimal minPrice, decimal maxPrice) GetProductTemplate(string category)
        {
            var templates = new Dictionary<string, List<(string, string, decimal, decimal)>>
            {
                ["Électronique"] = new() {
                    ("Smartphone Pro", "Téléphone haut de gamme écran AMOLED", 699.99m, 1499.99m),
                    ("Écouteurs Sans Fil", "Réduction de bruit active", 129.99m, 449.99m),
                    ("Montre Connectée", "Suivi santé et notifications", 199.99m, 799.99m)
                },
                ["Informatique"] = new() {
                    ("Laptop Gaming", "Processeur puissant et carte graphique dédiée", 899.99m, 2499.99m),
                    ("Souris Ergonomique", "Précision et confort pour longue session", 29.99m, 129.99m),
                    ("Écran 4K", "Affichage haute résolution pour créateurs", 299.99m, 999.99m)
                },
                ["Mode & Vêtements"] = new() {
                    ("Jeans Slim", "Coupe moderne et confortable", 49.99m, 129.99m),
                    ("Robe d'été", "Légère et élégante", 39.99m, 199.99m),
                    ("Chaussures de Sport", "Confort et performance", 59.99m, 179.99m)
                },
                ["Maison & Jardin"] = new() {
                    ("Canapé 3 places", "Confort et design moderne", 499.99m, 1499.99m),
                    ("Kit de Jardinage", "Outils essentiels pour entretien", 29.99m, 129.99m),
                    ("Robot Aspirateur", "Nettoyage automatique intelligent", 199.99m, 699.99m)
                },
                ["Sport & Loisirs"] = new() {
                    ("Vélo de Course", "Léger et performant", 299.99m, 1999.99m),
                    ("Tapis de Yoga", "Antidérapant et confortable", 19.99m, 79.99m),
                    ("Set de Golf", "Clubs complets pour débutants", 149.99m, 799.99m)
                },
                ["Beauté & Santé"] = new() {
                    ("Crème Hydratante", "Soin quotidien intensif", 19.99m, 89.99m),
                    ("Parfum Élégant", "Fragrance durable et raffinée", 49.99m, 299.99m),
                    ("Shampoing Revitalisant", "Soin capillaire professionnel", 9.99m, 49.99m)
                },
                ["Livres & Médias"] = new() {
                    ("Roman Bestseller", "Histoire captivante et émouvante", 14.99m, 29.99m),
                    ("Album Musical", "Collection des plus grands hits", 9.99m, 24.99m),
                    ("Jeu Vidéo", "Aventure interactive et immersive", 39.99m, 79.99m)
                },
                ["Jouets & Jeux"] = new() {
                    ("Jeu de Construction", "Créativité et motricité fine", 29.99m, 149.99m),
                    ("Poupée Interactive", "Jouet éducatif et amusant", 19.99m, 99.99m),
                    ("Puzzle 1000 pièces", "Challenge et détente", 14.99m, 49.99m)
                },
                ["Automobile"] = new() {
                    ("Kit d'Entretien", "Produits de soin automobile", 24.99m, 99.99m),
                    ("Accessoire Intérieur", "Confort et style pour voiture", 9.99m, 79.99m),
                    ("Outillage", "Équipement professionnel", 39.99m, 299.99m)
                },
                ["Téléphonie"] = new() {
                    ("Étui de Protection", "Protection optimale pour smartphone", 9.99m, 49.99m),
                    ("Chargeur Rapide", "Recharge efficace et sûre", 14.99m, 39.99m),
                    ("Power Bank", "Autonomie mobile prolongée", 19.99m, 89.99m)
                }
            };

            if (templates.ContainsKey(category) && templates[category].Any())
                return templates[category][_random.Next(templates[category].Count)];

            return ($"Produit {category}", "Description générique", 19.99m, 299.99m);
        }

        private string GetRandomFeatures(string category)
        {
            var featuresByCategory = new Dictionary<string, List<string>>
            {
                ["Électronique"] = new() { "Haute performance", "Batterie longue durée", "Écran haute résolution", "Connectivité avancée" },
                ["Informatique"] = new() { "Processeur rapide", "Mémoire élevée", "Stockage SSD", "Design ergonomique" },
                ["Mode & Vêtements"] = new() { "Matériau de qualité", "Confort optimal", "Design moderne", "Entretien facile" },
                ["Maison & Jardin"] = new() { "Durable", "Facile à installer", "Économique", "Écologique" },
                ["Sport & Loisirs"] = new() { "Performance", "Confort", "Sécurité", "Polyvalence" },
                ["Beauté & Santé"] = new() { "Naturel", "Efficace", "Doux", "Dermatologiquement testé" },
                ["Livres & Médias"] = new() { "Captivant", "Éducatif", "Divertissant", "Instructif" },
                ["Jouets & Jeux"] = new() { "Éducatif", "Sécuritaire", "Amusant", "Développe la créativité" },
                ["Automobile"] = new() { "Robuste", "Fiable", "Performant", "Facile à utiliser" },
                ["Téléphonie"] = new() { "Compact", "Efficace", "Compatible", "Durable" }
            };

            if (featuresByCategory.ContainsKey(category))
            {
                var features = featuresByCategory[category];
                return string.Join(", ", features.OrderBy(_ => _random.Next()).Take(3));
            }

            return "Qualité garantie, durable et fiable";
        }

        private OrderStatus GetRandomOrderStatus(DateTime orderDate)
        {
            var now = DateTime.UtcNow;
            var daysSinceOrder = (now - orderDate).Days;

            if (daysSinceOrder < 1) return OrderStatus.Pending;
            if (daysSinceOrder < 2) return OrderStatus.Processing;
            if (daysSinceOrder < 5) return OrderStatus.Shipped;
            if (daysSinceOrder < 10) return OrderStatus.Delivered;
            if (_random.Next(100) < 5) return OrderStatus.Cancelled;
            return OrderStatus.Delivered;
        }

        private string GetRandomPaymentMethod()
        {
            var methods = new[] { "Credit Card", "PayPal", "Bank Transfer", "Apple Pay", "Google Pay" };
            return methods[_random.Next(methods.Length)];
        }

        private string GetRandomShippingMethod()
        {
            var methods = new[] { "Standard", "Express", "Next Day", "International", "Pickup" };
            return methods[_random.Next(methods.Length)];
        }

        private DateTime? GetRandomShippedDate(DateTime orderDate)
        {
            return _random.Next(100) < 80 ? orderDate.AddDays(_random.Next(1, 3)) : (DateTime?)null;
        }

        private DateTime? GetRandomDeliveredDate(DateTime orderDate)
        {
            return _random.Next(100) < 70 ? orderDate.AddDays(_random.Next(3, 10)) : (DateTime?)null;
        }

        private int GetRandomRating(decimal productRating)
        {
            // Génère une note cohérente avec la note moyenne du produit
            var baseRating = (int)Math.Round(productRating);
            var variation = _random.Next(-1, 2);
            return Math.Clamp(baseRating + variation, 1, 5);
        }

        private string GetRandomReviewComment()
        {
            var comments = new[]
            {
                "Je suis très satisfait de mon achat, le produit est de bonne qualité.",
                "Correspond parfaitement à la description, je le recommande.",
                "Livraison rapide et emballage soigné, merci!",
                "Le produit est correct pour le prix, mais rien d'exceptionnel.",
                "Fonctionne très bien, facile à utiliser.",
                "Un peu déçu par la qualité, je m'attendais à mieux.",
                "Excellent rapport qualité-prix, je rachèterai sans hésiter.",
                "Bien mais pourrait être amélioré sur certains points.",
                "Parfait pour mon utilisation quotidienne."
            };
            return comments[_random.Next(comments.Length)];
        }

        private string GetRandomSearchQuery()
        {
            var queries = new[]
            {
                "smartphone", "ordinateur portable", "vêtements", "meubles",
                "livres", "jouets", "cosmétiques", "sport", "jardinage",
                "électronique", "mode", "maison", "loisirs", "high-tech"
            };
            return queries[_random.Next(queries.Length)];
        }

        private string GetRandomStreetName()
        {
            var streets = new[]
            {
                "de la République", "de Paris", "Victor Hugo", "Jean Jaurès", "Gambetta",
                "de la Gare", "Pasteur", "Curie", "de la Paix", "des Lilas",
                "du Château", "des Roses", "de la Mairie", "St Michel", "de Lyon"
            };
            return streets[_random.Next(streets.Length)];
        }
    }
}