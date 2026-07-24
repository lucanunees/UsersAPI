using Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using RedisCache.Library.Interfaces;
using UsersAPI.Domain;
using UsersAPI.Infra.Mongo;
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
                IUserProfileRepository profileRepository,
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

                    await profileRepository.UpsertAsync(new UserProfileDocument
                    {
                        UserId = user.Id.ToString(),
                        CreatedAt = DateTime.UtcNow
                    });

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

            group.MapGet("/{id:guid}/profile", async (
                Guid id,
                IUserProfileRepository profileRepository,
                ICacheService cacheService) =>
            {
                var cacheKey = $"profile:{id}";
                var cached = await cacheService.GetAsync<UserProfileDto>(cacheKey);

                if (cached is not null)
                {
                    AppMetrics.CacheHits.WithLabels("get_profile").Inc();
                    return Results.Ok(cached);
                }

                AppMetrics.CacheMisses.WithLabels("get_profile").Inc();

                var profile = await profileRepository.GetByUserIdAsync(id.ToString());
                if (profile is null) return Results.NotFound();

                var dto = new UserProfileDto
                {
                    UserId = profile.UserId,
                    Bio = profile.Bio,
                    AvatarUrl = profile.AvatarUrl,
                    FavoriteGenres = profile.FavoriteGenres,
                    Preferences = profile.Preferences
                };
                await cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));

                return Results.Ok(dto);
            });

            group.MapPut("/{id:guid}/profile", async (
                Guid id,
                UpdateProfileRequest request,
                IUserProfileRepository profileRepository,
                ICacheService cacheService) =>
            {
                var profile = await profileRepository.GetByUserIdAsync(id.ToString())
                    ?? new UserProfileDocument { UserId = id.ToString(), CreatedAt = DateTime.UtcNow };

                profile.Bio = request.Bio ?? profile.Bio;
                profile.AvatarUrl = request.AvatarUrl ?? profile.AvatarUrl;
                profile.FavoriteGenres = request.FavoriteGenres ?? profile.FavoriteGenres;
                profile.Preferences = request.Preferences ?? profile.Preferences;
                profile.UpdatedAt = DateTime.UtcNow;

                await profileRepository.UpsertAsync(profile);
                await cacheService.RemoveAsync($"profile:{id}");

                return Results.Ok(new UserProfileDto
                {
                    UserId = profile.UserId,
                    Bio = profile.Bio,
                    AvatarUrl = profile.AvatarUrl,
                    FavoriteGenres = profile.FavoriteGenres,
                    Preferences = profile.Preferences
                });
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

    public class UserProfileDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public List<string> FavoriteGenres { get; set; } = new();
        public Dictionary<string, string> Preferences { get; set; } = new();
    }

    public class UpdateProfileRequest
    {
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public List<string>? FavoriteGenres { get; set; }
        public Dictionary<string, string>? Preferences { get; set; }
    }
}
