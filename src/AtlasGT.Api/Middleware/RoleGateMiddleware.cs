using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AtlasGT.Api.Middleware
{
    /// <summary>
    /// Middleware de roles minimalista para Atlas GT.
    /// Sin IdP real, pero aplica el principio "passive-read-only por defecto":
    /// - Cualquier llamada sin header X-Atlas-Role es tratada como "viewer" (solo GET).
    /// - Escrituras (POST/PUT/DELETE) exigen rol "admin" o "lab" segun ruta.
    /// - /api/discovery/* requiere rol "lab" o "admin" (accion privilegiada).
    /// - /api/admin/* requiere "admin".
    /// - /api/alarms/{id}/ack|clear requiere "operator", "admin" o "lab" (accion explicita).
    ///
    /// No es un sistema de identidad; es cabecera explicita y auditada. Para produccion
    /// se reemplaza por OpenId Connect / Windows Auth / mTLS segun planta.
    /// </summary>
    public sealed class RoleGateMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RoleGateMiddleware> _log;

        public RoleGateMiddleware(RequestDelegate next, ILogger<RoleGateMiddleware> log)
        {
            _next = next;
            _log = log;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            var role = ctx.Request.Headers["X-Atlas-Role"].FirstOrDefault() ?? "viewer";
            var path = ctx.Request.Path.Value ?? string.Empty;
            var method = ctx.Request.Method;

            // Health y raiz siempre publicos
            if (path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                await _next(ctx);
                return;
            }

            // Hub SignalR: abierto a todos por ahora; las suscripciones son read-only
            if (path.StartsWith("/hubs/observations", StringComparison.OrdinalIgnoreCase))
            {
                await _next(ctx);
                return;
            }

            // /api/admin/* solo admin
            if (path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsIn(role, "admin"))
                {
                    await Deny(ctx, 403, "se requiere rol admin");
                    return;
                }
            }
            // /api/discovery/* requiere admin o lab (accion privilegiada, auditada)
            else if (path.StartsWith("/api/discovery", StringComparison.OrdinalIgnoreCase))
            {
                if (!IsIn(role, "admin", "lab"))
                {
                    await Deny(ctx, 403, "se requiere rol admin o lab");
                    return;
                }
            }
            // Escrituras genericas requieren admin (POST/PUT/DELETE/PATCH)
            else if (IsWrite(method) && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                // Excepciones: ack/clear de alarmas permiten operator
                if (path.Contains("/ack", StringComparison.OrdinalIgnoreCase)
                    || path.Contains("/clear", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsIn(role, "admin", "operator", "lab"))
                    {
                        await Deny(ctx, 403, "ack/clear requiere operator/admin/lab");
                        return;
                    }
                }
                // Sandbox publish: admin o lab (no produccion)
                else if (path.StartsWith("/api/sandbox", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsIn(role, "admin", "lab"))
                    {
                        await Deny(ctx, 403, "sandbox requiere admin o lab");
                        return;
                    }
                }
                else if (!IsIn(role, "admin"))
                {
                    await Deny(ctx, 403, $"escritura requiere admin (rol actual: {role})");
                    return;
                }
            }

            // Si llego aqui, dejar pasar y etiquetar al usuario
            ctx.Items["AtlasRole"] = role;
            await _next(ctx);
        }

        private static bool IsWrite(string method) =>
            method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("DELETE", StringComparison.OrdinalIgnoreCase) ||
            method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);

        private static bool IsIn(string role, params string[] allowed) =>
            allowed.Any(a => string.Equals(a, role, StringComparison.OrdinalIgnoreCase));

        private static async Task Deny(HttpContext ctx, int status, string msg)
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync($"{{\"error\":\"{msg}\"}}");
        }
    }
}
