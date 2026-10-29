using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RuinaRPG.Infrastructure.Images;

namespace RuinaRPG.Tests.Integration;

public class ApiFactoryTests
{
    private sealed class ScopedDep;

    private sealed class SingletonNeedingScoped(ScopedDep dep)
    {
        public ScopedDep Dep { get; } = dep;
    }

    [Fact]
    public async Task Test_hosts_validate_di_scopes_so_a_captive_dependency_fails_the_host_build()
    {
        var fixture = new ApiFixture();
        await fixture.InitializeAsync();
        try
        {
            await using var factory = new ApiFactory(fixture.ConnectionString).WithWebHostBuilder(b =>
                b.ConfigureServices(s =>
                {
                    s.AddScoped<ScopedDep>();
                    s.AddSingleton<SingletonNeedingScoped>();
                }));

            var build = () => factory.Services;

            build.Should().Throw<Exception>().Where(e => e.ToString().Contains(nameof(ScopedDep)));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    // Each OS user gets its own directory: the runner user (ghrunner) and a developer running the
    // suite on the same host must never share one, or the second gets "Permission denied".
    [Fact]
    public async Task Test_images_live_in_a_temp_directory_of_the_current_user()
    {
        var fixture = new ApiFixture();
        await fixture.InitializeAsync();
        try
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);

            var path = factory.Services.GetRequiredService<IOptions<ImageStorageOptions>>().Value.ImagesPath;

            path.Should().StartWith(Path.GetTempPath());
            Path.GetFileName(path).Should().Be($"ruinarpg-test-images-{Environment.UserName}");
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }
}
