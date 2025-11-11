// APM - Application Performance Monitoring
// Estado global
let currentTraces = [];
let currentStatistics = null;
let allSites = [];
let selectedSites = [];
let filtersActive = false; // Track si hay filtros activos
let lastRefreshTime = null; // Fecha y hora del último refresco
let loadingStatusCheckInterval = null; // Interval para verificar estado de carga

// Elementos del DOM
const elements = {
    // Loading banner
    loadingBanner: document.getElementById('loadingBanner'),

    // Estadísticas
    totalTraces: document.getElementById('totalTraces'),
    errorTraces: document.getElementById('errorTraces'),
    avgDuration: document.getElementById('avgDuration'),
    maxDuration: document.getElementById('maxDuration'),
    minDuration: document.getElementById('minDuration'),
    errorRate: document.getElementById('errorRate'),

    // Operaciones lentas
    slowestOperationsSection: document.getElementById('slowestOperationsSection'),
    slowestOperationsBody: document.getElementById('slowestOperationsBody'),

    // Sitios IIS
    selectedSitesTags: document.getElementById('selectedSitesTags'),
    siteSearchInput: document.getElementById('siteSearchInput'),
    siteDropdown: document.getElementById('siteDropdown'),
    siteOptions: document.getElementById('siteOptions'),
    saveSitesButton: document.getElementById('saveSitesButton'),

    // Filtros
    statusFilter: document.getElementById('statusFilter'),
    limitFilter: document.getElementById('limitFilter'),
    applyFiltersButton: document.getElementById('applyFiltersButton'),

    // Traces
    tracesLoading: document.getElementById('tracesLoading'),
    tracesError: document.getElementById('tracesError'),
    tracesTable: document.getElementById('tracesTable'),
    tracesTableBody: document.getElementById('tracesTableBody'),

    // Botones
    refreshButton: document.getElementById('refreshButton'),
    clearTracesButton: document.getElementById('clearTracesButton'),

    // Modal
    traceModalOverlay: document.getElementById('traceModalOverlay'),
    closeTraceModal: document.getElementById('closeTraceModal'),

    // Detalles del trace
    traceId: document.getElementById('traceId'),
    traceOperation: document.getElementById('traceOperation'),
    traceService: document.getElementById('traceService'),
    traceStatus: document.getElementById('traceStatus'),
    traceStartTime: document.getElementById('traceStartTime'),
    traceDuration: document.getElementById('traceDuration'),
    traceErrorContainer: document.getElementById('traceErrorContainer'),
    traceErrorMessage: document.getElementById('traceErrorMessage'),
    traceTagsContainer: document.getElementById('traceTagsContainer'),
    traceTagsList: document.getElementById('traceTagsList'),
    spansTimeline: document.getElementById('spansTimeline'),
    spansDetailList: document.getElementById('spansDetailList')
};

// Inicialización
document.addEventListener('DOMContentLoaded', () => {
    setupEventListeners();

    // Inicializar fecha de refresco
    updateLastRefreshTime();

    // Verificar estado de carga inicial
    checkLoadingStatus();

    loadDashboard();
    loadIISSites();

    // Auto-refresh cada 10 segundos
    setInterval(() => loadDashboard(), 10000);

    // Verificar estado de carga cada 3 segundos hasta que complete
    loadingStatusCheckInterval = setInterval(() => checkLoadingStatus(), 3000);
});

// Configurar event listeners
function setupEventListeners() {
    elements.refreshButton.addEventListener('click', () => loadDashboard());
    elements.clearTracesButton.addEventListener('click', () => clearTraces());
    elements.applyFiltersButton.addEventListener('click', () => {
        filtersActive = true;
        loadTraces();
    });
    elements.closeTraceModal.addEventListener('click', () => closeTraceModal());
    elements.traceModalOverlay.addEventListener('click', (e) => {
        if (e.target === elements.traceModalOverlay) {
            closeTraceModal();
        }
    });

    // Event listeners para pestañas
    setupTabListeners();
}

// Configurar listeners de pestañas
function setupTabListeners() {
    const tabButtons = document.querySelectorAll('.tab-button');
    tabButtons.forEach(button => {
        button.addEventListener('click', () => {
            const tabName = button.getAttribute('data-tab');
            switchTab(tabName);
        });
    });
}

