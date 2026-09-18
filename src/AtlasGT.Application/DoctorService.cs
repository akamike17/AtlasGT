using System;
using AtlasGT.Domain.Models;

namespace AtlasGT.Application
{
    /// <summary>
    /// Diagnostica el estado de un Endpoint (sec. 19 Atlas Doctor).
    /// "que sabe, que sospecha y que prueba sigue"
    /// </summary>
    public interface IDoctorService
    {
        DiagnosticReport Diagnose(Endpoint endpoint);
    }

    public sealed class DiagnosticReport
    {
        public string EndpointAddress { get; set; } = string.Empty;
        public TrustTier CurrentTrust { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public string? SuspectedIssue { get; set; }
        public string? NextTest { get; set; }
    }

    public sealed class DoctorService : IDoctorService
    {
        public DiagnosticReport Diagnose(Endpoint endpoint)
        {
            if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));

            var report = new DiagnosticReport
            {
                EndpointAddress = endpoint.Address,
                CurrentTrust = endpoint.TrustTier
            };

            if (endpoint.LastObservedAtUtc is null)
            {
                report.Verdict = "OFFLINE";
                report.SuspectedIssue = "No se ha recibido trafico jamas.";
                report.NextTest = "Confirmar cableado/alimentacion; escanear puerto.";
                return report;
            }

            var ageSeconds = (DateTime.UtcNow - endpoint.LastObservedAtUtc.Value).TotalSeconds;
            if (ageSeconds > 30)
            {
                report.Verdict = "STALE";
                report.SuspectedIssue = $"Sin trafico en {ageSeconds:F0}s.";
                report.NextTest = "Revisar conexion fisica y reintentar.";
                return report;
            }

            report.Verdict = endpoint.TrustTier >= TrustTier.Observed ? "ONLINE" : "PARTIAL";
            if (endpoint.TrustTier < TrustTier.Observed)
                report.NextTest = "Reintentar conexion";
            return report;
        }
    }
}
