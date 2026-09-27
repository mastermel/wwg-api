using Wwg.Api.Features;
using Wwg.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices();

var app = builder.Build();

app.UseApiPipeline();
app.MapApiEndpoints();

app.Run();
