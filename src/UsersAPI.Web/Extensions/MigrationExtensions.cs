using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UsersAPI.Domain;
using UsersAPI.Infra;

namespace UsersAPI.Web.Extensions
{
    public static class MigrationExtensions
    {
        public static async Task ApplyMigrationsAndSeed(this IApplicationBuilder app)
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();

            using AppDbContext dbContext = scope.ServiceProvider.GetService<AppDbContext>();
            await dbContext.Database.MigrateAsync();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            string[] roles = { "Admin", "Manager", "User" };

            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                }
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var adminEmail = "admin@techchallenge.com";

            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Administrar FCG Games",
                };

                await userManager.CreateAsync(adminUser, "##Password940@@");
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

        }
    }
}
