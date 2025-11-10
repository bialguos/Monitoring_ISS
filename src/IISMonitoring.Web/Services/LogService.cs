using IISMonitoring.Web.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace IISMonitoring.Web.Services;

/// <summary>
/// Servicio para gestionar y leer archivos de log de Serilog
/// </summary>
public class LogService : ILogService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LogService> _logger;

    // Patrón regex para parsear logs de Serilog
    // Formato: 2025-11-10 10:30:45.123 +00:00 [INF] Mensaje del log
    private static readonly Regex LogPattern = new(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}\s+[+-]\d{2}:\d{2})\s+\[(?<level>\w{3})\]\s+(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public LogService(IWebHostEnvironment environment, ILogger<LogService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LogFilesResponse> GetLogFilesAsync()
    {
        var response = new LogFilesResponse();
        var logDirectories = new HashSet<string>();

        try
        {
            // Buscar directorios "logs" desde el directorio raíz del contenido
            var contentRoot = _environment.ContentRootPath;
            var logsDir = Path.Combine(contentRoot, "logs");

            if (Directory.Exists(logsDir))
            {
                logDirectories.Add(logsDir);
                await ScanDirectoryForLogs(logsDir, response.LogFiles);
            }

            // Buscar recursivamente otros directorios llamados "logs"
            await ScanForLogDirectories(contentRoot, logDirectories, response.LogFiles);

            response.LogDirectories = logDirectories.OrderBy(d => d).ToList();
            response.TotalFiles = response.LogFiles.Count;
            response.TotalSizeBytes = response.LogFiles.Sum(f => f.SizeBytes);

            // Ordenar por fecha de modificación (más reciente primero)
            response.LogFiles = response.LogFiles.OrderByDescending(f => f.LastModified).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la lista de archivos de log");
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<LogContentResponse> GetLogContentAsync(
        string fileName,
        int skip = 0,
        int take = 100,
        string? level = null,
        string? search = null)
    {
        var response = new LogContentResponse
        {
            FileName = fileName
        };

        try
        {
            // Buscar el archivo en los directorios de logs
            var filePath = await FindLogFile(fileName);
            if (filePath == null)
            {
                _logger.LogWarning("Archivo de log no encontrado: {FileName}", fileName);
                return response;
            }

            // Leer todas las líneas del archivo
            var allLines = await File.ReadAllLinesAsync(filePath);
            response.TotalLines = allLines.Length;

            // Parsear las líneas
            var entries = ParseLogLines(allLines);

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(level))
            {
                entries = entries.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                entries = entries.Where(e =>
                    e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (e.Exception?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            // Aplicar paginación
            response.ReturnedLines = Math.Min(take, entries.Count - skip);
            response.HasMore = skip + take < entries.Count;
            response.Entries = entries.Skip(skip).Take(take).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al leer el contenido del archivo de log: {FileName}", fileName);
        }

        return response;
    }

    /// <summary>
    /// Escanea un directorio en busca de archivos .log
    /// </summary>
    private async Task ScanDirectoryForLogs(string directory, List<LogFileInfo> logFiles)
    {
        try
        {
            var files = Directory.GetFiles(directory, "*.log");

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var lineCount = await CountLines(file);

                logFiles.Add(new LogFileInfo
                {
                    FileName = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    SizeBytes = fileInfo.Length,
                    SizeFormatted = FormatFileSize(fileInfo.Length),
                    LastModified = fileInfo.LastWriteTime,
                    Directory = fileInfo.DirectoryName ?? string.Empty,
                    LineCount = lineCount
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al escanear el directorio: {Directory}", directory);
        }
    }

    /// <summary>
    /// Busca recursivamente directorios llamados "logs"
    /// </summary>
    private async Task ScanForLogDirectories(
        string rootDirectory,
        HashSet<string> logDirectories,
        List<LogFileInfo> logFiles,
        int maxDepth = 3,
        int currentDepth = 0)
    {
        if (currentDepth >= maxDepth)
            return;

        try
        {
            var directories = Directory.GetDirectories(rootDirectory);

            foreach (var dir in directories)
            {
                var dirName = new DirectoryInfo(dir).Name.ToLowerInvariant();

                // Si el directorio se llama "logs", agrégalo
                if (dirName == "logs" && !logDirectories.Contains(dir))
                {
                    logDirectories.Add(dir);
                    await ScanDirectoryForLogs(dir, logFiles);
                }

                // Continuar búsqueda recursiva (evitar directorios del sistema)
                if (dirName != "bin" && dirName != "obj" && dirName != "node_modules" &&
                    dirName != ".git" && dirName != ".vs")
                {
                    await ScanForLogDirectories(dir, logDirectories, logFiles, maxDepth, currentDepth + 1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar directorios de logs en: {RootDirectory}", rootDirectory);
        }
    }

    /// <summary>
    /// Busca un archivo de log por nombre
    /// </summary>
    private async Task<string?> FindLogFile(string fileName)
    {
        var contentRoot = _environment.ContentRootPath;
        var logsDir = Path.Combine(contentRoot, "logs");

        // Buscar en el directorio principal de logs
        var directPath = Path.Combine(logsDir, fileName);
        if (File.Exists(directPath))
            return directPath;

        // Buscar recursivamente
        var allLogFiles = new List<LogFileInfo>();
        var logDirectories = new HashSet<string>();
        await ScanForLogDirectories(contentRoot, logDirectories, allLogFiles);

        var foundFile = allLogFiles.FirstOrDefault(f => f.FileName == fileName);
        return foundFile?.FullPath;
    }

    /// <summary>
    /// Parsea las líneas de log utilizando el patrón de Serilog
    /// </summary>
    private List<LogEntry> ParseLogLines(string[] lines)
    {
        var entries = new List<LogEntry>();
        LogEntry? currentEntry = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var match = LogPattern.Match(line);

            if (match.Success)
            {
                // Si hay una entrada anterior, agrégala
                if (currentEntry != null)
                {
                    entries.Add(currentEntry);
                }

                // Crear nueva entrada
                currentEntry = new LogEntry
                {
                    Timestamp = DateTime.Parse(match.Groups["timestamp"].Value, CultureInfo.InvariantCulture),
                    Level = MapLogLevel(match.Groups["level"].Value),
                    Message = match.Groups["message"].Value.Trim(),
                    RawLine = line
                };
            }
            else if (currentEntry != null)
            {
                // Esta línea es parte de una excepción o mensaje multi-línea
                if (currentEntry.Exception == null && line.TrimStart().StartsWith("System."))
                {
                    currentEntry.Exception = line;
                }
                else if (currentEntry.Exception != null)
                {
                    currentEntry.Exception += Environment.NewLine + line;
                }
                else
                {
                    currentEntry.Message += Environment.NewLine + line;
                }
            }
        }

        // Agregar la última entrada
        if (currentEntry != null)
        {
            entries.Add(currentEntry);
        }

        return entries;
    }

    /// <summary>
    /// Mapea los niveles de log de Serilog (INF, WRN, ERR, etc.) a nombres completos
    /// </summary>
    private string MapLogLevel(string shortLevel)
    {
        return shortLevel.ToUpperInvariant() switch
        {
            "VRB" => "Verbose",
            "DBG" => "Debug",
            "INF" => "Information",
            "WRN" => "Warning",
            "ERR" => "Error",
            "FTL" => "Fatal",
            _ => shortLevel
        };
    }

    /// <summary>
    /// Cuenta las líneas de un archivo
    /// </summary>
    private async Task<int> CountLines(string filePath)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            return lines.Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Formatea el tamaño de archivo a una cadena legible
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
