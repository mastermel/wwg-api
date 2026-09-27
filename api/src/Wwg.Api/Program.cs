using Wwg.Api.Features;
using Wwg.Api.Infrastructure;

if (args.Contains(HealthCheckCommand.Argument, StringComparer.Ordinal))
{
    return await HealthCheckCommand.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices();

var app = builder.Build();

if (!BuildTime.IsGeneratingOpenApiDocument)
{
    app.InitializeDatabase();
}

app.UseApiPipeline();
app.MapApiEndpoints();
app.MapSpaFallback();

app.Run();
return 0;
