using System.Collections.Concurrent;
using System.Text.Json;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Servicio para gestionar la configuración de sitios monitorizados dinámicamente
/// </summary>
public class ApmConfigurationService
{
    private readonly ILogger<ApmConfigurationService> _logger;
    private readonly string _configFilePath;
    private readonly ConcurrentBag<string> _monitoredSites = new();
    private readonly object _lock = new();

    public ApmConfigurationService(ILogger<ApmConfigurationService> logger, IConfiguration configuration)
    {
        _logger = logger;

        // Ruta del archivo de configuración
        var contentRoot = configuration.GetValue<string>("ContentRoot") ?? Directory.GetCurrentDirectory();
        _configFilePath = Path.Combine(contentRoot, "Data", "apm-config.json");

        // Cargar configuración inicial
        LoadConfiguration();
    }

    /// <summary>
    /// Obtiene la lista de sitios monitorizados
    /// </summary>
    public List<string> GetMonitoredSites()
    {
        lock (_lock)
        {
            return _monitoredSites.ToList();
        }
    }

    /// <summary>
    /// Establece la lista de sitios a monitorizar y persiste la configuración
    /// </summary>
    public async Task SetMonitoredSitesAsync(List<string> siteIds)
    {
        lock (_lock)
        {
            _monitoredSites.Clear();
            foreach (var siteId in siteIds)
            {
                _monitoredSites.Add(siteId);
            }
        }

        // Persistir la configuración
        await SaveConfigurationAsync();

        _logger.LogInformation("Configuración de sitios monitorizados actualizada: {SiteIds}", string.Join(", ", siteIds));
    }

    /// <summary>
    /// Verifica si un sitio está siendo monitorizado
    /// </summary>
    public bool IsSiteMonitored(string siteId)
    {
        lock (_lock)
        {
            return _monitoredSites.Contains(siteId);
        }
    }

    /// <summary>
    /// Carga la configuración desde el archivo
    /// </summary>
    private void LoadConfiguration()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("Archivo de configuración APM no encontrado, se creará uno nuevo");
                return;
            }

            var json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<ApmConfigurationData>(json);

            if (config?.MonitoredSites != null)
            {
                lock (_lock)
                {
                    _monitoredSites.Clear();
                    foreach (var siteId in config.MonitoredSites)
                    {
                        _monitoredSites.Add(siteId);
                    }
                }

                _logger.LogInformation("Configuración APM cargada: {Count} sitios monitorizados", config.MonitoredSites.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar configuración APM desde archivo");
        }
    }

    /// <summary>
    /// Guarda la configuración en el archivo
    /// </summary>
    private async Task SaveConfigurationAsync()
    {
        try
        {
            // Crear directorio si no existe
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var config = new ApmConfigurationData
            {
                MonitoredSites = GetMonitoredSites(),
                LastUpdated = DateTime.UtcNow
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(config, options);
            await File.WriteAllTextAsync(_configFilePath, json);

            _logger.LogInformation("Configuración APM guardada en {FilePath}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar configuración APM en archivo");
        }
    }

    /// <summary>
    /// Recarga la configuración desde el archivo
    /// </summary>
    public void ReloadConfiguration()
    {
        LoadConfiguration();
    }
}

/// <summary>
/// Modelo de datos para la configuración de APM
/// </summary>
public class ApmConfigurationData
{
    public List<string> MonitoredSites { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}
