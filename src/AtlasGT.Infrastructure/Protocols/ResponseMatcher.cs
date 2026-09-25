using AtlasGT.Domain.Protocols;
using System;
using System.Linq;

namespace AtlasGT.Infrastructure.Protocols
{
    public class MatcherException : Exception
    {
        public MatcherException(string message) : base(message) { }
    }

    public class ResponseMatcher
    {
        public void Match(byte[] data, ResponseMatcherConfig config)
        {
            if (config == null) return;

            // 1. Exact Match Check
            if (config.ExactMatch != null && config.ExactMatch.Length > 0)
            {
                if (!data.SequenceEqual(config.ExactMatch))
                {
                    throw new MatcherException("Response does not exactly match the expected bytes.");
                }
            }

            // 2. Prefix Match Check
            if (config.ExpectedPrefix != null && config.ExpectedPrefix.Length > 0)
            {
                if (data.Length < config.ExpectedPrefix.Length)
                    throw new MatcherException("Payload too short to match expected prefix.");

                for (int i = 0; i < config.ExpectedPrefix.Length; i++)
                {
                    if (data[i] != config.ExpectedPrefix[i])
                    {
                        throw new MatcherException($"Response prefix mismatch at byte {i}. Expected {config.ExpectedPrefix[i]:X2}, Actual {data[i]:X2}");
                    }
                }
            }
        }
    }
}
