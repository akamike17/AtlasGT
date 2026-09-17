using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AtlasGT.Domain.Models;

namespace AtlasGT.Connectors.Network
{
    /// <summary>
    /// A connector that communicates over TCP to receive data from a simulator or device.
    /// </summary>
    public class TcpConnector : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected;

        public TcpConnector(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public bool Connect()
        {
            try
            {
                _client = new TcpClient(_host, _port);
                _stream = _client.GetStream();
                _isConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to connect to {_host}:{_port}: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
            if (_client != null)
            {
                _client.Close();
                _client = null;
            }
            _isConnected = false;
        }

        /// <summary>
        /// Reads a line of ASCII data from the stream.
        /// Returns null if the connection is broken or no data is available.
        /// </summary>
        public string ReadLine(CancellationToken cancellationToken = default)
        {
            if (!_isConnected || _stream == null)
                return null;

            try
            {
                var sb = new StringBuilder();
                while (true)
                {
                    if (_stream.DataAvailable)
                    {
                        int b = _stream.ReadByte();
                        if (b == -1) // disconnected
                        {
                            Disconnect();
                            return null;
                        }
                        if (b == '\n' || b == '\r')
                        {
                            if (sb.Length > 0)
                            {
                                string line = sb.ToString();
                                sb.Clear();
                                // If we got a \r, we might need to consume the next \n if present
                                if (b == '\r' && _stream.DataAvailable)
                                {
                                    int peek = _stream.ReadByte();
                                    if (peek != '\n')
                                    {
                                        // Put it back? Not possible with NetworkStream, we'll just ignore.
                                        // For simplicity, we assume messages end with \n only.
                                    }
                                }
                                return line;
                            }
                        }
                        else
                        {
                            sb.Append((char)b);
                        }
                    }
                    else
                    {
                        // No data available, wait a bit or return null? We'll return null for now.
                        // In a real implementation, we might wait for data with a timeout.
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from stream: {ex.Message}");
                Disconnect();
                return null;
            }
        }

        /// <summary>
        /// Attempts to read a temperature value from the stream.
        /// Expected format: "TEMP:25.0\n"
        /// </summary>
        public double? ReadTemperature(CancellationToken cancellationToken = default)
        {
            string line = ReadLine(cancellationToken);
            if (line == null)
                return null;

            if (line.StartsWith("TEMP:"))
            {
                string valueStr = line.Substring(5).TrimEnd();
                if (double.TryParse(valueStr, out double value))
                {
                    return value;
                }
            }
            return null;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
