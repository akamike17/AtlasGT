using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AtlasGT.Connectors.Simulators
{
    /// <summary>
    /// Orquesta N simuladores TCP con comportamientos configurables.
    /// </summary>
    public class FakeFactory : IDisposable
    {
        private readonly FakeFactoryOptions _options;
        private readonly List<TcpSimulator> _simulators = new();
        private readonly Random _rng = new Random();

        public IReadOnlyList<TcpSimulator> Machines => _simulators.AsReadOnly();

        public FakeFactory(FakeFactoryOptions? options = null)
        {
            _options = options ?? new FakeFactoryOptions();
        }

        public void Start(CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < _options.MachineCount; i++)
            {
                var port = _options.BasePort + i;
                var sim = new TcpSimulator(port);
                sim.Start();
                _simulators.Add(sim);
            }
        }

        public void StopAll()
        {
            foreach (var sim in _simulators)
                sim.Stop();
            _simulators.Clear();
        }

        public void Dispose()
        {
            StopAll();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Simula desconexion random de una maquina.
        /// </summary>
        public async Task MaybeSimulateDisconnectAsync(CancellationToken cancellationToken)
        {
            if (!_options.EnableDisconnects || _simulators.Count == 0) return;
            if (_rng.NextDouble() >= _options.DisconnectProbability) return;

            var victim = _simulators[_rng.Next(_simulators.Count)];
            victim.Stop();
            await Task.Delay(_options.DisconnectDuration, cancellationToken).ConfigureAwait(false);
            victim.Start(); // reconectar
        }

        /// <summary>
        /// Devuelve payload string con posibilidad de malformacion.
        /// </summary>
        public string GetNextMessage(int machineIndex, int counter)
        {
            bool bad = _options.EnableMalformedData && _rng.NextDouble() < _options.MalformedDataProbability;
            if (bad)
            {
                return _rng.Next(0, 3) switch
                {
                    0 => "GARBAGE",
                    1 => "TEMP:not_a_number",
                    _ => string.Empty,
                };
            }
            var temp = 20.0 + _rng.NextDouble() * 10.0;
            return $"TEMP:{temp:F1}";
        }
    }
}
