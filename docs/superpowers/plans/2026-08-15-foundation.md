# Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the whole solution skeleton (7 projects, Docker, Postgres, Makefile) and deliver one complete, working vertical slice of auth: a GM can register, log in, get a JWT + refresh token, call an authenticated endpoint, refresh, and log out — end-to-end through nginx via `make deploy`.

**Architecture:** Layered solution (`RuinaRPG.Domain` → `RuinaRPG.Infrastructure` → `RuinaRPG.Api`, with `RuinaRPG.Contracts` shared by `Api` and `RuinaRPG.Client`), backed by PostgreSQL via EF Core + ASP.NET Core Identity, everything containerized. Player registration (needs an invite code) is explicitly **out of scope** here — it depends on `InviteCodes`, which belongs to the next plan ("Convite de Jogador"). This plan only implements the GM tab of Cadastro (`Requisitos - Login e Cadastro.md` R0003) plus Login/Logout (R0002/R0004) and the Landing page (R0001).

**Tech Stack:** ASP.NET Core 8 Web API (controllers) + Serilog + Swashbuckle, ASP.NET Core Identity + JWT Bearer + refresh tokens, EF Core 8 + Npgsql/PostgreSQL, Blazor WebAssembly 8 standalone + Blazored.LocalStorage, Docker + nginx 1.27-alpine, xUnit + Moq + FluentAssertions + Bogus + Testcontainers.PostgreSql.

**Spec:** `Docs/Requisitos/Requisitos - Técnico.md`, `Docs/Requisitos/Requisitos - Modelo de Dados.md` (§1 Contas), `Docs/Requisitos/Requisitos - Login e Cadastro.md`

## Global Constraints

- Solution layout is exactly: `RuinaRPG.Domain`, `RuinaRPG.Infrastructure`, `RuinaRPG.Contracts`, `RuinaRPG.Api`, `RuinaRPG.Client`, `RuinaRPG.Tests.Unit`, `RuinaRPG.Tests.Integration` (Técnico R0002 — Tests split into Unit/Integration per its own "pode ser subdividido" allowance).
- `RuinaRPG.Domain` has zero dependency on EF Core, ASP.NET, or Identity — pure C#.
- No secret ever hardcoded — everything comes from environment variables (Técnico R0003).
- A freshly-installed host needs only `git`, `docker` (with the Compose plugin), and `make` — every other dependency is installed inside a container image (Técnico R0007).
- Migrations auto-apply on startup in `Development`; in `Production` they're an explicit `make migrate` step, never automatic (Técnico R0009).
- Login failures return one generic message, never revealing whether the Nickname or the Senha was wrong (Login e Cadastro R0002).

---

### Task 1: Solution and project scaffold

**Files:**
- Create: `RuinaRPG.sln`
- Create: `src/RuinaRPG.Domain/RuinaRPG.Domain.csproj`
- Create: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj`
- Create: `src/RuinaRPG.Contracts/RuinaRPG.Contracts.csproj`
- Create: `src/RuinaRPG.Api/RuinaRPG.Api.csproj`
- Create: `src/RuinaRPG.Client/RuinaRPG.Client.csproj`
- Create: `tests/RuinaRPG.Tests.Unit/RuinaRPG.Tests.Unit.csproj`
- Create: `tests/RuinaRPG.Tests.Integration/RuinaRPG.Tests.Integration.csproj`

**Interfaces:**
- Produces: the 7 projects and their reference graph, which every later task builds on. No types yet.

- [ ] **Step 1: Create the solution and all 7 projects**

```bash
dotnet new sln -n RuinaRPG

dotnet new classlib -n RuinaRPG.Domain -o src/RuinaRPG.Domain
dotnet new classlib -n RuinaRPG.Infrastructure -o src/RuinaRPG.Infrastructure
dotnet new classlib -n RuinaRPG.Contracts -o src/RuinaRPG.Contracts
dotnet new webapi -n RuinaRPG.Api -o src/RuinaRPG.Api --use-controllers
dotnet new blazorwasm -n RuinaRPG.Client -o src/RuinaRPG.Client

dotnet new xunit -n RuinaRPG.Tests.Unit -o tests/RuinaRPG.Tests.Unit
dotnet new xunit -n RuinaRPG.Tests.Integration -o tests/RuinaRPG.Tests.Integration

dotnet sln add \
  src/RuinaRPG.Domain \
  src/RuinaRPG.Infrastructure \
  src/RuinaRPG.Contracts \
  src/RuinaRPG.Api \
  src/RuinaRPG.Client \
  tests/RuinaRPG.Tests.Unit \
  tests/RuinaRPG.Tests.Integration
```

- [ ] **Step 2: Wire project references**

```bash
dotnet add src/RuinaRPG.Infrastructure reference src/RuinaRPG.Domain
dotnet add src/RuinaRPG.Api reference src/RuinaRPG.Domain src/RuinaRPG.Infrastructure src/RuinaRPG.Contracts
dotnet add src/RuinaRPG.Client reference src/RuinaRPG.Contracts
dotnet add tests/RuinaRPG.Tests.Unit reference src/RuinaRPG.Domain src/RuinaRPG.Infrastructure
dotnet add tests/RuinaRPG.Tests.Integration reference src/RuinaRPG.Api
```

- [ ] **Step 3: Remove template cruft**

Delete the sample `Class1.cs` from `Domain`, `Infrastructure`, and `Contracts` (the `dotnet new classlib` template stub), and the default `WeatherForecast`/`WeatherForecastController` from `RuinaRPG.Api` if the template generated them.

- [ ] **Step 4: Verify the solution builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` across all 7 projects.

