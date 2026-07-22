using Microsoft.AspNetCore.Identity;
using NemesisBakuApi.Entities;

namespace NemesisBakuApi.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        var roleManager =
            serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var userManager =
            serviceProvider.GetRequiredService<UserManager<AppUser>>();

        string[] roles =
        {
            "SuperAdmin",
            "Admin",
            "User"
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var superAdminPhone = "994775131331";
        var superAdminEmail = "superadmin@nemesisbaku.az";
        var superAdminPassword = "Eltac13!13";

        var superAdmin = await userManager.FindByNameAsync(superAdminPhone);

        if (superAdmin == null)
        {
            superAdmin = new AppUser
            {
                FullName = "Eltac Məmmədov",
                UserName = superAdminPhone,
                PhoneNumber = superAdminPhone,
                Email = superAdminEmail,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(superAdmin, superAdminPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    Console.WriteLine("SUPER ADMIN ERROR: " + error.Description);
                }

                return;
            }

            await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");

            Console.WriteLine("SUPER ADMIN CREATED SUCCESSFULLY");
        }
        else
        {
            var rolesOfUser = await userManager.GetRolesAsync(superAdmin);

            if (!rolesOfUser.Contains("SuperAdmin"))
            {
                await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
            }
        }
    }
}