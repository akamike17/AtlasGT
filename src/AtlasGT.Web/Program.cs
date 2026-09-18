using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

// HttpClient tipado: la UI consulta al API.
var apiBase = builder.Configuration["AtlasGt:ApiBaseUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient("atlas-api", c => c.BaseAddress = new Uri(apiBase));

var app = builder.Build();
app.UseStaticFiles();
app.MapRazorPages();
app.MapGet("/health", () => new { status = "OK", utc = System.DateTime.UtcNow });

app.Run();

public partial class Program { }
