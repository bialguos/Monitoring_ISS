using IISMonitoring.Web.Configuration;
using IISMonitoring.Web.Models;
using Microsoft.Extensions.Options;
using Microsoft.Web.Administration;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace IISMonitoring.Web.Services;

public class OptimizedIISMonitoringService : IIISMonitoringService, IDisposable
{
    // Estructuras para P/Invoke de Windows API
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    private readonly ILogger<OptimizedIISMonitoringService> _logger;
    private readonly MonitoringOptions _options;

    // Caché de Performance Counters para evitar crear/destruir constantemente
    private readonly ConcurrentDictionary<string, PerformanceCounter> _counterCache;
    private readonly ConcurrentDictionary<string, DateTime> _counterCacheTimestamps;

    // Contadores del sistema (reutilizables)
    private PerformanceCounter? _systemCpuCounter;
    private PerformanceCounter? _availableMemoryCounter;
    private PerformanceCounter? _committedBytesCounter;

    private readonly object _initLock = new object();
    private bool _disposed = false;
    private DateTime _lastCacheCleanup = DateTime.Now;

    public OptimizedIISMonitoringService(
        ILogger<OptimizedIISMonitoringService> logger,
        IOptions<MonitoringOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _counterCache = new ConcurrentDictionary<string, PerformanceCounter>();
        _counterCacheTimestamps = new ConcurrentDictionary<string, DateTime>();

        InitializeSystemCounters();
    }

    private void InitializeSystemCounters()
    {
        try
        {
            _systemCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes", true);
            _committedBytesCounter = new PerformanceCounter("Memory", "Committed Bytes", true);

            // Primera lectura para inicializar (sin bloqueo)
            _systemCpuCounter.NextValue();

            _logger.LogInformation("Contadores del sistema inicializados correctamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar contadores del sistema");
        }
    }

