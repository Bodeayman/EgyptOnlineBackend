using Microsoft.AspNetCore.Identity;
using EgyptOnline.Models;
namespace EgyptOnline.Utilities
{
    public static class IdentityExtensions
    {

        public static async Task SeedAdmin(UserManager<User> userManager)
        {
            string adminEmail = "mohamedKamal@gmail.com";
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new User
                {
                    UserName = "admin",
                    Email = adminEmail,
                    FirstName = "Super",
                    LastName = "Admin",
                    Governorate = "Cairo",
                    City = "Cairo"
                };

                var result = await userManager.CreateAsync(admin, "Admin123!Admin123?"); // secure password
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
                await userManager.AddToRoleAsync(admin, Roles.Admin);
        }

        public static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            if (!await roleManager.RoleExistsAsync(Roles.Admin))
                await roleManager.CreateAsync(new IdentityRole(Roles.Admin));

            if (!await roleManager.RoleExistsAsync(Roles.User))
                await roleManager.CreateAsync(new IdentityRole(Roles.User));
        }

    }
}