using IISMonitoring.Web.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Servicio de Application Performance Monitoring usando DiagnosticSource
/// </summary>
public class ApmService : IApmService, IDisposable
{
    private readonly ConcurrentDictionary<string, TraceInfo> _traces = new();
    private readonly ConcurrentDictionary<string, DateTime> _traceStartTimes = new();
    private readonly List<IDisposable> _subscriptions = new();
    private readonly int _maxTraces = 1000; // Máximo de traces en memoria
    private readonly string _serviceName;
    private readonly ILogger<ApmService> _logger;

    public ApmService(IConfiguration configuration, ILogger<ApmService> logger)
    {
        _logger = logger;
        _serviceName = configuration["ServiceName"] ?? "IISMonitoring";
        InitializeDiagnosticListeners();
    }

    private void InitializeDiagnosticListeners()
    {
        try
        {
            // Subscribirse a eventos de ASP.NET Core
            var listenerObserver = new DiagnosticListenerObserver(this, _logger);
            var aspNetCoreSubscription = DiagnosticListener.AllListeners.Subscribe(listenerObserver);

            _subscriptions.Add(aspNetCoreSubscription);
            _logger.LogInformation("APM Service inicializado correctamente");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inicializando DiagnosticListeners");
        }
    }

    public string StartTrace(string operationName, string serviceName)
    {
        var traceId = Guid.NewGuid().ToString("N");
        var trace = new TraceInfo
        {
            TraceId = traceId,
            ServiceName = serviceName,
            OperationName = operationName,
            StartTime = DateTime.UtcNow,
            Status = "InProgress"
        };

        _traces.TryAdd(traceId, trace);
        _traceStartTimes.TryAdd(traceId, DateTime.UtcNow);

        // Limpiar traces antiguos si excedemos el límite
        CleanupOldTracesIfNeeded();

        return traceId;
    }

    public void EndTrace(string traceId, string status = "Success", string? errorMessage = null)
    {
        if (_traces.TryGetValue(traceId, out var trace) &&
            _traceStartTimes.TryGetValue(traceId, out var startTime))
        {
            trace.DurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
            trace.Status = status;
            trace.ErrorMessage = errorMessage;
            _traceStartTimes.TryRemove(traceId, out _);
        }
    }

    public void AddSpan(string traceId, SpanInfo span)
    {
        if (_traces.TryGetValue(traceId, out var trace))
        {
            trace.Spans.Add(span);
        }
    }

    public ApmDashboard GetDashboard()
    {
        var traces = _traces.Values.OrderByDescending(t => t.StartTime).Take(50).ToList();

        return new ApmDashboard
        {
            RecentTraces = traces,
            Statistics = GetStatistics(),
            Timestamp = DateTime.UtcNow
        };
    }

    public List<TraceInfo> GetTraces(int limit = 100, string? status = null)
    {
        var query = _traces.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderByDescending(t => t.StartTime)
            .Take(limit)
            .ToList();
    }

    public TraceInfo? GetTrace(string traceId)
    {
        _traces.TryGetValue(traceId, out var trace);
        return trace;
    }

    public ApmStatistics GetStatistics()
    {
        var allTraces = _traces.Values.Where(t => t.Status != "InProgress").ToList();

        if (!allTraces.Any())
        {
            return new ApmStatistics();
        }

        var errorTraces = allTraces.Where(t => t.Status == "Error").ToList();
        var durations = allTraces.Select(t => t.DurationMs).ToList();

        // Agrupar por operación para encontrar las más lentas
        var operationStats = allTraces
            .GroupBy(t => t.OperationName)
            .Select(g => new OperationStatistic
            {
                OperationName = g.Key,
                AverageDurationMs = g.Average(t => t.DurationMs),
                Count = g.Count(),
                ErrorCount = g.Count(t => t.Status == "Error")
            })
            .OrderByDescending(o => o.AverageDurationMs)
            .Take(10)
            .ToList();

        return new ApmStatistics
        {
            TotalTraces = allTraces.Count,
            ErrorTraces = errorTraces.Count,
            AverageDurationMs = durations.Any() ? durations.Average() : 0,
            MaxDurationMs = durations.Any() ? durations.Max() : 0,
            MinDurationMs = durations.Any() ? durations.Min() : 0,
            ErrorRate = allTraces.Count > 0 ? (double)errorTraces.Count / allTraces.Count * 100 : 0,
            SlowestOperations = operationStats
        };
    }

    public void ClearTraces()
    {
        _traces.Clear();
        _traceStartTimes.Clear();
        _logger.LogInformation("Todos los traces han sido limpiados");
    }

    private void CleanupOldTracesIfNeeded()
    {
        if (_traces.Count > _maxTraces)
        {
            var tracesToRemove = _traces.Values
                .OrderBy(t => t.StartTime)
                .Take(_traces.Count - _maxTraces)
                .Select(t => t.TraceId)
                .ToList();

            foreach (var traceId in tracesToRemove)
            {
                _traces.TryRemove(traceId, out _);
                _traceStartTimes.TryRemove(traceId, out _);
            }
        }
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription?.Dispose();
        }
        _subscriptions.Clear();
    }
}

/// <summary>
/// Observer para eventos de ASP.NET Core
/// </summary>
internal class AspNetCoreDiagnosticObserver : IObserver<KeyValuePair<string, object?>>
{
    private readonly ApmService _apmService;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, string> _activityToTrace = new();

