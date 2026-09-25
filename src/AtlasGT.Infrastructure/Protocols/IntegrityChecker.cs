using AtlasGT.Domain.Protocols;
using System;
using System.Linq;

namespace AtlasGT.Infrastructure.Protocols
{
    public class IntegrityException : Exception
    {
        public IntegrityException(string message) : base(message) { }
    }

    public class IntegrityChecker
    {
        public void Validate(byte[] data, ValidationConfig config)
        {
            if (config == null || !config.EnableChecksum) return;
            if (config.ChecksumAlgorithm == "None") return;

            if (config.ChecksumAlgorithm == "Checksum8")
            {
                ValidateChecksum8(data);
            }
            else if (config.ChecksumAlgorithm == "CRC16")
            {
                ValidateCRC16(data);
            }
            else
            {
                throw new NotSupportedException($"Checksum algorithm {config.ChecksumAlgorithm} is not implemented.");
            }
        }

        private void ValidateChecksum8(byte[] data)
        {
            if (data.Length < 1) throw new IntegrityException("Payload too short for checksum.");
            
            byte sum = 0;
            for (int i = 0; i < data.Length - 1; i++)
            {
                sum = (byte)((sum + data[i]) & 0xFF);
            }

            if (sum != data[data.Length - 1])
            {
                throw new IntegrityException($"Checksum8 mismatch. Expected {data[data.Length - 1]:X2}, calculated {sum:X2}");
            }
        }

        private void ValidateCRC16(byte[] data)
        {
            if (data.Length < 2) throw new IntegrityException("Payload too short for CRC16.");

            // CRC16 Modbus: Polynomial 0x8005, Initial 0xFFFF, Reflected
            ushort calculatedCrc = CalculateCRC16(data.AsSpan(0, data.Length - 2));
            ushort receivedCrc = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(data.Length - 2));

            if (calculatedCrc != receivedCrc)
            {
                throw new IntegrityException($"CRC16 mismatch. Expected {receivedCrc:X4}, calculated {calculatedCrc:X4}");
            }
        }

        public static ushort CalculateCRC16(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }
    }
}
