using Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using RedisCache.Library.Interfaces;
using UsersAPI.Domain;
using UsersAPI.Web.Metrics;
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

                    AppMetrics.UsersRegistered.Inc();

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

            group.MapGet("/{id:guid}", async (
                Guid id,
                UserManager<User> userManager,
                ICacheService cacheService) =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                var cacheKey = $"user:{id}";
                var cachedUser = await cacheService.GetAsync<object>(cacheKey);

                if (cachedUser is not null)
                {
                    AppMetrics.CacheHits.WithLabels("get_user").Inc();
                    stopwatch.Stop();
                    AppMetrics.RequestDuration.WithLabels("get_user").Observe(stopwatch.Elapsed.TotalSeconds);
                    return Results.Ok(cachedUser);
                }

                AppMetrics.CacheMisses.WithLabels("get_user").Inc();

                var user = await userManager.FindByIdAsync(id.ToString());
                if (user is null)
                    return Results.NotFound();

                var userData = new { user.Id, user.Email, user.FullName };
                await cacheService.SetAsync(cacheKey, userData, TimeSpan.FromMinutes(10));

                stopwatch.Stop();
                AppMetrics.RequestDuration.WithLabels("get_user").Observe(stopwatch.Elapsed.TotalSeconds);
                return Results.Ok(userData);
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
