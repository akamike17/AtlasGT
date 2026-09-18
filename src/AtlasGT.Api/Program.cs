using AtlasGT.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<IDoctorService, DoctorService>();

var app = builder.Build();

app.MapGet("/", () => "AtlasGT.Api online. Endpoints: /api/endpoints, /api/endpoints/{id}/diagnose");
app.MapControllers();

app.Run();

public partial class Program { }
