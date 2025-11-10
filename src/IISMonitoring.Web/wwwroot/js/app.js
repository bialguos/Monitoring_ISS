// Variables globales para los gráficos
let cpuChart = null;
let memoryChart = null;
let connection = null;

// Variables para datos históricos
let historicalData = null;
let selectedPools = new Set();
let availablePools = [];
let chartUpdateInterval = null;

// Colores para las líneas de los pools (reutilizables)
const poolColors = [
    { border: 'rgb(37, 99, 235)', bg: 'rgba(37, 99, 235, 0.1)' },
    { border: 'rgb(16, 185, 129)', bg: 'rgba(16, 185, 129, 0.1)' },
    { border: 'rgb(245, 158, 11)', bg: 'rgba(245, 158, 11, 0.1)' },
    { border: 'rgb(239, 68, 68)', bg: 'rgba(239, 68, 68, 0.1)' },
    { border: 'rgb(139, 92, 246)', bg: 'rgba(139, 92, 246, 0.1)' },
    { border: 'rgb(236, 72, 153)', bg: 'rgba(236, 72, 153, 0.1)' },
    { border: 'rgb(6, 182, 212)', bg: 'rgba(6, 182, 212, 0.1)' },
    { border: 'rgb(251, 146, 60)', bg: 'rgba(251, 146, 60, 0.1)' },
    { border: 'rgb(34, 197, 94)', bg: 'rgba(34, 197, 94, 0.1)' },
    { border: 'rgb(168, 85, 247)', bg: 'rgba(168, 85, 247, 0.1)' }
];

// Inicializar la aplicación
document.addEventListener('DOMContentLoaded', () => {
    initializeCharts();
    initializeSignalR();
    loadInitialData();
    loadHistoricalData();

    // Actualizar gráficas cada 5 segundos
    chartUpdateInterval = setInterval(loadHistoricalData, 5000);
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
        loadHistoricalData();
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

// Cargar datos históricos
async function loadHistoricalData() {
    try {
        const response = await fetch('/api/monitoring/historical');
        if (response.ok) {
            historicalData = await response.json();

            // Si es la primera carga, inicializar el selector de pools
            if (availablePools.length === 0 && historicalData.availablePools.length > 0) {
                availablePools = historicalData.availablePools;
                initializePoolSelector();
            }

            // Actualizar gráficas
            updateHistoricalCharts();
        } else {
            console.error('Error al cargar datos históricos:', response.statusText);
        }
    } catch (error) {
        console.error('Error al cargar datos históricos:', error);
    }
}

// Inicializar selector de pools
function initializePoolSelector() {
    const selectorDiv = document.getElementById('poolSelector');

    if (availablePools.length === 0) {
        selectorDiv.innerHTML = '<span class="loading-text">No hay pools disponibles</span>';
        return;
    }

    // Seleccionar todos los pools por defecto
    selectedPools = new Set(availablePools);

    // Crear checkboxes para cada pool
    selectorDiv.innerHTML = availablePools.map((pool, index) => `
        <div class="pool-checkbox-wrapper selected" data-pool="${pool}">
            <input type="checkbox" id="pool-${index}" value="${pool}" checked onchange="togglePool('${pool}')">
            <label for="pool-${index}">${pool}</label>
        </div>
    `).join('');
}

// Toggle selección de pool
window.togglePool = function(poolName) {
    const wrapper = document.querySelector(`[data-pool="${poolName}"]`);

    if (selectedPools.has(poolName)) {
        selectedPools.delete(poolName);
        wrapper.classList.remove('selected');
    } else {
        selectedPools.add(poolName);
        wrapper.classList.add('selected');
    }

    updateHistoricalCharts();
};

// Seleccionar todos los pools
window.selectAllPools = function() {
    selectedPools = new Set(availablePools);

    document.querySelectorAll('.pool-checkbox-wrapper').forEach(wrapper => {
        wrapper.classList.add('selected');
        const checkbox = wrapper.querySelector('input[type="checkbox"]');
        checkbox.checked = true;
    });

    updateHistoricalCharts();
};

// Deseleccionar todos los pools
window.deselectAllPools = function() {
    selectedPools.clear();

    document.querySelectorAll('.pool-checkbox-wrapper').forEach(wrapper => {
        wrapper.classList.remove('selected');
        const checkbox = wrapper.querySelector('input[type="checkbox"]');
        checkbox.checked = false;
    });

    updateHistoricalCharts();
};

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
        tbody.innerHTML = '<tr><td colspan="8" class="loading">No hay datos disponibles</td></tr>';
        return;
    }

    tbody.innerHTML = appPools.map(pool => {
        const isStarted = pool.status.toLowerCase() === 'started';
        const actionButton = isStarted
            ? `<button class="action-btn btn-stop" onclick="controlAppPool('${pool.name}', 'stop')">⏹ Parar</button>`
            : `<button class="action-btn btn-start" onclick="controlAppPool('${pool.name}', 'start')">▶ Arrancar</button>`;

        return `
            <tr>
                <td><strong>${pool.name}</strong></td>
                <td><span class="status-badge status-${pool.status.toLowerCase()}">${pool.status}</span></td>
                <td><span class="status-badge ${pool.isEnabled ? 'status-enabled' : 'status-disabled'}">${pool.isEnabled ? 'Sí' : 'No'}</span></td>
                <td>${pool.cpuUsage.toFixed(2)}%</td>
                <td>${pool.memoryUsageMB.toLocaleString()} MB</td>
                <td>${pool.activeRequests}</td>
                <td>${pool.totalRequests.toLocaleString()}</td>
                <td>
                    <div class="action-buttons">
                        ${actionButton}
                    </div>
                </td>
            </tr>
        `;
    }).join('');
}

