using IISMonitoring.Web.Models;

namespace IISMonitoring.Web.Services;

public interface IIISMonitoringService
{
    Task<MonitoringDashboard> GetDashboardDataAsync();
    Task<List<ApplicationPoolInfo>> GetApplicationPoolsAsync();
    Task<List<WebSiteInfo>> GetWebSitesAsync();
    Task<PerformanceMetrics> GetPerformanceMetricsAsync();
}