    public async Task<MonitoringDashboard> GetDashboardDataAsync()
    {
        // Limpiar caché si es necesario
        CleanupCacheIfNeeded();

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

                    if (appPool.State == ObjectState.Started)
                    {
                        // Obtener contadores solo para app pools activos
                        GetAppPoolPerformanceCounters(appPool.Name, appPoolInfo);
                        GetAppPoolRequestCounters(appPool.Name, appPoolInfo);
                    }

                    appPools.Add(appPoolInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo Application Pools");
            }

            return appPools;
        });
    }

    private void GetAppPoolPerformanceCounters(string appPoolName, ApplicationPoolInfo appPoolInfo)
    {
        try
        {
            // Método 1: Intentar usar los contadores específicos de W3SVC_W3WP (organizados por Application Pool)
            var cpuKey = $"W3WP_{appPoolName}_CPU";
            var memKey = $"W3WP_{appPoolName}_Memory";

            var cpuCounter = GetOrCreateCounter("W3SVC_W3WP", "% Processor Time", appPoolName, cpuKey);
            var memCounter = GetOrCreateCounter("W3SVC_W3WP", "Active Threads", appPoolName, memKey);

            if (cpuCounter != null)
            {
                var cpuValue = cpuCounter.NextValue();
                appPoolInfo.CpuUsage = Math.Round(cpuValue, 2);
            }

            // Para memoria, buscar el proceso w3wp específico de este Application Pool
            GetAppPoolMemoryFromProcess(appPoolName, appPoolInfo);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error obteniendo contadores W3SVC_W3WP para {AppPoolName}, intentando método alternativo", appPoolName);

            // Método alternativo: buscar por PID usando WMI o Process
            try
            {
                GetAppPoolMemoryFromProcess(appPoolName, appPoolInfo);
            }
            catch (Exception ex2)
            {
                _logger.LogDebug(ex2, "No se pudieron obtener contadores de rendimiento para {AppPoolName}", appPoolName);
                appPoolInfo.CpuUsage = 0;
                appPoolInfo.MemoryUsageMB = 0;
            }
        }
    }

    private void GetAppPoolMemoryFromProcess(string appPoolName, ApplicationPoolInfo appPoolInfo)
    {
        try
        {
            // Obtener el PID del proceso w3wp que corresponde a este Application Pool usando WMI
            var targetPid = GetAppPoolProcessId(appPoolName);

            if (targetPid == 0)
            {
                _logger.LogTrace("No se encontró proceso w3wp para el Application Pool {AppPoolName}", appPoolName);
                return;
            }

            // Obtener el proceso específico
            try
            {
                var process = Process.GetProcessById(targetPid);

                // Obtener el nombre de instancia del contador para este proceso específico
                var instanceName = GetProcessInstanceName(process);

                if (string.IsNullOrEmpty(instanceName))
                {
                    _logger.LogTrace("No se pudo obtener el nombre de instancia para el proceso {ProcessId}", targetPid);
                    process.Dispose();
                    return;
                }

                // Crear claves únicas para el caché
                var cpuKey = $"Process_{appPoolName}_{targetPid}_CPU";
                var memKey = $"Process_{appPoolName}_{targetPid}_Memory";

                // Obtener contadores usando el nombre de instancia correcto
                var cpuCounter = GetOrCreateCounter("Process", "% Processor Time", instanceName, cpuKey);
                var memCounter = GetOrCreateCounter("Process", "Working Set - Private", instanceName, memKey);

                if (cpuCounter != null)
                {
                    var cpuValue = cpuCounter.NextValue();
                    appPoolInfo.CpuUsage = Math.Round(cpuValue, 2);
                }

                if (memCounter != null)
                {
                    var memValue = memCounter.NextValue();
                    appPoolInfo.MemoryUsageMB = (long)(memValue / 1024 / 1024);
                }

                process.Dispose();
            }
            catch (ArgumentException)
            {
                _logger.LogTrace("El proceso {ProcessId} ya no existe", targetPid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error obteniendo memoria desde procesos para {AppPoolName}", appPoolName);
        }
    }

    private int GetAppPoolProcessId(string appPoolName)
    {
        try
        {
            // Usar WMI para obtener el PID del proceso w3wp que corresponde a este Application Pool
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'w3wp.exe'");

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                try
                {
                    var commandLine = obj["CommandLine"]?.ToString() ?? string.Empty;

                    // La línea de comandos contiene el nombre del Application Pool como parámetro -ap "nombre"
                    if (commandLine.Contains($"-ap \"{appPoolName}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        return Convert.ToInt32(obj["ProcessId"]);
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error usando WMI para obtener PID del Application Pool {AppPoolName}", appPoolName);
        }

        return 0;
    }

    private string GetProcessInstanceName(Process process)
    {
        try
        {
            // Obtener el nombre de instancia correcto del contador de rendimiento
            var category = new PerformanceCounterCategory("Process");
            var instances = category.GetInstanceNames();

            // Buscar la instancia que corresponde a este PID
            foreach (var instance in instances)
            {
                if (instance.StartsWith("w3wp", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var pidCounter = new PerformanceCounter("Process", "ID Process", instance, true);
                        if ((int)pidCounter.NextValue() == process.Id)
                        {
                            return instance;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error obteniendo nombre de instancia para proceso {ProcessId}", process.Id);
        }

        return string.Empty;
    }

    private void GetAppPoolRequestCounters(string appPoolName, ApplicationPoolInfo appPoolInfo)
    {
        try
        {
            var activeRequestKey = $"AppPool_{appPoolName}_ActiveRequests";
            var totalRequestKey = $"AppPool_{appPoolName}_TotalRequests";

            var activeRequestCounter = GetOrCreateCounter("W3SVC_W3WP", "Active Requests", appPoolName, activeRequestKey);
            var totalRequestCounter = GetOrCreateCounter("W3SVC_W3WP", "Total HTTP Requests Served", appPoolName, totalRequestKey);

            if (activeRequestCounter != null)
            {
                appPoolInfo.ActiveRequests = (int)activeRequestCounter.NextValue();
            }

            if (totalRequestCounter != null)
            {
                appPoolInfo.TotalRequests = (int)totalRequestCounter.NextValue();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudieron obtener contadores de requests para {AppPoolName}", appPoolName);
        }
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
                    var rootApp = site.Applications["/"];
                    var physicalPath = string.Empty;

                    // Obtener la ruta física del directorio virtual raíz
                    if (rootApp != null && rootApp.VirtualDirectories.Count > 0)
                    {
                        physicalPath = rootApp.VirtualDirectories["/"]?.PhysicalPath ?? string.Empty;
                    }

                    var siteInfo = new WebSiteInfo
                    {
                        Name = site.Name,
                        Id = (int)site.Id,
                        Status = site.State.ToString(),
                        ApplicationPool = rootApp?.ApplicationPoolName ?? "N/A",
                        PhysicalPath = physicalPath,
                        Bindings = site.Bindings.Select(b => $"{b.Protocol}://{b.Host}:{b.EndPoint.Port}").ToList()
                    };

                    if (site.State == ObjectState.Started)
                    {
                        GetWebSitePerformanceCounters(site.Name, siteInfo);
                    }

                    webSites.Add(siteInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo sitios web");
            }

            return webSites;
        });
    }

    private void GetWebSitePerformanceCounters(string siteName, WebSiteInfo siteInfo)
    {
        try
        {
            var requestsKey = $"Site_{siteName}_Requests";
            var bytesSentKey = $"Site_{siteName}_BytesSent";
            var bytesReceivedKey = $"Site_{siteName}_BytesReceived";
            var connectionsKey = $"Site_{siteName}_Connections";

            var requestsCounter = GetOrCreateCounter("Web Service", "Total Method Requests/sec", siteName, requestsKey);
            var bytesSentCounter = GetOrCreateCounter("Web Service", "Bytes Sent/sec", siteName, bytesSentKey);
            var bytesReceivedCounter = GetOrCreateCounter("Web Service", "Bytes Received/sec", siteName, bytesReceivedKey);
            var connectionsCounter = GetOrCreateCounter("Web Service", "Current Connections", siteName, connectionsKey);

            if (requestsCounter != null)
                siteInfo.RequestsPerSecond = (long)requestsCounter.NextValue();

            if (bytesSentCounter != null)
                siteInfo.BytesSentPerSecond = (long)bytesSentCounter.NextValue();

            if (bytesReceivedCounter != null)
                siteInfo.BytesReceivedPerSecond = (long)bytesReceivedCounter.NextValue();

            if (connectionsCounter != null)
                siteInfo.CurrentConnections = (int)connectionsCounter.NextValue();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudieron obtener contadores para el sitio {SiteName}", siteName);
        }
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
                // Obtener CPU con un pequeño delay para mayor precisión
                if (_systemCpuCounter != null)
                {
                    try
                    {
                        // Primera lectura (ignorar el valor)
                        _systemCpuCounter.NextValue();
                        // Pequeño delay para que el contador calcule correctamente
                        Thread.Sleep(100);
                        // Segunda lectura (valor real)
                        metrics.TotalCpuUsage = Math.Round(_systemCpuCounter.NextValue(), 2);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error obteniendo CPU del sistema");
                    }
                }

                // Usar API nativa de Windows para memoria (más precisa que performance counters)
                var memStatus = new MEMORYSTATUSEX
                {
                    dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
                };

                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    // Memoria total física en MB
                    var totalPhysicalMB = (long)(memStatus.ullTotalPhys / 1024 / 1024);

                    // Memoria disponible física en MB
                    metrics.AvailableMemoryMB = (long)(memStatus.ullAvailPhys / 1024 / 1024);

                    // Memoria usada = Total - Disponible
                    metrics.TotalMemoryUsageMB = totalPhysicalMB - metrics.AvailableMemoryMB;

                    _logger.LogTrace(
                        "Memoria del sistema: Total={TotalMB}MB, Usada={UsedMB}MB, Disponible={AvailableMB}MB, Porcentaje={Percentage}%",
                        totalPhysicalMB,
                        metrics.TotalMemoryUsageMB,
                        metrics.AvailableMemoryMB,
                        memStatus.dwMemoryLoad);
                }
                else
                {
                    // Fallback a performance counters si la API falla
                    _logger.LogWarning("No se pudo usar GlobalMemoryStatusEx, usando performance counters");

                    if (_availableMemoryCounter != null)
                    {
                        metrics.AvailableMemoryMB = (long)_availableMemoryCounter.NextValue();
                    }

                    if (_committedBytesCounter != null)
                    {
                        metrics.TotalMemoryUsageMB = (long)(_committedBytesCounter.NextValue() / 1024 / 1024);
                    }
                }

                // Contar Application Pools y sitios
                using var serverManager = new ServerManager();
                metrics.TotalApplicationPools = serverManager.ApplicationPools.Count;
                metrics.RunningApplicationPools = serverManager.ApplicationPools.Count(ap => ap.State == ObjectState.Started);
                metrics.StoppedApplicationPools = serverManager.ApplicationPools.Count(ap => ap.State == ObjectState.Stopped);

                metrics.TotalWebSites = serverManager.Sites.Count;
                metrics.RunningWebSites = serverManager.Sites.Count(s => s.State == ObjectState.Started);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo métricas de rendimiento");
            }

            return metrics;
        });
    }

    private PerformanceCounter? GetOrCreateCounter(string categoryName, string counterName, string instanceName, string cacheKey)
    {
        if (!_options.EnableCounterCaching)
        {
            // Si el caché está deshabilitado, crear contador temporal
            try
            {
                return new PerformanceCounter(categoryName, counterName, instanceName, true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No se pudo crear contador {Category}/{Counter}/{Instance}",
                    categoryName, counterName, instanceName);
                return null;
            }
        }

        // Intentar obtener del caché
        if (_counterCache.TryGetValue(cacheKey, out var counter))
        {
            return counter;
        }

        // Crear nuevo contador y agregarlo al caché
        try
        {
            var newCounter = new PerformanceCounter(categoryName, counterName, instanceName, true);

            // Realizar primera lectura para inicializar (sin bloqueo)
            newCounter.NextValue();

            if (_counterCache.TryAdd(cacheKey, newCounter))
            {
                _counterCacheTimestamps.TryAdd(cacheKey, DateTime.Now);
                return newCounter;
            }
            else
            {
                // Si no se pudo agregar al caché (race condition), disponer y retornar el existente
                newCounter.Dispose();
                _counterCache.TryGetValue(cacheKey, out counter);
                return counter;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo crear contador {CacheKey}", cacheKey);
            return null;
        }
    }

    private void CleanupCacheIfNeeded()
    {
        // Limpiar caché cada 5 minutos para evitar contadores obsoletos
        if ((DateTime.Now - _lastCacheCleanup).TotalMinutes < 5)
            return;

        lock (_initLock)
        {
            if ((DateTime.Now - _lastCacheCleanup).TotalMinutes < 5)
                return;

            try
            {
                var expiredKeys = new List<string>();
                var cacheLifetime = TimeSpan.FromMinutes(_options.CounterCacheLifetimeMinutes);

                foreach (var kvp in _counterCacheTimestamps)
                {
                    if (DateTime.Now - kvp.Value > cacheLifetime)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    if (_counterCache.TryRemove(key, out var counter))
                    {
                        counter?.Dispose();
                    }
                    _counterCacheTimestamps.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    _logger.LogInformation("Limpieza de caché: {Count} contadores expirados removidos", expiredKeys.Count);
                }

                _lastCacheCleanup = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante limpieza de caché");
            }
        }
    }

    public async Task<bool> StartApplicationPoolAsync(string poolName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var serverManager = new ServerManager();
                var appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name == poolName);

                if (appPool == null)
                {
                    _logger.LogWarning($"Application Pool '{poolName}' no encontrado");
                    return false;
                }

                if (appPool.State == ObjectState.Stopped || appPool.State == ObjectState.Stopping)
                {
                    appPool.Start();
                    _logger.LogInformation($"Application Pool '{poolName}' iniciado correctamente");
                    return true;
                }

                _logger.LogInformation($"Application Pool '{poolName}' ya está en estado {appPool.State}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al iniciar Application Pool '{poolName}'");
                return false;
            }
        });
    }

    public async Task<bool> StopApplicationPoolAsync(string poolName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var serverManager = new ServerManager();
                var appPool = serverManager.ApplicationPools.FirstOrDefault(ap => ap.Name == poolName);

                if (appPool == null)
                {
                    _logger.LogWarning($"Application Pool '{poolName}' no encontrado");
                    return false;
                }

                if (appPool.State == ObjectState.Started || appPool.State == ObjectState.Starting)
                {
                    appPool.Stop();
                    _logger.LogInformation($"Application Pool '{poolName}' detenido correctamente");
                    return true;
                }

                _logger.LogInformation($"Application Pool '{poolName}' ya está en estado {appPool.State}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al detener Application Pool '{poolName}'");
                return false;
            }
        });
    }

    public async Task<bool> StartWebSiteAsync(string siteName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var serverManager = new ServerManager();
                var site = serverManager.Sites.FirstOrDefault(s => s.Name == siteName);

                if (site == null)
                {
                    _logger.LogWarning($"Sitio Web '{siteName}' no encontrado");
                    return false;
                }

                if (site.State == ObjectState.Stopped || site.State == ObjectState.Stopping)
                {
                    site.Start();
                    _logger.LogInformation($"Sitio Web '{siteName}' iniciado correctamente");
                    return true;
                }

                _logger.LogInformation($"Sitio Web '{siteName}' ya está en estado {site.State}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al iniciar Sitio Web '{siteName}'");
                return false;
            }
        });
    }

    public async Task<bool> StopWebSiteAsync(string siteName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var serverManager = new ServerManager();
                var site = serverManager.Sites.FirstOrDefault(s => s.Name == siteName);

                if (site == null)
                {
                    _logger.LogWarning($"Sitio Web '{siteName}' no encontrado");
                    return false;
                }

                if (site.State == ObjectState.Started || site.State == ObjectState.Starting)
                {
                    site.Stop();
                    _logger.LogInformation($"Sitio Web '{siteName}' detenido correctamente");
                    return true;
                }

                _logger.LogInformation($"Sitio Web '{siteName}' ya está en estado {site.State}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al detener Sitio Web '{siteName}'");
                return false;
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_initLock)
        {
            if (_disposed)
                return;

            try
            {
                // Disponer contadores del sistema
                _systemCpuCounter?.Dispose();
                _availableMemoryCounter?.Dispose();
                _committedBytesCounter?.Dispose();

                // Disponer todos los contadores en caché
                foreach (var counter in _counterCache.Values)
                {
                    counter?.Dispose();
                }

                _counterCache.Clear();
                _counterCacheTimestamps.Clear();

                _logger.LogInformation("Recursos de monitorización liberados correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al liberar recursos");
            }
            finally
            {
                _disposed = true;
            }
        }

        GC.SuppressFinalize(this);
    }

    ~OptimizedIISMonitoringService()
    {
        Dispose();
    }
}
