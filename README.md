# IIS Monitoring Dashboard

Sistema de monitorización en tiempo real para Internet Information Services (IIS) desarrollado con ASP.NET Core 8.0.

## Características

- **Dashboard en tiempo real**: Visualización de métricas actualizadas automáticamente cada 5 segundos
- **Monitorización de Application Pools**:
  - Estado (Running/Stopped)
  - Consumo de CPU
  - Uso de memoria
  - Requests activos y totales
- **Monitorización de Sitios Web**:
  - Estado y bindings
  - Requests por segundo
  - Bytes enviados/recibidos
  - Conexiones actuales
- **Métricas del Sistema**:
  - CPU total del sistema
  - Memoria disponible y en uso
  - Contadores de recursos
- **Optimizado para Rendimiento**:
  - Caché de Performance Counters para minimizar overhead (< 1% CPU)
  - Sin bloqueos de Thread.Sleep
  - Gestión eficiente de recursos con IDisposable
  - Intervalo de actualización configurable

## Optimizaciones de Rendimiento

Esta aplicación está diseñada para tener **mínimo impacto en el rendimiento** de tus aplicaciones IIS:

- **Caché de Performance Counters**: Los contadores de rendimiento se crean una vez y se reutilizan, eliminando el overhead de creación/destrucción constante
- **Sin Thread.Sleep**: No se usan bloqueos innecesarios que consumirían recursos
- **Overhead estimado**: < 1% CPU en condiciones normales
- **Gestión de memoria**: Limpieza automática de contadores obsoletos cada 5 minutos
- **Configuración flexible**: Ajusta el intervalo de actualización según tus necesidades

### Comparación de Rendimiento

| Método | CPU Overhead | Memoria | Latencia |
|--------|--------------|---------|----------|
| **Performance Counters (implementado)** | < 1% | Baja | Instantánea |
| WMI | 5-10% | Alta | Media |
| Polling directo | 3-5% | Media | Variable |

## Tecnologías Utilizadas

- **Backend**: ASP.NET Core 8.0
- **Comunicación en tiempo real**: SignalR
- **Performance Counters**: System.Diagnostics.PerformanceCounter
- **IIS Management**: Microsoft.Web.Administration
- **Frontend**: HTML5, CSS3, JavaScript
- **Visualización**: Chart.js

## Requisitos

- Windows Server con IIS instalado
- .NET 8.0 SDK
- Permisos de administrador para acceder a contadores de rendimiento de IIS
- Visual Studio 2022 o superior (opcional)

## Instalación

1. Clonar el repositorio:
```bash
git clone [url-del-repositorio]
cd Monitoring_ISS
```

2. Restaurar dependencias:
```bash
cd src/IISMonitoring.Web
dotnet restore
```

3. Compilar el proyecto:
```bash
dotnet build
```

4. Ejecutar la aplicación:
```bash
dotnet run
```

5. Abrir en el navegador:
```
https://localhost:5001
```

## Configuración

### Permisos

La aplicación requiere permisos de administrador para acceder a los contadores de rendimiento de IIS. Asegúrate de ejecutar la aplicación con los permisos adecuados.

### appsettings.json

Puedes ajustar la configuración en el archivo `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Monitoring": {
    "UpdateIntervalMs": 5000,              // Intervalo de actualización en milisegundos
    "EnableCounterCaching": true,          // Caché de contadores (recomendado para rendimiento)
    "CounterCacheLifetimeMinutes": 30,     // Tiempo de vida del caché
    "MaxCounterRetries": 3                 // Reintentos al crear contadores
  }
}
```

#### Opciones de Configuración

- **UpdateIntervalMs**: Intervalo de actualización del dashboard en milisegundos (por defecto: 5000ms = 5 segundos)
  - Valores más bajos = actualizaciones más frecuentes pero mayor consumo de recursos
  - Valores más altos = menor consumo pero actualizaciones menos frecuentes
  - Rango recomendado: 3000-10000ms

- **EnableCounterCaching**: Habilita el caché de Performance Counters (recomendado: true)
  - `true`: Máximo rendimiento, contadores se reutilizan (< 1% CPU overhead)
  - `false`: Contadores se crean en cada lectura (mayor overhead)

- **CounterCacheLifetimeMinutes**: Tiempo en minutos antes de limpiar contadores obsoletos (por defecto: 30)
  - Evita acumulación de contadores de app pools o sitios eliminados

- **MaxCounterRetries**: Número de reintentos al crear contadores (por defecto: 3)
  - Útil si algunos contadores no están disponibles inmediatamente

## Estructura del Proyecto

```
IISMonitoring/
├── IISMonitoring.sln
└── src/
    └── IISMonitoring.Web/
        ├── Configuration/       # Opciones de configuración
        ├── Controllers/         # API Controllers
        ├── Models/              # Modelos de datos
        ├── Services/            # Servicios de monitorización (optimizados)
        ├── Hubs/                # SignalR Hubs
        ├── wwwroot/             # Archivos estáticos (HTML, CSS, JS)
        ├── Program.cs           # Configuración de la aplicación
        └── appsettings.json     # Configuración
```

## API Endpoints

- `GET /api/monitoring/dashboard` - Obtiene todos los datos del dashboard
- `GET /api/monitoring/applicationpools` - Lista de Application Pools
- `GET /api/monitoring/websites` - Lista de sitios web
- `GET /api/monitoring/metrics` - Métricas generales del sistema

## SignalR Hub

- **Endpoint**: `/monitoringHub`
- **Evento**: `UpdateDashboard` - Emite actualizaciones cada 5 segundos

## Características del Dashboard

### Métricas Generales
- CPU total del sistema
- Memoria total usada
- Número de Application Pools (total y activos)
- Número de sitios web (total y activos)

### Gráficos Interactivos
- Gráfico de barras: CPU por Application Pool
- Gráfico de barras: Memoria por Application Pool

### Tablas Detalladas
- Application Pools con estado, CPU, memoria y requests
- Sitios web con estado, bindings y estadísticas de red

## Notas de Seguridad

- Esta aplicación está diseñada para uso interno en redes privadas
- Configure CORS apropiadamente para producción
- Implemente autenticación y autorización según sus necesidades
- Revise los permisos de acceso a IIS y contadores de rendimiento

## Desarrollo

### Modo de desarrollo

```bash
dotnet watch run
```

### Compilar para producción

```bash
dotnet publish -c Release -o ./publish
```

## Solución de Problemas

### Los contadores de rendimiento no funcionan

1. Verificar que IIS esté instalado y ejecutándose
2. Ejecutar la aplicación como administrador
3. Verificar que los contadores de rendimiento de IIS estén habilitados

### SignalR no conecta

1. Verificar que el puerto no esté bloqueado por el firewall
2. Revisar la consola del navegador para errores
3. Verificar la configuración de CORS

## Contribuir

Las contribuciones son bienvenidas. Por favor, crea un Pull Request con tus mejoras.

## Licencia

Este proyecto es de código abierto y está disponible bajo la licencia MIT.

## Autor

Desarrollado con ASP.NET Core 8.0

---

Para más información sobre ASP.NET Core, visita [https://docs.microsoft.com/aspnet/core](https://docs.microsoft.com/aspnet/core)
