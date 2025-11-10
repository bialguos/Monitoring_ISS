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
    initializePoolSelector();
    initializeSortableHeaders();
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

// Inicializar selector de pools (Searchable Multi-Select)
function initializePoolSelector() {
    const tagsContainer = document.getElementById('selectedPoolsTags');
    const searchInput = document.getElementById('poolSearchInput');
    const dropdown = document.getElementById('poolDropdown');

    if (availablePools.length === 0) {
        tagsContainer.innerHTML = '<span class="loading-text">No hay pools disponibles</span>';
        return;
    }

    // Seleccionar todos los pools por defecto
    selectedPools = new Set(availablePools);

    // Renderizar tags iniciales
    renderSelectedTags();

    // Event listener para el input de búsqueda
    searchInput.addEventListener('focus', () => {
        renderDropdownOptions('');
        dropdown.style.display = 'block';
    });

    searchInput.addEventListener('input', (e) => {
        renderDropdownOptions(e.target.value);
    });

    // Cerrar dropdown al hacer clic fuera
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.multiselect-search-wrapper')) {
            dropdown.style.display = 'none';
            searchInput.value = '';
        }
    });
}

// Renderizar los chips/tags de pools seleccionados
function renderSelectedTags() {
    const tagsContainer = document.getElementById('selectedPoolsTags');

    if (selectedPools.size === 0) {
        tagsContainer.innerHTML = '<span class="loading-text">No hay pools seleccionados</span>';
        return;
    }

    tagsContainer.innerHTML = Array.from(selectedPools)
        .sort()
        .map(pool => `
            <div class="multiselect-tag">
                <span>${pool}</span>
                <button class="multiselect-tag-remove" onclick="removePoolTag('${pool}')" title="Eliminar">×</button>
            </div>
        `).join('');
}

// Renderizar opciones del dropdown con búsqueda
function renderDropdownOptions(searchTerm) {
    const optionsContainer = document.getElementById('poolOptions');
    const filteredPools = availablePools.filter(pool =>
        pool.toLowerCase().includes(searchTerm.toLowerCase())
    );

    if (filteredPools.length === 0) {
        optionsContainer.innerHTML = '<div class="multiselect-no-results">No se encontraron pools</div>';
        return;
    }

    optionsContainer.innerHTML = filteredPools
        .sort()
        .map(pool => {
            const isSelected = selectedPools.has(pool);
            return `
                <div class="multiselect-option ${isSelected ? 'selected' : ''}" onclick="togglePool('${pool}')">
                    <input type="checkbox" class="multiselect-option-checkbox" ${isSelected ? 'checked' : ''} onchange="togglePool('${pool}')">
                    <span>${pool}</span>
                </div>
            `;
        }).join('');
}

// Toggle selección de pool
window.togglePool = function(poolName) {
    if (selectedPools.has(poolName)) {
        selectedPools.delete(poolName);
    } else {
        selectedPools.add(poolName);
    }

    renderSelectedTags();
    renderDropdownOptions(document.getElementById('poolSearchInput').value);
    updateHistoricalCharts();
};

// Remover un pool específico desde el tag
window.removePoolTag = function(poolName) {
    selectedPools.delete(poolName);
    renderSelectedTags();
    renderDropdownOptions(document.getElementById('poolSearchInput').value);
    updateHistoricalCharts();
};

// Seleccionar todos los pools
window.selectAllPools = function() {
    selectedPools = new Set(availablePools);
    renderSelectedTags();
    renderDropdownOptions(document.getElementById('poolSearchInput').value);
    updateHistoricalCharts();
};

