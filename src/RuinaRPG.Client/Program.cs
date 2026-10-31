using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RuinaRPG.Client;
using RuinaRPG.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddMudServices();
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<PericiaCatalogo>();
builder.Services.AddScoped<CatalogoDeElementos>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());

var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "/";
var httpClientBaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBaseAddress);

builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddTransient(_ => new RetryHandler());
builder.Services
    .AddHttpClient("Api", client => client.BaseAddress = httpClientBaseAddress)
    .AddHttpMessageHandler<BearerTokenHandler>()
    .AddHttpMessageHandler<RetryHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));
builder.Services.AddScoped<INotificationConnection, SignalRNotificationConnection>();
builder.Services.AddTransient<IEncounterConnection, SignalREncounterConnection>();
builder.Services.AddScoped<AvisosDoHub>();
builder.Services.AddScoped(sp => new NewVersionWatcher(sp.GetRequiredService<HttpClient>(), RuinaRPG.Domain.BuildInfo.ShaOf(typeof(App).Assembly)));

await builder.Build().RunAsync();
