using IISMonitoring.Web.Models;

namespace IISMonitoring.Web.Services;

public interface IIISMonitoringService
{
    Task<MonitoringDashboard> GetDashboardDataAsync();
    Task<List<ApplicationPoolInfo>> GetApplicationPoolsAsync();
    Task<List<WebSiteInfo>> GetWebSitesAsync();
    Task<PerformanceMetrics> GetPerformanceMetricsAsync();

    // Métodos de control para Application Pools
    Task<bool> StartApplicationPoolAsync(string poolName);
    Task<bool> StopApplicationPoolAsync(string poolName);

    // Métodos de control para Sitios Web
    Task<bool> StartWebSiteAsync(string siteName);
    Task<bool> StopWebSiteAsync(string siteName);
}
