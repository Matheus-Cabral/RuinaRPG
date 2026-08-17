using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
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
    .AddEntityFrameworkStores<RuinaRpgDbContext>()
    .AddDefaultTokenProviders();

// Fail fast: a deploy that forgets the Jwt__* environment variables must not boot green
// and only blow up deep inside request handling.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey)
        && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32
        && !string.IsNullOrWhiteSpace(o.Issuer)
        && !string.IsNullOrWhiteSpace(o.Audience),
        "Jwt configuration is missing or invalid (SigningKey must be present and at least 32 bytes; Issuer and Audience must be present).")
    .ValidateOnStart();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

builder.Services.Configure<ImageStorageOptions>(options =>
{
    options.ImagesPath = builder.Configuration["Storage:ImagesPath"] ?? "/images";
    options.MaxSizeMb = int.Parse(builder.Configuration["Img:MaxSizeMb"] ?? "10");
});
builder.Services.AddScoped<IImageFileStore, DiskImageFileStore>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// The bearer options are configured *from DI* rather than from a JwtOptions instance bound
// eagerly here, because at this point the container isn't built yet: reading configuration
// directly would silently ignore anything that registers/overrides JwtOptions later in the
// pipeline (the integration test host does exactly that). Taking IOptions<JwtOptions> as a
// dependency of the named-options Configure means validation (below) and token validation
// always agree on one single source of truth.
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        // Keep the claim types exactly as JwtTokenService wrote them ("sub"/"role"/"nickname")
        // instead of letting the handler rewrite them into the legacy WS-Fed URIs.
        bearerOptions.MapInboundClaims = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            RoleClaimType = "role",
            NameClaimType = JwtRegisteredClaimNames.Sub
        };
    });

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
// nginx proxies /api/ through unmodified, so /api/health is the only externally
// reachable form; the bare /health stays for container-internal healthchecks.
app.MapGet("/health", () => Results.Ok("OK"));
app.MapGet("/api/health", () => Results.Ok("OK"));

app.UseAuthentication();
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
