using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var roleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));

            if (!roleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    roleResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"{roleName} rolu yaradıla bilmədi: {errors}");
            }
        }

        const string superAdminFullName = "Eltac Məmmədov";
        const string superAdminPhone = "994775131331";
        const string superAdminEmail = "eltcmmdv13@mail.ru";

        // Təhlükəsizlik üçün bunu sonradan environment variable-a keçir.
        const string superAdminPassword = "Eltac13!13";

        /*
         * Əvvəl email ilə axtarırıq.
         * Telefon dəyişsə belə SuperAdmin həmin email ilə tapılacaq.
         */
        var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);

        /*
         * Email ilə tapılmasa, SuperAdmin rolundakı mövcud useri axtarırıq.
         * Bu, əvvəlki telefon və ya email dəyişikliklərindən sonra köhnə
         * SuperAdmin hesabını tapmağa kömək edir.
         */
        if (superAdmin == null)
        {
            var superAdmins = await userManager.GetUsersInRoleAsync("SuperAdmin");

            superAdmin = superAdmins
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefault();
        }

        if (superAdmin == null)
        {
            superAdmin = new AppUser
            {
                FullName = superAdminFullName,

                UserName = superAdminPhone,
                PhoneNumber = superAdminPhone,

                Email = superAdminEmail,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,

                IsActive = true,
                IsDeleted = false,

                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(
                superAdmin,
                superAdminPassword);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    createResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"SuperAdmin yaradıla bilmədi: {errors}");
            }

            var addRoleResult = await userManager.AddToRoleAsync(
                superAdmin,
                "SuperAdmin");

            if (!addRoleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    addRoleResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"SuperAdmin rolu verilə bilmədi: {errors}");
            }

            Console.WriteLine("SUPER ADMIN CREATED SUCCESSFULLY");
            return;
        }

        /*
         * Mövcud SuperAdmin tapıldı:
         * ad, nömrə, email və aktivlik məlumatlarını yeniləyirik.
         */
        superAdmin.FullName = superAdminFullName;

        superAdmin.UserName = superAdminPhone;
        superAdmin.PhoneNumber = superAdminPhone;

        superAdmin.Email = superAdminEmail;
        superAdmin.EmailConfirmed = true;
        superAdmin.PhoneNumberConfirmed = true;

        superAdmin.IsActive = true;
        superAdmin.IsDeleted = false;
        superAdmin.UpdatedAt = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(superAdmin);

        if (!updateResult.Succeeded)
        {
            var errors = string.Join(
                ", ",
                updateResult.Errors.Select(x => x.Description));

            throw new InvalidOperationException(
                $"SuperAdmin məlumatları yenilənmədi: {errors}");
        }

        var currentRoles = await userManager.GetRolesAsync(superAdmin);

        if (!currentRoles.Contains("SuperAdmin"))
        {
            var addRoleResult = await userManager.AddToRoleAsync(
                superAdmin,
                "SuperAdmin");

            if (!addRoleResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    addRoleResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"SuperAdmin rolu verilə bilmədi: {errors}");
            }
        }

        /*
         * Şifrə dəyişdirilibsə database-də də yeniləyirik.
         * Hər start zamanı səbəbsiz yerə reset etmir.
         */
        var passwordIsCorrect = await userManager.CheckPasswordAsync(
            superAdmin,
            superAdminPassword);

        if (!passwordIsCorrect)
        {
            var resetToken =
                await userManager.GeneratePasswordResetTokenAsync(superAdmin);

            var resetResult = await userManager.ResetPasswordAsync(
                superAdmin,
                resetToken,
                superAdminPassword);

            if (!resetResult.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    resetResult.Errors.Select(x => x.Description));

                throw new InvalidOperationException(
                    $"SuperAdmin şifrəsi yenilənmədi: {errors}");
            }
        }

        Console.WriteLine("SUPER ADMIN UPDATED SUCCESSFULLY");
    }
}