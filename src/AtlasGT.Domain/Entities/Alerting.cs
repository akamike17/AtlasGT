namespace AtlasGT.Domain.Entities
{
    public class AlertRule
    {
        public Guid Id { get; set; }
        public string TagName { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public AlertCondition Condition { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public enum AlertCondition
    {
        GreaterThan,
        LessThan,
        EqualTo
    }

    public class AlertEvent
    {
        public Guid Id { get; set; }
        public Guid RuleId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Resolved { get; set; } = false;
    }
}
