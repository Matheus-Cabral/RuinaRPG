using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Images;
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

            // Jwt options have no appsettings defaults (they're env-var-only in prod, per the
            // "no secret hardcoded" constraint - see docker-compose.yml's Jwt__* mappings). The
            // test host has no such env vars, so supply non-secret test-fixture values here,
            // mirroring the pattern already used in JwtTokenServiceTests.
            services.Configure<JwtOptions>(options =>
            {
                options.SigningKey = "integration-test-signing-key-needs-32-bytes-minimum";
                options.Issuer = "RuinaRPG.Tests";
                options.Audience = "RuinaRPG.Tests";
            });

            // Storage:ImagesPath has no appsettings default that's writable inside the test
            // container (Program.cs falls back to "/images", the production container-only
            // path). Point it at a temp directory instead, mirroring the JwtOptions override
            // above.
            services.Configure<ImageStorageOptions>(options =>
            {
                options.ImagesPath = Path.Combine(Path.GetTempPath(), "ruinarpg-test-images");
                options.MaxSizeMb = 10;
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.Database.Migrate();
        });
    }
}