// Actualizar tabla de sitios web
function updateWebSitesTable(websites) {
    const tbody = document.querySelector('#webSitesTable tbody');

    if (!websites || websites.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" class="loading">No hay datos disponibles</td></tr>';
        return;
    }

    tbody.innerHTML = websites.map(site => {
        const isStarted = site.status.toLowerCase() === 'started';
        const actionButton = isStarted
            ? `<button class="action-btn btn-stop" onclick="controlWebSite('${site.name}', 'stop')">⏹ Parar</button>`
            : `<button class="action-btn btn-start" onclick="controlWebSite('${site.name}', 'start')">▶ Arrancar</button>`;

        const openButton = site.bindings && site.bindings.length > 0
            ? `<button class="action-btn btn-open" onclick="openWebSite('${site.bindings[0].replace(/'/g, "\\'")}')">🌐 Abrir</button>`
            : '';

        return `
            <tr>
                <td><strong>${site.name}</strong></td>
                <td><span class="status-badge status-${site.status.toLowerCase()}">${site.status}</span></td>
                <td>${site.id}</td>
                <td>${site.applicationPool}</td>
                <td><small>${site.bindings.join('<br>')}</small></td>
                <td>${site.requestsPerSecond.toLocaleString()}</td>
                <td>${site.currentConnections}</td>
                <td>
                    <div class="action-buttons">
                        ${actionButton}
                        ${openButton}
                    </div>
                </td>
            </tr>
        `;
    }).join('');
}

// Inicializar gráficos (como líneas temporales)
function initializeCharts() {
    const cpuCtx = document.getElementById('cpuChart').getContext('2d');
    const memoryCtx = document.getElementById('memoryChart').getContext('2d');

    const commonOptions = {
        responsive: true,
        maintainAspectRatio: true,
        interaction: {
            mode: 'index',
            intersect: false,
        },
        plugins: {
            legend: {
                display: true,
                position: 'top',
            },
            tooltip: {
                mode: 'index',
                intersect: false,
            }
        },
        scales: {
            x: {
                type: 'time',
                time: {
                    unit: 'minute',
                    displayFormats: {
                        minute: 'HH:mm'
                    }
                },
                title: {
                    display: true,
                    text: 'Tiempo'
                }
            },
            y: {
                beginAtZero: true
            }
        }
    };

    cpuChart = new Chart(cpuCtx, {
        type: 'line',
        data: {
            datasets: []
        },
        options: {
            ...commonOptions,
            scales: {
                ...commonOptions.scales,
                y: {
                    ...commonOptions.scales.y,
                    max: 100,
                    title: {
                        display: true,
                        text: 'CPU (%)'
                    }
                }
            }
        }
    });

    memoryChart = new Chart(memoryCtx, {
        type: 'line',
        data: {
            datasets: []
        },
        options: {
            ...commonOptions,
            scales: {
                ...commonOptions.scales,
                y: {
                    ...commonOptions.scales.y,
                    title: {
                        display: true,
                        text: 'Memoria (MB)'
                    }
                }
            }
        }
    });
}

