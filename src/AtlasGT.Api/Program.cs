using AtlasGT.Application;
using AtlasGT.Application.Alarms;
using AtlasGT.Api.Hubs;
using AtlasGT.Api.Services;
using AtlasGT.Discovery;
using AtlasGT.Domain.Models;
using AtlasGT.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

builder.Services.AddSingleton(sp =>
{
    var cfgPath = builder.Configuration["AtlasGT:ConfigPath"];
    return new ConfigStore(cfgPath);
});
builder.Services.AddSingleton<IDoctorService, DoctorService>();
builder.Services.AddSingleton<AlarmEngine>();
builder.Services.AddSingleton<ObservationStreamer>();
builder.Services.AddSingleton<TcpPortDiscovery>();

var app = builder.Build();

// Cargar alarm rules persistidas al arrancar
{
    var store = app.Services.GetRequiredService<ConfigStore>();
    var engine = app.Services.GetRequiredService<AlarmEngine>();
    var snap = await store.LoadAsync();
    foreach (var dto in snap.AlarmRules)
    {
        if (!Enum.TryParse<AlarmComparison>(dto.Comparison, true, out var cmp)) cmp = AlarmComparison.GreaterThan;
        if (!Enum.TryParse<AlarmSeverity>(dto.Severity, true, out var sev)) sev = AlarmSeverity.Warning;
        engine.AddRule(new AlarmRule
        {
            Id = dto.Id,
            Name = dto.Name,
            SignalKey = dto.SignalKey,
            Threshold = dto.Threshold,
            Comparison = cmp,
            Severity = sev,
            Debounce = dto.Debounce,
            MaxPerMinute = dto.MaxPerMinute
        });
    }
}

app.MapGet("/", () => "AtlasGT.Api online. Endpoints: /api/endpoints, /api/assets, /api/profiles, /api/alarms, /api/discovery, /api/sandbox, /hubs/observations");
app.MapGet("/health", () => new { status = "OK", utc = DateTime.UtcNow });
app.MapControllers();
app.MapHub<ObservationsHub>("/hubs/observations");

app.Run();

public partial class Program { }
