using IISMonitoring.Web.Models;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Interfaz del servicio de Application Performance Monitoring (APM)
/// </summary>
public interface IApmService
{
    /// <summary>
    /// Obtiene el dashboard completo de APM con traces recientes y estadísticas
    /// </summary>
    ApmDashboard GetDashboard();

    /// <summary>
    /// Obtiene todos los traces capturados
    /// </summary>
    /// <param name="limit">Límite de traces a devolver</param>
    /// <param name="status">Filtro por estado (Success, Error, null para todos)</param>
    List<TraceInfo> GetTraces(int limit = 100, string? status = null);

    /// <summary>
    /// Obtiene un trace específico por su ID
    /// </summary>
    TraceInfo? GetTrace(string traceId);

    /// <summary>
    /// Obtiene estadísticas de APM
    /// </summary>
    ApmStatistics GetStatistics();

    /// <summary>
    /// Limpia todos los traces capturados
    /// </summary>
    void ClearTraces();

    /// <summary>
    /// Inicia manualmente un trace personalizado
    /// </summary>
    string StartTrace(string operationName, string serviceName);

    /// <summary>
    /// Finaliza manualmente un trace personalizado
    /// </summary>
    void EndTrace(string traceId, string status = "Success", string? errorMessage = null);

    /// <summary>
    /// Añade un span a un trace existente
    /// </summary>
    void AddSpan(string traceId, SpanInfo span);
}
