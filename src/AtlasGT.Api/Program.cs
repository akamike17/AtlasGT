using AtlasGT.Application;
using AtlasGT.Api.Hubs;
using AtlasGT.Domain.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IDoctorService, DoctorService>();

var app = builder.Build();

app.MapGet("/", () => "AtlasGT.Api online. Endpoints: /api/endpoints, /hubs/observations");
app.MapControllers();
app.MapHub<ObservationsHub>("/hubs/observations");

app.Run();

public partial class Program { }
