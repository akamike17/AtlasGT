using System;
using AtlasGT.Domain.Models;

namespace AtlasGT.Security
{
    /// <summary>
    /// Implementa la escalera de confianza de la spec seccion 2.
    /// Reglas:
    ///   - Todo endpoint arranca en Passive (TrustTier.Passive).
    ///   - El sistema puede promover automaticamente hasta Observed (recibio datos).
    ///   - Decoded NO se promueve silenciosamente: solo registra una hipotesis con evidencia.
    ///   - Verified, ReadOnly, WriteCapable, ControlCapable requieren autorizacion explicita.
    /// </summary>
    public interface ITrustLadder
    {
        /// <summary>
        /// Marca un endpoint como "observado" (recibimos bytes de el). Nunca baja el tier.
        /// </summary>
        TrustTier MarkObserved(Endpoint endpoint);

        /// <summary>
        /// Intenta promover a un tier > Observed. Devuelve false si no esta permitido.
        /// </summary>
        bool TryAuthorize(Endpoint endpoint, TrustTier target, string reason, string authorizedBy);

        /// <summary>
        /// Consulta si un endpoint tiene al menos el tier dado.
        /// </summary>
        bool HasAtLeast(Endpoint endpoint, TrustTier minimum);
    }

    public sealed class TrustLadder : ITrustLadder
    {
        public TrustTier MarkObserved(Endpoint endpoint)
        {
            if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));
            if (endpoint.TrustTier < TrustTier.Observed)
            {
                endpoint.TrustTier = TrustTier.Observed;
                endpoint.UpdatedAt = DateTime.UtcNow;
            }
            endpoint.LastObservedAtUtc = DateTime.UtcNow;
            return endpoint.TrustTier;
        }

        public bool TryAuthorize(Endpoint endpoint, TrustTier target, string reason, string authorizedBy)
        {
            if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));

            // Si el target ya esta alcanzado, no se requiere autorizacion adicional.
            if (target <= endpoint.TrustTier) return true;

            // Promover requiere un autorizador.
            if (string.IsNullOrWhiteSpace(authorizedBy)) return false;

            // Jamas autorizar mas alla de Observed sin razon explicita.
            if (target > TrustTier.Observed && string.IsNullOrWhiteSpace(reason))
                return false;

            endpoint.TrustTier = target;
            endpoint.UpdatedAt = DateTime.UtcNow;
            return true;
        }

        public bool HasAtLeast(Endpoint endpoint, TrustTier minimum)
            => endpoint is not null && endpoint.TrustTier >= minimum;
    }
}
