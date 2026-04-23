// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Raycoon.RayMigrator.ConfigWizard.Web;
using Raycoon.RayMigrator.ConfigWizard.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddTransient<MudBlazor.MudLocalizer, WizardMudLocalizer>();
builder.Services.AddScoped<WizardStateService>();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<FileInteropService>();
builder.Services.AddScoped<ZipExportService>();
builder.Services.AddScoped<JsonHighlightService>();

var host = builder.Build();

// Read language from localStorage BEFORE first render
host.Services.GetRequiredService<LocalizationService>().InitializeSync();

await host.RunAsync();
