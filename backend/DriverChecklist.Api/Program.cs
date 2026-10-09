using System.Diagnostics;
using DriverChecklist.Api.Configuration;
using DriverChecklist.Api.Endpoints;
using DriverChecklist.Api.Services;

var portable = args.Contains("--portable", StringComparer.Ordinal)
    || File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.json"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Where(argument => argument != "--portable").ToArray(),
    ContentRootPath = portable ? AppContext.BaseDirectory : Directory.GetCurrentDirectory(),
});
const string PortableUrl = "http://localhost:5080";
if (portable)
{
    builder.Configuration.AddJsonFile("portable.json", optional: false, reloadOnChange: false);
    builder.WebHost.UseUrls(PortableUrl);
}

builder.Services.Configure<TemplateOptions>(
    builder.Configuration.GetSection(TemplateOptions.SectionName));
builder.Services.Configure<MasterDataOptions>(
    builder.Configuration.GetSection(MasterDataOptions.SectionName));

builder.Services.AddSingleton<IMasterDataService, MasterDataService>();
builder.Services.AddSingleton<ITemplateResolver, TemplateResolver>();
builder.Services.AddSingleton<IChecklistGeneratorService, ChecklistGeneratorService>();

const string FrontendCorsPolicy = "FrontendCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors(FrontendCorsPolicy);
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapInitDataEndpoints();
app.MapChecklistEndpoints();
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

if (portable)
{
    await app.StartAsync();
    app.Logger.LogInformation("Driver Checklist körs på {Url}. Stäng med Ctrl+C. Konfiguration: {Path}",
        PortableUrl, Path.Combine(AppContext.BaseDirectory, "portable.json"));
    try
    {
        Process.Start(new ProcessStartInfo(PortableUrl) { UseShellExecute = true });
    }
    catch (System.ComponentModel.Win32Exception exception)
    {
        app.Logger.LogWarning(exception, "Kunde inte öppna webbläsaren. Öppna {Url} manuellt.", PortableUrl);
    }
    await app.WaitForShutdownAsync();
}
else
{
    app.Run();
}