- [ ] **Step 5: Commit**

```bash
git add RuinaRPG.sln src tests
git commit -m "chore: scaffold solution with 7 projects"
```

---

### Task 2: Docker, Makefile, and a health endpoint

**Files:**
- Create: `src/RuinaRPG.Api/Dockerfile`
- Create: `infra/nginx/Dockerfile`
- Create: `infra/nginx/nginx.conf`
- Create: `docker-compose.yml`
- Create: `.env.example`
- Create: `Makefile`
- Modify: `src/RuinaRPG.Api/Program.cs`

**Interfaces:**
- Produces: `GET /health` on the Api, reachable at `http://localhost/api/health` once routed through nginx. Later tasks' integration tests and the final smoke test rely on the container topology (service names `api`, `db`, `nginx`) set up here.

- [ ] **Step 1: Write the Api Dockerfile**

`src/RuinaRPG.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/RuinaRPG.Api/RuinaRPG.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RuinaRPG.Api.dll"]
```

- [ ] **Step 2: Write the nginx Dockerfile (builds the Blazor client, then serves it)**

`infra/nginx/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release -o /app

FROM nginx:1.27-alpine
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY infra/nginx/nginx.conf /etc/nginx/conf.d/default.conf
```

- [ ] **Step 3: Write nginx.conf**

`infra/nginx/nginx.conf`:

```nginx
server {
    listen 80;

    location /api/ {
        proxy_pass http://api:8080/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }

    location /hubs/ {
        proxy_pass http://api:8080/hubs/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }

    location / {
        root /usr/share/nginx/html;
        try_files $uri $uri/ /index.html;
    }
}
```

- [ ] **Step 4: Write docker-compose.yml**

`docker-compose.yml`:

```yaml
services:
  db:
    image: postgres:16-alpine
    restart: unless-stopped
    environment:
      POSTGRES_DB: ruinarpg
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 5s
      timeout: 5s
      retries: 5

  api:
    build:
      context: .
      dockerfile: src/RuinaRPG.Api/Dockerfile
    restart: unless-stopped
    depends_on:
      db:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT}
      ConnectionStrings__Postgres: "Host=db;Database=ruinarpg;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      Jwt__SigningKey: ${JWT_SIGNING_KEY}
      Jwt__Issuer: ${JWT_ISSUER}
      Jwt__Audience: ${JWT_AUDIENCE}
      Jwt__AccessTokenMinutes: ${JWT_ACCESS_TOKEN_MINUTES}
      Jwt__RefreshTokenDays: ${JWT_REFRESH_TOKEN_DAYS}
      Cors__AllowedOrigins: ${CORS_ALLOWED_ORIGINS}
      Storage__ImagesPath: /images
    volumes:
      - images:/images

  nginx:
    build:
      context: .
      dockerfile: infra/nginx/Dockerfile
    restart: unless-stopped
    depends_on:
      - api
    ports:
      - "80:80"
    volumes:
      - images:/usr/share/nginx/html/images:ro

volumes:
  pgdata:
  images:
```

- [ ] **Step 5: Write .env.example**

`.env.example`:

```dotenv
ASPNETCORE_ENVIRONMENT=Development
POSTGRES_USER=ruinarpg
POSTGRES_PASSWORD=change-me
JWT_SIGNING_KEY=change-me-to-a-long-random-string
JWT_ISSUER=RuinaRPG
JWT_AUDIENCE=RuinaRPG
JWT_ACCESS_TOKEN_MINUTES=15
JWT_REFRESH_TOKEN_DAYS=30
CORS_ALLOWED_ORIGINS=http://localhost
```

- [ ] **Step 6: Write the Makefile**

`Makefile`:

```makefile
.PHONY: deploy up down clean logs migrate

deploy up:
	docker compose --env-file .env up --build -d

down:
	docker compose down

clean:
	docker compose down --rmi local --volumes --remove-orphans

logs:
	docker compose logs -f

migrate:
	docker compose exec api dotnet RuinaRPG.Api.dll --migrate
```

- [ ] **Step 7: Add a `/health` endpoint and CORS**

In `src/RuinaRPG.Api/Program.cs`, before `var app = builder.Build();`, add (Técnico R0003 — `Cors__AllowedOrigins` is one of the three explicitly-required env vars):

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',') ?? [];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});
```

After `var app = builder.Build();`, add:

```csharp
app.UseCors("Default");
app.MapGet("/health", () => Results.Ok("OK"));
```

- [ ] **Step 8: Boot the stack and verify the health endpoint**

```bash
cp .env.example .env
make deploy
curl -sf http://localhost/api/health
```

Expected: the curl prints `OK` and exits 0. Then tear down: `make down`.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Api/Dockerfile infra docker-compose.yml .env.example Makefile src/RuinaRPG.Api/Program.cs
git commit -m "chore: containerize the stack with a health endpoint"
```

