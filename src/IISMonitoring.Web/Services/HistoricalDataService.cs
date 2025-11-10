using System.Text.Json;
using IISMonitoring.Web.Configuration;
using IISMonitoring.Web.Models;
using Microsoft.Extensions.Options;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Servicio para gestionar el almacenamiento y recuperación de datos históricos
/// </summary>
public class HistoricalDataService : IDisposable
{
    private readonly MonitoringOptions _options;
    private readonly ILogger<HistoricalDataService> _logger;
    private readonly string _dataFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly Timer _cleanupTimer;

    public HistoricalDataService(
        IOptions<MonitoringOptions> options,
        ILogger<HistoricalDataService> logger,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _logger = logger;

        // Construir la ruta completa del archivo
        _dataFilePath = Path.Combine(environment.ContentRootPath, _options.HistoricalDataPath);

        // Asegurar que el directorio existe
        var directory = Path.GetDirectoryName(_dataFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Directorio de datos históricos creado: {Directory}", directory);
        }

        // Inicializar archivo si no existe
        if (!File.Exists(_dataFilePath))
        {
            File.WriteAllText(_dataFilePath, "[]");
            _logger.LogInformation("Archivo de datos históricos inicializado: {FilePath}", _dataFilePath);
        }

        // Configurar timer para limpieza automática cada minuto
        _cleanupTimer = new Timer(
            callback: async _ => await CleanupOldDataAsync(),
            state: null,
            dueTime: TimeSpan.FromMinutes(1),
            period: TimeSpan.FromMinutes(1));

        _logger.LogInformation(
            "Servicio de datos históricos inicializado (Retención: {RetentionMinutes} minutos, Ruta: {FilePath})",
            _options.HistoricalDataRetentionMinutes,
            _dataFilePath);
    }

    /// <summary>
    /// Añade datos históricos de application pools
    /// </summary>
    public async Task AddDataPointsAsync(IEnumerable<ApplicationPoolInfo> appPools)
    {
        if (!_options.EnableHistoricalData)
        {
            return;
        }

        await _fileLock.WaitAsync();
        try
        {
            var timestamp = DateTime.UtcNow;
            var dataPoints = appPools.Select(pool => new HistoricalDataPoint
            {
                Timestamp = timestamp,
                PoolName = pool.Name,
                CpuUsage = pool.CpuUsage,
                MemoryUsageMB = pool.MemoryUsageMB,
                Status = pool.Status,
                ActiveRequests = pool.ActiveRequests
            }).ToList();

            // Leer datos existentes
            var existingData = await ReadDataAsync();

            // Añadir nuevos datos
            existingData.AddRange(dataPoints);

            // Guardar en archivo
            await WriteDataAsync(existingData);

            _logger.LogDebug("Añadidos {Count} puntos de datos históricos", dataPoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al añadir datos históricos");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Obtiene los datos históricos
    /// </summary>
    public async Task<HistoricalDataResponse> GetHistoricalDataAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var allData = await ReadDataAsync();

            // Agrupar por pool
            var groupedData = allData
                .GroupBy(d => d.PoolName)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(d => d.Timestamp).ToList()
                );

            var availablePools = groupedData.Keys.OrderBy(p => p).ToList();

            return new HistoricalDataResponse
            {
                PoolData = groupedData,
                AvailablePools = availablePools,
                RetentionMinutes = _options.HistoricalDataRetentionMinutes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer datos históricos");
            return new HistoricalDataResponse
            {
                RetentionMinutes = _options.HistoricalDataRetentionMinutes
            };
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Limpia datos antiguos según el tiempo de retención configurado
    /// </summary>
    private async Task CleanupOldDataAsync()
    {
        if (!_options.EnableHistoricalData)
        {
            return;
        }

        await _fileLock.WaitAsync();
        try
        {
            var allData = await ReadDataAsync();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-_options.HistoricalDataRetentionMinutes);

            var originalCount = allData.Count;
            var filteredData = allData.Where(d => d.Timestamp >= cutoffTime).ToList();
            var removedCount = originalCount - filteredData.Count;

            if (removedCount > 0)
            {
                await WriteDataAsync(filteredData);
                _logger.LogInformation(
                    "Limpieza de datos históricos completada: {RemovedCount} registros eliminados, {RemainingCount} registros restantes",
                    removedCount,
                    filteredData.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar datos históricos antiguos");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Lee los datos del archivo
    /// </summary>
    private async Task<List<HistoricalDataPoint>> ReadDataAsync()
    {
        try
        {
            if (!File.Exists(_dataFilePath))
            {
                return new List<HistoricalDataPoint>();
            }

            var json = await File.ReadAllTextAsync(_dataFilePath);
            var data = JsonSerializer.Deserialize<List<HistoricalDataPoint>>(json);
            return data ?? new List<HistoricalDataPoint>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error al deserializar datos históricos, reiniciando archivo");
            await File.WriteAllTextAsync(_dataFilePath, "[]");
            return new List<HistoricalDataPoint>();
        }
    }

    /// <summary>
    /// Escribe los datos al archivo
    /// </summary>
    private async Task WriteDataAsync(List<HistoricalDataPoint> data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = false // Sin formato para reducir tamaño
        });

        await File.WriteAllTextAsync(_dataFilePath, json);
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _fileLock?.Dispose();
    }
}
