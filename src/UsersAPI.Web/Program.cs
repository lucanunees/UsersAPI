using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

# region Prometheus

// Configuração do OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("UsersAPI"))
        .AddAspNetCoreInstrumentation() // Métricas de requisições HTTP
        .AddHttpClientInstrumentation() // Métricas de chamadas para outros microsserviços
        .AddRuntimeInstrumentation()   // Métricas de CPU e Memória do .NET
        .AddPrometheusExporter());     // Expõe as métricas

# endregion

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

// Mapeia o endpoint para o Prometheus coletar
app.MapPrometheusScrapingEndpoint();

app.MapControllers();

app.Run();

public record LoginRequest(string Email, string Password);


