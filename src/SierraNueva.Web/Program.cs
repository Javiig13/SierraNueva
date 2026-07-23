using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SierraNueva.Web;
using SierraNueva.Web.Services;

CultureInfo spanishCulture = CultureInfo.GetCultureInfo("es-ES");
CultureInfo.DefaultThreadCurrentCulture = spanishCulture;
CultureInfo.DefaultThreadCurrentUICulture = spanishCulture;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddScoped<PublicDataService>();

await builder.Build().RunAsync();
