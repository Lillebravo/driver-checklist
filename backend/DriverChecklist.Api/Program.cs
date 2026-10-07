using DriverChecklist.Api.Configuration;
using DriverChecklist.Api.Endpoints;
using DriverChecklist.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TemplateOptions>(
    builder.Configuration.GetSection(TemplateOptions.SectionName));

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

app.MapInitDataEndpoints();
app.MapChecklistEndpoints();

app.Run();