    public AspNetCoreDiagnosticObserver(ApmService apmService, ILogger logger)
    {
        _apmService = apmService;
        _logger = logger;
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        try
        {
            var eventName = value.Key;
            var payload = value.Value;

            switch (eventName)
            {
                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start":
                    HandleRequestStart(payload);
                    break;

                case "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop":
                    HandleRequestStop(payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando evento de diagnóstico: {EventName}", value.Key);
        }
    }

    private void HandleRequestStart(object? payload)
    {
        if (payload == null) return;

        try
        {
            var httpContext = GetProperty<object>(payload, "HttpContext");
            if (httpContext == null) return;

            var request = GetProperty<object>(httpContext, "Request");
            if (request == null) return;

            var method = GetProperty<object>(request, "Method")?.ToString() ?? "UNKNOWN";
            var pathObj = GetProperty<object>(request, "Path");
            var path = pathObj?.ToString() ?? "/";
            var operationName = $"{method} {path}";

            var activityId = Activity.Current?.Id ?? Guid.NewGuid().ToString();
            var traceId = _apmService.StartTrace(operationName, "IISMonitoring.Web");

            _activityToTrace[activityId] = traceId;

            // Añadir span raíz
            var scheme = GetProperty<object>(request, "Scheme")?.ToString() ?? "";
            var hostObj = GetProperty<object>(request, "Host");
            var host = hostObj?.ToString() ?? "";

            var span = new SpanInfo
            {
                SpanId = Guid.NewGuid().ToString("N"),
                TraceId = traceId,
                OperationName = operationName,
                SpanKind = "Server",
                ServiceName = "IISMonitoring.Web",
                StartTime = DateTime.UtcNow,
                Tags = new Dictionary<string, string>
                {
                    ["http.method"] = method,
                    ["http.path"] = path,
                    ["http.scheme"] = scheme,
                    ["http.host"] = host
                }
            };

            _apmService.AddSpan(traceId, span);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en HandleRequestStart");
        }
    }

    private void HandleRequestStop(object? payload)
    {
        if (payload == null) return;

        try
        {
            var activityId = Activity.Current?.Id ?? "";

            if (_activityToTrace.TryRemove(activityId, out var traceId))
            {
                var httpContext = GetProperty<object>(payload, "HttpContext");
                var response = httpContext != null ? GetProperty<object>(httpContext, "Response") : null;
                var statusCodeObj = response != null ? GetProperty<object>(response, "StatusCode") : null;
                var statusCode = statusCodeObj != null ? Convert.ToInt32(statusCodeObj) : 200;
                var status = statusCode >= 400 ? "Error" : "Success";
                var errorMessage = statusCode >= 400 ? $"HTTP {statusCode}" : null;

                _apmService.EndTrace(traceId, status, errorMessage);

                // Actualizar el span raíz con la duración
                var trace = _apmService.GetTrace(traceId);
                if (trace != null && trace.Spans.Any())
                {
                    var rootSpan = trace.Spans.First();
                    rootSpan.DurationMs = (DateTime.UtcNow - rootSpan.StartTime).TotalMilliseconds;
                    rootSpan.Status = status;
                    rootSpan.Tags["http.status_code"] = statusCode.ToString();
                    if (errorMessage != null)
                    {
                        rootSpan.ErrorMessage = errorMessage;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en HandleRequestStop");
        }
    }

    private T? GetProperty<T>(object obj, string propertyName)
    {
        try
        {
            var type = obj.GetType();
            var property = type.GetProperty(propertyName);
            return property != null ? (T?)property.GetValue(obj) : default;
        }
        catch
        {
            return default;
        }
    }

    public void OnCompleted() { }
    public void OnError(Exception error)
    {
        _logger.LogError(error, "Error en AspNetCoreDiagnosticObserver");
    }
}

/// <summary>
/// Observer para eventos de HttpClient
/// </summary>
internal class HttpClientDiagnosticObserver : IObserver<KeyValuePair<string, object?>>
{
    private readonly ApmService _apmService;
    private readonly ILogger _logger;

    public HttpClientDiagnosticObserver(ApmService apmService, ILogger logger)
    {
        _apmService = apmService;
        _logger = logger;
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        try
        {
            // Aquí se pueden capturar eventos de HttpClient si es necesario
            // Por ahora dejamos la estructura preparada
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando evento HttpClient: {EventName}", value.Key);
        }
    }

    public void OnCompleted() { }
    public void OnError(Exception error)
    {
        _logger.LogError(error, "Error en HttpClientDiagnosticObserver");
    }
}

/// <summary>
/// Observer para DiagnosticListener.AllListeners
/// </summary>
internal class DiagnosticListenerObserver : IObserver<DiagnosticListener>
{
    private readonly ApmService _apmService;
    private readonly ILogger _logger;
    private readonly List<IDisposable> _subscriptions = new();

    public DiagnosticListenerObserver(ApmService apmService, ILogger logger)
    {
        _apmService = apmService;
        _logger = logger;
    }

    public void OnNext(DiagnosticListener listener)
    {
        try
        {
            if (listener.Name == "Microsoft.AspNetCore")
            {
                var subscription = listener.Subscribe(new AspNetCoreDiagnosticObserver(_apmService, _logger));
                _subscriptions.Add(subscription);
            }
            else if (listener.Name == "HttpHandlerDiagnosticListener")
            {
                var subscription = listener.Subscribe(new HttpClientDiagnosticObserver(_apmService, _logger));
                _subscriptions.Add(subscription);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al subscribirse al listener: {ListenerName}", listener.Name);
        }
    }

    public void OnCompleted() { }

    public void OnError(Exception error)
    {
        _logger.LogError(error, "Error en DiagnosticListenerObserver");
    }
}
