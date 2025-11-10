using IISMonitoring.Web.Models;
using Microsoft.Web.Administration;
using System.Diagnostics;

namespace IISMonitoring.Web.Services;

public class IISMonitoringService : IIISMonitoringService
{
    private readonly ILogger<IISMonitoringService> _logger;

    public IISMonitoringService(ILogger<IISMonitoringService> logger)
    {
        _logger = logger;
    }

    public async Task<MonitoringDashboard> GetDashboardDataAsync()
    {
        var dashboard = new MonitoringDashboard
        {
            PerformanceMetrics = await GetPerformanceMetricsAsync(),
            ApplicationPools = await GetApplicationPoolsAsync(),
            WebSites = await GetWebSitesAsync()
        };

        return dashboard;
    }

    public async Task<List<ApplicationPoolInfo>> GetApplicationPoolsAsync()
    {
        return await Task.Run(() =>
        {
            var appPools = new List<ApplicationPoolInfo>();

            try
            {
                using var serverManager = new ServerManager();

                foreach (var appPool in serverManager.ApplicationPools)
                {
                    var appPoolInfo = new ApplicationPoolInfo
                    {
                        Name = appPool.Name,
                        Status = appPool.State.ToString(),
                        IsEnabled = appPool.State == ObjectState.Started,
                        LastUpdated = DateTime.Now
                    };

                    // Obtener contadores de rendimiento para cada Application Pool
                    try
                    {
                        var cpuCounter = new PerformanceCounter("Process", "% Processor Time", $"w3wp_{appPool.Name}", true);
                        var memoryCounter = new PerformanceCounter("Process", "Working Set - Private", $"w3wp_{appPool.Name}", true);

                        // Primera lectura (necesaria para inicializar)
                        cpuCounter.NextValue();
                        Thread.Sleep(100);

                        appPoolInfo.CpuUsage = Math.Round(cpuCounter.NextValue(), 2);
                        appPoolInfo.MemoryUsageMB = (long)(memoryCounter.NextValue() / 1024 / 1024);

                        cpuCounter.Dispose();
                        memoryCounter.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"No se pudieron obtener contadores para {appPool.Name}: {ex.Message}");
                        appPoolInfo.CpuUsage = 0;
                        appPoolInfo.MemoryUsageMB = 0;
                    }

                    // Obtener requests
                    try
                    {
                        var requestCounter = new PerformanceCounter("APP_POOL_WAS", "Current Application Pool State", appPool.Name, true);
                        var totalRequestCounter = new PerformanceCounter("APP_POOL_WAS", "Total Application Pool Recycles", appPool.Name, true);

                        appPoolInfo.ActiveRequests = (int)requestCounter.NextValue();
                        appPoolInfo.TotalRequests = (int)totalRequestCounter.NextValue();

                        requestCounter.Dispose();
                        totalRequestCounter.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"No se pudieron obtener requests para {appPool.Name}: {ex.Message}");
                    }

                    appPools.Add(appPoolInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error obteniendo Application Pools: {ex.Message}");
            }

            return appPools;
        });
    }

    public async Task<List<WebSiteInfo>> GetWebSitesAsync()
    {
        return await Task.Run(() =>
        {
            var webSites = new List<WebSiteInfo>();

            try
            {
                using var serverManager = new ServerManager();

                foreach (var site in serverManager.Sites)
                {
                    var siteInfo = new WebSiteInfo
                    {
                        Name = site.Name,
                        Id = (int)site.Id,
                        Status = site.State.ToString(),
                        ApplicationPool = site.Applications["/"]?.ApplicationPoolName ?? "N/A",
                        Bindings = site.Bindings.Select(b => $"{b.Protocol}://{b.Host}:{b.EndPoint.Port}").ToList()
                    };

                    // Obtener contadores de rendimiento por sitio
                    try
                    {
                        var requestsCounter = new PerformanceCounter("Web Service", "Total Method Requests/sec", site.Name, true);
                        var bytesSentCounter = new PerformanceCounter("Web Service", "Bytes Sent/sec", site.Name, true);
                        var bytesReceivedCounter = new PerformanceCounter("Web Service", "Bytes Received/sec", site.Name, true);
                        var connectionsCounter = new PerformanceCounter("Web Service", "Current Connections", site.Name, true);

                        siteInfo.RequestsPerSecond = (long)requestsCounter.NextValue();
                        siteInfo.BytesSentPerSecond = (long)bytesSentCounter.NextValue();
                        siteInfo.BytesReceivedPerSecond = (long)bytesReceivedCounter.NextValue();
                        siteInfo.CurrentConnections = (int)connectionsCounter.NextValue();

                        requestsCounter.Dispose();
                        bytesSentCounter.Dispose();
                        bytesReceivedCounter.Dispose();
                        connectionsCounter.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"No se pudieron obtener contadores para el sitio {site.Name}: {ex.Message}");
                    }

                    webSites.Add(siteInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error obteniendo sitios web: {ex.Message}");
            }

            return webSites;
        });
    }

    public async Task<PerformanceMetrics> GetPerformanceMetricsAsync()
    {
        return await Task.Run(() =>
        {
            var metrics = new PerformanceMetrics
            {
                Timestamp = DateTime.Now
            };

            try
            {
                // CPU total del sistema
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                cpuCounter.NextValue();
                Thread.Sleep(100);
                metrics.TotalCpuUsage = Math.Round(cpuCounter.NextValue(), 2);
                cpuCounter.Dispose();

                // Memoria
                var memoryCounter = new PerformanceCounter("Memory", "Available MBytes", true);
                metrics.AvailableMemoryMB = (long)memoryCounter.NextValue();
                memoryCounter.Dispose();

                var commitCounter = new PerformanceCounter("Memory", "Committed Bytes", true);
                metrics.TotalMemoryUsageMB = (long)(commitCounter.NextValue() / 1024 / 1024);
                commitCounter.Dispose();

                // Contar Application Pools
                using var serverManager = new ServerManager();
                metrics.TotalApplicationPools = serverManager.ApplicationPools.Count;
                metrics.RunningApplicationPools = serverManager.ApplicationPools.Count(ap => ap.State == ObjectState.Started);
                metrics.StoppedApplicationPools = serverManager.ApplicationPools.Count(ap => ap.State == ObjectState.Stopped);

                // Contar sitios web
                metrics.TotalWebSites = serverManager.Sites.Count;
                metrics.RunningWebSites = serverManager.Sites.Count(s => s.State == ObjectState.Started);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error obteniendo métricas de rendimiento: {ex.Message}");
            }

            return metrics;
        });
    }
}
