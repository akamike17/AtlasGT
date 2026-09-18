using System;
using System.Collections.Generic;
using System.Linq;
using AtlasGT.Domain.Models;

namespace AtlasGT.ProtocolInference
{
    /// <summary>
    /// Correlaciona cambios de señal con eventos observados para producir hipotesis.
    /// Regla: solo genera hipotesis (Provenance = Experimental o Inferred).
    /// Jamas VERIFIED/DOCUMENTED sin confirmacion humana.
    /// </summary>
    public sealed class SignalCorrelator
    {
        /// <summary>
        /// Compara dos conjuntos de senales (antes/despues) y sugiere correlaciones.
        /// </summary>
        public IEnumerable<Hypothesis> Correlate(IReadOnlyList<SignalSnapshot> before, IReadOnlyList<SignalSnapshot> after)
        {
            if (before is null || after is null) yield break;

            var beforeMap = before.ToDictionary(s => s.Key, s => s.Value);
            foreach (var s in after)
            {
                if (!beforeMap.TryGetValue(s.Key, out var beforeVal)) continue;
                if (beforeVal == s.Value) continue; // sin cambio
                if (s.Value is null || beforeVal is null) continue;

                yield return new Hypothesis
                {
                    SignalKey = s.Key,
                    ObservedChange = $"{beforeVal} -> {s.Value}",
                    Confidence = ProvenanceKind.Inferred,
                    Notes = "Detectado por correlacion. Requiere confirmacion."
                };
            }
        }

        public sealed class SignalSnapshot
        {
            public string Key { get; set; } = string.Empty;
            public double? Value { get; set; }
        }

        public sealed class Hypothesis
        {
            public string SignalKey { get; set; } = string.Empty;
            public string ObservedChange { get; set; } = string.Empty;
            public ProvenanceKind Confidence { get; set; }
            public string? Notes { get; set; }
        }
    }
}
