using System;
using System.Globalization;
using System.Text;
using AtlasGT.Connectors.Abstractions;
using AtlasGT.Domain.Models;

namespace AtlasGT.Normalization
{
    /// <summary>
    /// Convierte <see cref="RawSample"/> en <see cref="Observation"/>.
    /// Estrategia conservadora: si no coincide ningun parser, devuelve una
    /// observacion "cruda" con TrustTier.Observed y Value=null.
    /// </summary>
    public interface IObservationNormalizer
    {
        Observation Normalize(RawSample sample, Guid? endpointId = null);
    }

    public sealed class ObservationNormalizer : IObservationNormalizer
    {
        public Observation Normalize(RawSample sample, Guid? endpointId = null)
        {
            if (sample is null) throw new ArgumentNullException(nameof(sample));

            var baseObs = new Observation
            {
                Id = Guid.NewGuid(),
                EndpointId = endpointId,
                ReceivedAtUtc = sample.ReceivedAtUtc,
                RawPayload = sample.Payload.ToArray(),
                Transport = sample.TransportKind,
                TrustTier = TrustTier.Observed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Intentar como texto ASCII.
            string text;
            try
            {
                text = Encoding.ASCII.GetString(sample.Payload.Span).Trim();
            }
            catch
            {
                baseObs.Name = "raw";
                return baseObs;
            }

            if (string.IsNullOrEmpty(text))
            {
                baseObs.Name = "raw";
                return baseObs;
            }

            // Formato "KEY:value" (ej. "TEMP:22.5")
            int colon = text.IndexOf(':');
            if (colon > 0 && colon < text.Length - 1)
            {
                var key = text.Substring(0, colon).Trim();
                var valueStr = text.Substring(colon + 1).Trim();
                if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    baseObs.Name = key;
                    baseObs.Value = v;
                    baseObs.Unit = GuessUnit(key);
                    return baseObs;
                }
            }

            // Intentar como double plano ("22.5")
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var solo))
            {
                baseObs.Name = "value";
                baseObs.Value = solo;
                return baseObs;
            }

            // Sin parsear: observacion cruda.
            baseObs.Name = "text";
            baseObs.Description = text;
            return baseObs;
        }

        private static string GuessUnit(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var k = key.ToUpperInvariant();
            if (k.Contains("TEMP")) return "°C";
            if (k.Contains("VOLT")) return "V";
            if (k.Contains("AMP") || k.Contains("CURR")) return "A";
            if (k.Contains("PRESS")) return "bar";
            if (k.Contains("HUM")) return "%";
            return string.Empty;
        }
    }
}
