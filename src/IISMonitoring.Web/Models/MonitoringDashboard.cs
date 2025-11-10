namespace IISMonitoring.Web.Models;

public class MonitoringDashboard
{
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();
    public List<ApplicationPoolInfo> ApplicationPools { get; set; } = new();
    public List<WebSiteInfo> WebSites { get; set; } = new();
}
