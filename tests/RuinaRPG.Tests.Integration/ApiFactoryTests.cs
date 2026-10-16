using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

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
}
