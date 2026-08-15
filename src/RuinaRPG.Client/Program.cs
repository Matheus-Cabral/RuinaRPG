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
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = httpClientBaseAddress });

await builder.Build().RunAsync();
