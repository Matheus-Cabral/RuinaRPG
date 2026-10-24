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
var failures = await SmokeRunner.RunAsync(http, jwt, db, plan, settings.Deadline);
foreach (var f in failures) Console.WriteLine(f);
Console.WriteLine(failures.Count == 0 ? $"OK {plan.Count} requests" : $"FAILED {failures.Count}/{plan.Count}");
return failures.Count == 0 ? 0 : 1;
