// Variables globales
let currentFileName = null;
let currentFileId = null; // Identificador del archivo (ruta codificada en Base64)
let currentPage = 1;
let pageSize = 50;
let currentLevel = '';
let currentSearch = '';
let totalLines = 0;
let hasMore = false;
let currentSiteFilter = '';

// API Base URL
const API_BASE = '/api/logs';

// Inicialización
document.addEventListener('DOMContentLoaded', () => {
    initializeEventListeners();
    loadIISSites();
    loadLogFiles();
});

/**
 * Inicializa los event listeners
 */
function initializeEventListeners() {
    // Botón de cerrar visor de logs
    document.getElementById('closeLogViewer').addEventListener('click', closeLogViewer);

    // Filtros
    document.getElementById('applyFilters').addEventListener('click', applyFilters);
    document.getElementById('clearFilters').addEventListener('click', clearFilters);

    // Filtro por sitio IIS
    document.getElementById('siteFilter').addEventListener('change', (e) => {
        currentSiteFilter = e.target.value;
        loadLogFiles();
    });

    // Enter en el campo de búsqueda
    document.getElementById('searchFilter').addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            applyFilters();
        }
    });

    // Paginación
    document.getElementById('prevPage').addEventListener('click', () => changePage(-1));
    document.getElementById('nextPage').addEventListener('click', () => changePage(1));
}

/**
 * Carga la lista de sitios IIS
 */
async function loadIISSites() {
    try {
        const response = await fetch(`${API_BASE}/iis-sites`);
        if (!response.ok) {
            throw new Error('Error al cargar los sitios IIS');
        }

        const sites = await response.json();
        const siteFilter = document.getElementById('siteFilter');

        // Añadir opciones de sitios
        sites.forEach(site => {
            const option = document.createElement('option');
            option.value = site;
            option.textContent = site;
            siteFilter.appendChild(option);
        });
    } catch (error) {
        console.error('Error al cargar sitios IIS:', error);
    }
}

/**
 * Carga la lista de archivos de log disponibles
 */
async function loadLogFiles() {
    try {
        let url = `${API_BASE}/files`;
        if (currentSiteFilter) {
            url += `?iisSite=${encodeURIComponent(currentSiteFilter)}`;
        }

        const response = await fetch(url);
        if (!response.ok) {
            throw new Error('Error al cargar los archivos de log');
        }

        const data = await response.json();
        displayLogFiles(data);
        displayLogDirectories(data.logDirectories);
        updateLogStats(data);
    } catch (error) {
        console.error('Error al cargar archivos de log:', error);
        document.getElementById('logFilesList').innerHTML =
            '<p class="error-message">❌ Error al cargar los archivos de log. Asegúrate de que la aplicación esté generando logs.</p>';
    }
}

/**
 * Muestra las estadísticas de logs
 */
function updateLogStats(data) {
    document.getElementById('totalFiles').textContent = data.totalFiles;
    document.getElementById('totalSize').textContent = formatBytes(data.totalSizeBytes);
    document.getElementById('totalDirs').textContent = data.logDirectories.length;
}

/**
 * Muestra los directorios de logs
 */
function displayLogDirectories(directories) {
    const container = document.getElementById('logDirectories');

    if (directories.length === 0) {
        container.innerHTML = '<p class="info-message">No se encontraron directorios de logs.</p>';
        return;
    }

    const html = `
        <div class="directories-list">
            <h3>📁 Directorios de Logs:</h3>
            <ul>
                ${directories.map(dir => `<li><code>${dir}</code></li>`).join('')}
            </ul>
        </div>
    `;
    container.innerHTML = html;
}

/**
 * Muestra la lista de archivos de log
 */
function displayLogFiles(data) {
    const container = document.getElementById('logFilesList');

    if (data.logFiles.length === 0) {
        container.innerHTML = `
            <div class="no-logs-message">
                <p>📭 No se encontraron archivos de log.</p>
                <p>Los logs se generarán automáticamente cuando la aplicación esté en funcionamiento.</p>
                <button onclick="generateTestLogs()" class="btn-generate-test">Generar Logs de Prueba</button>
            </div>
        `;
        return;
    }

    const html = data.logFiles.map(file => `
        <div class="log-file-card ${file.iisSiteName ? 'iis-site-log' : ''}" onclick="loadLogContent('${escapeHtml(file.fullPath)}', '${escapeHtml(file.fileName)}')">
            <div class="log-file-header">
                <h3>📄 ${file.fileName}</h3>
                <span class="log-file-size">${file.sizeFormatted}</span>
            </div>
            ${file.iisSiteName ? `
                <div class="iis-site-badge">
                    🌐 Sitio IIS: <strong>${escapeHtml(file.iisSiteName)}</strong>
                </div>
            ` : ''}
            <div class="log-file-details">
                <div class="detail-item">
                    <span class="detail-label">Directorio:</span>
                    <span class="detail-value">${file.directory}</span>
                </div>
                <div class="detail-item">
                    <span class="detail-label">Última modificación:</span>
                    <span class="detail-value">${formatDate(file.lastModified)}</span>
                </div>
            </div>
            <button class="btn-view-log">Ver Contenido →</button>
        </div>
    `).join('');

    container.innerHTML = html;
}

/**
 * Carga el contenido de un archivo de log
 */
