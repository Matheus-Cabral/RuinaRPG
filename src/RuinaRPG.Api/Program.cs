using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// nginx is the only reverse proxy in front of the Api, and it reaches Kestrel over the
// private Docker network on an address that changes with every `docker compose up`.
// Without clearing the (loopback-only) defaults, the middleware would reject nginx's
// own container address and drop the forwarded header - leaving the per-IP rate limiter
// (Técnico R0006) partitioned on nginx's address, i.e. one shared bucket for everyone.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<RuinaRpgDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<RuinaRpgDbContext>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? [];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

// Must run before anything that reads the connection (CORS, rate limiting, auth),
// so those see the real client IP rather than nginx's.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Default");
app.MapGet("/health", () => Results.Ok("OK"));

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

if (args.Contains("--migrate"))
{
    using var migrateScope = app.Services.CreateScope();
    migrateScope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>().Database.Migrate();
    return;
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program { }
