using AtlasGT.Domain.Protocols;
using AtlasGT.Infrastructure.Protocols;
using Microsoft.Extensions.Logging.Abstractions;

namespace AtlasGT.Tests.Integration
{
    public class GpcIntegrationTests
    {
        public static void Run()
        {
            var logger = new NullLogger<ProtocolTestBed>();
            var testBed = new ProtocolTestBed(logger);

            // TEST CASE: Simulated Industrial Frame
            // Payload: [0x01, 0x02, 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x01]
            // Meaning: Header(2), Temp(Float32 = 1.0f), Status(Int32 = 1)
            byte[] mockPayload = { 0x01, 0x02, 0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x01 };

            string jsonSchema = @"
            {
                ""ProtocolName"": ""TestIndustrialProtocol"",
                ""Transport"": ""Tcp"",
                ""Framing"": { ""Type"": ""FixedLength"", ""FixedLength"": 10 },
                ""Fields"": [
                    { ""Name"": ""Temperature"", ""Offset"": 2, ""Length"": 4, ""Type"": ""Float32"", ""Endianness"": ""Big"" },
                    { ""Name"": ""Status"", ""Offset"": 6, ""Length"": 4, ""Type"": ""Int32"", ""Endianness"": ""Big"" }
                ]
            }";

            var result = testBed.TestSchema(jsonSchema, mockPayload);

            if (result.IsSuccess)
            {
                Console.WriteLine("✅ Integration Test Passed!");
                foreach (var kvp in result.DecodedData!)
                {
                    Console.WriteLine($"Field {kvp.Key}: {kvp.Value}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Integration Test Failed: {result.Error}");
            }
        }
    }
}