// Cambiar de pestaña
function switchTab(tabName) {
    // Actualizar botones
    const tabButtons = document.querySelectorAll('.tab-button');
    tabButtons.forEach(btn => {
        btn.classList.remove('active');
        if (btn.getAttribute('data-tab') === tabName) {
            btn.classList.add('active');
        }
    });

    // Actualizar contenido
    const tabContents = document.querySelectorAll('.tab-content');
    tabContents.forEach(content => {
        content.classList.remove('active');
    });

    if (tabName === 'traces') {
        document.getElementById('tracesTab').classList.add('active');
    } else if (tabName === 'operations') {
        document.getElementById('operationsTab').classList.add('active');
    }
}

// Cargar dashboard completo
async function loadDashboard() {
    try {
        const response = await fetch('/api/apm/dashboard');
        if (!response.ok) throw new Error('Error al cargar dashboard');

        const data = await response.json();
        updateStatistics(data.statistics);

        // Si hay filtros activos, recargar traces con filtros
        // Si no, usar los traces del dashboard
        if (filtersActive) {
            await loadTraces();
        } else {
            updateTraces(data.recentTraces);
        }

        // Actualizar fecha de último refresco
        updateLastRefreshTime();

    } catch (error) {
        console.error('Error cargando dashboard:', error);
        showError('Error al cargar el dashboard de APM');
    }
}

// Cargar traces con filtros
async function loadTraces() {
    try {
        showLoading();

        const status = elements.statusFilter.value;
        const limit = elements.limitFilter.value;

        let url = `/api/apm/traces?limit=${limit}`;
        if (status) url += `&status=${status}`;

        const response = await fetch(url);
        if (!response.ok) throw new Error('Error al cargar traces');

        const traces = await response.json();
        updateTraces(traces);

        // Actualizar fecha de último refresco
        updateLastRefreshTime();

    } catch (error) {
        console.error('Error cargando traces:', error);
        showError('Error al cargar los traces');
    }
}

// Actualizar estadísticas
function updateStatistics(stats) {
    if (!stats) return;

    currentStatistics = stats;

    elements.totalTraces.textContent = stats.totalTraces || 0;
    elements.errorTraces.textContent = stats.errorTraces || 0;
    elements.avgDuration.textContent = formatDuration(stats.averageDurationMs || 0);
    elements.maxDuration.textContent = formatDuration(stats.maxDurationMs || 0);
    elements.minDuration.textContent = formatDuration(stats.minDurationMs || 0);
    elements.errorRate.textContent = `${(stats.errorRate || 0).toFixed(2)}%`;

    // Actualizar operaciones más lentas
    updateSlowestOperations(stats.slowestOperations || []);
}

// Actualizar operaciones más lentas
function updateSlowestOperations(operations) {
    if (!operations || operations.length === 0) {
        elements.slowestOperationsSection.style.display = 'none';
        return;
    }

    elements.slowestOperationsSection.style.display = 'block';
    elements.slowestOperationsBody.innerHTML = operations.map(op => `
        <tr>
            <td>${escapeHtml(op.operationName)}</td>
            <td>${formatDuration(op.averageDurationMs)}</td>
            <td>${op.count}</td>
            <td>${op.errorCount}</td>
            <td>${op.count > 0 ? ((op.errorCount / op.count) * 100).toFixed(2) : 0}%</td>
        </tr>
    `).join('');
}

// Actualizar lista de traces
function updateTraces(traces) {
    currentTraces = traces || [];

    // Ordenar traces por fecha de inicio descendente (más recientes primero)
    currentTraces.sort((a, b) => new Date(b.startTime) - new Date(a.startTime));

    elements.tracesLoading.style.display = 'none';
    elements.tracesError.style.display = 'none';

    if (currentTraces.length === 0) {
        elements.tracesTable.style.display = 'none';
        showError('No hay traces disponibles');
        return;
    }

    elements.tracesTable.style.display = 'table';
    elements.tracesTableBody.innerHTML = currentTraces.map(trace => {
        const statusClass = trace.status.toLowerCase();
        const statusIcon = trace.status === 'Success' ? '✅' : '❌';

        return `
            <tr>
                <td><span class="status-badge status-${statusClass}">${statusIcon} ${trace.status}</span></td>
                <td class="operation-name" title="${escapeHtml(trace.operationName)}">${escapeHtml(trace.operationName)}</td>
                <td>${escapeHtml(trace.serviceName)}</td>
                <td>${formatDateTime(trace.startTime)}</td>
                <td class="${trace.durationMs > 1000 ? 'duration-slow' : ''}">${formatDuration(trace.durationMs)}</td>
                <td>${trace.spans?.length || 0}</td>
                <td>
                    <button class="btn-view-trace" onclick="viewTrace('${trace.traceId}')">Ver Detalles</button>
                </td>
            </tr>
        `;
    }).join('');
}

