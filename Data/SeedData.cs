using E_commerce.Models.Entities;
using E_commerce.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace E_commerce.Data;

public static class SeedData
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = new[] { "Admin", "Customer" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // Créer un admin par défaut
        var adminEmail = "fatimaezzahraahajari1@gmail.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser { UserName = "admin", Email = adminEmail };
            await userManager.CreateAsync(admin, "Admin123!");
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
