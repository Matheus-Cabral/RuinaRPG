using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration;

// "Testing" is neither Development nor Production: Program.cs skips its startup migrate/seed, so the
// database must already be initialized (a clone of the template, see ApiFixture), or the caller must run
// DatabaseInitializer.InitializeAsync explicitly.
public class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // "Testing" isn't Development, so the host no longer validates DI scopes on its own; keep the
        // captive-dependency check the suite had before 2.0.0.
        builder.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; });
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // One host serves a whole test class; production's 10 logins/min would throttle it.
        builder.UseSetting("RateLimiting:LoginPermitLimit", "100000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RuinaRpgDbContext>>();
            // Mesmo registro do Program.cs, só com outra connection string: sem o interceptor aqui, o log de
            // ficha não seria gravado em nenhum teste.
            services.AddDbContext<RuinaRpgDbContext>((sp, options) => options
                .UseNpgsql(connectionString)
                .AddInterceptors(sp.GetRequiredService<SheetLogInterceptor>()));

            // Jwt options are env-var-only in prod (docker-compose.yml's Jwt__* mappings); supply
            // non-secret test values, as JwtTokenServiceTests does.
            services.Configure<JwtOptions>(options =>
            {
                options.SigningKey = "integration-test-signing-key-needs-32-bytes-minimum";
                options.Issuer = "RuinaRPG.Tests";
                options.Audience = "RuinaRPG.Tests";
            });

            // Program.cs falls back to "/images", a container-only path; use a temp directory.
            services.Configure<ImageStorageOptions>(options =>
            {
                options.ImagesPath = Path.Combine(Path.GetTempPath(), "ruinarpg-test-images");
                options.MaxSizeMb = 10;
            });
        });
    }
}
