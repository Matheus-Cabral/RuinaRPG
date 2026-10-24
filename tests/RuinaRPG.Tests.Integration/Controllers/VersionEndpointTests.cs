using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts;
using RuinaRPG.Domain;

namespace RuinaRPG.Tests.Integration.Controllers;

public class VersionEndpointTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Version_is_anonymous_and_reports_app_version_and_build_sha()
    {
        var response = await Client.GetAsync("/api/version");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VersionResponse>();
        body!.Versao.Should().Be(AppVersionInfo.Current);
        body.Sha.Should().Be(BuildInfo.ShaOf(typeof(Program).Assembly));
    }
}