async function loadLogContent(fullPath, fileName) {
    currentFileName = fileName;
    // Codificar la ruta completa en Base64 para usarla como identificador
    currentFileId = btoa(fullPath);
    currentPage = 1;

    // Mostrar sección de contenido
    document.getElementById('logsContentSection').style.display = 'block';
    document.getElementById('currentFileName').textContent = fileName;

    // Scroll al visor
    document.getElementById('logsContentSection').scrollIntoView({ behavior: 'smooth' });

    await fetchLogContent();
}

/**
 * Obtiene el contenido del log desde la API
 */
async function fetchLogContent() {
    try {
        const skip = (currentPage - 1) * pageSize;
        let url = `${API_BASE}/content/${encodeURIComponent(currentFileId)}?skip=${skip}&take=${pageSize}`;

        if (currentLevel) {
            url += `&level=${encodeURIComponent(currentLevel)}`;
        }

        if (currentSearch) {
            url += `&search=${encodeURIComponent(currentSearch)}`;
        }

        const response = await fetch(url);
        if (!response.ok) {
            throw new Error('Error al cargar el contenido del log');
        }

        const data = await response.json();
        displayLogContent(data);
    } catch (error) {
        console.error('Error al cargar contenido del log:', error);
        document.getElementById('logsTableBody').innerHTML =
            '<tr><td colspan="3" class="error-cell">❌ Error al cargar el contenido del log</td></tr>';
    }
}

/**
 * Muestra el contenido del log en la tabla
 */
function displayLogContent(data) {
    const tbody = document.getElementById('logsTableBody');
    totalLines = data.totalLines;
    hasMore = data.hasMore;

    // Actualizar información
    document.getElementById('entriesShown').textContent = data.returnedLines;
    document.getElementById('totalEntries').textContent = data.totalLines;
    document.getElementById('currentPage').textContent = currentPage;

    // Actualizar botones de paginación
    document.getElementById('prevPage').disabled = currentPage === 1;
    document.getElementById('nextPage').disabled = !hasMore;

    if (data.entries.length === 0) {
        tbody.innerHTML = '<tr><td colspan="3" class="empty-cell">No se encontraron entradas con los filtros aplicados</td></tr>';
        return;
    }

    const html = data.entries.map(entry => {
        const levelClass = getLevelClass(entry.level);
        const hasException = entry.exception && entry.exception.trim() !== '';

        return `
            <tr class="log-entry ${levelClass}">
                <td class="col-timestamp">${formatTimestamp(entry.timestamp)}</td>
                <td class="col-level">
                    <span class="badge badge-${levelClass}">${entry.level}</span>
                </td>
                <td class="col-message">
                    <div class="message-content">${escapeHtml(entry.message)}</div>
                    ${hasException ? `
                        <details class="exception-details">
                            <summary>Ver excepción</summary>
                            <pre class="exception-content">${escapeHtml(entry.exception)}</pre>
                        </details>
                    ` : ''}
                </td>
            </tr>
        `;
    }).join('');

    tbody.innerHTML = html;
}

/**
 * Aplica los filtros seleccionados
 */
function applyFilters() {
    currentLevel = document.getElementById('levelFilter').value;
    currentSearch = document.getElementById('searchFilter').value;
    currentPage = 1;
    fetchLogContent();
}

/**
 * Limpia los filtros
 */
function clearFilters() {
    document.getElementById('levelFilter').value = '';
    document.getElementById('searchFilter').value = '';
    currentLevel = '';
    currentSearch = '';
    currentPage = 1;
    fetchLogContent();
}

/**
 * Cambia de página
 */
function changePage(delta) {
    currentPage += delta;
    if (currentPage < 1) currentPage = 1;
    fetchLogContent();
}

/**
 * Cierra el visor de logs
 */
function closeLogViewer() {
    document.getElementById('logsContentSection').style.display = 'none';
    currentFileName = null;
    currentFileId = null;
    currentPage = 1;
    window.scrollTo({ top: 0, behavior: 'smooth' });
}

/**
 * Genera logs de prueba
 */
async function generateTestLogs() {
    try {
        const response = await fetch(`${API_BASE}/generate-test-logs`, {
            method: 'POST'
        });

        if (response.ok) {
            alert('✅ Logs de prueba generados exitosamente');
            // Recargar la lista de archivos
            setTimeout(() => loadLogFiles(), 1000);
        } else {
            alert('❌ Error al generar logs de prueba');
        }
    } catch (error) {
        console.error('Error al generar logs de prueba:', error);
        alert('❌ Error al generar logs de prueba');
    }
}

/**
 * Obtiene la clase CSS para el nivel de log
 */
function getLevelClass(level) {
    const levelMap = {
        'Verbose': 'verbose',
        'Debug': 'debug',
        'Information': 'info',
        'Warning': 'warning',
        'Error': 'error',
        'Fatal': 'fatal'
    };
    return levelMap[level] || 'info';
}

/**
 * Formatea un timestamp
 */
function formatTimestamp(timestamp) {
    const date = new Date(timestamp);
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

/**
 * Formatea una fecha
 */
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('es-ES', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    });
}

/**
 * Formatea bytes a formato legible
 */
function formatBytes(bytes) {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

/**
 * Escapa HTML para prevenir XSS
 */
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}