// Deseleccionar todos los pools
window.deselectAllPools = function() {
    selectedPools.clear();
    renderSelectedTags();
    renderDropdownOptions(document.getElementById('poolSearchInput').value);
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
        currentAppPoolsData = [];
        return;
    }

    // Store data for sorting (only if not already sorted)
    if (appPoolsSortState.column === null) {
        currentAppPoolsData = appPools;
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
        currentWebSitesData = [];
        return;
    }

    // Store data for sorting (only if not already sorted)
    if (webSitesSortState.column === null) {
        currentWebSitesData = websites;
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

// ==================== SORTING FUNCTIONALITY ====================

// Global state for table sorting
let currentAppPoolsData = [];
let currentWebSitesData = [];
let appPoolsSortState = { column: null, direction: null };
let webSitesSortState = { column: null, direction: null };

// Initialize sortable table headers
function initializeSortableHeaders() {
    // Application Pools table headers
    const appPoolsHeaders = document.querySelectorAll('#appPoolsTable thead th');
    appPoolsHeaders.forEach((header, index) => {
        // Skip the last column (Acciones)
        if (index < appPoolsHeaders.length - 1) {
            header.classList.add('sortable');
            header.addEventListener('click', () => sortAppPoolsTable(index));
        }
    });

    // Web Sites table headers
    const webSitesHeaders = document.querySelectorAll('#webSitesTable thead th');
    webSitesHeaders.forEach((header, index) => {
        // Skip the last column (Acciones)
        if (index < webSitesHeaders.length - 1) {
            header.classList.add('sortable');
            header.addEventListener('click', () => sortWebSitesTable(index));
        }
    });
}

// Generic sorting function
function sortData(data, columnIndex, currentDirection, getValueFn) {
    const direction = currentDirection === 'asc' ? 'desc' : 'asc';

    const sortedData = [...data].sort((a, b) => {
        const valueA = getValueFn(a, columnIndex);
        const valueB = getValueFn(b, columnIndex);

        // Handle numeric values
        if (!isNaN(valueA) && !isNaN(valueB)) {
            return direction === 'asc' ? valueA - valueB : valueB - valueA;
        }

        // Handle string values
        const strA = String(valueA).toLowerCase();
        const strB = String(valueB).toLowerCase();

        if (direction === 'asc') {
            return strA.localeCompare(strB);
        } else {
            return strB.localeCompare(strA);
        }
    });

    return { sortedData, direction };
}

// Update sort indicators
function updateSortIndicators(tableId, columnIndex, direction) {
    const headers = document.querySelectorAll(`#${tableId} thead th`);
    headers.forEach((header, index) => {
        header.classList.remove('sort-asc', 'sort-desc');
        if (index === columnIndex) {
            header.classList.add(`sort-${direction}`);
        }
    });
}

// Extract value from Application Pool for sorting
function getAppPoolValue(pool, columnIndex) {
    switch (columnIndex) {
        case 0: return pool.name;
        case 1: return pool.status;
        case 2: return pool.isEnabled ? 1 : 0;
        case 3: return pool.cpuUsage;
        case 4: return pool.memoryUsageMB;
        case 5: return pool.activeRequests;
        case 6: return pool.totalRequests;
        default: return '';
    }
}

// Extract value from Web Site for sorting
function getWebSiteValue(site, columnIndex) {
    switch (columnIndex) {
        case 0: return site.name;
        case 1: return site.status;
        case 2: return site.id;
        case 3: return site.applicationPool;
        case 4: return site.bindings.join(',');
        case 5: return site.requestsPerSecond;
        case 6: return site.currentConnections;
        default: return '';
    }
}

// Sort Application Pools table
function sortAppPoolsTable(columnIndex) {
    if (currentAppPoolsData.length === 0) return;

    const currentDirection = appPoolsSortState.column === columnIndex ? appPoolsSortState.direction : null;
    const { sortedData, direction } = sortData(currentAppPoolsData, columnIndex, currentDirection, getAppPoolValue);

    appPoolsSortState = { column: columnIndex, direction };
    currentAppPoolsData = sortedData;

    updateApplicationPoolsTable(sortedData);
    updateSortIndicators('appPoolsTable', columnIndex, direction);
}

// Sort Web Sites table
function sortWebSitesTable(columnIndex) {
    if (currentWebSitesData.length === 0) return;

    const currentDirection = webSitesSortState.column === columnIndex ? webSitesSortState.direction : null;
    const { sortedData, direction } = sortData(currentWebSitesData, columnIndex, currentDirection, getWebSiteValue);

    webSitesSortState = { column: columnIndex, direction };
    currentWebSitesData = sortedData;

    updateWebSitesTable(sortedData);
    updateSortIndicators('webSitesTable', columnIndex, direction);
}
