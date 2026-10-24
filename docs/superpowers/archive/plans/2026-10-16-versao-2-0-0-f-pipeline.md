# Versão 2.0.0 — F: Validated pipeline + zero-downtime deploy — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build, on `release/2.0.0`, everything needed so a PR to `main` is validated against a throwaway clone of production and, if it passes, auto-merged and deployed blue-green with automatic rollback — without touching production during F.

**Architecture:** One bash deploy script (`deploy/deploy.sh`) drives a new compose stack (`db`, `api-blue`, `api-green`, long-lived `web` nginx, client releases in a volume switched by symlink); the same script runs on the clone (this VM) and on production (behind a root-owned forced-command gate). Data loss is detected by snapshotting every row as JSON before/after and diffing in Python against an allowlist. A read-only .NET smoke console mints JWTs locally and GETs every campaign/sheet/encounter/catalog. GitHub Actions on a self-hosted runner orchestrate. The client gets a retry handler, a longer SignalR retry policy and a "new version" snackbar.

**Tech Stack:** bash, Docker Compose v5, nginx 1.27, PostgreSQL 16 (`pg_dump -Fc`), Python 3.12 (stdlib + PyYAML, already on this VM), bats-core 1.11.0 (fetched to a git-ignored cache by a runner script), .NET 8 (API, Blazor WASM, console), xUnit/bUnit, GitHub Actions + `gh`.

**Spec:** `docs/superpowers/archive/specs/2026-10-16-versao-2-0-0-f-pipeline-design.md` (umbrella: `docs/superpowers/specs/2026-10-16-versao-2-0-0-roadmap-design.md`).