---

### Task 3: Users, Identity, and the initial migration

**Files:**
- Create: `src/RuinaRPG.Domain/Enums/UserRole.cs`
- Create: `src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs`
- Create: `src/RuinaRPG.Infrastructure/Identity/RefreshToken.cs`
- Create: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj` (packages)
- Modify: `src/RuinaRPG.Api/RuinaRPG.Api.csproj` (packages)
- Modify: `src/RuinaRPG.Api/Program.cs` (DbContext + Identity registration, migrate-on-startup in Development)
- Create: `tests/RuinaRPG.Tests.Integration/PostgresFixture.cs`
- Create: `tests/RuinaRPG.Tests.Integration/Persistence/MigrationTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/RuinaRPG.Tests.Integration.csproj` (packages)

**Interfaces:**
- Consumes: nothing outside this task.
- Produces: `ApplicationUser` (properties: `Guid Id`, `string Nickname`, `UserRole Role`, `Guid? InvitedByGmId`, plus inherited `IdentityUser<Guid>` members `Email`, `PasswordHash`, ...), `RefreshToken` (`Guid Id`, `Guid UserId`, `string TokenHash`, `DateTime ExpiresAt`, `DateTime? RevokedAt`), `RuinaRpgDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` with `DbSet<RefreshToken> RefreshTokens`. Every later task depends on these three types and this DbContext.

- [ ] **Step 1: Add the NuGet packages**

```bash
dotnet add src/RuinaRPG.Infrastructure package Microsoft.EntityFrameworkCore --version 8.0.10
dotnet add src/RuinaRPG.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.10
dotnet add src/RuinaRPG.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.10

dotnet add src/RuinaRPG.Api package Microsoft.EntityFrameworkCore.Design --version 8.0.10

dotnet add tests/RuinaRPG.Tests.Integration package Testcontainers.PostgreSql --version 3.10.0
dotnet add tests/RuinaRPG.Tests.Integration package FluentAssertions --version 6.12.1
dotnet add tests/RuinaRPG.Tests.Integration package Microsoft.EntityFrameworkCore.Design --version 8.0.10
```

- [ ] **Step 2: Write the `UserRole` enum**

`src/RuinaRPG.Domain/Enums/UserRole.cs`:

```csharp
namespace RuinaRPG.Domain.Enums;

public enum UserRole
{
    GM,
    Jogador
}
```

- [ ] **Step 3: Write `ApplicationUser`**

`src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string Nickname { get; set; }
    public UserRole Role { get; set; }
    public Guid? InvitedByGmId { get; set; }
}
```

- [ ] **Step 4: Write `RefreshToken`**

`src/RuinaRPG.Infrastructure/Identity/RefreshToken.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Identity;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
```

- [ ] **Step 5: Write the `DbContext`**

`src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Persistence;

