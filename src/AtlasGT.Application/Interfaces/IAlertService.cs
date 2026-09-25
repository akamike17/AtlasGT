namespace AtlasGT.Application.Interfaces
{
    public interface IAlertService
    {
        Task ProcessValueAsync(string tagName, double value);
        Task<IEnumerable<AlertEvent>> GetActiveAlertsAsync();
        Task CreateRuleAsync(AlertRule rule);
    }
}
