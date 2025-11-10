namespace IISMonitoring.Web.Models;

public class ApplicationPoolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public double CpuUsage { get; set; }
    public long MemoryUsageMB { get; set; }
    public int ActiveRequests { get; set; }
    public int TotalRequests { get; set; }
    public DateTime LastUpdated { get; set; }
}