// Actualizar gráficas con datos históricos
function updateHistoricalCharts() {
    if (!historicalData || !historicalData.poolData) return;

    // Preparar datasets para CPU
    const cpuDatasets = [];
    const memoryDatasets = [];

    let colorIndex = 0;
    Array.from(selectedPools).sort().forEach(poolName => {
        const poolData = historicalData.poolData[poolName];
        if (!poolData || poolData.length === 0) return;

        const color = poolColors[colorIndex % poolColors.length];
        colorIndex++;

        // Dataset para CPU
        cpuDatasets.push({
            label: poolName,
            data: poolData.map(point => ({
                x: new Date(point.timestamp),
                y: point.cpuUsage
            })),
            borderColor: color.border,
            backgroundColor: color.bg,
            borderWidth: 2,
            tension: 0.4,
            fill: true
        });

        // Dataset para Memoria
        memoryDatasets.push({
            label: poolName,
            data: poolData.map(point => ({
                x: new Date(point.timestamp),
                y: point.memoryUsageMB
            })),
            borderColor: color.border,
            backgroundColor: color.bg,
            borderWidth: 2,
            tension: 0.4,
            fill: true
        });
    });

    // Actualizar gráfica de CPU
    cpuChart.data.datasets = cpuDatasets;
    cpuChart.update('none');

    // Actualizar gráfica de Memoria
    memoryChart.data.datasets = memoryDatasets;
    memoryChart.update('none');
}

// Función para refrescar manualmente (opcional)
window.refreshDashboard = function() {
    loadInitialData();
    loadHistoricalData();
};

// Función para cambiar entre pestañas
window.switchTab = function(tabName) {
    const tabButtons = document.querySelectorAll('.tab-button');
    const tabContents = document.querySelectorAll('.tab-content');

    tabButtons.forEach(button => button.classList.remove('active'));
    tabContents.forEach(content => content.classList.remove('active'));

    const activeButton = Array.from(tabButtons).find(btn =>
        btn.textContent.includes(tabName === 'appPools' ? 'Application Pools' : 'Sitios Web')
    );
    const activeContent = document.getElementById(tabName + 'Tab');

    if (activeButton) activeButton.classList.add('active');
    if (activeContent) activeContent.classList.add('active');
};

// Función para controlar Application Pool (start/stop)
window.controlAppPool = async function(poolName, action) {
    const button = event.target;
    button.disabled = true;
    button.classList.add('btn-loading');

    try {
        const response = await fetch(`/api/monitoring/applicationpools/${encodeURIComponent(poolName)}/${action}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        const result = await response.json();

        if (response.ok && result.success) {
            console.log(`Application Pool ${poolName} ${action === 'start' ? 'iniciado' : 'detenido'} correctamente`);
            setTimeout(() => loadInitialData(), 1000);
        } else {
            console.error(`Error al ${action === 'start' ? 'iniciar' : 'detener'} Application Pool:`, result.message);
            alert(`Error: ${result.message || 'No se pudo completar la operación'}`);
            button.disabled = false;
            button.classList.remove('btn-loading');
        }
    } catch (error) {
        console.error(`Error al ${action === 'start' ? 'iniciar' : 'detener'} Application Pool:`, error);
        alert(`Error de conexión: ${error.message}`);
        button.disabled = false;
        button.classList.remove('btn-loading');
    }
};

// Función para controlar Sitio Web (start/stop)
window.controlWebSite = async function(siteName, action) {
    const button = event.target;
    button.disabled = true;
    button.classList.add('btn-loading');

    try {
        const response = await fetch(`/api/monitoring/websites/${encodeURIComponent(siteName)}/${action}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        const result = await response.json();

        if (response.ok && result.success) {
            console.log(`Sitio Web ${siteName} ${action === 'start' ? 'iniciado' : 'detenido'} correctamente`);
            setTimeout(() => loadInitialData(), 1000);
        } else {
            console.error(`Error al ${action === 'start' ? 'iniciar' : 'detener'} Sitio Web:`, result.message);
            alert(`Error: ${result.message || 'No se pudo completar la operación'}`);
            button.disabled = false;
            button.classList.remove('btn-loading');
        }
    } catch (error) {
        console.error(`Error al ${action === 'start' ? 'iniciar' : 'detener'} Sitio Web:`, error);
        alert(`Error de conexión: ${error.message}`);
        button.disabled = false;
        button.classList.remove('btn-loading');
    }
};

// Función para abrir sitio web en nueva pestaña
window.openWebSite = function(binding) {
    try {
        let url = binding;

        if (url.includes('*')) {
            url = url.replace('*', 'localhost');
        }

        if (url.includes(':/:') || url.includes(':///:')) {
            url = url.replace(':///', '://localhost/').replace('://', '://localhost:');
        }

        if (!url.startsWith('http://') && !url.startsWith('https://')) {
            url = 'http://' + url;
        }

        window.open(url, '_blank');
        console.log(`Abriendo sitio web: ${url}`);
    } catch (error) {
        console.error('Error al abrir sitio web:', error);
        alert(`Error al abrir el sitio web: ${error.message}`);
    }
};
