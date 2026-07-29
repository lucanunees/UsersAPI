using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using RedisCache.Library.Extensions;
using UsersAPI.Domain;
using UsersAPI.Infra;
using UsersAPI.Infra.Mongo;
using UsersAPI.Web.Endpoints;
using UsersAPI.Web.Extensions;
using UsersAPI.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<AppDbContext, AppDbContext>();
builder.Services.AddScoped<TokenService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentityCore<User>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

})
 .AddRoles<IdentityRole<Guid>>()
 .AddEntityFrameworkStores<AppDbContext>()
 .AddDefaultTokenProviders();

builder.Services.AddDataProtection();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

# region MassTransit
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMq:Host"];
        var username = builder.Configuration["RabbitMq:Username"];
        var password = builder.Configuration["RabbitMq:Password"];

        cfg.Host(host, "/", h =>
        {
            h.Username(username);
            h.Password(password);
        });

        cfg.ConfigureEndpoints(context);
    });
});
# endregion

// ─── Redis Cache via Kubernetes Secrets ────────────────────────
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "";

var redisConnectionString = string.IsNullOrEmpty(redisPassword)
    ? $"{redisHost}:{redisPort}"
    : $"{redisHost}:{redisPort},password={redisPassword},abortConnect=false";

builder.Services.AddRedisCache(options =>
{
    options.ConnectionString = redisConnectionString;
    options.KeyPrefix = "users:";
    options.DefaultExpirationInMinutes = 30;
    options.Enabled = true;
});

// ─── MongoDB (perfil expandido do usuário) ──────────────────────
var mongoDb = Environment.GetEnvironmentVariable("MONGO_DB") ?? "users";
var mongoConnectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
    ?? $"mongodb://{Environment.GetEnvironmentVariable("MONGO_HOST") ?? "localhost"}:{Environment.GetEnvironmentVariable("MONGO_PORT") ?? "27017"}";

builder.Services.AddSingleton(new UsersMongoContext(mongoConnectionString, mongoDb));
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();

builder.Services.AddHealthChecks();

var app = builder.Build();

await app.ApplyMigrationsAndSeed();
app.MapUserEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// ─── Prometheus ────────────────────────────────────────────────
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("app", context => "users-api");
});
app.MapMetrics();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

app.Run();

public record LoginRequest(string Email, string Password);


