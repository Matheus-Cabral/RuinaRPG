using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RuinaRPG.Client;
using RuinaRPG.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<AuthStateService>();

var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "/";
var httpClientBaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), apiBaseAddress);

builder.Services.AddTransient<BearerTokenHandler>();
builder.Services
    .AddHttpClient("Api", client => client.BaseAddress = httpClientBaseAddress)
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

await builder.Build().RunAsync();