// Ver detalles de un trace
async function viewTrace(traceId) {
    try {
        const response = await fetch(`/api/apm/traces/${traceId}`);
        if (!response.ok) throw new Error('Error al cargar trace');

        const trace = await response.json();
        showTraceModal(trace);

    } catch (error) {
        console.error('Error cargando trace:', error);
        alert('Error al cargar los detalles del trace');
    }
}

// Mostrar modal con detalles del trace
function showTraceModal(trace) {
    // Información básica
    elements.traceId.textContent = trace.traceId;
    elements.traceOperation.textContent = trace.operationName;
    elements.traceService.textContent = trace.serviceName;
    elements.traceStatus.textContent = trace.status;
    elements.traceStatus.className = `info-value status-badge status-${trace.status.toLowerCase()}`;
    elements.traceStartTime.textContent = formatDateTime(trace.startTime);
    elements.traceDuration.textContent = formatDuration(trace.durationMs);

    // Error message
    if (trace.errorMessage) {
        elements.traceErrorContainer.style.display = 'block';
        elements.traceErrorMessage.textContent = trace.errorMessage;
    } else {
        elements.traceErrorContainer.style.display = 'none';
    }

    // Tags
    if (trace.tags && Object.keys(trace.tags).length > 0) {
        elements.traceTagsContainer.style.display = 'block';
        elements.traceTagsList.innerHTML = Object.entries(trace.tags).map(([key, value]) => `
            <div class="tag-item">
                <span class="tag-key">${escapeHtml(key)}:</span>
                <span class="tag-value">${escapeHtml(value)}</span>
            </div>
        `).join('');
    } else {
        elements.traceTagsContainer.style.display = 'none';
    }

    // Spans
    if (trace.spans && trace.spans.length > 0) {
        renderSpansTimeline(trace.spans, trace.durationMs);
        renderSpansDetails(trace.spans);
    }

    elements.traceModalOverlay.style.display = 'flex';
}

