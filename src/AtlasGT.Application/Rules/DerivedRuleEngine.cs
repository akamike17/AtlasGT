using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AtlasGT.Domain.Models;

namespace AtlasGT.Application.Rules
{
    /// <summary>
    /// Regla derivada simple: evalúa condición sobre señales y emite una señal derivada.
    /// Ej: "RUNNING = cycle_sensor_active AND current &gt; 3.2"
    /// </summary>
    public sealed class DerivedRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string OutputSignalKey { get; set; } = string.Empty;
        public string OutputName { get; set; } = string.Empty;

        /// <summary>Función que toma un lookup de señales y devuelve bool? (null = no evaluable).</summary>
        public Func<IReadOnlyDictionary<string, double?>, bool?>? Evaluate { get; set; }

        /// <summary>Claves de entrada (para trazabilidad).</summary>
        public IReadOnlyList<string> Inputs { get; set; } = Array.Empty<string>();
    }

    /// <summary>Motor de reglas derivadas.</summary>
    public sealed class DerivedRuleEngine
    {
        private readonly List<DerivedRule> _rules = new();
        private readonly object _lock = new();

        public IReadOnlyList<DerivedRule> Rules
        {
            get { lock (_lock) return _rules.ToList(); }
        }

        public void Add(DerivedRule rule)
        {
            if (rule is null) throw new ArgumentNullException(nameof(rule));
            if (rule.Evaluate is null) throw new ArgumentException("rule.Evaluate requerido");
            if (string.IsNullOrEmpty(rule.OutputSignalKey)) throw new ArgumentException("OutputSignalKey requerido");
            lock (_lock) { _rules.Add(rule); }
        }

        /// <summary>
        /// Evalúa todas las reglas con el estado actual de señales.
        /// Devuelve las señales derivadas que se resolvieron a true/false.
        /// </summary>
        public IReadOnlyList<(DerivedRule Rule, bool Value)> EvaluateAll(IReadOnlyDictionary<string, double?> signals)
        {
            if (signals is null) throw new ArgumentNullException(nameof(signals));
            var result = new List<(DerivedRule, bool)>();
            foreach (var rule in Rules)
            {
                var value = rule.Evaluate!(signals);
                if (value.HasValue) result.Add((rule, value.Value));
            }
            return result;
        }

        /// <summary>
        /// Constructor cómodo: "OutputKey = input1 &gt; thresh1 AND input2 != null"
        /// </summary>
        public static DerivedRule ThresholdAndPresent(
            string outputKey,
            string input1, double threshold,
            string input2)
        {
            return new DerivedRule
            {
                Name = $"{outputKey}: {input1}>{threshold} && {input2}!=null",
                OutputSignalKey = outputKey,
                Inputs = new[] { input1, input2 },
                Evaluate = dict =>
                {
                    if (!dict.TryGetValue(input1, out var v1) || v1 is null) return null;
                    if (v1.Value <= threshold) return false;
                    if (!dict.TryGetValue(input2, out var v2) || v2 is null) return null; // no evaluable
                    return true;
                }
            };
        }
    }
}