**Refinements to the spec decided while planning** (no user decision changed):
- **bats** is fetched by `deploy/tests/run-bats.sh` into `deploy/tests/.bats/` (git-ignored, pinned v1.11.0) and run with the host's bash, instead of the `bats/bats` image: the deploy tests must drive the host's Docker and compose, which that image does not ship. Nothing is installed system-wide.
- **Fingerprint** = `deploy/fingerprint.sh` (psql inside the `db` container dumps every row as JSON lines, keyed by primary key) + `deploy/compare.py` (Python; column-level diff restricted to snapshot A's columns, allowlist from YAML). The spec's "separate `fingerprint` database" is replaced by files on the runner — same detection, no cross-database SQL. Compare logic is unit-tested in Python; `fingerprint.sh` is covered by a bats test against a real Postgres container.
- The **build sha** is stamped through MSBuild's `SourceRevisionId` (→ `AssemblyInformationalVersion` `x.y.z+<sha>`), read by a tiny `BuildInfo` helper in `RuinaRPG.Domain` used by both the API and the client.
- `deploy.sh` does not require root: it only uses `sudo -u <owner>` for git when it runs as root (production). On the clone and in tests it runs as the invoking user (who must be in the `docker` group).

## Global Constraints

- `dotnet build` ends with **0 warnings, 0 errors** (`dotnet build --no-incremental` before each commit that touches C#).
- TDD (Técnico R0011): failing test first for every unit of behavior — C#, Python and bash (bats).
- **Production (`192.168.80.10`) is never contacted during F.** No task runs `ssh deploy@…`, edits `/opt`, or touches the user's running stacks. Tests use throwaway compose projects named `ruinarpg-bats*` / `ruinarpg-clone*` and remove them (`down --volumes`) when done.
- Compose project for production is pinned `name: ruinarpg`; volumes stay `pgdata` and `images` (→ `ruinarpg_pgdata`, `ruinarpg_images`).
- Exact names: header `X-Ruina-Manutencao: 1`; label `deploy:janela`; endpoint `GET /api/version` → `{ "versao", "sha" }`; snackbar text `Nova versão disponível`, action `Recarregar`, icon `Icons.Material.Filled.Refresh`; backups `/var/backups/ruinarpg/<UTC yyyyMMddTHHmmssZ>-<sha7>/` (production), keep 5; off-machine copies `/var/backups/ruinarpg-offsite/` on this VM, keep 5; releases kept 3; health timeout 120 s; drain 30 s; client retry cap ~90 s; SignalR retry up to 5 min.
- UI uses MudBlazor icons, never emojis.
- Commit messages in Portuguese, conventional prefix, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; `git push` after each task.
- Do not delete or weaken existing test assertions.

## Review Focus

1. **A POST retried after it reached the API** (duplicate sheet/encounter) — `RetryHandler` must retry non-GET methods only on 503 carrying `X-Ruina-Manutencao: 1`. Pinned in Task 2 (`Post_is_not_retried_on_a_plain_503`, `Post_is_not_retried_on_a_network_failure`).
2. **A deploy that leaves nginx pointing at a dead upstream** (reload with a stopped color, or first deploy on an empty stack) — `deploy.sh` must start and health-check the new color before writing `upstream.conf`, and never reload onto a color that is down. Pinned in Task 8 (`first deploy serves the app`, `failed health check keeps the old color serving`).
3. **Rollback that loses data written after the deploy** — zero-downtime rollback must not restore a backup (schema is compatible, data written since is kept); only window mode restores, and only the backup taken inside the window. Pinned in Task 9 (`zero-downtime rollback keeps rows written after the switch`).
4. **Fingerprint false negatives**: a row deleted from a table without a primary key, or a value changed to NULL — both must be reported. Pinned in Task 6 (`test_lost_row_in_table_without_pk`, `test_value_changed_to_null`).
5. **Gate bypass**: `deploy <sha>; rm -rf /` or a sha not on `origin/main` — the gate must reject anything not matching its exact grammar. Pinned in Task 10 (`rejects command chaining`, `rejects sha not on main`).

---

## File Structure

| File | Responsibility |
|---|---|
| `src/RuinaRPG.Domain/BuildInfo.cs` | Extract the git sha from an assembly's informational version |
| `src/RuinaRPG.Contracts/VersionResponse.cs` | `record VersionResponse(string Versao, string Sha)` |
| `src/RuinaRPG.Api/Program.cs` | `MapGet("/api/version")` |
| `src/RuinaRPG.Client/Services/RetryHandler.cs` | Invisible retry |
| `src/RuinaRPG.Client/Services/LongReconnectPolicy.cs` | SignalR retry policy (2–5 s, ≤ 5 min) |
| `src/RuinaRPG.Client/Services/NewVersionWatcher.cs` | Compares client sha with `/api/version`, raises an event |
| `src/RuinaRPG.Client/Shared/NewVersionNotice.razor` | Snackbar on the event; mounted in `MainLayout` |
| `tools/RuinaRPG.Smoke/` | Read-only smoke console + Dockerfile |
| `tests/RuinaRPG.Tests.Unit/Smoke/` | Smoke unit tests |
| `docker-compose.yml` | New production stack |
| `deploy/nginx/default.conf`, `deploy/nginx/upstream.conf.example` | nginx config (bind-mounted) |
| `infra/client/Dockerfile` | Client build image (replaces `infra/nginx/Dockerfile`) |
| `deploy/lib.sh` | Shared bash helpers for deploy/clone/gate |
| `deploy/deploy.sh` | `deploy`, `rollback`, `backup`, `restore`, `status` |
| `deploy/gate.sh` | Forced-command gate |
| `deploy/fingerprint.sh`, `deploy/compare.py`, `deploy/data-changes.yml` | Data-loss detection |
| `deploy/clone.sh` | Runner-side clone orchestration |
| `deploy/tests/run-bats.sh`, `deploy/tests/*.bats`, `deploy/tests/test_compare.py`, `deploy/tests/fixtures/` | Tests |
| `.github/workflows/release.yml`, `.github/workflows/deploy.yml` | Pipeline |
| `deploy/provision/*.md`, `deploy/provision/*.sh` | One-time setup runbooks/scripts |
| `Makefile`, `.gitignore`, `.dockerignore` (if absent), `Docs/Requisitos/Requisitos - Técnico.md`, `README.md`, `CLAUDE.md` | Glue and docs |

---

### Task 1: Build sha + `GET /api/version`

**Files:**
- Create: `src/RuinaRPG.Domain/BuildInfo.cs`, `src/RuinaRPG.Contracts/VersionResponse.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (next to `MapGet("/api/health")`), `src/RuinaRPG.Api/Dockerfile`
- Test: `tests/RuinaRPG.Tests.Unit/BuildInfoTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/VersionEndpointTests.cs`

**Interfaces:**
- Produces: `public static class RuinaRPG.Domain.BuildInfo { public static string ShaOf(System.Reflection.Assembly assembly); public static string ShaFromInformationalVersion(string? informationalVersion); }` — returns the part after `+` (max 40 hex chars) or `"dev"` when absent. `public record RuinaRPG.Contracts.VersionResponse(string Versao, string Sha);` Endpoint `GET /api/version` (anonymous) → `VersionResponse(AppVersionInfo.Current, BuildInfo.ShaOf(typeof(Program).Assembly))`.

- [ ] **Step 1: Failing tests**

```csharp
// tests/RuinaRPG.Tests.Unit/BuildInfoTests.cs
using FluentAssertions;
using RuinaRPG.Domain;
using Xunit;

namespace RuinaRPG.Tests.Unit;

public class BuildInfoTests
{
    [Theory]
    [InlineData("1.0.0+0123456789abcdef0123456789abcdef01234567", "0123456789abcdef0123456789abcdef01234567")]
    [InlineData("1.0.0+abc1234", "abc1234")]
    [InlineData("1.0.0", "dev")]
    [InlineData(null, "dev")]
    [InlineData("1.0.0+not-a-sha!", "dev")]
    public void Sha_is_the_hex_suffix_after_plus_or_dev(string? informational, string expected) =>
        BuildInfo.ShaFromInformationalVersion(informational).Should().Be(expected);
}
```

```csharp
// tests/RuinaRPG.Tests.Integration/Controllers/VersionEndpointTests.cs
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
```

(Check `AppVersionInfo`'s namespace with `grep -n namespace src/RuinaRPG.Domain/AppVersionInfo.cs` and fix the `using`.)

- [ ] **Step 2: Run, expect build failure** — `dotnet build` → `BuildInfo`/`VersionResponse` not found.

- [ ] **Step 3: Implement**

```csharp
// src/RuinaRPG.Domain/BuildInfo.cs
using System.Reflection;
using System.Text.RegularExpressions;

namespace RuinaRPG.Domain;

/// The git commit a build came from, stamped by the Dockerfiles through MSBuild's SourceRevisionId
/// (AssemblyInformationalVersion "x.y.z+<sha>"). "dev" for local builds without it.
public static partial class BuildInfo
{
    public static string ShaOf(Assembly assembly) =>
        ShaFromInformationalVersion(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static string ShaFromInformationalVersion(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+') ?? -1;
        if (plus < 0) return "dev";
        var suffix = informationalVersion![(plus + 1)..];
        return HexSha().IsMatch(suffix) ? suffix : "dev";
    }

    [GeneratedRegex("^[0-9a-f]{7,40}$")]
    private static partial Regex HexSha();
}
```

```csharp
// src/RuinaRPG.Contracts/VersionResponse.cs
namespace RuinaRPG.Contracts;

public record VersionResponse(string Versao, string Sha);
```

`Program.cs`, right after the two health endpoints:

```csharp
// Read by the client to notice a new deploy (Técnico R0014); anonymous like /api/health.
app.MapGet("/api/version", () => new VersionResponse(AppVersionInfo.Current, BuildInfo.ShaOf(typeof(Program).Assembly)));
```

`src/RuinaRPG.Api/Dockerfile` — accept and pass the sha:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG GIT_SHA=dev
WORKDIR /src
COPY . .
RUN dotnet publish src/RuinaRPG.Api/RuinaRPG.Api.csproj -c Release -o /app -p:SourceRevisionId=$GIT_SHA

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RuinaRPG.Api.dll"]
```

(When `GIT_SHA=dev`, `BuildInfo` returns `"dev"` because "dev" isn't hex — intended.)

- [ ] **Step 4: Verify** — build 0 warnings; `dotnet test tests/RuinaRPG.Tests.Unit --filter BuildInfoTests` and `dotnet test tests/RuinaRPG.Tests.Integration --filter VersionEndpointTests` pass.

- [ ] **Step 5: Commit + push** — `feat: endpoint /api/version com a versão e o sha do build`.

---

### Task 2: Client `RetryHandler`

**Files:**
- Create: `src/RuinaRPG.Client/Services/RetryHandler.cs`
- Modify: `src/RuinaRPG.Client/Program.cs:24-27`
- Test: `tests/RuinaRPG.Tests.Client/Services/RetryHandlerTests.cs`

**Interfaces:**
- Produces: `public sealed class RetryHandler(Func<TimeSpan, CancellationToken, Task>? delay = null) : DelegatingHandler` with `public const string MaintenanceHeader = "X-Ruina-Manutencao";` and `public static readonly TimeSpan TotalBudget = TimeSpan.FromSeconds(90);` Delays 1, 2, 4, 8, 16, 32 s, then 32 s repeated, until the next wait would exceed `TotalBudget` (sum of waits ≤ 90 s); then returns the last response / rethrows the last exception.

- [ ] **Step 1: Failing tests**

```csharp
using System.Net;
using FluentAssertions;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class RetryHandlerTests
{
    private sealed class ScriptedHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var step = steps[Math.Min(Calls, steps.Length - 1)];
            Calls++;
            return Task.FromResult(step(request));
        }
    }

    private static HttpResponseMessage Maintenance()
    {
        var r = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        r.Headers.Add(RetryHandler.MaintenanceHeader, "1");
        return r;
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);
    private static HttpResponseMessage Throw() => throw new HttpRequestException("network down");

    private static (HttpClient Client, ScriptedHandler Inner, List<TimeSpan> Waits) Build(params Func<HttpRequestMessage, HttpResponseMessage>[] steps)
    {
        var waits = new List<TimeSpan>();
        var inner = new ScriptedHandler(steps);
        var handler = new RetryHandler((d, _) => { waits.Add(d); return Task.CompletedTask; }) { InnerHandler = inner };
        return (new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") }, inner, waits);
    }

    [Fact]
    public async Task Post_is_retried_on_maintenance_503_until_success()
    {
        var (client, inner, waits) = Build(_ => Maintenance(), _ => Maintenance(), _ => Status(HttpStatusCode.Created));

        var response = await client.PostAsync("api/x", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        inner.Calls.Should().Be(3);
        waits.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Post_is_not_retried_on_a_plain_503()
    {
        var (client, inner, _) = Build(_ => Status(HttpStatusCode.ServiceUnavailable), _ => Status(HttpStatusCode.OK));

        var response = await client.PostAsync("api/x", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Post_is_not_retried_on_a_network_failure()
    {
        var (client, inner, _) = Build(_ => Throw(), _ => Status(HttpStatusCode.OK));

        var act = () => client.PostAsync("api/x", new StringContent("{}"));

        await act.Should().ThrowAsync<HttpRequestException>();
        inner.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Get_is_retried_on_502_and_503(HttpStatusCode code)
    {
        var (client, inner, _) = Build(_ => Status(code), _ => Status(HttpStatusCode.OK));

        (await client.GetAsync("api/x")).StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Get_is_retried_on_a_network_failure()
    {
        var (client, inner, _) = Build(_ => Throw(), _ => Status(HttpStatusCode.OK));

        (await client.GetAsync("api/x")).StatusCode.Should().Be(HttpStatusCode.OK);
        inner.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Other_statuses_are_returned_untouched()
    {
        var (client, inner, _) = Build(_ => Status(HttpStatusCode.InternalServerError));

        (await client.GetAsync("api/x")).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Gives_up_when_the_total_wait_would_exceed_the_budget()
    {
        var (client, _, waits) = Build(_ => Maintenance());

        var response = await client.GetAsync("api/x");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        waits.Sum(w => w.TotalSeconds).Should().BeLessThanOrEqualTo(RetryHandler.TotalBudget.TotalSeconds);
        waits.Should().StartWith(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) });
    }
}
```

- [ ] **Step 2: Run, expect build failure.**

- [ ] **Step 3: Implement**

```csharp
using System.Net;

namespace RuinaRPG.Client.Services;

/// Holds requests through a deploy (Técnico R0014): during a maintenance window nginx answers 503 with
/// X-Ruina-Manutencao — the request never reached the API, so any method is retried. Other 502/503s and
/// network failures are retried only for GET/HEAD, since a POST may already have been processed.
/// No message is shown; the page just loads a bit slower.
public sealed class RetryHandler(Func<TimeSpan, CancellationToken, Task>? delay = null) : DelegatingHandler
{
    public const string MaintenanceHeader = "X-Ruina-Manutencao";
    public static readonly TimeSpan TotalBudget = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MaxStep = TimeSpan.FromSeconds(32);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var idempotent = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
        var waited = TimeSpan.Zero;
        var step = TimeSpan.FromSeconds(1);

        while (true)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
                var maintenance = response.StatusCode == HttpStatusCode.ServiceUnavailable && response.Headers.Contains(MaintenanceHeader);
                var transient = response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable;
                if (!(maintenance || (idempotent && transient)) || waited + step > TotalBudget)
                    return response;
            }
            catch (HttpRequestException) when (idempotent && waited + step <= TotalBudget)
            {
            }

            response?.Dispose();
            await _delay(step, cancellationToken);
            waited += step;
            step = step * 2 > MaxStep ? MaxStep : step * 2;
        }
    }
}
```

`Program.cs` chain (retry outermost, so a 401 refresh by `BearerTokenHandler` is not re-run on each retry attempt? No — put `RetryHandler` **after** `BearerTokenHandler` so the bearer is attached once and the retry re-sends the same request):

```csharp
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddTransient(_ => new RetryHandler());
builder.Services
    .AddHttpClient("Api", client => client.BaseAddress = httpClientBaseAddress)
    .AddHttpMessageHandler<BearerTokenHandler>()
    .AddHttpMessageHandler<RetryHandler>();
```

Note: `HttpRequestMessage` content can be re-sent by `HttpClientHandler` in Blazor WASM (browser fetch buffers it); `StringContent`/`JsonContent` are re-readable. Do not add content cloning.

- [ ] **Step 4: Verify** — client tests `RetryHandlerTests` pass; build 0 warnings; full `dotnet test tests/RuinaRPG.Tests.Client` still green.

- [ ] **Step 5: Commit + push** — `feat(client): retentativa invisível durante a janela de manutenção do deploy`.

---

### Task 3: SignalR long reconnect policy

**Files:**
- Create: `src/RuinaRPG.Client/Services/LongReconnectPolicy.cs`
- Modify: `src/RuinaRPG.Client/Services/SignalRNotificationConnection.cs:23`, `src/RuinaRPG.Client/Services/SignalREncounterConnection.cs:16`
- Test: `tests/RuinaRPG.Tests.Client/Services/LongReconnectPolicyTests.cs`

**Interfaces:**
- Produces: `public sealed class LongReconnectPolicy : IRetryPolicy` — `NextRetryDelay(RetryContext ctx)` returns `0 s` for the first retry, then `2 s`, `3 s`, `5 s`, then `5 s` repeatedly, and `null` once `ctx.ElapsedTime >= 5 min`.

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using RuinaRPG.Client.Services;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class LongReconnectPolicyTests
{
    private static TimeSpan? Next(long previousRetries, TimeSpan elapsed) =>
        new LongReconnectPolicy().NextRetryDelay(new RetryContext { PreviousRetryCount = previousRetries, ElapsedTime = elapsed });

    [Fact]
    public void Retries_quickly_then_every_five_seconds() =>
        new[] { Next(0, TimeSpan.Zero), Next(1, TimeSpan.FromSeconds(1)), Next(2, TimeSpan.FromSeconds(3)), Next(3, TimeSpan.FromSeconds(6)), Next(10, TimeSpan.FromSeconds(60)) }
            .Should().Equal(TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    [Fact]
    public void Gives_up_after_five_minutes() =>
        Next(60, TimeSpan.FromMinutes(5)).Should().BeNull();
}
```

- [ ] **Step 2: Run, expect build failure.**

- [ ] **Step 3: Implement**

```csharp
using Microsoft.AspNetCore.SignalR.Client;

namespace RuinaRPG.Client.Services;

/// The default automatic reconnect gives up after ~42 s, shorter than a deploy's maintenance window
/// (Técnico R0014). This keeps trying every few seconds for up to five minutes.
public sealed class LongReconnectPolicy : IRetryPolicy
{
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan[] FirstDelays = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)];

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.ElapsedTime >= GiveUpAfter) return null;
        return retryContext.PreviousRetryCount < FirstDelays.Length
            ? FirstDelays[retryContext.PreviousRetryCount]
            : TimeSpan.FromSeconds(5);
    }
}
```

In both connection classes replace `.WithAutomaticReconnect()` with `.WithAutomaticReconnect(new LongReconnectPolicy())`.

- [ ] **Step 4: Verify** — tests pass; build 0 warnings.

- [ ] **Step 5: Commit + push** — `feat(client): SignalR tenta reconectar por até 5 minutos`.

---

### Task 4: New version notice

**Files:**
- Create: `src/RuinaRPG.Client/Services/NewVersionWatcher.cs`, `src/RuinaRPG.Client/Shared/NewVersionNotice.razor`
- Modify: `src/RuinaRPG.Client/Program.cs` (register `NewVersionWatcher` scoped), `src/RuinaRPG.Client/Layout/MainLayout.razor` (render `<NewVersionNotice />`), `src/RuinaRPG.Client/Services/SignalRNotificationConnection.cs` (call `watcher.CheckAsync()` on `Reconnected`), `src/RuinaRPG.Client/wwwroot/index.html` (focus hook), `infra/client/Dockerfile` is created in Task 7 and passes `-p:SourceRevisionId=$GIT_SHA`
- Test: `tests/RuinaRPG.Tests.Client/Services/NewVersionWatcherTests.cs`, `tests/RuinaRPG.Tests.Client/Shared/NewVersionNoticeTests.cs`

**Interfaces:**
- Consumes: `GET /api/version` → `VersionResponse` (Task 1); `BuildInfo.ShaOf` (Task 1).
- Produces: `public sealed class NewVersionWatcher(HttpClient http, string clientSha)` with `event Action? NewVersionAvailable;`, `Task CheckAsync()` (GETs `/api/version`; raises the event at most once when the API sha differs from `clientSha` and **neither is `"dev"`**; swallows `HttpRequestException`/non-success), `void Start()` (a `PeriodicTimer` of 5 min calling `CheckAsync`), `[JSInvokable] Task OnFocusAsync()` (calls `CheckAsync`). DI registration passes `BuildInfo.ShaOf(typeof(App).Assembly)`.

- [ ] **Step 1: Failing tests**

```csharp
// NewVersionWatcherTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class NewVersionWatcherTests
{
    private sealed class VersionHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromResult(respond());
    }

    private static NewVersionWatcher Watcher(string clientSha, Func<HttpResponseMessage> respond) =>
        new(new HttpClient(new VersionHandler(respond)) { BaseAddress = new Uri("https://localhost/") }, clientSha);

    private static HttpResponseMessage Api(string sha) => new(HttpStatusCode.OK) { Content = JsonContent.Create(new VersionResponse("2.0.0", sha)) };

    [Fact]
    public async Task Raises_once_when_the_api_sha_differs()
    {
        var watcher = Watcher("aaaaaaa", () => Api("bbbbbbb"));
        var raised = 0;
        watcher.NewVersionAvailable += () => raised++;

        await watcher.CheckAsync();
        await watcher.CheckAsync();

        raised.Should().Be(1);
    }

    [Theory]
    [InlineData("aaaaaaa", "aaaaaaa")]
    [InlineData("dev", "bbbbbbb")]
    [InlineData("aaaaaaa", "dev")]
    public async Task Stays_quiet_when_same_or_unknown(string clientSha, string apiSha)
    {
        var watcher = Watcher(clientSha, () => Api(apiSha));
        var raised = false;
        watcher.NewVersionAvailable += () => raised = true;

        await watcher.CheckAsync();

        raised.Should().BeFalse();
    }

    [Fact]
    public async Task Failures_are_ignored()
    {
        var watcher = Watcher("aaaaaaa", () => throw new HttpRequestException());
        await watcher.Invoking(w => w.CheckAsync()).Should().NotThrowAsync();
    }
}
```

`NewVersionNoticeTests.cs` (bUnit, using the existing `MudBunitContext` helper in `tests/RuinaRPG.Tests.Client/MudBunitContext.cs` — read it to see how it registers MudBlazor and a snackbar provider): render `NewVersionNotice` with a `NewVersionWatcher` whose handler returns a different sha, call `CheckAsync()`, and assert the rendered `MudSnackbarProvider` markup contains `Nova versão disponível` and `Recarregar`.

- [ ] **Step 2: Run, expect build failure.**

- [ ] **Step 3: Implement**

```csharp
using System.Net.Http.Json;
using Microsoft.JSInterop;
using RuinaRPG.Contracts;

