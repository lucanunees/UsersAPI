using Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using UsersAPI.Domain;
using UsersAPI.Web.Services;

namespace UsersAPI.Web.Endpoints
{
    public static class UserEndpoints
    {
        public static async void MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/users").WithTags("Users");

            group.MapPost("/register", async (
                RegisterRequest request,
                UserManager<User> userManager,
                IPublishEndpoint publishEndpoint,
                CancellationToken ct) =>
                {

                    var user = new User
                    {
                        UserName = request.Email,
                        Email = request.Email,
                        FullName = request.FullName,
                        CreatedAt = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(user, request.Password);

                    if (!result.Succeeded)
                    {
                        return Results.BadRequest(result.Errors);
                    }

                    var roleToAssign = !string.IsNullOrEmpty(request.Role) ? request.Role : "User";
                    await userManager.AddToRoleAsync(user, roleToAssign);

                    await publishEndpoint.Publish(new UserCreatedEventV1(Guid.NewGuid(), DateTime.Now, user.Id, user.Email, user.FullName), ct);

                    return Results.Created($"/api/users/{user.Id}", new
                    {
                        user.Id,
                        user.Email,
                        user.FullName,
                        Message = "Usuário registrado com sucesso"
                    });

                });

            group.MapPost("/login", async (
                    LoginRequest login,
                    UserManager<User> userManager,
                    TokenService tokenService) =>
            {
                var user = await userManager.FindByEmailAsync(login.Email);
                if (user != null && await userManager.CheckPasswordAsync(user, login.Password))
                {
                    var roles = await userManager.GetRolesAsync(user);
                    var token = tokenService.GenerateToken(user, roles);

                    return Results.Ok(new
                    {
                        Token = token,
                        User = new { user.Email, user.FullName, Roles = roles }
                    });
                }

                return Results.Unauthorized();
            });

            // Health check endpoints
            app.MapGet("/health", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "users-api"
            }))
            .WithTags("Health")
            .Produces(200);

            app.MapGet("/health/ready", async (UserManager<User> userManager) =>
            {
                try
                {
                    // Verifica conectividade com o banco de dados
                    var usersCount = userManager.Users.Count();
                    return Results.Ok(new
                    {
                        status = "ready",
                        timestamp = DateTime.UtcNow,
                        database = "connected"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Json(new
                    {
                        status = "not ready",
                        timestamp = DateTime.UtcNow,
                        error = ex.Message
                    }, statusCode: 503);
                }
            })
            .WithTags("Health")
            .Produces(200)
            .Produces(503);
        }
    }
}
