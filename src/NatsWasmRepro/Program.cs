using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NatsWasmRepro;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// NATS.Client.Core debug logs (e.g. "Error occured during read loop") appear in the browser console.
builder.Logging.SetMinimumLevel(LogLevel.Debug);

await builder.Build().RunAsync();
