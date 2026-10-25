using RuinaRPG.Api;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Invites;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Contracts;
using RuinaRPG.Domain;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Requisitos - Técnico R0003's chosen logging stack. Structured console output (captured by
// `docker logs`/`make logs`, same as the default logger it replaces) driven by the standard
// Logging:LogLevel config section already in appsettings.json/appsettings.*.json — no separate
// Serilog:* section needed for this project's scale.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

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

// Log de ficha (Técnico R0012): o interceptor é por requisição (precisa saber quem está salvando), por isso
// entra pelo provider do escopo. O ApiFactory dos testes de integração refaz este registro — manter os dois iguais.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
builder.Services.AddScoped<SheetLogDoServidor>();
builder.Services.AddScoped<CemiterioDaFicha>();
builder.Services.AddScoped<SheetLogInterceptor>();
builder.Services.AddDbContext<RuinaRpgDbContext>((sp, options) => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
    .AddInterceptors(sp.GetRequiredService<SheetLogInterceptor>()));

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
builder.Services.AddSingleton<IRulesDataProvider, RulesDataProvider>();
builder.Services.AddScoped<IRulebookRenderer, RulebookRenderer>();
builder.Services.AddScoped<IPericiaCatalogo, PericiaCatalogo>();
builder.Services.AddScoped<LivroDeRegrasEfeitosSync>();
builder.Services.AddScoped<DurabilidadePorRankProvider>();
builder.Services.AddScoped<TabelaDeAfinidadesProvider>();
builder.Services.AddScoped<ITabelaDeNiveis, TabelaDeNiveis>();
builder.Services.AddScoped<EquipmentKitGrantService>();
builder.Services.AddScoped<CondicoesAtivasDaFicha>();
builder.Services.AddScoped<CharacterSheetStats>();
builder.Services.AddScoped<EncounterLog>();
builder.Services.AddScoped<EncounterTurnos>();
builder.Services.AddScoped<CondicoesDoEncontro>();
builder.Services.AddScoped<ItensDesabilitaveisDaFicha>();
builder.Services.AddScoped<EquipmentPenaltyService>();
builder.Services.AddScoped<NpcSheetStats>();
builder.Services.AddScoped<CreatureSheetStats>();
builder.Services.AddScoped<ITipoDeFicha<CharacterSheet>, TipoPersonagem>();
builder.Services.AddScoped<ITipoDeFicha<NpcSheet>, TipoNpc>();
builder.Services.AddScoped<ITipoDeFicha<CreatureSheet>, TipoCriatura>();
builder.Services.AddScoped<ResumoDeFichaDoParticipante>();

builder.Services.Configure<ImageStorageOptions>(options =>
{
    options.ImagesPath = builder.Configuration["Storage:ImagesPath"] ?? "/images";
    options.MaxSizeMb = ImageStorageOptions.ResolveMaxSizeMb(builder.Configuration["Img:MaxSizeMb"]);
});
builder.Services.AddScoped<IImageFileStore, DiskImageFileStore>();

builder.Services.Configure<InviteOptions>(options =>
{
    options.CodeExpirationHours = InviteOptions.ResolveCodeExpirationHours(builder.Configuration["Invite:CodeExpirationHours"]);
});

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

        // Browsers/SignalR clients can't reliably set a custom Authorization header on a
        // WebSocket upgrade request, so the SignalR .NET/JS client instead sends the token as
        // an ?access_token= query string parameter (the standard, Microsoft-documented pattern)
        // when HttpConnectionOptions.AccessTokenProvider is set. Scoped to /hubs paths only —
        // regular REST calls keep using the Authorization header exclusively.
        bearerOptions.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
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

// Production and Development keep 10/min (Técnico R0006); the integration tests raise it, since one
// host now serves a whole test class.
var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

app.UseSerilogRequestLogging();

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
// Read by the client to notice a new deploy (Técnico R0014); anonymous like /api/health.
app.MapGet("/api/version", () => new VersionResponse(AppVersionInfo.Current, BuildInfo.ShaOf(typeof(Program).Assembly)));

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<EncounterHub>("/hubs/encounters");
app.MapHub<NotificationHub>("/hubs/notifications");

if (args.Contains("--migrate"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);
    return;
}

if (args.Contains("--grant-rules-auditor") || args.Contains("--revoke-rules-auditor"))
{
    var grant = args.Contains("--grant-rules-auditor");
    var flagIndex = Array.IndexOf(args, grant ? "--grant-rules-auditor" : "--revoke-rules-auditor");
    var email = flagIndex >= 0 && flagIndex + 1 < args.Length ? args[flagIndex + 1] : null;

    if (string.IsNullOrWhiteSpace(email))
    {
        app.Logger.LogError("Uso: dotnet RuinaRPG.Api.dll --{Flag} <email>", grant ? "grant-rules-auditor" : "revoke-rules-auditor");
        return;
    }

    using var rulesAuditorScope = app.Services.CreateScope();
    var rulesAuditorDb = rulesAuditorScope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
    var error = await RulesAuditorCli.SetRulesAuditorAsync(rulesAuditorDb, email, grant);
    if (error is not null)
        app.Logger.LogError("{Error}", error);
    else
        app.Logger.LogInformation("{Action} Auditor de Regras: {Email}", grant ? "Concedido" : "Revogado", email);
    return;
}

if (args.Contains("--reset-gm-password"))
{
    var flagIndex = Array.IndexOf(args, "--reset-gm-password");
    var email = flagIndex >= 0 && flagIndex + 1 < args.Length ? args[flagIndex + 1] : null;

    if (string.IsNullOrWhiteSpace(email))
    {
        app.Logger.LogError("Uso: dotnet RuinaRPG.Api.dll --reset-gm-password <email>");
        return;
    }

    using var resetScope = app.Services.CreateScope();
    var resetDb = resetScope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
    var resetUserManager = resetScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var resetResult = await GmPasswordResetCli.ResetGmPasswordAsync(resetDb, resetUserManager, email);
    if (resetResult.Error is not null)
        app.Logger.LogError("{Error}", resetResult.Error);
    else
        app.Logger.LogInformation("Nickname: {Nickname} | Senha temporária: {TemporaryPassword}", resetResult.Nickname, resetResult.TemporaryPassword);
    return;
}

if (app.Environment.IsDevelopment())
    await DatabaseInitializer.InitializeAsync(app.Services, app.Logger);

app.Run();

public partial class Program { }
