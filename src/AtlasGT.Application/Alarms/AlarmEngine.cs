using System;
using System.Collections.Generic;
using System.Linq;
using AtlasGT.Domain.Models;

namespace AtlasGT.Application.Alarms
{
    /// <summary>
    /// Definicion estatica de una alarma (regla) — por ejemplo "temp alta".
    /// </summary>
    public sealed class AlarmRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string SignalKey { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public AlarmComparison Comparison { get; set; } = AlarmComparison.GreaterThan;
        public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;
        /// <summary>Muestras consecutivas por encima/ debajo del umbral para disparar.</summary>
        public int Debounce { get; set; } = 3;
        /// <summary>Máximo de activaciones por minuto (anti-storm).</summary>
        public int MaxPerMinute { get; set; } = 10;
    }

    public enum AlarmComparison
    {
        GreaterThan,
        LessThan,
        Equal,
        NotEqual
    }

    /// <summary>Instancia de alarma disparada.</summary>
    public sealed class AlarmInstance
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string SignalKey { get; set; } = string.Empty;
        public double Value { get; set; }
        public double Threshold { get; set; }
        public AlarmSeverity Severity { get; set; }
        public AlarmState State { get; set; } = AlarmState.Active;
        public DateTimeOffset RaisedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? AcknowledgedAtUtc { get; set; }
        public string? AcknowledgedBy { get; set; }
        public DateTimeOffset? ClearedAtUtc { get; set; }
        public string? ClearReason { get; set; }
    }

    /// <summary>
    /// Motor de alarmas: aplica reglas a observaciones con debounce y anti-storm.
    /// </summary>
    public sealed class AlarmEngine
    {
        private readonly List<AlarmRule> _rules = new();
        private readonly List<AlarmInstance> _active = new();
        private readonly Dictionary<Guid, int> _consecutive = new();
        private readonly Dictionary<Guid, Queue<DateTimeOffset>> _history = new();
        private readonly object _lock = new();

        public IReadOnlyList<AlarmRule> Rules => _rules.ToList();
        public IReadOnlyList<AlarmInstance> ActiveAlarms
        {
            get { lock (_lock) return _active.Where(a => a.State != AlarmState.Cleared).ToList(); }
        }

        public void AddRule(AlarmRule rule)
        {
            if (rule is null) throw new ArgumentNullException(nameof(rule));
            lock (_lock)
            {
                _rules.RemoveAll(r => r.Id == rule.Id);
                _rules.Add(rule);
            }
        }

        /// <summary>Process a new observation. Returns alarms raised this trip.</summary>
        public List<AlarmInstance> Process(Observation obs)
        {
            if (obs is null || obs.Value is null) return new List<AlarmInstance>();
            var newAlarms = new List<AlarmInstance>();

            lock (_lock)
            {
                foreach (var rule in _rules) // iteracion explicita; C# List<T> soporta foreach
                {
                    if (!MatchesSignal(rule, obs)) continue;
                    if (!CheckComparison(rule.Comparison, obs.Value.Value, rule.Threshold)) continue;

                    var count = _consecutive.TryGetValue(rule.Id, out var c) ? c + 1 : 1;
                    _consecutive[rule.Id] = count;

                    if (count < rule.Debounce) continue; // no disparar aun

                    if (!CheckRateLimit(rule)) continue; // anti-storm

                    var alarm = new AlarmInstance
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        SignalKey = obs.Name,
                        Value = obs.Value.Value,
                        Threshold = rule.Threshold,
                        Severity = rule.Severity
                    };

                    _active.Add(alarm);
                    newAlarms.Add(alarm);

                    RecordFire(rule.Id);
                }
            }
            return newAlarms;
        }

        public bool Acknowledge(Guid alarmId, string actor)
        {
            lock (_lock)
            {
                var a = _active.FirstOrDefault(x => x.Id == alarmId);
                if (a is null || a.State != AlarmState.Active) return false;
                a.State = AlarmState.Acknowledged;
                a.AcknowledgedAtUtc = DateTimeOffset.UtcNow;
                a.AcknowledgedBy = actor;
                return true;
            }
        }

        public bool Clear(Guid alarmId, string reason)
        {
            lock (_lock)
            {
                var a = _active.FirstOrDefault(x => x.Id == alarmId);
                if (a is null) return false;
                a.State = AlarmState.Cleared;
                a.ClearedAtUtc = DateTimeOffset.UtcNow;
                a.ClearReason = reason;
                return true;
            }
        }

        public void ResetDebounce(string signalKey)
        {
            lock (_lock)
            {
                var ids = _rules.Where(r => r.SignalKey == signalKey).Select(r => r.Id).ToList();
                foreach (var id in ids) _consecutive[id] = 0;
            }
        }

        private bool MatchesSignal(AlarmRule rule, Observation obs)
            => string.Equals(rule.SignalKey, obs.Name, StringComparison.OrdinalIgnoreCase);

        private static bool CheckComparison(AlarmComparison cmp, double value, double threshold) => cmp switch
        {
            AlarmComparison.GreaterThan => value > threshold,
            AlarmComparison.LessThan => value < threshold,
            AlarmComparison.Equal => Math.Abs(value - threshold) < 1e-9,
            AlarmComparison.NotEqual => Math.Abs(value - threshold) > 1e-9,
            _ => false
        };

        private bool CheckRateLimit(AlarmRule rule)
        {
            if (!_history.TryGetValue(rule.Id, out var q)) { q = new Queue<DateTimeOffset>(); _history[rule.Id] = q; }
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
            while (q.Count > 0 && q.Peek() < cutoff) q.Dequeue();
            return q.Count < rule.MaxPerMinute;
        }

        private void RecordFire(Guid ruleId)
        {
            if (!_history.TryGetValue(ruleId, out var q)) { q = new Queue<DateTimeOffset>(); _history[ruleId] = q; }
            q.Enqueue(DateTimeOffset.UtcNow);
        }
    }
}