public class RuinaRpgDbContext(DbContextOptions<RuinaRpgDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

- [ ] **Step 6: Register EF Core + Identity in `Program.cs`, and migrate-on-startup in Development**

In `src/RuinaRPG.Api/Program.cs`, add near the top (after `var builder = WebApplication.CreateBuilder(args);`):

```csharp
builder.Services.AddDbContext<RuinaRpgDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<RuinaRpgDbContext>();
```

Add the corresponding `using` statements:

```csharp
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
```

After `var app = builder.Build();` and before `app.Run();`, add (Técnico R0009 — auto-migrate only in Development; the `--migrate` branch is what `make migrate`, from Task 2's Makefile, actually runs in Production, since it applies migrations then exits instead of starting the web server):

```csharp
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
```

- [ ] **Step 7: Write the Postgres test fixture**

`tests/RuinaRPG.Tests.Integration/PostgresFixture.cs`:

```csharp
using Testcontainers.PostgreSql;

namespace RuinaRPG.Tests.Integration;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ruinarpg")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

- [ ] **Step 8: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/MigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class MigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public MigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_AspNetUsers_and_RefreshTokens_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("InitialCreate"));
    }
}
```

- [ ] **Step 9: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter MigrationTests`
Expected: FAIL — no migrations exist yet, so `GetAppliedMigrationsAsync()` returns an empty list and the `Should().Contain(...)` assertion fails.

- [ ] **Step 10: Create the initial migration**

```bash
dotnet ef migrations add InitialCreate \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

(If `dotnet ef` isn't installed: `dotnet tool install --global dotnet-ef --version 8.*` first.)

- [ ] **Step 11: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter MigrationTests`
Expected: PASS (requires Docker running locally, since Testcontainers starts a real Postgres container).

- [ ] **Step 12: Commit**

```bash
git add src/RuinaRPG.Domain/Enums src/RuinaRPG.Infrastructure src/RuinaRPG.Api tests/RuinaRPG.Tests.Integration
git commit -m "feat: add Identity users, refresh tokens, and initial migration"
```

---

### Task 4: JWT and refresh token services

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Auth/IJwtTokenService.cs`
- Create: `src/RuinaRPG.Infrastructure/Auth/JwtOptions.cs`
- Create: `src/RuinaRPG.Infrastructure/Auth/JwtTokenService.cs`
- Create: `src/RuinaRPG.Infrastructure/Auth/IRefreshTokenService.cs`
- Create: `src/RuinaRPG.Infrastructure/Auth/RefreshTokenService.cs`
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj` (package)
- Create: `tests/RuinaRPG.Tests.Unit/Auth/JwtTokenServiceTests.cs`
- Create: `tests/RuinaRPG.Tests.Unit/Auth/RefreshTokenServiceTests.cs`
- Modify: `tests/RuinaRPG.Tests.Unit/RuinaRPG.Tests.Unit.csproj` (packages)

**Interfaces:**
- Consumes: `ApplicationUser`, `RefreshToken` (Task 3).
- Produces: `IJwtTokenService.CreateAccessToken(ApplicationUser user) : string`, `IRefreshTokenService.Generate(Guid userId) : (string plainTextToken, RefreshToken entity)`, `IRefreshTokenService.Hash(string plainTextToken) : string`. Task 5/6 wire these into controllers.

- [ ] **Step 1: Add packages**

```bash
dotnet add src/RuinaRPG.Infrastructure package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.10
dotnet add tests/RuinaRPG.Tests.Unit package FluentAssertions --version 6.12.1
dotnet add tests/RuinaRPG.Tests.Unit package Moq --version 4.20.72
```

- [ ] **Step 2: Write `JwtOptions`**

`src/RuinaRPG.Infrastructure/Auth/JwtOptions.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
```

- [ ] **Step 3: Write the failing unit test for `JwtTokenService`**

`tests/RuinaRPG.Tests.Unit/Auth/JwtTokenServiceTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Tests.Unit.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateSut() => new(Options.Create(new JwtOptions
    {
        SigningKey = "unit-test-signing-key-needs-32-bytes-min",
        Issuer = "RuinaRPG.Tests",
        Audience = "RuinaRPG.Tests",
        AccessTokenMinutes = 15
    }));

    [Fact]
    public void CreateAccessToken_embeds_user_id_and_role_claims()
    {
        var sut = CreateSut();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Nickname = "TestGM",
            Role = UserRole.GM
        };

        var token = sut.CreateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == nameof(UserRole.GM));
    }
}
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter JwtTokenServiceTests`
Expected: FAIL to compile — `JwtTokenService` doesn't exist yet.

- [ ] **Step 5: Write `IJwtTokenService` and `JwtTokenService`**

`src/RuinaRPG.Infrastructure/Auth/IJwtTokenService.cs`:

```csharp
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Auth;

public interface IJwtTokenService
{
    string CreateAccessToken(ApplicationUser user);
}
```

`src/RuinaRPG.Infrastructure/Auth/JwtTokenService.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Auth;

