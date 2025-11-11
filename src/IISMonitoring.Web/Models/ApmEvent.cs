namespace IISMonitoring.Web.Models;

/// <summary>
/// Representa un evento puntual que ocurrió durante la ejecución de un span
/// </summary>
public class ApmEvent
{
    /// <summary>
    /// Nombre del evento
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp cuando ocurrió el evento
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Atributos adicionales del evento
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();
}
