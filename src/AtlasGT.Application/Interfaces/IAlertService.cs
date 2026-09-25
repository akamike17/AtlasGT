using AtlasGT.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AtlasGT.Application.Interfaces
{
    public interface IAlertService
    {
        Task ProcessValueAsync(string tagName, double value);
        Task<IEnumerable<AlertEvent>> GetActiveAlertsAsync();
        Task CreateRuleAsync(AlertRule rule);
    }
}
