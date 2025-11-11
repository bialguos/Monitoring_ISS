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
    private bool _isInitialLoadComplete = false;

    public bool IsInitialLoadComplete => _isInitialLoadComplete;

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
        _logger.LogInformation("IIS APM Background Service iniciado. Intervalo: {IntervalMs}ms", _intervalMs);

        // Esperar un poco antes de empezar para que los servicios se inicialicen
        await Task.Delay(2000, stoppingToken);

        var iteration = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            iteration++;
            _logger.LogInformation("=== IIS APM Background Service - Iteración #{Iteration} iniciada ===", iteration);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var apmService = scope.ServiceProvider.GetRequiredService<IApmService>();
                var logParser = scope.ServiceProvider.GetRequiredService<IISLogParserService>();

                // Obtener los sitios configurados para monitorizar
                var monitoredSites = GetMonitoredSites();
                _logger.LogInformation("Sitios monitorizados: {Count} - IDs: {SiteIds}",
                    monitoredSites.Count,
                    string.Join(", ", monitoredSites));

                if (monitoredSites.Any())
                {
                    // Obtener nuevas entradas de log
                    _logger.LogDebug("Obteniendo nuevas entradas de log...");
                    var entries = await logParser.GetNewLogEntriesAsync(monitoredSites, maxEntries: 500);
                    _logger.LogInformation("Entradas de log obtenidas: {Count}", entries.Count);

                    // Convertir entradas de log a traces
                    var tracesCreated = 0;
                    foreach (var entry in entries)
                    {
                        ConvertLogEntryToTrace(apmService, entry);
                        tracesCreated++;
                    }

                    if (entries.Any())
                    {
                        _logger.LogInformation("Procesadas {Count} entradas de log de IIS, {TracesCreated} traces creados",
                            entries.Count, tracesCreated);

                        // Marcar carga inicial como completada después de la primera iteración con datos
                        if (!_isInitialLoadComplete && iteration == 1)
                        {
                            // TEMPORAL: Delay para ver el banner de carga (eliminar en producción)
                            await Task.Delay(5000, stoppingToken);

                            _isInitialLoadComplete = true;
                            _logger.LogInformation("Carga inicial de APM completada");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("No hay nuevas entradas de log para procesar");
                    }
                }
                else
                {
                    _logger.LogWarning("No hay sitios configurados para monitorizar");
                }

                _logger.LogInformation("=== IIS APM Background Service - Iteración #{Iteration} completada ===", iteration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en IIS APM Background Service - Iteración #{Iteration}", iteration);
            }

            _logger.LogDebug("Esperando {IntervalMs}ms hasta la próxima iteración...", _intervalMs);
            await Task.Delay(_intervalMs, stoppingToken);
        }

        _logger.LogInformation("IIS APM Background Service detenido después de {Iterations} iteraciones", iteration);
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

                // Copiar tags importantes del span al trace para facilitar el filtrado
                trace.Tags["iis.site.id"] = entry.SiteId;
                trace.Tags["iis.site.name"] = entry.SiteName;
                trace.Tags["http.method"] = entry.Method;
                trace.Tags["http.status_code"] = entry.StatusCode.ToString();
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
            var apmConfig = scope.ServiceProvider.GetRequiredService<ApmConfigurationService>();

            var monitoredSites = apmConfig.GetMonitoredSites();

            // Si no hay sitios configurados, monitorizar todos
            if (!monitoredSites.Any())
            {
                var logParser = scope.ServiceProvider.GetRequiredService<IISLogParserService>();
                var availableSites = logParser.GetAvailableSites();
                return availableSites.Select(s => s.Id).ToList();
            }

            return monitoredSites;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo sitios monitorizados");
            return new List<string>();
        }
    }
}
