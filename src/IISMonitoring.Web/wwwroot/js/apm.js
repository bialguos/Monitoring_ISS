// APM - Application Performance Monitoring
// Estado global
let currentTraces = [];
let currentStatistics = null;

// Elementos del DOM
const elements = {
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
    loadDashboard();

    // Auto-refresh cada 10 segundos
    setInterval(() => loadDashboard(), 10000);
});

// Configurar event listeners
function setupEventListeners() {
    elements.refreshButton.addEventListener('click', () => loadDashboard());
    elements.clearTracesButton.addEventListener('click', () => clearTraces());
    elements.applyFiltersButton.addEventListener('click', () => loadTraces());
    elements.closeTraceModal.addEventListener('click', () => closeTraceModal());
    elements.traceModalOverlay.addEventListener('click', (e) => {
        if (e.target === elements.traceModalOverlay) {
            closeTraceModal();
        }
    });
}

// Cargar dashboard completo
async function loadDashboard() {
    try {
        const response = await fetch('/api/apm/dashboard');
        if (!response.ok) throw new Error('Error al cargar dashboard');

        const data = await response.json();
        updateStatistics(data.statistics);
        updateTraces(data.recentTraces);

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
