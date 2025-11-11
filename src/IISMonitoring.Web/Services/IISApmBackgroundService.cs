using IISMonitoring.Web.Models;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Servicio de background que monitoriza logs de IIS y genera traces de APM
/// </summary>
public class IISApmBackgroundService : BackgroundService
{
    private readonly ILogger<IISApmBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _intervalMs;

    public IISApmBackgroundService(
        ILogger<IISApmBackgroundService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _intervalMs = configuration.GetValue<int>("Apm:UpdateIntervalMs", 5000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IIS APM Background Service iniciado");

        // Esperar un poco antes de empezar para que los servicios se inicialicen
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var apmService = scope.ServiceProvider.GetRequiredService<IApmService>();
                var logParser = scope.ServiceProvider.GetRequiredService<IISLogParserService>();

                // Obtener los sitios configurados para monitorizar
                var monitoredSites = GetMonitoredSites();

                if (monitoredSites.Any())
                {
                    // Obtener nuevas entradas de log
                    var entries = await logParser.GetNewLogEntriesAsync(monitoredSites, maxEntries: 500);

                    // Convertir entradas de log a traces
                    foreach (var entry in entries)
                    {
                        ConvertLogEntryToTrace(apmService, entry);
                    }

                    if (entries.Any())
                    {
                        _logger.LogDebug("Procesadas {Count} entradas de log de IIS", entries.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en IIS APM Background Service");
            }

            await Task.Delay(_intervalMs, stoppingToken);
        }

        _logger.LogInformation("IIS APM Background Service detenido");
    }

    /// <summary>
    /// Convierte una entrada de log de IIS a un trace de APM
    /// </summary>
    private void ConvertLogEntryToTrace(IApmService apmService, IISLogEntry entry)
    {
        try
        {
            var operationName = $"{entry.Method} {entry.UriStem}";
            var serviceName = entry.SiteName;

            var traceId = apmService.StartTrace(operationName, serviceName);

            // Crear span principal
            var span = new SpanInfo
            {
                SpanId = Guid.NewGuid().ToString("N"),
                TraceId = traceId,
                OperationName = operationName,
                SpanKind = "Server",
                ServiceName = serviceName,
                StartTime = entry.DateTime,
                DurationMs = entry.TimeTaken,
                Status = entry.StatusCode >= 400 ? "Error" : "Success",
                ErrorMessage = entry.StatusCode >= 400 ? $"HTTP {entry.StatusCode}" : null,
                Tags = new Dictionary<string, string>
                {
                    ["http.method"] = entry.Method,
                    ["http.url"] = entry.UriStem,
                    ["http.query"] = entry.UriQuery,
                    ["http.status_code"] = entry.StatusCode.ToString(),
                    ["http.host"] = entry.Host,
                    ["client.ip"] = entry.ClientIp,
                    ["server.ip"] = entry.ServerIp,
                    ["server.port"] = entry.ServerPort.ToString(),
                    ["iis.site.id"] = entry.SiteId,
                    ["iis.site.name"] = entry.SiteName,
                    ["time.taken.ms"] = entry.TimeTaken.ToString()
                }
            };

            if (!string.IsNullOrEmpty(entry.UserAgent))
            {
                span.Tags["http.user_agent"] = entry.UserAgent;
            }

            if (!string.IsNullOrEmpty(entry.Username))
            {
                span.Tags["user.name"] = entry.Username;
            }

            if (!string.IsNullOrEmpty(entry.Referer))
            {
                span.Tags["http.referer"] = entry.Referer;
            }

            apmService.AddSpan(traceId, span);

            // Finalizar el trace
            var status = entry.StatusCode >= 400 ? "Error" : "Success";
            var errorMessage = entry.StatusCode >= 400 ? $"HTTP {entry.StatusCode}" : null;
            apmService.EndTrace(traceId, status, errorMessage);

            // Actualizar el trace con información correcta
            var trace = apmService.GetTrace(traceId);
            if (trace != null)
            {
                trace.StartTime = entry.DateTime;
                trace.DurationMs = entry.TimeTaken;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error convirtiendo entrada de log a trace");
        }
    }

    /// <summary>
    /// Obtiene la lista de sitios configurados para monitorizar
    /// </summary>
    private List<string> GetMonitoredSites()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var sitesConfig = configuration.GetSection("Apm:MonitoredSites").Get<string[]>();
            if (sitesConfig != null && sitesConfig.Any())
            {
                return sitesConfig.ToList();
            }

            // Si no hay configuración, monitorizar todos los sitios
            var logParser = scope.ServiceProvider.GetRequiredService<IISLogParserService>();
            var availableSites = logParser.GetAvailableSites();
            return availableSites.Select(s => s.Id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo sitios monitorizados");
            return new List<string>();
        }
    }
}
