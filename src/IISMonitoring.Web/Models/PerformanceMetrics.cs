namespace IISMonitoring.Web.Models;

public class PerformanceMetrics
{
    public double TotalCpuUsage { get; set; }
    public long TotalMemoryUsageMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public int TotalApplicationPools { get; set; }
    public int RunningApplicationPools { get; set; }
    public int StoppedApplicationPools { get; set; }
    public int TotalWebSites { get; set; }
    public int RunningWebSites { get; set; }
    public DateTime Timestamp { get; set; }
}
