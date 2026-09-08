using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Raycoon.RayMigrator.ConfigWizard.Web;
using Raycoon.RayMigrator.ConfigWizard.Web.Services;

// Code-page encodings (e.g. windows-1252 for MigrationFilesEncoding) need the provider registered once,
// so the wizard's encoding validation accepts exactly what the engine accepts (#4)
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddTransient<MudBlazor.MudLocalizer, WizardMudLocalizer>();
builder.Services.AddScoped<WizardStateService>();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<FileInteropService>();
builder.Services.AddScoped<ZipExportService>();
builder.Services.AddScoped<TermsAcceptanceService>();
builder.Services.AddScoped<JsonHighlightService>();

var host = builder.Build();

// Read language from localStorage BEFORE first render
host.Services.GetRequiredService<LocalizationService>().InitializeSync();

await host.RunAsync();
