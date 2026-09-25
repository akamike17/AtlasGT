using System;
using System.Collections.Generic;
using System.Linq;

namespace AtlasGT.Infrastructure.Protocols.Testing
{
    public class ScriptedTransport
    {
        private readonly List<(byte[] ExpectedRequest, byte[] Response)> _exchanges = new();
        private int _currentStep = 0;

        public void AddExchange(byte[] expectedRequest, byte[] response)
        {
            _exchanges.Add((expectedRequest, response));
        }

        public byte[] SendAndReceive(byte[] actualRequest)
        {
            if (_currentStep >= _exchanges.Count)
            {
                throw new InvalidOperationException("No scripted response configured for this step.");
            }

            var (expected, response) = _exchanges[_currentStep];
            
            if (!actualRequest.SequenceEqual(expected))
            {
                throw new InvalidOperationException($"Request mismatch. Expected: {BitConverter.ToString(expected)}, Actual: {BitConverter.ToString(actualRequest)}");
            }

            _currentStep++;
            return response;
        }

        public void Reset() => _currentStep = 0;
    }
}