public class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateAccessToken(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim("nickname", user.Nickname)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter JwtTokenServiceTests`
Expected: PASS.

- [ ] **Step 7: Write the failing unit test for `RefreshTokenService`**

`tests/RuinaRPG.Tests.Unit/Auth/RefreshTokenServiceTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Infrastructure.Auth;

namespace RuinaRPG.Tests.Unit.Auth;

public class RefreshTokenServiceTests
{
    [Fact]
    public void Generate_returns_a_plaintext_token_whose_hash_matches_the_entity()
    {
        var sut = new RefreshTokenService();
        var userId = Guid.NewGuid();

        var (plainTextToken, entity) = sut.Generate(userId, refreshTokenDays: 30);

        entity.UserId.Should().Be(userId);
        entity.TokenHash.Should().Be(sut.Hash(plainTextToken));
        entity.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
        entity.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Hash_is_deterministic_for_the_same_input()
    {
        var sut = new RefreshTokenService();

        sut.Hash("same-token").Should().Be(sut.Hash("same-token"));
    }
}
```

- [ ] **Step 8: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RefreshTokenServiceTests`
Expected: FAIL to compile — `RefreshTokenService` doesn't exist yet.

- [ ] **Step 9: Write `IRefreshTokenService` and `RefreshTokenService`**

`src/RuinaRPG.Infrastructure/Auth/IRefreshTokenService.cs`:

```csharp
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Auth;

public interface IRefreshTokenService
{
    (string plainTextToken, RefreshToken entity) Generate(Guid userId, int refreshTokenDays);
    string Hash(string plainTextToken);
}
```

`src/RuinaRPG.Infrastructure/Auth/RefreshTokenService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using RuinaRPG.Infrastructure.Identity;

namespace RuinaRPG.Infrastructure.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    public (string plainTextToken, RefreshToken entity) Generate(Guid userId, int refreshTokenDays)
    {
        var plainTextToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(plainTextToken),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays)
        };

        return (plainTextToken, entity);
    }

    public string Hash(string plainTextToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainTextToken));
        return Convert.ToHexString(bytes);
    }
}
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter RefreshTokenServiceTests`
Expected: PASS.

- [ ] **Step 11: Register both services in `Program.cs`**

Add, next to the Identity registration from Task 3:

```csharp
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
```

Add `using RuinaRPG.Infrastructure.Auth;` to the top of `Program.cs`.

- [ ] **Step 12: Commit**

```bash
git add src/RuinaRPG.Infrastructure/Auth src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Unit
git commit -m "feat: add JWT and refresh token services"
```

---

### Task 5: GM registration endpoint (Cadastro R0003, GM tab)

**Files:**
- Create: `src/RuinaRPG.Contracts/Auth/RegisterGmRequest.cs`
- Create: `src/RuinaRPG.Contracts/Auth/AuthResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/AuthController.cs`
- Create: `tests/RuinaRPG.Tests.Integration/ApiFactory.cs`
- Create: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRegisterTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/RuinaRPG.Tests.Integration.csproj` (package)

**Interfaces:**
- Consumes: `UserManager<ApplicationUser>` (Identity, Task 3), `IJwtTokenService`, `IRefreshTokenService` (Task 4).
- Produces: `POST /api/auth/register/gm` accepting `RegisterGmRequest { string Nickname, string Email, string Senha, string ConfirmacaoSenha }`, returning `201` with `AuthResponse { string AccessToken, string RefreshToken }` on success. Task 6 reuses `AuthController` and `AuthResponse`; Task 8 (Client) consumes this contract shape directly.

- [ ] **Step 1: Add packages**

```bash
dotnet add tests/RuinaRPG.Tests.Integration package Microsoft.AspNetCore.Mvc.Testing --version 8.0.10
```

- [ ] **Step 2: Write the request/response contracts**

`src/RuinaRPG.Contracts/Auth/RegisterGmRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Auth;

public record RegisterGmRequest(string Nickname, string Email, string Senha, string ConfirmacaoSenha);
```

`src/RuinaRPG.Contracts/Auth/AuthResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Auth;

public record AuthResponse(string AccessToken, string RefreshToken);
```

- [ ] **Step 3: Write the `ApiFactory` test harness**

`tests/RuinaRPG.Tests.Integration/ApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

public class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RuinaRpgDbContext>>();
            services.AddDbContext<RuinaRpgDbContext>(options => options.UseNpgsql(connectionString));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.Database.Migrate();
        });
    }
}
```

`RuinaRPG.Api` needs a public `Program` partial class for `WebApplicationFactory<Program>` to see. At the very end of `src/RuinaRPG.Api/Program.cs`, add:

```csharp
public partial class Program { }
```

- [ ] **Step 4: Write the failing integration test**

`tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRegisterTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerRegisterTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerRegisterTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task Register_gm_with_valid_data_returns_201_and_tokens()
    {
        var request = new RegisterGmRequest("MestreTeste", "mestre@teste.com", "Senha!123", "Senha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_gm_with_mismatched_password_confirmation_returns_400()
    {
        var request = new RegisterGmRequest("MestreTeste2", "mestre2@teste.com", "Senha!123", "OutraSenha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_gm_with_duplicate_email_returns_400()
    {
        var request = new RegisterGmRequest("Original", "duplicado@teste.com", "Senha!123", "Senha!123");
        await _client.PostAsJsonAsync("/api/auth/register/gm", request);

        var duplicate = new RegisterGmRequest("Duplicado", "duplicado@teste.com", "Senha!123", "Senha!123");
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", duplicate);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerRegisterTests`
Expected: FAIL to compile — `AuthController` doesn't exist (route 404s once it compiles without it).

- [ ] **Step 6: Write `AuthController`**

`src/RuinaRPG.Api/Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    RuinaRpgDbContext db,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("register/gm")]
    public async Task<ActionResult<AuthResponse>> RegisterGm(RegisterGmRequest request)
    {
        if (request.Senha != request.ConfirmacaoSenha)
            return BadRequest("A confirmação de senha não confere com a senha.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            Nickname = request.Nickname,
            Role = UserRole.GM
        };

        var result = await userManager.CreateAsync(user, request.Senha);
        if (!result.Succeeded)
            return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));

        return Created(string.Empty, await IssueTokensAsync(user));
    }

    private async Task<AuthResponse> IssueTokensAsync(ApplicationUser user)
    {
        var accessToken = jwtTokenService.CreateAccessToken(user);
        var (plainTextRefreshToken, refreshTokenEntity) =
            refreshTokenService.Generate(user.Id, jwtOptions.Value.RefreshTokenDays);

        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, plainTextRefreshToken);
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerRegisterTests`
Expected: PASS (all 3 tests).

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Contracts/Auth src/RuinaRPG.Api/Controllers/AuthController.cs src/RuinaRPG.Api/Program.cs tests/RuinaRPG.Tests.Integration
git commit -m "feat: add GM registration endpoint"
```

---

### Task 6: Login, refresh, and logout endpoints (R0002, R0004)

**Files:**
- Create: `src/RuinaRPG.Contracts/Auth/LoginRequest.cs`
- Create: `src/RuinaRPG.Contracts/Auth/RefreshRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/AuthController.cs`
- Create: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerLoginTests.cs`

**Interfaces:**
- Consumes: `AuthController`, `AuthResponse` (Task 5); `IRefreshTokenService.Hash` (Task 4).
- Produces: `POST /api/auth/login` (`LoginRequest { string Nickname, string Senha }` → `AuthResponse` or `401`), `POST /api/auth/refresh` (`RefreshRequest { string RefreshToken }` → new `AuthResponse`, rotates the token), `POST /api/auth/logout` (`RefreshRequest` → `204`, revokes it). Task 8 (Client) calls all three.

- [ ] **Step 1: Write the request contracts**

`src/RuinaRPG.Contracts/Auth/LoginRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Auth;

public record LoginRequest(string Nickname, string Senha);
```

`src/RuinaRPG.Contracts/Auth/RefreshRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Auth;

public record RefreshRequest(string RefreshToken);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerLoginTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerLoginTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerLoginTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private async Task RegisterAsync(string nickname, string email, string senha) =>
        await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, senha, senha));

    [Fact]
    public async Task Login_with_correct_credentials_returns_200_and_tokens()
    {
        await RegisterAsync("LoginGm", "login@teste.com", "Senha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LoginGm", "Senha!123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_with_generic_message()
    {
        await RegisterAsync("LoginGm2", "login2@teste.com", "Senha!123");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LoginGm2", "SenhaErrada"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("senha", "a mensagem não pode indicar qual campo estava errado (R0002)");
    }

    [Fact]
    public async Task Login_with_unknown_nickname_returns_the_same_401_message_as_wrong_password()
    {
        var unknownResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("NaoExiste", "Qualquer1!"));
        await RegisterAsync("LoginGm3", "login3@teste.com", "Senha!123");
        var wrongPasswordResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LoginGm3", "SenhaErrada"));

        var unknownBody = await unknownResponse.Content.ReadAsStringAsync();
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadAsStringAsync();
        unknownBody.Should().Be(wrongPasswordBody);
    }

    [Fact]
    public async Task Refresh_with_a_valid_token_returns_a_new_token_pair()
    {
        await RegisterAsync("RefreshGm", "refresh@teste.com", "Senha!123");
        var login = await (await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("RefreshGm", "Senha!123")))
            .Content.ReadFromJsonAsync<AuthResponse>();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login!.RefreshToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.RefreshToken.Should().NotBe(login.RefreshToken, "refresh should rotate the token");
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_so_it_cannot_be_reused()
    {
        await RegisterAsync("LogoutGm", "logout@teste.com", "Senha!123");
        var login = await (await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("LogoutGm", "Senha!123")))
            .Content.ReadFromJsonAsync<AuthResponse>();

        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest(login!.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        reuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerLoginTests`
Expected: FAIL — the `/login`, `/refresh`, `/logout` routes don't exist yet (404).

- [ ] **Step 4: Add login/refresh/logout to `AuthController`**

Append to `src/RuinaRPG.Api/Controllers/AuthController.cs`, inside the `AuthController` class:

```csharp
[HttpPost("login")]
public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
{
    // UserName is set to Email at registration (Task 5), so login — which is by
    // Nickname (R0002) — must query by the Nickname column directly, not FindByNameAsync.
    var user = await FindByNicknameAsync(request.Nickname);

    if (user is null || !await userManager.CheckPasswordAsync(user, request.Senha))
        return Unauthorized("Nickname ou senha inválidos.");

    return Ok(await IssueTokensAsync(user));
}

[HttpPost("refresh")]
public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
{
    var hash = refreshTokenService.Hash(request.RefreshToken);
    var stored = db.RefreshTokens.SingleOrDefault(t => t.TokenHash == hash);

    if (stored is null || !stored.IsActive)
        return Unauthorized();

    stored.RevokedAt = DateTime.UtcNow;
    var user = await userManager.FindByIdAsync(stored.UserId.ToString());
    var response = await IssueTokensAsync(user!);
    await db.SaveChangesAsync();

    return Ok(response);
}

[HttpPost("logout")]
public async Task<IActionResult> Logout(RefreshRequest request)
{
    var hash = refreshTokenService.Hash(request.RefreshToken);
    var stored = db.RefreshTokens.SingleOrDefault(t => t.TokenHash == hash);

    if (stored is not null && stored.IsActive)
    {
        stored.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    return NoContent();
}

private async Task<ApplicationUser?> FindByNicknameAsync(string nickname) =>
    await db.Users.SingleOrDefaultAsync(u => u.Nickname == nickname);
```

Add `using Microsoft.EntityFrameworkCore;` to the top of the file (needed for `SingleOrDefaultAsync`).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerLoginTests`
Expected: PASS (all 5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/Auth src/RuinaRPG.Api/Controllers/AuthController.cs tests/RuinaRPG.Tests.Integration
git commit -m "feat: add login, refresh, and logout endpoints"
```

---

### Task 7: Rate limiting on login and refresh (Técnico R0006)

**Files:**
- Modify: `src/RuinaRPG.Api/Program.cs`
- Modify: `src/RuinaRPG.Api/Controllers/AuthController.cs`
- Create: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRateLimitTests.cs`

**Interfaces:**
- Consumes: `AuthController` (Task 6).
- Produces: a `"login"` rate-limiter policy applied to `/api/auth/login` and `/api/auth/refresh`; the invite-code-redemption endpoint (next plan) reuses the same named policy.

- [ ] **Step 1: Write the failing test**

`tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerRateLimitTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Tests.Integration.Controllers;

public class AuthControllerRateLimitTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthControllerRateLimitTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task Login_returns_429_after_exceeding_the_rate_limit()
    {
        var request = new LoginRequest("RateLimitTest", "WrongPassword!");
        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 15; i++)
            lastResponse = await _client.PostAsJsonAsync("/api/auth/login", request);

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerRateLimitTests`
Expected: FAIL — no request in the loop returns 429 yet (all come back 401).

- [ ] **Step 3: Register the rate limiter in `Program.cs`, partitioned per IP**

Técnico R0006 requires the limit to apply **per IP/usuário**, not as one shared global counter — a single named `AddFixedWindowLimiter` policy is global and would let one abusive client exhaust the quota for everyone else. Use `AddPolicy` with a partition key instead. Add, before `var app = builder.Build();`:

```csharp
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
```

Add `using Microsoft.AspNetCore.RateLimiting;` and `using System.Threading.RateLimiting;` to the top of `Program.cs`, and add `app.UseRateLimiter();` right after `app.UseRouting();` (or right before `app.MapControllers();` if the template has no explicit `UseRouting`).

- [ ] **Step 4: Apply the policy to `Login` and `Refresh`**

In `src/RuinaRPG.Api/Controllers/AuthController.cs`, add `[EnableRateLimiting("login")]` immediately above both the `Login` and `Refresh` action methods, and add `using Microsoft.AspNetCore.RateLimiting;` to the top of the file.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter AuthControllerRateLimitTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Api/Program.cs src/RuinaRPG.Api/Controllers/AuthController.cs tests/RuinaRPG.Tests.Integration
git commit -m "feat: rate-limit login and refresh endpoints"
```

---

### Task 8: Client — Landing, Login, and Cadastro (GM) pages

**Files:**
- Create: `src/RuinaRPG.Client/Services/AuthStateService.cs`
- Create: `src/RuinaRPG.Client/Pages/Landing.razor`
- Create: `src/RuinaRPG.Client/Pages/Login.razor`
- Create: `src/RuinaRPG.Client/Pages/CadastroGm.razor`
- Modify: `src/RuinaRPG.Client/Program.cs`
- Modify: `src/RuinaRPG.Client/RuinaRPG.Client.csproj` (package)
- Modify: `src/RuinaRPG.Client/wwwroot/appsettings.json` (or create it, for the API base address)

**Interfaces:**
- Consumes: `RegisterGmRequest`, `LoginRequest`, `AuthResponse` (Contracts, Tasks 5/6) via `HttpClient` calls to `/api/auth/*`.
- Produces: `AuthStateService.SetTokensAsync(AuthResponse)`, `AuthStateService.GetAccessTokenAsync()`, `AuthStateService.ClearAsync()` — later plans' authenticated pages read the access token from this service.

- [ ] **Step 1: Add Blazored.LocalStorage**

```bash
dotnet add src/RuinaRPG.Client package Blazored.LocalStorage --version 4.5.0
```

- [ ] **Step 2: Write `AuthStateService`**

`src/RuinaRPG.Client/Services/AuthStateService.cs`:

```csharp
using Blazored.LocalStorage;
using RuinaRPG.Contracts.Auth;

namespace RuinaRPG.Client.Services;

public class AuthStateService(ILocalStorageService localStorage)
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    public async Task SetTokensAsync(AuthResponse response)
    {
        await localStorage.SetItemAsStringAsync(AccessTokenKey, response.AccessToken);
        await localStorage.SetItemAsStringAsync(RefreshTokenKey, response.RefreshToken);
    }

    public ValueTask<string?> GetAccessTokenAsync() => localStorage.GetItemAsStringAsync(AccessTokenKey);
    public ValueTask<string?> GetRefreshTokenAsync() => localStorage.GetItemAsStringAsync(RefreshTokenKey);

    public async Task ClearAsync()
    {
        await localStorage.RemoveItemAsync(AccessTokenKey);
        await localStorage.RemoveItemAsync(RefreshTokenKey);
    }
}
```

- [ ] **Step 3: Register services in `Program.cs`**

In `src/RuinaRPG.Client/Program.cs`, add before `await builder.Build().RunAsync();`:

```csharp
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress)
});
```

Add `using Blazored.LocalStorage;` and `using RuinaRPG.Client.Services;` to the top.

- [ ] **Step 4: Configure the API base address**

`src/RuinaRPG.Client/wwwroot/appsettings.json`:

```json
{
  "ApiBaseAddress": "/api/"
}
```

- [ ] **Step 5: Write the Landing page**

`src/RuinaRPG.Client/Pages/Landing.razor`:

```razor
@page "/"

<h1>Ruína RPG</h1>
<p>Um sistema de RPG de mesa de fantasia sombria. Crie seu personagem, junte-se a uma campanha, e sobreviva à Ruína.</p>

<a href="/cadastro">Cadastrar</a>
<a href="/login">Entrar</a>
```

- [ ] **Step 6: Write the Login page**

`src/RuinaRPG.Client/Pages/Login.razor`:

```razor
@page "/login"
@inject HttpClient Http
@inject AuthStateService AuthState
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Auth

<h1>Entrar</h1>

<EditForm Model="_request" OnValidSubmit="SubmitAsync">
    <label>Nickname <InputText @bind-Value="_request.Nickname" /></label>
    <label>Senha <InputText type="password" @bind-Value="_request.Senha" /></label>
    <button type="submit">Entrar</button>
</EditForm>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

@code {
    private readonly LoginFormModel _request = new();
    private string? _errorMessage;

    private async Task SubmitAsync()
    {
        var response = await Http.PostAsJsonAsync("auth/login", new LoginRequest(_request.Nickname, _request.Senha));

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Nickname ou senha inválidos.";
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        await AuthState.SetTokensAsync(body!);
        Navigation.NavigateTo("/painel");
    }

    private class LoginFormModel
    {
        public string Nickname { get; set; } = "";
        public string Senha { get; set; } = "";
    }
}
```

- [ ] **Step 7: Write the Cadastro (GM) page**

`src/RuinaRPG.Client/Pages/CadastroGm.razor`:

```razor
@page "/cadastro"
@inject HttpClient Http
@inject AuthStateService AuthState
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Auth

<h1>Cadastro do Gamemaster</h1>

<EditForm Model="_request" OnValidSubmit="SubmitAsync">
    <label>Nickname <InputText @bind-Value="_request.Nickname" /></label>
    <label>Email <InputText @bind-Value="_request.Email" /></label>
    <label>Senha <InputText type="password" @bind-Value="_request.Senha" /></label>
    <label>Confirmação da Senha <InputText type="password" @bind-Value="_request.ConfirmacaoSenha" /></label>
    <button type="submit">Cadastrar</button>
</EditForm>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

@code {
    private readonly RegisterGmFormModel _request = new();
    private string? _errorMessage;

    private async Task SubmitAsync()
    {
        var response = await Http.PostAsJsonAsync("auth/register/gm",
            new RegisterGmRequest(_request.Nickname, _request.Email, _request.Senha, _request.ConfirmacaoSenha));

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = await response.Content.ReadAsStringAsync();
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        await AuthState.SetTokensAsync(body!);
        Navigation.NavigateTo("/painel");
    }

    private class RegisterGmFormModel
    {
        public string Nickname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Senha { get; set; } = "";
        public string ConfirmacaoSenha { get; set; } = "";
    }
}
```

- [ ] **Step 8: Add a placeholder `/painel` route with a Logout control (R0004)**

Create `src/RuinaRPG.Client/Pages/Painel.razor` (the real GM/Jogador panels are out of scope for this plan, but Logout — R0004 — must be reachable from any authenticated screen, and this is currently the only one):

```razor
@page "/painel"
@inject HttpClient Http
@inject AuthStateService AuthState
@inject NavigationManager Navigation

<h1>Painel</h1>
<p>Em construção.</p>
<button @onclick="LogoutAsync">Sair</button>

@code {
    private async Task LogoutAsync()
    {
        var refreshToken = await AuthState.GetRefreshTokenAsync();
        if (refreshToken is not null)
            await Http.PostAsJsonAsync("auth/logout", new RuinaRPG.Contracts.Auth.RefreshRequest(refreshToken));

        await AuthState.ClearAsync();
        Navigation.NavigateTo("/");
    }
}
```

- [ ] **Step 9: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Client
git commit -m "feat: add landing, login, and GM signup pages"
```

---

### Task 9: End-to-end smoke test through Docker/nginx

**Files:**
- Modify: `docker-compose.yml` (CORS env var, if not already covering `http://localhost`)
- No new source files — this task only exercises what Tasks 1–8 built.

**Interfaces:**
- Consumes: the full stack (`db`, `api`, `nginx`) built by every earlier task.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers report healthy/running (`docker compose ps`).

- [ ] **Step 2: Register a GM through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/gm \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeTestGm","email":"smoke@teste.com","senha":"Senha!123","confirmacaoSenha":"Senha!123"}'
```

Expected: HTTP 201 with a JSON body containing `accessToken` and `refreshToken`.

- [ ] **Step 3: Log in with the same credentials**

```bash
curl -sf -X POST http://localhost/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeTestGm","senha":"Senha!123"}'
```

Expected: HTTP 200 with a fresh token pair.

- [ ] **Step 4: Load the Blazor client**

```bash
curl -sf http://localhost/ | grep -q "Ruína RPG"
```

Expected: exits 0 — nginx is serving the built Blazor `index.html`/app shell (note: the page title/text is client-rendered, so this check confirms nginx serves the shell; full rendering verification is a manual browser check).

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if Step 1's CORS fix was needed)**

```bash
git add docker-compose.yml
git commit -m "chore: verify end-to-end smoke test through nginx"
```

---

## Explicitly out of scope for this plan

- Jogador registration (needs `InviteCodes` — next plan, "Convite de Jogador").
- The real GM/Jogador painéis (`/painel` is a placeholder).
- Password recovery by e-mail (intentionally GM-reset-only, per `Requisitos - Login e Cadastro.md`).
- SignalR (not needed until the Encontro plan).
