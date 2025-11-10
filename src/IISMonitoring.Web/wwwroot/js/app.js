// Variables globales para los gráficos
let cpuChart = null;
let memoryChart = null;
let connection = null;

// Inicializar la aplicación
document.addEventListener('DOMContentLoaded', () => {
    initializeCharts();
    initializeSignalR();
    loadInitialData();
});

// Inicializar conexión SignalR
async function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/monitoringHub")
        .withAutomaticReconnect()
        .build();

    connection.on("UpdateDashboard", (data) => {
        updateDashboard(data);
    });

    connection.onreconnecting(() => {
        updateConnectionStatus('Reconectando...', 'status-connecting');
    });

    connection.onreconnected(() => {
        updateConnectionStatus('Conectado', 'status-connected');
        loadInitialData();
    });

    connection.onclose(() => {
        updateConnectionStatus('Desconectado', 'status-disconnected');
    });

    try {
        await connection.start();
        updateConnectionStatus('Conectado', 'status-connected');
        console.log('SignalR conectado correctamente');
    } catch (err) {
        console.error('Error al conectar SignalR:', err);
        updateConnectionStatus('Error de conexión', 'status-disconnected');
        // Reintentar después de 5 segundos
        setTimeout(() => initializeSignalR(), 5000);
    }
}

// Actualizar estado de conexión
function updateConnectionStatus(text, className) {
    const statusElement = document.getElementById('connectionStatus');
    statusElement.textContent = text;
    statusElement.className = className;
}

// Cargar datos iniciales
async function loadInitialData() {
    try {
        const response = await fetch('/api/monitoring/dashboard');
        if (response.ok) {
            const data = await response.json();
            updateDashboard(data);
        } else {
            console.error('Error al cargar datos:', response.statusText);
        }
    } catch (error) {
        console.error('Error al cargar datos:', error);
    }
}

// Actualizar dashboard completo
function updateDashboard(data) {
    if (!data) return;

    // Actualizar timestamp
    const now = new Date();
    document.getElementById('lastUpdate').textContent = now.toLocaleTimeString('es-ES');

    // Actualizar métricas generales
    if (data.performanceMetrics) {
        updateMetrics(data.performanceMetrics);
    }

    // Actualizar application pools
    if (data.applicationPools) {
        updateApplicationPoolsTable(data.applicationPools);
        updateCharts(data.applicationPools);
    }

    // Actualizar sitios web
    if (data.webSites) {
        updateWebSitesTable(data.webSites);
    }
}

// Actualizar métricas generales
function updateMetrics(metrics) {
    document.getElementById('totalCpu').textContent = metrics.totalCpuUsage.toFixed(2);
    document.getElementById('totalMemory').textContent = metrics.totalMemoryUsageMB.toLocaleString();
    document.getElementById('totalAppPools').textContent = metrics.totalApplicationPools;
    document.getElementById('runningAppPools').textContent = metrics.runningApplicationPools;
    document.getElementById('totalWebSites').textContent = metrics.totalWebSites;
    document.getElementById('runningWebSites').textContent = metrics.runningWebSites;
}

// Actualizar tabla de Application Pools
function updateApplicationPoolsTable(appPools) {
    const tbody = document.querySelector('#appPoolsTable tbody');

    if (!appPools || appPools.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="loading">No hay datos disponibles</td></tr>';
        return;
    }

    tbody.innerHTML = appPools.map(pool => `
        <tr>
            <td><strong>${pool.name}</strong></td>
            <td><span class="status-badge status-${pool.status.toLowerCase()}">${pool.status}</span></td>
            <td><span class="status-badge ${pool.isEnabled ? 'status-enabled' : 'status-disabled'}">${pool.isEnabled ? 'Sí' : 'No'}</span></td>
            <td>${pool.cpuUsage.toFixed(2)}%</td>
            <td>${pool.memoryUsageMB.toLocaleString()} MB</td>
            <td>${pool.activeRequests}</td>
            <td>${pool.totalRequests.toLocaleString()}</td>
        </tr>
    `).join('');
}

// Actualizar tabla de sitios web
function updateWebSitesTable(websites) {
    const tbody = document.querySelector('#webSitesTable tbody');

    if (!websites || websites.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="loading">No hay datos disponibles</td></tr>';
        return;
    }

    tbody.innerHTML = websites.map(site => `
        <tr>
            <td><strong>${site.name}</strong></td>
            <td><span class="status-badge status-${site.status.toLowerCase()}">${site.status}</span></td>
            <td>${site.id}</td>
            <td>${site.applicationPool}</td>
            <td><small>${site.bindings.join('<br>')}</small></td>
            <td>${site.requestsPerSecond.toLocaleString()}</td>
            <td>${site.currentConnections}</td>
        </tr>
    `).join('');
}

// Inicializar gráficos
function initializeCharts() {
    const cpuCtx = document.getElementById('cpuChart').getContext('2d');
    const memoryCtx = document.getElementById('memoryChart').getContext('2d');

    cpuChart = new Chart(cpuCtx, {
        type: 'bar',
        data: {
            labels: [],
            datasets: [{
                label: 'CPU %',
                data: [],
                backgroundColor: 'rgba(37, 99, 235, 0.8)',
                borderColor: 'rgba(37, 99, 235, 1)',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            scales: {
                y: {
                    beginAtZero: true,
                    max: 100,
                    ticks: {
                        callback: function(value) {
                            return value + '%';
                        }
                    }
                }
            },
            plugins: {
                legend: {
                    display: false
                }
            }
        }
    });

    memoryChart = new Chart(memoryCtx, {
        type: 'bar',
        data: {
            labels: [],
            datasets: [{
                label: 'Memoria (MB)',
                data: [],
                backgroundColor: 'rgba(16, 185, 129, 0.8)',
                borderColor: 'rgba(16, 185, 129, 1)',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: function(value) {
                            return value + ' MB';
                        }
                    }
                }
            },
            plugins: {
                legend: {
                    display: false
                }
            }
        }
    });
}

// Actualizar gráficos
function updateCharts(appPools) {
    if (!appPools || appPools.length === 0) return;

    // Filtrar solo los app pools activos para los gráficos
    const activeAppPools = appPools.filter(pool => pool.isEnabled);

    // Actualizar gráfico de CPU
    cpuChart.data.labels = activeAppPools.map(pool => pool.name);
    cpuChart.data.datasets[0].data = activeAppPools.map(pool => pool.cpuUsage);
    cpuChart.update('none'); // 'none' para actualización sin animación

    // Actualizar gráfico de memoria
    memoryChart.data.labels = activeAppPools.map(pool => pool.name);
    memoryChart.data.datasets[0].data = activeAppPools.map(pool => pool.memoryUsageMB);
    memoryChart.update('none');
}

// Función para refrescar manualmente (opcional)
window.refreshDashboard = function() {
    loadInitialData();
};
