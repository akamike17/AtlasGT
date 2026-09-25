using AtlasGT.Domain.Entities;
using AtlasGT.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AtlasGT.Infrastructure.Services
{
    public class AlertService : IAlertService
    {
        private readonly ILogger<AlertService> _logger;
        // In a real implementation, this would use a DB context to fetch rules
        private readonly List<AlertRule> _rules = new(); 

        public AlertService(ILogger<AlertService> logger)
        {
            _logger = logger;
        }

        public async Task ProcessValueAsync(string tagName, double value)
        {
            var matchingRules = _rules.Where(r => r.IsActive && r.TagName == tagName);
            
            foreach (var rule in matchingRules)
            {
                bool triggered = rule.Condition switch
                {
                    AlertCondition.GreaterThan => value > rule.Threshold,
                    AlertCondition.LessThan => value < rule.Threshold,
                    AlertCondition.EqualTo => Math.Abs(value - rule.Threshold) < 0.001,
                    _ => false
                };

                if (triggered)
                {
                    _logger.LogWarning($"ALERT TRIGGERED: {rule.Message} | Value: {value}");
                    // Save AlertEvent to DB here
                }
            }
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<AlertEvent>> GetActiveAlertsAsync()
        {
            return await Task.FromResult(new List<AlertEvent>());
        }

        public async Task CreateRuleAsync(AlertRule rule)
        {
            _rules.Add(rule);
            await Task.CompletedTask;
        }
    }
}
