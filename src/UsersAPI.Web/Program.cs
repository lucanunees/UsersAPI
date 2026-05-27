using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using RedisCache.Library.Extensions;
using UsersAPI.Domain;
using UsersAPI.Infra;
using UsersAPI.Web.Endpoints;
using UsersAPI.Web.Extensions;
using UsersAPI.Web.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

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
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username("admin");
            h.Password("admin123");
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

var app = builder.Build();

await app.ApplyMigrationsAndSeed();
app.MapUserEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ─── Prometheus ────────────────────────────────────────────────
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("app", context => "users-api");
});
app.MapMetrics();

app.Run();

public record LoginRequest(string Email, string Password);