// Renderizar timeline de spans
function renderSpansTimeline(spans, totalDuration) {
    if (!spans || spans.length === 0) {
        elements.spansTimeline.innerHTML = '<p class="no-data">No hay spans disponibles</p>';
        return;
    }

    // Ordenar spans por tiempo de inicio
    const sortedSpans = [...spans].sort((a, b) =>
        new Date(a.startTime) - new Date(b.startTime)
    );

    const baseTime = new Date(sortedSpans[0].startTime).getTime();

    elements.spansTimeline.innerHTML = sortedSpans.map(span => {
        const startOffset = new Date(span.startTime).getTime() - baseTime;
        const leftPercent = (startOffset / totalDuration) * 100;
        const widthPercent = Math.max((span.durationMs / totalDuration) * 100, 1);

        const statusClass = span.status.toLowerCase();
        const spanTypeClass = span.spanKind.toLowerCase();

        return `
            <div class="span-timeline-item">
                <div class="span-timeline-label">
                    <span class="span-name" title="${escapeHtml(span.operationName)}">${escapeHtml(span.operationName)}</span>
                    <span class="span-duration">${formatDuration(span.durationMs)}</span>
                </div>
                <div class="span-timeline-bar-container">
                    <div class="span-timeline-bar span-${statusClass} span-type-${spanTypeClass}"
                         style="left: ${leftPercent}%; width: ${widthPercent}%;"
                         title="${escapeHtml(span.operationName)} - ${formatDuration(span.durationMs)}">
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

// Renderizar detalles de spans
function renderSpansDetails(spans) {
    if (!spans || spans.length === 0) {
        elements.spansDetailList.innerHTML = '<p class="no-data">No hay spans disponibles</p>';
        return;
    }

    elements.spansDetailList.innerHTML = spans.map((span, index) => {
        const statusClass = span.status.toLowerCase();
        const statusIcon = span.status === 'Success' ? '✅' : '❌';

        return `
            <div class="span-detail-card">
                <div class="span-detail-header">
                    <div class="span-detail-title">
                        <span class="span-index">#${index + 1}</span>
                        <span class="span-operation">${escapeHtml(span.operationName)}</span>
                        <span class="status-badge status-${statusClass}">${statusIcon} ${span.status}</span>
                    </div>
                    <div class="span-detail-meta">
                        <span class="span-kind">${escapeHtml(span.spanKind)}</span>
                        <span class="span-duration">${formatDuration(span.durationMs)}</span>
                    </div>
                </div>
                <div class="span-detail-body">
                    <div class="span-info-row">
                        <strong>Span ID:</strong> ${span.spanId}
                    </div>
                    ${span.parentSpanId ? `
                        <div class="span-info-row">
                            <strong>Parent Span ID:</strong> ${span.parentSpanId}
                        </div>
                    ` : ''}
                    <div class="span-info-row">
                        <strong>Servicio:</strong> ${escapeHtml(span.serviceName)}
                    </div>
                    <div class="span-info-row">
                        <strong>Inicio:</strong> ${formatDateTime(span.startTime)}
                    </div>
                    ${span.errorMessage ? `
                        <div class="span-error-message">
                            <strong>Error:</strong> ${escapeHtml(span.errorMessage)}
                        </div>
                    ` : ''}
                    ${span.tags && Object.keys(span.tags).length > 0 ? `
                        <div class="span-tags">
                            <strong>Tags:</strong>
                            <div class="tags-list">
                                ${Object.entries(span.tags).map(([key, value]) => `
                                    <div class="tag-item">
                                        <span class="tag-key">${escapeHtml(key)}:</span>
                                        <span class="tag-value">${escapeHtml(value)}</span>
                                    </div>
                                `).join('')}
                            </div>
                        </div>
                    ` : ''}
                    ${span.events && span.events.length > 0 ? `
                        <div class="span-events">
                            <strong>Eventos:</strong>
                            <div class="events-list">
                                ${span.events.map(event => `
                                    <div class="event-item">
                                        <span class="event-name">${escapeHtml(event.name)}</span>
                                        <span class="event-time">${formatDateTime(event.timestamp)}</span>
                                    </div>
                                `).join('')}
                            </div>
                        </div>
                    ` : ''}
                </div>
            </div>
        `;
    }).join('');
}

// Cerrar modal
function closeTraceModal() {
    elements.traceModalOverlay.style.display = 'none';
}

// Limpiar todos los traces
async function clearTraces() {
    if (!confirm('¿Estás seguro de que quieres limpiar todos los traces?')) {
        return;
    }

    try {
        const response = await fetch('/api/apm/clear', { method: 'POST' });
        if (!response.ok) throw new Error('Error al limpiar traces');

        alert('Traces limpiados correctamente');
        loadDashboard();

    } catch (error) {
        console.error('Error limpiando traces:', error);
        alert('Error al limpiar los traces');
    }
}

// Utilidades
function showLoading() {
    elements.tracesLoading.style.display = 'block';
    elements.tracesTable.style.display = 'none';
    elements.tracesError.style.display = 'none';
}

function showError(message) {
    elements.tracesLoading.style.display = 'none';
    elements.tracesTable.style.display = 'none';
    elements.tracesError.style.display = 'block';
    elements.tracesError.textContent = message;
}

function formatDuration(ms) {
    if (ms < 1) return `${(ms * 1000).toFixed(2)} µs`;
    if (ms < 1000) return `${ms.toFixed(2)} ms`;
    if (ms < 60000) return `${(ms / 1000).toFixed(2)} s`;
    return `${(ms / 60000).toFixed(2)} min`;
}

function formatDateTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('es-ES', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        fractionalSecondDigits: 3
    });
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function updateLastRefreshTime() {
    lastRefreshTime = new Date();
    const element = document.getElementById('lastRefreshTime');
    if (element) {
        element.textContent = formatDateTime(lastRefreshTime);
    }
}

// Funciones para gestión de sitios IIS
async function loadIISSites() {
    try {
        const response = await fetch('/api/apm/sites');
        if (!response.ok) throw new Error('Error al cargar sitios IIS');

        const sites = await response.json();
        allSites = sites;

        // Inicializar sitios seleccionados
        selectedSites = sites.filter(s => s.isMonitored).map(s => ({
            id: s.id,
            name: s.name
        }));

        renderSitesMultiselect();
        setupSiteSearchListeners();
    } catch (error) {
        console.error('Error cargando sitios IIS:', error);
        elements.selectedSitesTags.innerHTML = '<span class="error-text">Error al cargar sitios IIS</span>';
    }
}

function renderSitesMultiselect() {
    // Renderizar tags de seleccionados
    if (selectedSites.length === 0) {
        elements.selectedSitesTags.innerHTML = '<span class="placeholder-text">No hay sitios seleccionados. Usa "Todos" o busca sitios...</span>';
    } else {
        elements.selectedSitesTags.innerHTML = selectedSites.map(site => `
            <span class="tag">
                ${escapeHtml(site.name)} (${escapeHtml(site.id)})
                <span class="tag-remove" onclick="removeSite('${site.id}')">×</span>
            </span>
        `).join('');
    }

    // Renderizar opciones del dropdown
    renderSiteOptions('');
}

function renderSiteOptions(searchTerm) {
    const term = searchTerm.toLowerCase();
    const filteredSites = allSites.filter(site =>
        site.name.toLowerCase().includes(term) ||
        site.id.toLowerCase().includes(term)
    );

    if (filteredSites.length === 0) {
        elements.siteOptions.innerHTML = '<div class="no-options">No se encontraron sitios</div>';
        return;
    }

    elements.siteOptions.innerHTML = filteredSites.map(site => {
        const isSelected = selectedSites.some(s => s.id === site.id);
        return `
            <div class="multiselect-option ${isSelected ? 'selected' : ''}"
                 onclick="toggleSite('${site.id}', '${escapeHtml(site.name).replace(/'/g, "\\'")}')">
                <span class="option-checkbox">${isSelected ? '✓' : ''}</span>
                <span class="option-text">${escapeHtml(site.name)} (ID: ${escapeHtml(site.id)})</span>
            </div>
        `;
    }).join('');
}

function setupSiteSearchListeners() {
    // Input focus - mostrar dropdown
    elements.siteSearchInput.addEventListener('focus', () => {
        elements.siteDropdown.style.display = 'block';
    });

    // Input - búsqueda
    elements.siteSearchInput.addEventListener('input', (e) => {
        renderSiteOptions(e.target.value);
    });

    // Clic fuera - ocultar dropdown
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.multiselect-search-wrapper')) {
            elements.siteDropdown.style.display = 'none';
        }
    });

    // Botón guardar
    elements.saveSitesButton.addEventListener('click', saveSitesConfiguration);
}

function toggleSite(siteId, siteName) {
    const index = selectedSites.findIndex(s => s.id === siteId);

    if (index === -1) {
        // Añadir
        selectedSites.push({ id: siteId, name: siteName });
    } else {
        // Remover
        selectedSites.splice(index, 1);
    }

    renderSitesMultiselect();
    elements.siteSearchInput.value = '';
}

function removeSite(siteId) {
    selectedSites = selectedSites.filter(s => s.id !== siteId);
    renderSitesMultiselect();
}

function selectAllSites() {
    selectedSites = allSites.map(site => ({
        id: site.id,
        name: site.name
    }));
    renderSitesMultiselect();
}

function deselectAllSites() {
    selectedSites = [];
    renderSitesMultiselect();
}

async function saveSitesConfiguration() {
    try {
        const selectedSiteIds = selectedSites.map(s => s.id);

        elements.saveSitesButton.disabled = true;
        elements.saveSitesButton.textContent = '💾 Guardando...';

        const response = await fetch('/api/apm/sites/monitor', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ siteIds: selectedSiteIds })
        });

        if (!response.ok) throw new Error('Error al guardar configuración');

        const result = await response.json();

        // Mostrar mensaje de éxito
        elements.saveSitesButton.textContent = '✅ Guardado';
        elements.saveSitesButton.style.backgroundColor = '#10b981';

        setTimeout(() => {
            elements.saveSitesButton.disabled = false;
            elements.saveSitesButton.textContent = '💾 Guardar';
            elements.saveSitesButton.style.backgroundColor = '';
        }, 2000);

        console.log('Configuración guardada:', result);
    } catch (error) {
        console.error('Error guardando configuración:', error);
        alert('Error al guardar la configuración de sitios');

        elements.saveSitesButton.disabled = false;
        elements.saveSitesButton.textContent = '💾 Guardar';
    }
}

// Verificar estado de carga inicial
async function checkLoadingStatus() {
    try {
        const response = await fetch('/api/apm/loading-status');
        if (!response.ok) {
            throw new Error('Error al obtener estado de carga');
        }

        const status = await response.json();
        console.log('Loading status:', status);

        // Mostrar u ocultar el banner según el estado
        if (status.isLoading) {
            console.log('Mostrando banner de carga...');
            elements.loadingBanner.style.display = 'flex';
        } else {
            console.log('Ocultando banner de carga...');
            elements.loadingBanner.style.display = 'none';

            // Detener el interval cuando la carga inicial complete
            if (status.isInitialLoadComplete && loadingStatusCheckInterval) {
                clearInterval(loadingStatusCheckInterval);
                loadingStatusCheckInterval = null;
                console.log('Carga inicial completada. Traces disponibles:', status.tracesCount);
            }
        }
    } catch (error) {
        console.error('Error verificando estado de carga:', error);
        // En caso de error, ocultar el banner para no bloquear la interfaz
        elements.loadingBanner.style.display = 'none';
    }
}