namespace RuinaRPG.Client.Services;

/// Notices that a new version was deployed while this tab was open (Técnico R0014) by comparing this
/// client's build sha with the API's. Checked every 5 minutes, on SignalR reconnect and on window focus.
public sealed class NewVersionWatcher(HttpClient http, string clientSha) : IAsyncDisposable
{
    private bool _raised;
    private PeriodicTimer? _timer;

    public event Action? NewVersionAvailable;

    public async Task CheckAsync()
    {
        if (_raised || clientSha == "dev") return;
        try
        {
            var api = await http.GetFromJsonAsync<VersionResponse>("api/version");
            if (api is null || api.Sha == "dev" || api.Sha == clientSha) return;
            _raised = true;
            NewVersionAvailable?.Invoke();
        }
        catch (HttpRequestException) { }
    }

    public void Start()
    {
        if (_timer is not null) return;
        _timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        _ = Task.Run(async () => { while (await _timer.WaitForNextTickAsync()) await CheckAsync(); });
    }

    [JSInvokable]
    public Task OnFocusAsync() => CheckAsync();

    public ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

(If `GetFromJsonAsync` on a non-success status throws `HttpRequestException` — it does — that is the swallowed path. `NotSupportedException`/`JsonException` from a malformed body: catch them too.)

`NewVersionNotice.razor`:

```razor
@implements IDisposable
@inject NewVersionWatcher Watcher
@inject ISnackbar Snackbar
@inject NavigationManager Navigation
@inject IJSRuntime JS

@code {
    private DotNetObjectReference<NewVersionWatcher>? _ref;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        Watcher.NewVersionAvailable += Show;
        Watcher.Start();
        _ref = DotNetObjectReference.Create(Watcher);
        try { await JS.InvokeVoidAsync("ruinaVersion.onFocus", _ref); } catch (JSException) { }
        await Watcher.CheckAsync();
    }

    private void Show() => InvokeAsync(() => Snackbar.Add("Nova versão disponível", Severity.Info, options =>
    {
        options.Icon = Icons.Material.Filled.Refresh;
        options.RequireInteraction = true;
        options.Action = "Recarregar";
        options.OnClick = _ => { Navigation.NavigateTo(Navigation.Uri, forceLoad: true); return Task.CompletedTask; };
    }));

    public void Dispose()
    {
        Watcher.NewVersionAvailable -= Show;
        _ref?.Dispose();
    }
}
```

In `index.html`, before `blazor.webassembly.js`:

```html
<script>
  window.ruinaVersion = { onFocus: function (ref) { window.addEventListener('focus', function () { ref.invokeMethodAsync('OnFocusAsync'); }); } };
</script>
```

`Program.cs`: `builder.Services.AddScoped(sp => new NewVersionWatcher(sp.GetRequiredService<HttpClient>(), BuildInfo.ShaOf(typeof(App).Assembly)));`. `SignalRNotificationConnection`: inject `NewVersionWatcher watcher` and add `connection.Reconnected += _ => watcher.CheckAsync();` next to the existing `Reconnected` handler. `MainLayout.razor`: add `<NewVersionNotice />` next to the existing snackbar-using markup.

- [ ] **Step 4: Verify** — client tests green (whole project, since `SignalRNotificationConnection`'s constructor changed — fix any test that constructs it); build 0 warnings.

- [ ] **Step 5: Commit + push** — `feat(client): aviso de nova versão disponível com botão Recarregar`.

---

### Task 5: Read-only smoke console

**Files:**
- Create: `tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj`, `tools/RuinaRPG.Smoke/Program.cs`, `tools/RuinaRPG.Smoke/SmokePlan.cs`, `tools/RuinaRPG.Smoke/SmokeSettings.cs`, `tools/RuinaRPG.Smoke/Dockerfile`
- Modify: `RuinaRPG.sln` (add project), `tests/RuinaRPG.Tests.Unit/RuinaRPG.Tests.Unit.csproj` (reference it)
- Test: `tests/RuinaRPG.Tests.Unit/Smoke/SmokeSettingsTests.cs`, `tests/RuinaRPG.Tests.Integration/Smoke/SmokeRunTests.cs`

**Interfaces:**
- Produces:
  - `record SmokeSettings(string BaseUrl, string ConnectionString, JwtOptions Jwt)` with `static SmokeSettings FromEnvironment(IDictionary env)` reading `SMOKE_BASE_URL`, `POSTGRES_HOST` (default `db`), `POSTGRES_DB` (default `ruinarpg`), `POSTGRES_USER`, `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`; throws `InvalidOperationException` naming the missing variable.
  - `record SmokeRequest(Guid UserId, string Path)`.
  - `static class SmokePlan { static async Task<IReadOnlyList<SmokeRequest>> BuildAsync(RuinaRpgDbContext db); }` — the request list, built only from reads.
  - `static class SmokeRunner { static async Task<IReadOnlyList<string>> RunAsync(HttpClient http, IJwtTokenService jwt, RuinaRpgDbContext db, IReadOnlyList<SmokeRequest> plan); }` — sends each GET with a token for its user; a failure is `"<status> <path>"` when status ≠ 200 or the body is not valid JSON; returns the failures.
  - `Program`: exit 0 when no failures, 1 otherwise; prints `OK <n> requests` or one failure per line; never sends anything but GET.

The plan (`SmokePlan.BuildAsync`), every id read with `AsNoTracking`:
- for each campaign `c` (as its GM `c.GmId`): `/api/campaigns`, `/api/campaigns/{c}/members`, `/api/campaigns/{c}/character-sheets`, `/api/campaigns/{c}/encounters`, `/api/campaigns/{c}/diary`, `/api/campaigns/{c}/available-items`;
- for each character sheet `s` (as the GM of its campaign): `/api/character-sheets/{s}`;
- for each NPC sheet and creature sheet (as its owning GM — check the entity for the property name, e.g. `GmId`): `/api/npc-sheets/{id}`, `/api/creature-sheets/{id}`;
- for each encounter `e` (as the GM of its campaign): `/api/encounters/{e}/log`;
- for each GM user `g`: `/api/items`, `/api/spell-ability-bank`, `/api/traits`, `/api/rulebook`, `/api/npc-sheets`, `/api/creature-sheets`, `/api/auth/me`.

- [ ] **Step 1: Failing tests**

The unit project tests `SmokeSettings`; the plan and runner are tested against a real API in the integration project (this repo does not use EF InMemory):

```csharp
// tests/RuinaRPG.Tests.Unit/Smoke/SmokeSettingsTests.cs
using FluentAssertions;
using RuinaRPG.Smoke;
using Xunit;

namespace RuinaRPG.Tests.Unit.Smoke;

public class SmokeSettingsTests
{
    private static Dictionary<string, string?> Env() => new()
    {
        ["SMOKE_BASE_URL"] = "http://api-blue:8080", ["POSTGRES_USER"] = "u", ["POSTGRES_PASSWORD"] = "p",
        ["JWT_SIGNING_KEY"] = new string('k', 40), ["JWT_ISSUER"] = "i", ["JWT_AUDIENCE"] = "a",
    };

    [Fact]
    public void Reads_settings_with_defaults_for_host_and_database()
    {
        var s = SmokeSettings.FromEnvironment(Env());
        s.BaseUrl.Should().Be("http://api-blue:8080");
        s.ConnectionString.Should().Contain("Host=db").And.Contain("Database=ruinarpg");
        s.Jwt.Issuer.Should().Be("i");
    }

    [Fact]
    public void Names_the_missing_variable()
    {
        var env = Env(); env.Remove("JWT_SIGNING_KEY");
        var act = () => SmokeSettings.FromEnvironment(env);
        act.Should().Throw<InvalidOperationException>().WithMessage("*JWT_SIGNING_KEY*");
    }
}
```

(`FromEnvironment` takes `IReadOnlyDictionary<string, string?>`; `Program` builds it from `Environment.GetEnvironmentVariables()`.)

```csharp
// tests/RuinaRPG.Tests.Integration/Smoke/SmokeRunTests.cs
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Smoke;

namespace RuinaRPG.Tests.Integration.Smoke;

public class SmokeRunTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Every_planned_request_succeeds_against_a_populated_api()
    {
        var gm = await RegisterGmAndGetTokenAsync("SmokeGm", "smokegm@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Smoke");
        await RegisterJogadorLinkedToAsync(gm, "SmokePlayer", "smokeplayer@teste.com");
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gm));
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", gm));
        // character sheet + encounter: use the same requests CharacterSheetsControllerTests / EncountersControllerTests use

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var plan = await SmokePlan.BuildAsync(db);

        var failures = await SmokeRunner.RunAsync(Client, jwt, db, plan);

        plan.Should().Contain(r => r.Path == $"/api/campaigns/{campaignId}/members");
        plan.Should().Contain(r => r.Path.StartsWith("/api/npc-sheets/"));
        failures.Should().BeEmpty();
    }

    [Fact]
    public async Task A_broken_route_is_reported()
    {
        var gm = await RegisterGmAndGetTokenAsync("SmokeGm2", "smokegm2@teste.com");
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var gmId = (await db.Users.SingleAsync(u => u.Email == "smokegm2@teste.com")).Id;

        var failures = await SmokeRunner.RunAsync(Client, jwt, db, [new SmokeRequest(gmId, "/api/does-not-exist")]);

        failures.Should().ContainSingle().Which.Should().Be("404 /api/does-not-exist");
    }
}
```

For the character sheet and encounter creation, copy the exact request bodies from the existing tests (`grep -n "character-sheets\"" tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs | head`; same for encounters) so the plan contains those paths too, and assert `plan` contains a `/api/character-sheets/` and a `/log` entry.

- [ ] **Step 2: Run, expect build failure.**

- [ ] **Step 3: Implement** the console (`net8.0`, `OutputType Exe`, `RootNamespace RuinaRPG.Smoke`, references `src/RuinaRPG.Infrastructure`). `SmokeRunner` mints the token with `jwt.CreateAccessToken(user)` (load users once into a dictionary). `Program.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RuinaRPG.Infrastructure.Auth;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Smoke;

// Read-only post-deploy smoke (Técnico R0013/R0014): GETs every campaign, sheet, encounter and catalog as
// their real users, with tokens minted locally — no password is touched, nothing is written.
var env = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
    .ToDictionary(e => (string)e.Key, e => (string?)e.Value);
var settings = SmokeSettings.FromEnvironment(env);

await using var db = new RuinaRpgDbContext(new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(settings.ConnectionString).Options);
var jwt = new JwtTokenService(Options.Create(settings.Jwt));
using var http = new HttpClient { BaseAddress = new Uri(settings.BaseUrl), Timeout = TimeSpan.FromSeconds(30) };

var plan = await SmokePlan.BuildAsync(db);
var failures = await SmokeRunner.RunAsync(http, jwt, db, plan);
foreach (var f in failures) Console.WriteLine(f);
Console.WriteLine(failures.Count == 0 ? $"OK {plan.Count} requests" : $"FAILED {failures.Count}/{plan.Count}");
return failures.Count == 0 ? 0 : 1;
```

(Check `RuinaRpgDbContext`'s constructor and `JwtOptions` property names; adapt.) `tools/RuinaRPG.Smoke/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish tools/RuinaRPG.Smoke/RuinaRPG.Smoke.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "RuinaRPG.Smoke.dll"]
```

- [ ] **Step 4: Verify** — build 0 warnings (the new project is in the solution); unit + integration tests above pass.

- [ ] **Step 5: Commit + push** — `feat: smoke somente-leitura que percorre campanhas, fichas, encontros e catálogos`.

---

### Task 6: Fingerprint and compare

**Files:**
- Create: `deploy/compare.py`, `deploy/fingerprint.sh`, `deploy/data-changes.yml`, `deploy/tests/test_compare.py`, `deploy/tests/run-bats.sh`, `deploy/tests/fingerprint.bats`, `deploy/lib.sh`
- Modify: `.gitignore` (add `deploy/tests/.bats/`, `deploy/state/`)

**Interfaces:**
- Produces:
  - `deploy/fingerprint.sh <compose-project> <out.jsonl>` — writes one JSON object per line `{"table": "...", "pk": "<pk values joined by |, or null>", "row": {<col>: <value as text or null>}}` for every base table in schema `public` except `__EFMigrationsHistory`, via `docker compose -p <project> exec -T db psql`.
  - `python3 deploy/compare.py <a.jsonl> <b.jsonl> <allowlist.yml> [--markdown <report.md>]` — exit 0 when no unallowed findings, 1 otherwise; prints and optionally writes a Markdown report (`### <table>` with lost/altered counts and the altered column names, up to 20 sample pks per table).
  - `deploy/data-changes.yml`: `changes: []` (format: list of `{table, columns: [..] | "*", change: altered|lost, reason}`).
  - `deploy/tests/run-bats.sh [files…]` — downloads `https://github.com/bats-core/bats-core/archive/refs/tags/v1.11.0.tar.gz` once into `deploy/tests/.bats/` and runs `deploy/tests/.bats/bin/bats` on the given files (default: all `deploy/tests/*.bats`).
  - `deploy/lib.sh` — `die`, `log` (to stderr, prefixed with UTC time), `compose` wrapper (`docker compose -p "$RUINA_PROJECT" -f "$RUINA_REPO_DIR/docker-compose.yml" --env-file "$RUINA_ENV_FILE" --env-file "$RUINA_STATE_DIR/colors.env"`), `require_cmd`.

Rules in `compare.py`:
- Rows are matched by `(table, pk)`; for tables with `pk == null`, rows are compared as a multiset of their JSON (only A's columns).
- **lost**: in A, not in B. **altered**: same key, some column present in A differs in B (a value changed to NULL counts). Columns only in B are ignored; tables only in B are ignored.
- A finding is allowed when an allowlist entry has the same `table`, the same `change`, and `columns` is `"*"` or contains every differing column.

- [ ] **Step 1: Failing Python tests**

```python
# deploy/tests/test_compare.py
import json, os, subprocess, sys, tempfile, textwrap, unittest

HERE = os.path.dirname(__file__)
COMPARE = os.path.join(HERE, "..", "compare.py")

def write(path, rows):
    with open(path, "w") as f:
        for r in rows:
            f.write(json.dumps(r) + "\n")

def run(a, b, allow="changes: []\n"):
    d = tempfile.mkdtemp()
    pa, pb, pl = (os.path.join(d, n) for n in ("a.jsonl", "b.jsonl", "allow.yml"))
    write(pa, a); write(pb, b)
    open(pl, "w").write(textwrap.dedent(allow))
    p = subprocess.run([sys.executable, COMPARE, pa, pb, pl], capture_output=True, text=True)
    return p.returncode, p.stdout

def row(table, pk, **cols):
    return {"table": table, "pk": pk, "row": cols}

class CompareTests(unittest.TestCase):
    def test_identical_snapshots_pass(self):
        rows = [row("Users", "1", Name="a")]
        self.assertEqual(run(rows, rows)[0], 0)

    def test_lost_row_fails(self):
        code, out = run([row("Users", "1", Name="a"), row("Users", "2", Name="b")], [row("Users", "1", Name="a")])
        self.assertEqual(code, 1); self.assertIn("Users", out); self.assertIn("lost", out)

    def test_altered_value_fails_and_names_the_column(self):
        code, out = run([row("Users", "1", Name="a", Age="3")], [row("Users", "1", Name="z", Age="3")])
        self.assertEqual(code, 1); self.assertIn("Name", out); self.assertNotIn("Age", out)

    def test_value_changed_to_null(self):
        code, _ = run([row("Users", "1", Name="a")], [row("Users", "1", Name=None)])
        self.assertEqual(code, 1)

    def test_new_column_new_row_new_table_are_ignored(self):
        a = [row("Users", "1", Name="a")]
        b = [row("Users", "1", Name="a", Extra="x"), row("Users", "2", Name="b"), row("NewTable", "1", X="y")]
        self.assertEqual(run(a, b)[0], 0)

    def test_lost_row_in_table_without_pk(self):
        code, _ = run([row("Log", None, Msg="a"), row("Log", None, Msg="a")], [row("Log", None, Msg="a")])
        self.assertEqual(code, 1)

    def test_allowlisted_alteration_passes(self):
        allow = """
        changes:
          - { table: Users, columns: [Name], change: altered, reason: rename }
        """
        self.assertEqual(run([row("Users", "1", Name="a")], [row("Users", "1", Name="z")], allow)[0], 0)

    def test_allowlist_for_other_column_does_not_cover(self):
        allow = """
        changes:
          - { table: Users, columns: [Age], change: altered, reason: x }
        """
        self.assertEqual(run([row("Users", "1", Name="a")], [row("Users", "1", Name="z")], allow)[0], 1)

    def test_allowlisted_loss_with_star_passes(self):
        allow = """
        changes:
          - { table: Users, columns: "*", change: lost, reason: purge }
        """
        self.assertEqual(run([row("Users", "1", Name="a")], [], allow)[0], 0)

if __name__ == "__main__":
    unittest.main()
```

Run: `python3 -m unittest deploy/tests/test_compare.py -v` → errors (compare.py missing).

- [ ] **Step 2: Implement `compare.py`** (stdlib `json`, `argparse`, `collections.Counter`, plus `yaml.safe_load`), following the rules above. Report format (stdout and `--markdown`):

```
## Comparação de dados (clone)
Resultado: FALHOU — 2 achado(s) não previsto(s) em deploy/data-changes.yml
### Users
- lost: 1 linha(s) (pk: 2)
- altered: 1 linha(s), colunas: Name (pk: 1)
```

(`Resultado: OK` when clean.) Run the tests → all pass.

- [ ] **Step 3: bats runner + failing fingerprint test**

`deploy/tests/run-bats.sh`:

```bash
#!/usr/bin/env bash
# Runs the deploy bats tests with a pinned bats-core fetched into a git-ignored cache (nothing installed system-wide).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
bats_dir="$here/.bats"
if [[ ! -x "$bats_dir/bin/bats" ]]; then
  mkdir -p "$bats_dir"
  curl -fsSL https://github.com/bats-core/bats-core/archive/refs/tags/v1.11.0.tar.gz | tar -xz -C "$bats_dir" --strip-components=1
fi
if (( $# == 0 )); then set -- "$here"/*.bats; fi
exec "$bats_dir/bin/bats" "$@"
```

`deploy/tests/fingerprint.bats` — starts a throwaway Postgres in its own compose-less container is not enough because `fingerprint.sh` talks to a compose project; use a minimal compose file `deploy/tests/fixtures/pg-only.yml` (`services: { db: { image: postgres:16-alpine, environment: { POSTGRES_DB: ruinarpg, POSTGRES_USER: t, POSTGRES_PASSWORD: t } } }`):

```bash
setup_file() {
  export PROJ="ruinarpg-bats-fp-$$"
  docker compose -p "$PROJ" -f "$BATS_TEST_DIRNAME/fixtures/pg-only.yml" up -d --wait
  docker compose -p "$PROJ" -f "$BATS_TEST_DIRNAME/fixtures/pg-only.yml" exec -T db psql -U t -d ruinarpg -v ON_ERROR_STOP=1 -c '
    CREATE TABLE "Users"("Id" int PRIMARY KEY, "Name" text);
    CREATE TABLE "Log"("Msg" text);
    CREATE TABLE "__EFMigrationsHistory"("MigrationId" text PRIMARY KEY);
    INSERT INTO "Users" VALUES (1, $$a$$), (2, NULL);
    INSERT INTO "Log" VALUES ($$x$$);
    INSERT INTO "__EFMigrationsHistory" VALUES ($$m1$$);'
}
teardown_file() { docker compose -p "$PROJ" -f "$BATS_TEST_DIRNAME/fixtures/pg-only.yml" down --volumes; }

@test "fingerprint dumps every row as JSON keyed by primary key, skipping the migrations table" {
  RUINA_COMPOSE_FILE="$BATS_TEST_DIRNAME/fixtures/pg-only.yml" RUINA_DB_USER=t \
    run "$BATS_TEST_DIRNAME/../fingerprint.sh" "$PROJ" "$BATS_TEST_TMPDIR/a.jsonl"
  [ "$status" -eq 0 ]
  run python3 -c 'import json,sys; rows=[json.loads(l) for l in open(sys.argv[1])]; print(sorted((r["table"], r["pk"]) for r in rows))' "$BATS_TEST_TMPDIR/a.jsonl"
  [ "$output" = "[('Log', None), ('Users', '1'), ('Users', '2')]" ]
  grep -q '"Name": null' "$BATS_TEST_TMPDIR/a.jsonl"
}
```

Run `deploy/tests/run-bats.sh deploy/tests/fingerprint.bats` → fails (no fingerprint.sh).

- [ ] **Step 4: Implement `fingerprint.sh`** — `RUINA_COMPOSE_FILE` (default `$repo/docker-compose.yml`) and `RUINA_DB_USER` (default read from the db container's `$POSTGRES_USER`) overridable. It runs, inside the db container, one SQL that builds the JSON lines:

```sql
SELECT json_build_object('table', t.relname, 'pk', pk.val, 'row', to_jsonb(r) )
-- generated per table by a plpgsql DO block / or by a bash loop over:
--   SELECT c.relname, array_agg(a.attname ORDER BY k.ord) FILTER (WHERE a.attname IS NOT NULL)
--   FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace AND n.nspname='public'
--   LEFT JOIN pg_index i ON i.indrelid=c.oid AND i.indisprimary
--   LEFT JOIN LATERAL unnest(i.indkey) WITH ORDINALITY k(attnum, ord) ON true
--   LEFT JOIN pg_attribute a ON a.attrelid=c.oid AND a.attnum=k.attnum
--   WHERE c.relkind='r' AND c.relname <> '__EFMigrationsHistory' GROUP BY c.relname;
```

Concretely: a bash loop gets `table<TAB>pkcols` pairs, then for each table runs
`psql -At -c "SELECT json_build_object('table', '<t>', 'pk', <pkexpr>, 'row', (SELECT jsonb_object_agg(key, value #>> '{}') FROM jsonb_each(to_jsonb(x))))::text FROM \"<t>\" x"`
where `<pkexpr>` is `concat_ws('|', x."c1"::text, x."c2"::text)` or `NULL` when the table has no pk; values become text (`#>> '{}'`), JSON null stays null. Identifiers are double-quoted. Append all output to the out file. Run the bats test → pass.

- [ ] **Step 5: Commit + push** — `feat(deploy): impressão digital dos dados e comparação com allowlist`.

---

### Task 7: New compose stack, nginx config, client build image

**Files:**
- Modify: `docker-compose.yml` (rewrite), `.gitignore`
- Create: `deploy/nginx/default.conf`, `deploy/nginx/upstream.conf.example`, `infra/client/Dockerfile`, `.dockerignore` (if absent: `**/bin`, `**/obj`, `**/TestResults`, `.git`, `deploy/state`, `deploy/tests/.bats`, `tools/bench/results`)
- Delete: `infra/nginx/Dockerfile`, `infra/nginx/nginx.conf` (content moves to `deploy/nginx/default.conf`)

No behavior test of its own: Task 8's bats tests are its tests (first deploy serves the app). Verify here with `docker compose config` and an nginx syntax check.

**Interfaces:**
- Produces: compose services `db`, `api-blue`, `api-green`, `web`, `client-build` (profile `tools`), `smoke` (profile `tools`); variables `BLUE_TAG`, `GREEN_TAG` (from `deploy/state/colors.env`), `HTTP_BIND` (default `0.0.0.0`), `HTTP_PORT` (default `80`); named volumes `pgdata`, `images`, `webroot`; network `default` (→ `<project>_default`). nginx upstream name `app`; maintenance server on `127.0.0.1:8081`.

- [ ] **Step 1: `docker-compose.yml`**

```yaml
name: ruinarpg

x-api: &api
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
    Img__MaxSizeMb: ${IMG_MAX_SIZE_MB}
    Invite__CodeExpirationHours: ${INVITE_CODE_EXPIRATION_HOURS}
  volumes:
    - images:/images

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

  # Blue-green (Técnico R0014): deploy/deploy.sh starts the idle color with the new image, health-checks
  # it, and points nginx's upstream at it. Tags come from deploy/state/colors.env.
  api-blue:
    <<: *api
    image: ruinarpg-api:${BLUE_TAG:-none}
  api-green:
    <<: *api
    image: ruinarpg-api:${GREEN_TAG:-none}

  # Long-lived: never rebuilt by a deploy; config is bind-mounted and reloaded.
  web:
    image: nginx:1.27-alpine
    restart: unless-stopped
    ports:
      - "${HTTP_BIND:-0.0.0.0}:${HTTP_PORT:-80}:80"
    volumes:
      - ./deploy/nginx/default.conf:/etc/nginx/conf.d/default.conf:ro
      - ./deploy/state/upstream.conf:/etc/nginx/upstream.conf:ro
      - webroot:/webroot:ro
      - images:/images:ro

  client-build:
    profiles: ["tools"]
    image: ruinarpg-client:${CLIENT_TAG:-none}
    volumes:
      - webroot:/webroot

  smoke:
    profiles: ["tools"]
    image: ruinarpg-smoke:${SMOKE_TAG:-none}
    environment:
      SMOKE_BASE_URL: ${SMOKE_BASE_URL:-http://api-blue:8080}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      JWT_SIGNING_KEY: ${JWT_SIGNING_KEY}
      JWT_ISSUER: ${JWT_ISSUER}
      JWT_AUDIENCE: ${JWT_AUDIENCE}

volumes:
  pgdata:
  images:
  webroot:
```

Note: the bind mount of `deploy/state/upstream.conf` is a **file** mount: `deploy.sh` must rewrite it in place (`cat > file`), never replace it with `mv`, or the container keeps the old inode.

- [ ] **Step 2: nginx config** — `deploy/nginx/default.conf`:

```nginx
include /etc/nginx/upstream.conf;   # defines "upstream app { … }" — rewritten by deploy/deploy.sh

# Maintenance window (Técnico R0014): the request never reaches the API; the client retries on this header.
server {
    listen 127.0.0.1:8081;
    location / {
        add_header X-Ruina-Manutencao 1 always;
        return 503;
    }
}

server {
    listen 80;

    location /api/ {
        # Matches ImagesController's [RequestSizeLimit(20_000_000)].
        client_max_body_size 20m;
        proxy_pass http://app;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        # Keeps the per-IP rate limiter (Técnico R0006) per client instead of one bucket.
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    location /hubs/ {
        proxy_pass http://app;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    # Uploaded images live in their own volume, served apart from the client build (no more shadowing).
    location /images/ {
        alias /images/;
    }

    location = /index.html {
        root /webroot/current;
        add_header Cache-Control "no-cache";
    }

    location = /_framework/blazor.boot.json {
        root /webroot/current;
        add_header Cache-Control "no-cache";
    }

    location / {
        root /webroot/current;
        try_files $uri $uri/ /index.html;
    }
}
```

`deploy/nginx/upstream.conf.example`: `upstream app { server api-blue:8080; }`.

- [ ] **Step 3: `infra/client/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG GIT_SHA=dev
WORKDIR /src
COPY . .
RUN dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release -o /app -p:SourceRevisionId=$GIT_SHA

# Holds the published client; deploy/deploy.sh copies /app/wwwroot into webroot/releases/<sha>.
FROM alpine:3.20
COPY --from=build /app/wwwroot /app/wwwroot
```

- [ ] **Step 4: Verify** — `HTTP_PORT=8090 BLUE_TAG=x GREEN_TAG=y docker compose -f docker-compose.yml --env-file .env.example config -q` exits 0; `docker run --rm -v "$PWD/deploy/nginx/default.conf:/etc/nginx/conf.d/default.conf:ro" -v "$PWD/deploy/nginx/upstream.conf.example:/etc/nginx/upstream.conf:ro" --add-host api-blue:127.0.0.1 nginx:1.27-alpine nginx -t` prints `syntax is ok`. Update `Makefile` `deploy up` target to call `deploy/deploy.sh deploy "$$(git rev-parse HEAD)"` and `migrate` to `deploy/deploy.sh migrate` (a subcommand added in Task 8) — keep the other targets, replacing `api` with the active color via `deploy/deploy.sh exec-api …` (Task 8 adds it). Grep `README.md`/`CLAUDE.md` for `infra/nginx` and leave a note for Task 12.

- [ ] **Step 5: Commit + push** — `feat(deploy): stack blue-green com nginx de longa duração e cliente em volume versionado`.

---

### Task 8: `deploy.sh` — first deploy, zero-downtime switch, backup, status

**Files:**
- Create: `deploy/deploy.sh`, `deploy/tests/deploy.bats`, `deploy/tests/fixtures/fake-smoke.sh`, `deploy/tests/helpers.bash`
- Modify: `deploy/lib.sh`

**Interfaces:**
- Consumes: compose stack (Task 7), smoke image (Task 5), `lib.sh` (Task 6).
- Produces: `deploy/deploy.sh <cmd>`:
  - `deploy <sha> [--window]` (window in Task 9), `backup`, `restore <backup-dir>`, `status`, `migrate`, `exec-api <args…>`, `rollback` (Task 9).
  - Configuration by environment (defaults are production's): `RUINA_REPO_DIR` (script's repo), `RUINA_PROJECT` (`ruinarpg`), `RUINA_ENV_FILE` (`$RUINA_REPO_DIR/.env`), `RUINA_STATE_DIR` (`$RUINA_REPO_DIR/deploy/state`), `RUINA_BACKUP_DIR` (`/var/backups/ruinarpg`), `RUINA_KEEP_BACKUPS` (5), `RUINA_KEEP_RELEASES` (3), `RUINA_HEALTH_TIMEOUT` (120), `RUINA_DRAIN_SECONDS` (30), `RUINA_SMOKE_CMD` (unset → run the `smoke` compose service; set → `bash -c "$RUINA_SMOKE_CMD"` with `RUINA_COLOR` exported — tests use it), `RUINA_SKIP_FETCH` (1 in tests: don't `git fetch`).
  - State file `$RUINA_STATE_DIR/colors.env`: `ACTIVE`, `BLUE_TAG`, `GREEN_TAG`, `CURRENT_SHA`, `PREVIOUS_SHA`, `PREVIOUS_COLOR`, `LAST_BACKUP`, `WINDOW_BACKUP`, `MODE`.
  - Last stdout line of `deploy`: `RESULT mode=<zero-downtime|window> sha=<sha> active=<color> backup=<dir> smoke=<ok|failed> rollback=<none|ok|failed>`.

`deploy <sha>` (zero-downtime) sequence:
1. `flock -n 9` on `$RUINA_STATE_DIR/deploy.lock` → `die "outro deploy em andamento"`.
2. Unless `RUINA_SKIP_FETCH=1`: `git fetch --quiet origin`. Then `git checkout --quiet --detach <sha>`. When `EUID == 0`, git runs as the repo owner (`sudo -u "$(stat -c %U "$RUINA_REPO_DIR")" git …`).
3. `backup` (see below) → `LAST_BACKUP`.
4. Build: `docker build -q -f src/RuinaRPG.Api/Dockerfile --build-arg GIT_SHA=<sha> -t ruinarpg-api:<sha> .`, same for `infra/client/Dockerfile` → `ruinarpg-client:<sha>`, `tools/RuinaRPG.Smoke/Dockerfile` → `ruinarpg-smoke:<sha>` — skip any tag that already exists (`docker image inspect`).
5. Publish client: `CLIENT_TAG=<sha> compose run --rm client-build sh -c 'mkdir -p /webroot/releases && rm -rf /webroot/releases/<sha> && cp -r /app/wwwroot /webroot/releases/<sha>'`.
6. Colors: `old=$ACTIVE` (`none` on first deploy), `new` = the other color (`blue` when `old` is `none` or `green`). Write `<NEW>_TAG=<sha>` to `colors.env`.
7. `compose up -d db --wait`; migrate with the new image: `compose run --rm --no-deps api-<new> dotnet RuinaRPG.Api.dll --migrate`.
8. `compose up -d --no-deps api-<new>`; health: loop until `RUINA_HEALTH_TIMEOUT`: `docker run --rm --network "${RUINA_PROJECT}_default" busybox:1.36 wget -qO- "http://api-<new>:8080/health"` returns `"OK"`. On timeout: stop `api-<new>`, keep `old` serving, `die "health check falhou"` (no switch happened).
9. Switch: `compose run --rm client-build sh -c 'ln -sfn releases/<sha> /webroot/current.tmp && mv -Tf /webroot/current.tmp /webroot/current'`; write `upstream app { server api-<new>:8080; }` **in place** into `$RUINA_STATE_DIR/upstream.conf`; if `web` is not running → `compose up -d web`, else `compose exec -T web nginx -s reload`.
10. Smoke: `RUINA_SMOKE_CMD` or `SMOKE_TAG=<sha> SMOKE_BASE_URL=http://api-<new>:8080 compose run --rm smoke`.
11. Smoke OK: sleep `RUINA_DRAIN_SECONDS`, `compose stop api-<old>` (if not `none`), update state (`ACTIVE=new`, `PREVIOUS_SHA`, `PREVIOUS_COLOR=old`, `CURRENT_SHA`, `MODE`), prune `webroot/releases` and `ruinarpg-{api,client,smoke}:*` images beyond the newest `RUINA_KEEP_RELEASES` (never the active or previous sha), print RESULT, exit 0.
12. Smoke failed: Task 9 adds the rollback; in this task, exit 1 after restoring the upstream to `old` (or leaving it if `old` is `none`) — the bats test for rollback lives in Task 9.

`backup`: `dir=$RUINA_BACKUP_DIR/$(date -u +%Y%m%dT%H%M%SZ)-<sha7 of CURRENT_SHA or "inicial">`; `compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -Fc ruinarpg' > $dir/db.dump`; `docker run --rm -v "${RUINA_PROJECT}_images:/images:ro" busybox:1.36 tar -C /images -cf - . > $dir/images.tar`; write `$dir/sha`; keep newest `RUINA_KEEP_BACKUPS`; print `$dir`.
`restore <dir>`: `compose exec -T db sh -c 'pg_restore -U "$POSTGRES_USER" -d ruinarpg --clean --if-exists --no-owner'` < db.dump; images: `docker run --rm -i -v "${RUINA_PROJECT}_images:/images" busybox:1.36 sh -c 'rm -rf /images/* && tar -C /images -xf -'` < images.tar.
`status`: prints `colors.env` (without secrets — it has none) and `ls -1t $RUINA_BACKUP_DIR | head -5`.
`migrate`: runs step 7's migrate against the active color's image. `exec-api <args>`: `compose exec api-$ACTIVE dotnet RuinaRPG.Api.dll <args>` (for `make grant-rules-auditor` etc.).

- [ ] **Step 1: Failing bats tests** — `deploy/tests/helpers.bash` creates an isolated environment per test file: `git clone --quiet --shared "$REPO" "$BATS_FILE_TMPDIR/repo"` of the repo under test at `HEAD`, a `.env` copied from `.env.example` with `ASPNETCORE_ENVIRONMENT=Production`, a random 48-char `JWT_SIGNING_KEY`, and exports `RUINA_REPO_DIR`, `RUINA_PROJECT=ruinarpg-bats-$$`, `RUINA_BACKUP_DIR=$BATS_FILE_TMPDIR/backups`, `RUINA_SKIP_FETCH=1`, `RUINA_DRAIN_SECONDS=0`, `HTTP_PORT=18089`, `HTTP_BIND=127.0.0.1` (put `HTTP_PORT`/`HTTP_BIND` in the test `.env`), and a `teardown_file` running `docker compose -p "$RUINA_PROJECT" … down --volumes --remove-orphans` and removing `ruinarpg-*:<test sha>` images only if they were created by the test (tag them with the clone's sha; the clone's HEAD is the same sha as the working repo, so **do not** delete images in teardown — they are reused by the next run and by the real deploy; document this in the helper).
  `deploy/tests/fixtures/fake-smoke.sh`: exits with `${FAKE_SMOKE_EXIT:-0}` after appending `$RUINA_COLOR` to `$BATS_FILE_TMPDIR/smoke.log`.
  Tests in `deploy/tests/deploy.bats` (one stack per file; tests run in order):

```bash
load helpers

@test "first deploy serves the app on the blue color" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == *"active=blue"*"smoke=ok"* ]]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
  run curl -fsS "http://127.0.0.1:18089/api/version"
  [[ "$output" == *"\"sha\":\"$SHA\""* ]]
  run curl -fsSI "http://127.0.0.1:18089/"
  [[ "$output" == *"Cache-Control: no-cache"* ]]
}

@test "second deploy switches to green with no failed request during the switch" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  ( end=$((SECONDS+240)); fails=0; while (( SECONDS < end )); do curl -fsS -o /dev/null "http://127.0.0.1:18089/api/health" || fails=$((fails+1)); [[ -f "$BATS_FILE_TMPDIR/stop" ]] && break; sleep 0.2; done; echo "$fails" > "$BATS_FILE_TMPDIR/fails" ) &
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  touch "$BATS_FILE_TMPDIR/stop"; wait
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == *"active=green"* ]]
  [ "$(cat "$BATS_FILE_TMPDIR/fails")" -eq 0 ]
  run docker compose -p "$RUINA_PROJECT" ps --status running --services
  [[ "$output" != *"api-blue"* ]]
}

@test "backup writes db.dump and images.tar and keeps the newest five" {
  for i in 1 2 3 4 5 6; do "$RUINA_REPO_DIR/deploy/deploy.sh" backup >/dev/null; sleep 1; done
  [ "$(ls -1 "$RUINA_BACKUP_DIR" | wc -l)" -eq 5 ]
  newest="$RUINA_BACKUP_DIR/$(ls -1t "$RUINA_BACKUP_DIR" | head -1)"
  [ -s "$newest/db.dump" ] && [ -f "$newest/images.tar" ]
}

@test "failed health check keeps the old color serving" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  export RUINA_HEALTH_TIMEOUT=5
  docker tag busybox:1.36 "ruinarpg-api:$BROKEN_SHA"   # a fake "API" that never answers /health
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$BROKEN_SHA"
  [ "$status" -ne 0 ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "a concurrent deploy is refused" {
  exec 9>"$RUINA_STATE_DIR/deploy.lock"; flock 9
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  flock -u 9
  [ "$status" -ne 0 ]
  [[ "$output" == *"outro deploy em andamento"* ]]
}
```

`BROKEN_SHA` is a commit created in the test clone (`git commit --allow-empty -m broken`) whose image tag is pre-seeded so the build is skipped; `deploy.sh` must therefore skip building a tag that already exists (step 4). `RUINA_STATE_DIR` defaults inside the clone. Run: `deploy/tests/run-bats.sh deploy/tests/deploy.bats` → fails (no deploy.sh).

- [ ] **Step 2: Implement `deploy.sh`** with `set -euo pipefail`, sourcing `lib.sh`, one function per subcommand, following the sequence above. Every docker/compose call goes through `compose` from `lib.sh`. Log each step with `log`.

- [ ] **Step 3: Run the bats file** → all pass. The first run builds three images (several minutes); use generous timeouts and `run_in_background`.

- [ ] **Step 4: Commit + push** — `feat(deploy): deploy.sh com troca blue-green sem downtime, backup e status`.

---

### Task 9: `deploy.sh` — window mode and rollback

**Files:**
- Modify: `deploy/deploy.sh`
- Test: `deploy/tests/rollback.bats` (its own stack; reuses `helpers.bash`)

**Interfaces:**
- Produces: `deploy <sha> --window`; automatic rollback on smoke failure; `rollback` subcommand. RESULT line fields `mode`, `rollback`.

Window sequence (replaces steps 7–9 of Task 8 when `--window`):
1. Write `upstream app { server 127.0.0.1:8081; }` in place; reload (`web` must be running — if not, behave as zero-downtime first deploy).
2. `WINDOW_BACKUP=$(backup)`.
3. `compose stop api-<old>`; migrate with the new image; `compose up -d --no-deps api-<new>`; health check (on timeout → rollback window).
4. Switch `current` + upstream to `api-<new>`; reload.

Rollback (automatic after a failed smoke or failed window health, or `deploy.sh rollback` on demand using state):
- **zero-downtime** (`MODE=zero-downtime`, old color still running): upstream + `current` back to `api-<old>` / `releases/<PREVIOUS_SHA>`; reload; `compose stop api-<new>`; state restored. **No restore.**
- **window**: upstream → maintenance; `compose stop api-<new>`; `restore $WINDOW_BACKUP`; `compose up -d --no-deps api-<old>`; health; upstream + `current` → old; reload.
- RESULT `rollback=ok` (or `failed` + exit 2 if the old color fails health — then nginx stays in maintenance and the message says to run `deploy.sh status` and restore manually).

- [ ] **Step 1: Failing bats tests** (`rollback.bats`, in order):

```bash
load helpers

@test "setup: first deploy" {
  RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh" run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  [ "$status" -eq 0 ]
}

@test "zero-downtime rollback keeps rows written after the switch" {
  # A smoke that writes a row while the new color is live, then fails — the row must survive the rollback.
  export RUINA_SMOKE_CMD="docker compose -p $RUINA_PROJECT exec -T db sh -c 'psql -U \"\$POSTGRES_USER\" -d ruinarpg -c \"CREATE TABLE IF NOT EXISTS smoke_marker(x int); INSERT INTO smoke_marker VALUES (1)\"' && exit 1"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA"
  [ "$status" -ne 0 ]
  [[ "${lines[-1]}" == *"rollback=ok"* ]]
  [[ "${lines[-1]}" == *"active=blue"* ]]
  run docker compose -p "$RUINA_PROJECT" exec -T db sh -c 'psql -U "$POSTGRES_USER" -d ruinarpg -Atc "SELECT count(*) FROM smoke_marker"'
  [ "$output" = "1" ]
  run curl -fsS "http://127.0.0.1:18089/api/health"
  [ "$output" = '"OK"' ]
}

@test "window deploy answers 503 with the maintenance header while switching, then serves the new color" {
  export RUINA_SMOKE_CMD="$BATS_TEST_DIRNAME/fixtures/fake-smoke.sh"
  ( for _ in $(seq 1 600); do curl -s -o /dev/null -D - "http://127.0.0.1:18089/api/health" >> "$BATS_FILE_TMPDIR/window-headers"; sleep 0.2; [[ -f "$BATS_FILE_TMPDIR/stop2" ]] && break; done ) &
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA" --window
  touch "$BATS_FILE_TMPDIR/stop2"; wait
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == *"mode=window"*"active=green"* ]]
  grep -qi "X-Ruina-Manutencao: 1" "$BATS_FILE_TMPDIR/window-headers"
}

@test "window rollback restores the backup taken inside the window" {
  export RUINA_SMOKE_CMD="docker compose -p $RUINA_PROJECT exec -T db sh -c 'psql -U \"\$POSTGRES_USER\" -d ruinarpg -c \"DROP TABLE smoke_marker\"' && exit 1"
  run "$RUINA_REPO_DIR/deploy/deploy.sh" deploy "$SHA" --window
  [ "$status" -ne 0 ]
  [[ "${lines[-1]}" == *"rollback=ok"* ]]
  run docker compose -p "$RUINA_PROJECT" exec -T db sh -c 'psql -U "$POSTGRES_USER" -d ruinarpg -Atc "SELECT count(*) FROM smoke_marker"'
  [ "$output" = "1" ]
}
```

Run → fails.

- [ ] **Step 2: Implement**, Step 3: run `rollback.bats` and `deploy.bats` → all pass. Step 4: commit + push — `feat(deploy): modo janela e rollback automático`.

---

### Task 10: Gate

**Files:**
- Create: `deploy/gate.sh`, `deploy/tests/gate.bats`

**Interfaces:**
- Produces: `deploy/gate.sh` reading `$SSH_ORIGINAL_COMMAND`. Config: `RUINA_REPO_DIR` (default `/home/ruina/RuinaRPG`), `RUINA_DEPLOY` (default `$RUINA_REPO_DIR/deploy/deploy.sh`), `RUINA_SKIP_FETCH`.
  - `dump` → `"$RUINA_DEPLOY" backup` (stdout = dir) then `tar -C "$dir" -cf - .` on stdout.
  - `deploy <40-hex> [--window]` → fetch, `git merge-base --is-ancestor <sha> origin/main` (as repo owner when root) else exit 3 `sha não está na main`; then `exec "$RUINA_DEPLOY" deploy <sha> [--window]`.
  - `status` → `exec "$RUINA_DEPLOY" status`.
  - anything else, including extra tokens or shell metacharacters → stderr `comando recusado`, exit 2. Parsing: `read -r -a words <<< "$SSH_ORIGINAL_COMMAND"`, then match the whole command against `^(dump|status|deploy [0-9a-f]{40}( --window)?)$` before splitting — the regex is the grammar.

- [ ] **Step 1: Failing bats tests** — a temp git repo with an `origin` (bare) whose `main` has commit `M`, and a side commit `S` not on main; `RUINA_DEPLOY` = a fake script recording its argv to a file and echoing a fake backup dir (create that dir with a file inside):

```bash
@test "rejects unknown verbs"            { SSH_ORIGINAL_COMMAND="bash" run "$GATE"; [ "$status" -eq 2 ]; }
@test "rejects command chaining"         { SSH_ORIGINAL_COMMAND="deploy $M; rm -rf /" run "$GATE"; [ "$status" -eq 2 ]; [ ! -s "$CALLS" ]; }
@test "rejects a malformed sha"          { SSH_ORIGINAL_COMMAND="deploy abc" run "$GATE"; [ "$status" -eq 2 ]; }
@test "rejects sha not on main"          { SSH_ORIGINAL_COMMAND="deploy $S" run "$GATE"; [ "$status" -eq 3 ]; [ ! -s "$CALLS" ]; }
@test "deploys a sha on main"            { SSH_ORIGINAL_COMMAND="deploy $M" run "$GATE"; [ "$status" -eq 0 ]; grep -qx "deploy $M" "$CALLS"; }
@test "passes --window through"          { SSH_ORIGINAL_COMMAND="deploy $M --window" run "$GATE"; grep -qx "deploy $M --window" "$CALLS"; }
@test "dump streams a tar of the backup" { SSH_ORIGINAL_COMMAND="dump" run bash -c "$GATE | tar -tf -"; [[ "$output" == *"db.dump"* ]]; }
@test "status is passed through"         { SSH_ORIGINAL_COMMAND="status" run "$GATE"; grep -qx "status" "$CALLS"; }
```

- [ ] **Step 2: Implement**, Step 3: tests pass, Step 4: commit + push — `feat(deploy): porteiro de comando forçado para a chave do pipeline`.

---

### Task 11: Clone orchestration and workflows

**Files:**
- Create: `deploy/clone.sh`, `deploy/tests/clone.bats`, `.github/workflows/release.yml`, `.github/workflows/deploy.yml`

**Interfaces:**
- Consumes: `deploy.sh` (Tasks 8–9), `fingerprint.sh` + `compare.py` (Task 6), smoke (Task 5).
- Produces: `deploy/clone.sh run <base-sha> <head-sha> <workdir>`; config `RUINA_DUMP_CMD` (default `ssh -o BatchMode=yes deploy@192.168.80.10 dump`), `RUINA_CLONE_PROJECT` (default `ruinarpg-clone`), `RUINA_CLONE_PORT` (8089), `RUINA_OFFSITE_DIR` (`/var/backups/ruinarpg-offsite`), `RUINA_KEEP_OFFSITE` (5). Writes into `<workdir>`: `report.md` (Markdown for the PR comment), `compat` (`ok` or `janela`), `logs/*.log`; exit 0 on success, 1 on a validation failure (report explains), 2 on an infrastructure error. Always tears the clone down (`trap`).

`clone.sh run` steps (each logs to `<workdir>/logs/<step>.log` and appends a line to `report.md`):
1. `dump`: `$RUINA_DUMP_CMD > <workdir>/dump.tar`; extract to `<workdir>/dump/`; copy to `$RUINA_OFFSITE_DIR/<backup dir name>/`, keep newest 5.
2. `checkout`: `git worktree add --detach <workdir>/repo <base-sha>`; write `<workdir>/repo/.env` from `.env.example` with `ASPNETCORE_ENVIRONMENT=Production`, generated `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY` (48 random chars), `HTTP_BIND=127.0.0.1`, `HTTP_PORT=$RUINA_CLONE_PORT`.
3. `restore`: export `RUINA_REPO_DIR=<workdir>/repo RUINA_PROJECT=$RUINA_CLONE_PROJECT RUINA_BACKUP_DIR=<workdir>/backups RUINA_SKIP_FETCH=1`; `compose up -d db --wait`; `deploy.sh restore <workdir>/dump`.
4. `base`: `deploy.sh deploy <base-sha>` (smoke runs as part of it).
5. `fingerprint A`.
6. `head`: `git -C <workdir>/repo checkout --detach <head-sha>` is done by `deploy.sh deploy <head-sha>` itself (fetch skipped; the worktree shares the runner's object store, which has the PR head because the workflow checked it out). Smoke failure → rollback happens inside; record and fail.
7. `fingerprint B` + `python3 deploy/compare.py A B <workdir>/repo/deploy/data-changes.yml --markdown <workdir>/compare.md` (the allowlist comes from the **PR head**).
8. `compat`: `compose stop api-<new>`; `<OLD>_TAG=<base-sha>` start `api-<old>` with the base image against the migrated DB; health + smoke against it (`SMOKE_BASE_URL=http://api-<old>:8080`, smoke image of the base sha). Pass → `compat=ok`; fail → `compat=janela` (not a failure of the PR).
9. `teardown` (trap, always): `compose down --volumes --remove-orphans`, `git worktree remove --force <workdir>/repo`, `rm -rf <workdir>/dump <workdir>/dump.tar <workdir>/backups`.

- [ ] **Step 1: Failing bats test** (`clone.bats`): produce a dump from a throwaway stack deployed at `HEAD` (reuse `helpers.bash` + `deploy.sh deploy` + `deploy.sh backup`), set `RUINA_DUMP_CMD="tar -C <that backup dir> -cf - ."`, `RUINA_OFFSITE_DIR=$BATS_FILE_TMPDIR/offsite`, `RUINA_CLONE_PROJECT=ruinarpg-bats-clone-$$`, `RUINA_CLONE_PORT=18090`, then:
  - `@test "clone run passes for an unchanged version and reports compat ok"` → `clone.sh run $SHA $SHA $wd` exits 0, `report.md` contains `Resultado: OK`, `compat` = `ok`, `docker compose -p ruinarpg-bats-clone-$$ ps -q` is empty afterwards, offsite has 1 copy.
  - `@test "clone run fails when the head version deletes data"`: create a commit in the test repo that adds a migration-free change — simplest: a commit whose `deploy/data-changes.yml` is unchanged but whose head deploy runs a smoke that deletes a row (`RUINA_SMOKE_CMD` deleting from `AspNetUsers` after the base fingerprint — set it only for the head deploy via `RUINA_HEAD_SMOKE_CMD`, an env override honored by `clone.sh` for tests) → exit 1, `report.md` contains `lost`.
  Run → fails.
- [ ] **Step 2: Implement `clone.sh`** (honoring `RUINA_HEAD_SMOKE_CMD` only for the head deploy, documented as a test hook).
- [ ] **Step 3: Workflows**

`.github/workflows/release.yml`:

```yaml
name: release
on:
  pull_request:
    branches: [main]
    types: [opened, synchronize, reopened, ready_for_review]
  workflow_dispatch:
    inputs:
      head:
        description: "sha a validar (padrão: HEAD do branch)"
        required: false

concurrency:
  group: release-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  gate:
    runs-on: ubuntu-latest
    steps:
      - name: Somente PRs do próprio repositório e do dono
        run: |
          if [ "${{ github.event_name }}" = "workflow_dispatch" ]; then
            test "${{ github.actor }}" = "${{ github.repository_owner }}"
          else
            test "${{ github.event.pull_request.head.repo.full_name }}" = "${{ github.repository }}"
            test "${{ github.event.pull_request.user.login }}" = "${{ github.repository_owner }}"
          fi

  ci:
    needs: gate
    runs-on: [self-hosted, ruinarpg]
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }
      - run: dotnet build --no-incremental -warnaserror
      - run: dotnet test tests/RuinaRPG.Tests.Unit --no-build
      - run: dotnet test tests/RuinaRPG.Tests.Client --no-build
      - run: dotnet test tests/RuinaRPG.Tests.Integration --no-build
      - run: python3 -m unittest deploy/tests/test_compare.py
      - run: deploy/tests/run-bats.sh deploy/tests/gate.bats deploy/tests/fingerprint.bats

  clone:
    needs: ci
    runs-on: [self-hosted, ruinarpg]
    outputs:
      compat: ${{ steps.run.outputs.compat }}
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }
      - id: run
        run: |
          wd="$RUNNER_TEMP/clone-${{ github.run_id }}"
          head="${{ github.event.pull_request.head.sha || inputs.head || github.sha }}"
          base="$(git rev-parse origin/main)"
          set +e; deploy/clone.sh run "$base" "$head" "$wd"; code=$?; set -e
          echo "compat=$(cat "$wd/compat" 2>/dev/null || echo ok)" >> "$GITHUB_OUTPUT"
          cp "$wd/report.md" "$RUNNER_TEMP/report.md" || true
          exit $code
      - if: always()
        uses: actions/upload-artifact@v4
        with:
          name: clone-logs
          path: ${{ runner.temp }}/clone-${{ github.run_id }}/logs
      - if: always()
        uses: actions/upload-artifact@v4
        with:
          name: clone-report
          path: ${{ runner.temp }}/report.md

  report:
    needs: [gate, ci, clone]
    if: always() && github.event_name == 'pull_request' && needs.gate.result == 'success'
    runs-on: [self-hosted, ruinarpg]
    permissions:
      pull-requests: write
      contents: write
    env:
      GH_TOKEN: ${{ secrets.RELEASE_BOT_TOKEN }}
      PR: ${{ github.event.pull_request.number }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/download-artifact@v4
        with: { name: clone-report, path: report }
        continue-on-error: true
      - name: Rótulo de janela
        if: needs.clone.result == 'success'
        run: |
          if [ "${{ needs.clone.outputs.compat }}" = "janela" ]; then gh pr edit "$PR" --add-label deploy:janela; else gh pr edit "$PR" --remove-label deploy:janela || true; fi
      - name: Sucesso → auto-merge
        if: needs.ci.result == 'success' && needs.clone.result == 'success'
        run: gh pr merge "$PR" --auto --merge
      - name: Falha → comentário e rascunho
        if: needs.ci.result != 'success' || needs.clone.result != 'success'
        run: |
          {
            echo "## Validação do release falhou"
            echo "- ci: ${{ needs.ci.result }}"
            echo "- clone: ${{ needs.clone.result }}"
            echo
            cat report/report.md 2>/dev/null || echo "(sem relatório do clone — veja os logs da execução)"
            echo
            echo "Logs completos: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}"
          } > comment.md
          gh pr comment "$PR" --body-file comment.md
          gh pr ready "$PR" --undo
```

`RELEASE_BOT_TOKEN` is a fine-grained PAT (Task 12 provisioning) because merges by `GITHUB_TOKEN` do not trigger the `push` workflow.

`.github/workflows/deploy.yml`:

```yaml
name: deploy
on:
  push:
    branches: [main]

concurrency:
  group: deploy-production
  cancel-in-progress: false

permissions:
  contents: read
  pull-requests: write

jobs:
  deploy:
    runs-on: [self-hosted, ruinarpg]
    environment: production
    env:
      GH_TOKEN: ${{ secrets.RELEASE_BOT_TOKEN }}
    steps:
      - name: PR e modo
        id: pr
        run: |
          pr=$(gh api "repos/${{ github.repository }}/commits/${{ github.sha }}/pulls" --jq '.[0].number // empty')
          echo "number=$pr" >> "$GITHUB_OUTPUT"
          window=""
          if [ -n "$pr" ] && gh pr view "$pr" -R "${{ github.repository }}" --json labels --jq '.labels[].name' | grep -qx 'deploy:janela'; then window="--window"; fi
          echo "window=$window" >> "$GITHUB_OUTPUT"
      - name: Deploy
        id: deploy
        run: |
          set -o pipefail
          ssh -o BatchMode=yes deploy@192.168.80.10 "deploy ${{ github.sha }} ${{ steps.pr.outputs.window }}" | tee "$RUNNER_TEMP/deploy.log"
      - name: Cópia do backup fora da máquina
        if: always()
        run: |
          d=/var/backups/ruinarpg-offsite/$(date -u +%Y%m%dT%H%M%SZ)-pos-deploy
          mkdir -p "$d" && ssh -o BatchMode=yes deploy@192.168.80.10 dump | tar -C "$d" -xf -
          ls -1dt /var/backups/ruinarpg-offsite/*/ | tail -n +6 | xargs -r rm -rf
      - name: Resultado no PR
        if: always() && steps.pr.outputs.number != ''
        run: |
          { echo "## Deploy em produção: ${{ steps.deploy.outcome }}"; echo '```'; tail -n 20 "$RUNNER_TEMP/deploy.log" 2>/dev/null; echo '```'; } > c.md
          gh pr comment "${{ steps.pr.outputs.number }}" -R "${{ github.repository }}" --body-file c.md
```

Validate both with `docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint:1.7.1 -color` → no errors.

- [ ] **Step 4: Run** `deploy/tests/run-bats.sh deploy/tests/clone.bats` → pass; actionlint clean.
- [ ] **Step 5: Commit + push** — `feat(deploy): orquestração do clone e workflows de release e deploy`.

---

### Task 12: Provisioning runbooks

**Files:**
- Create: `deploy/provision/README.md` (order of the steps), `deploy/provision/producao.md`, `deploy/provision/runner.md`, `deploy/provision/github.md`, `deploy/provision/sudoers-ruinarpg`, `deploy/provision/install-gate.sh`, `deploy/provision/setup-runner.sh`
- Test: `deploy/tests/provision.bats` (static checks only — these scripts are **not run** in F)

Contents (Portuguese, imperative, copy-pasteable):
- **producao.md** (on `192.168.80.10`, as a sudoer): `sudo adduser --disabled-password --gecos "" deploy`; `sudo install -d -m 755 /opt/ruinarpg`; `sudo deploy/provision/install-gate.sh` (copies `deploy/gate.sh` to `/opt/ruinarpg/gate.sh` root:root 0755, installs `sudoers-ruinarpg` to `/etc/sudoers.d/ruinarpg` 0440 after `visudo -cf`, creates `/var/backups/ruinarpg` root 0700); append the runner's public key to `/home/deploy/.ssh/authorized_keys` with the exact prefix `command="sudo /opt/ruinarpg/gate.sh",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding `; verify with `ssh deploy@192.168.80.10 status` from this VM (expects the gate's status output) and `ssh deploy@192.168.80.10 bash` (expects `comando recusado`).
- **sudoers-ruinarpg**: `Defaults!/opt/ruinarpg/gate.sh env_keep += "SSH_ORIGINAL_COMMAND"` and `deploy ALL=(root) NOPASSWD: /opt/ruinarpg/gate.sh`.
- **runner.md** (this VM): fix `chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys`; `sudo adduser --disabled-password --gecos "" ghrunner`; `sudo usermod -aG docker ghrunner` (with the explicit note that the docker group is root-equivalent **on this dev VM**, accepted because the runner must drive the clone); `setup-runner.sh` downloads the Actions runner pinned to a version, registers it with labels `self-hosted,ruinarpg` (token from GitHub settings), installs it as a service under `ghrunner`; generates `ssh-keygen -t ed25519 -f ~ghrunner/.ssh/ruinarpg_deploy -N ""` and prints the public key for producao.md; creates `/var/backups/ruinarpg-offsite` owned by `ghrunner`, 0700; installs .NET 8 SDK if absent (`dotnet --version`).
- **github.md**: Settings → Actions → "Require approval for all external contributors"; Environment `production` with deployment branch rule `main` only; secret `RELEASE_BOT_TOKEN` (fine-grained PAT scoped to this repo: contents read/write, pull requests read/write) in the repo secrets; branch protection on `main`: require status checks `ci` and `clone`, allow auto-merge; create label `deploy:janela`.

`provision.bats`: `bash -n` on both scripts; `visudo -cf` on `sudoers-ruinarpg` when `visudo` exists (skip otherwise); grep that `producao.md` contains the exact authorized_keys prefix and that `gate.sh` and `sudoers-ruinarpg` reference the same path `/opt/ruinarpg/gate.sh`.

- [ ] Steps: failing `provision.bats` → write files → pass → commit + push — `docs(deploy): runbooks de provisionamento da produção, do runner e do GitHub`.

---

### Task 13: Requirements, README, CLAUDE.md, Makefile

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Técnico.md` (add R0013–R0016; update R0007–R0010), `README.md`, `CLAUDE.md`, `Makefile`

- [ ] **Step 1: Técnico** — add, continuing the numbering and the file's existing format (`# **R00NN** - Título.` then `**Descrição**: …`):
  - **R0013 — Releases validados contra um clone da produção.** PR para `main` → CI + clone com dados reais (dump via porteiro), impressão digital antes/depois (perda ou alteração fora de `deploy/data-changes.yml` reprova), smoke somente-leitura, verificação de compatibilidade da versão anterior com o novo schema; reprovação → comentário com diagnóstico e PR volta a rascunho; aprovação → auto-merge.
  - **R0014 — Deploy blue-green sem downtime.** Duas cores da API, nginx de longa duração recarregado, cliente em releases versionados trocados por symlink; migration incompatível → janela de manutenção em que o nginx responde 503 com `X-Ruina-Manutencao` e o cliente retenta sem mensagem; SignalR reconecta por até 5 min; aviso "Nova versão disponível".
  - **R0015 — Backups e rollback.** Backup antes de cada deploy (5 na produção + 5 nesta VM); smoke pós-deploy na própria produção; rollback automático (sem restauração no modo sem downtime; restauração do backup da janela no modo janela).
  - **R0016 — Porteiro de comando forçado.** Chave do pipeline restrita a `dump`, `deploy <sha da main>`, `status`; sudoers só para o porteiro.
  - R0007/R0008/R0009/R0010: replace mentions of `make deploy` + `make migrate` as *the* deploy with "o deploy normal é o pipeline (R0013–R0015); `make deploy`/`make migrate` chamam `deploy/deploy.sh` como caminho manual de emergência"; R0009: migrations in Production run inside `deploy.sh` (one-off container with the new image) before the switch.
- [ ] **Step 2: README.md** — replace the deploy section with: what happens on a PR to `main`, how to read a failed validation comment, how to rehearse (`workflow_dispatch`), manual emergency path, where backups are, link to `deploy/provision/README.md`. Remove references to `infra/nginx`.
- [ ] **Step 3: CLAUDE.md** — update "Build, test, and run" (`make deploy` now calls `deploy/deploy.sh`; bats/python test commands: `deploy/tests/run-bats.sh`, `python3 -m unittest deploy/tests/test_compare.py`), the `make` table, "Development vs. Production" (migrations run inside `deploy.sh`), and the code map (`deploy/`, `tools/RuinaRPG.Smoke`, `.github/workflows/`; `infra/client/` replaces `infra/nginx/`).
- [ ] **Step 4: Makefile** — `deploy up` → `deploy/deploy.sh deploy "$$(git rev-parse HEAD)"`; `down` → `docker compose down`; `migrate` → `deploy/deploy.sh migrate`; CLI targets → `deploy/deploy.sh exec-api --grant-rules-auditor $(EMAIL)` etc.; new `status` and `rollback` targets. Keep `clean`/`clean-data` semantics.
- [ ] **Step 5: Verify** — `make -n deploy migrate status rollback grant-rules-auditor EMAIL=x` prints the expected commands; grep docs for `infra/nginx` → none. Commit + push — `docs: requisitos R0013–R0016 e documentação do novo deploy`.

---

### Task 14: Full verification

- [ ] `dotnet build --no-incremental` → 0 warnings.
- [ ] `dotnet test` (all three projects) → green (integration expected 1807 + new tests from Tasks 1 and 5).
- [ ] `python3 -m unittest deploy/tests/test_compare.py` → green.
- [ ] `deploy/tests/run-bats.sh` (all files) → green; afterwards `docker ps -a --filter name=ruinarpg-bats` is empty and no `ruinarpg-bats*` volumes remain.
- [ ] actionlint clean.
- [ ] Record results in the plan's ledger/report; commit nothing unless a fix was needed (then `fix:` commit + push).

---

## Self-review notes

- Spec coverage: components table → Tasks 5–12; production stack → Task 7; deploy sequence → Tasks 8–9; gate → Task 10; fingerprint → Task 6; release/deploy workflows → Task 11; client changes → Tasks 2–4 (+ version endpoint Task 1); testing section → each task; documentation → Task 13; rollout (provisioning, rehearsals) → Task 12 runbooks (execution is the user's, after F; rehearsals precede H).
- Names consistent across tasks: `RUINA_*` variables, `colors.env` keys, RESULT line fields, `ruinarpg-{api,client,smoke}:<sha>`, `api-blue`/`api-green`, `deploy:janela`, `X-Ruina-Manutencao`, `VersionResponse(Versao, Sha)`, `BuildInfo.ShaOf`.
