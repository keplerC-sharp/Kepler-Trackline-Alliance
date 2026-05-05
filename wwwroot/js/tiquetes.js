/**
 * @file tiquetes.js
 * @description Manages the local audit log of generated tickets.
 * Handles filtering, rendering, reprinting, and purging of local records.
 */

let currentFilter = 'all';

// Inject scoped styles for the ticket audit interface.
const style = document.createElement('style');
style.textContent = `
  .filter-btn {
    padding: 6px 14px;
    background: var(--bg3);
    border: 1px solid var(--border);
    border-radius: 3px;
    color: var(--text-dim);
    font-family: var(--font-mono);
    font-size: 0.65rem;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    cursor: pointer;
    transition: all 0.15s;
  }
  .filter-btn:hover { border-color: var(--cyan); color: var(--cyan); }
  .filter-btn.active { border-color: var(--cyan); color: var(--cyan); background: rgba(0,229,255,0.08); }

  .tiquete-card {
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 3px;
    overflow: hidden;
    animation: ticketAppear 0.35s ease both;
    height: 100%;
  }
  .tiquete-card-header {
    padding: 12px 16px;
    border-bottom: 1px dashed var(--border);
    display: flex;
    align-items: center;
    justify-content: space-between;
  }
  .tiquete-card-header.turn     { border-top: 3px solid var(--cyan); }
  .tiquete-card-header.pilot    { border-top: 3px solid var(--red); }
  .tiquete-card-header.vehicle  { border-top: 3px solid var(--yellow); }
  .tiquete-type {
    font-family: var(--font-mono);
    font-size: 0.58rem;
    letter-spacing: 0.2em;
    text-transform: uppercase;
    color: var(--text-muted);
  }
  .tiquete-id {
    font-family: var(--font-display);
    font-size: 1.6rem;
    font-weight: 900;
    line-height: 1;
  }
  .tiquete-id.turn    { color: var(--cyan); }
  .tiquete-id.pilot   { color: var(--red); }
  .tiquete-id.vehicle { color: var(--yellow); }
  .tiquete-card-body { padding: 14px 16px; }
  .tiquete-row {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    padding: 5px 0;
    border-bottom: 1px solid rgba(255,255,255,0.04);
    gap: 8px;
  }
  .tiquete-row:last-child { border-bottom: none; }
  .tiquete-key {
    font-family: var(--font-mono);
    font-size: 0.58rem;
    letter-spacing: 0.12em;
    color: var(--text-muted);
    text-transform: uppercase;
    flex-shrink: 0;
  }
  .tiquete-val {
    font-family: var(--font-mono);
    font-size: 0.72rem;
    color: var(--text);
    text-align: right;
    word-break: break-word;
  }
  .tiquete-actions {
    padding: 10px 16px;
    border-top: 1px solid var(--border);
    display: flex;
    gap: 8px;
  }
`;
document.head.appendChild(style);

window.addEventListener('DOMContentLoaded', async () => {
  await initLayout('tiquetes');
  renderTiquetes();
});

/**
 * Retrieves the local audit log from the browser's persistent storage.
 */
function getTiquetes() {
  return JSON.parse(localStorage.getItem('apex_tiquetes') || '[]');
}

/**
 * Updates the active filter and refreshes the audit grid.
 */
function setFilter(f, btn) {
  currentFilter = f;
  document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');
  renderTiquetes();
}

/**
 * Dynamically builds and renders the audit record grid.
 */
function renderTiquetes() {
  const all  = getTiquetes();
  const list = currentFilter === 'all' ? all : all.filter(t => t.tipo === currentFilter);

  const grid  = document.getElementById('tiquetesGrid');
  const empty = document.getElementById('emptyState');

  if (!list.length) {
    grid.innerHTML = '';
    empty.style.display = 'block';
    return;
  }
  empty.style.display = 'none';

  grid.innerHTML = list.map((t, i) => {
    const tipoClass = (t.tipo || 'TURN').toLowerCase();
    const rows = buildRows(t);
    const btnId = `print-btn-${t.id || i}`;
    return `
      <div class="col-12 col-sm-6 col-xl-4">
        <div class="tiquete-card">
          <div class="tiquete-card-header ${tipoClass}">
            <div>
              <div class="tiquete-type">${t.tipo || 'TURN'}</div>
              <div class="tiquete-id ${tipoClass}">${getTiqueteId(t)}</div>
            </div>
            <span class="tag ${getTipoTag(t.tipo)}">
              ${t.tipo === 'TURN' ? 'QUEUED' : 'ARCHIVED'}
            </span>
          </div>
          <div class="tiquete-card-body">
            ${rows}
          </div>
          <div class="tiquete-actions">
            <button class="btn-print" id="${btnId}" onclick='reprintTiquete(${JSON.stringify(t).replace(/'/g,"&#39;")}, "${btnId}")'>
              <i class="bi bi-printer-fill"></i> Reprint
            </button>
            <button class="btn-outline" style="padding:8px 12px;" onclick="deleteTiquete(${t.id || i})">
              <i class="bi bi-trash"></i>
            </button>
          </div>
        </div>
      </div>
    `;
  }).join('');
}

function getTiqueteId(t) {
  if (t.tipo === 'TURN')    return t.turno || 'T-???';
  if (t.tipo === 'PILOT')   return t.driverId || 'ID-???';
  if (t.tipo === 'VEHICLE') return t.vin || 'VIN-???';
  return '—';
}

function getTipoTag(tipo) {
  if (tipo === 'TURN')    return 'tag-cyan';
  if (tipo === 'PILOT')   return 'tag-red';
  if (tipo === 'VEHICLE') return 'tag-yellow';
  return 'tag-gray';
}

/**
 * Constructs the metadata display for each audit entry based on its classification.
 */
function buildRows(t) {
  const rows = [];
  if (t.tipo === 'TURN') {
    rows.push(['Pilot',    t.nombre   || t.pilot   || '—']);
    rows.push(['Vehicle',  t.vehiculo || t.vehicle  || '—']);
    rows.push(['Duration', (t.duracion || t.duration || '?') + ' min']);
    rows.push(['Issued',   t.createdAt || '—']);
  } else if (t.tipo === 'PILOT') {
    rows.push(['Legal Name', t.nombre   || '—']);
    rows.push(['Grade',      t.licencia || '—']);
    rows.push(['Registry ID', t.driverId || '—']);
  } else if (t.tipo === 'VEHICLE') {
    rows.push(['Specs',      t.modelo    || '—']);
    rows.push(['Class',      t.categoria || '—']);
    rows.push(['Pit Garage', t.garage    || '—']);
  }
  rows.push(['Timestamp', t.fecha || '—']);
  return rows.map(([k, v]) => `
    <div class="tiquete-row">
      <span class="tiquete-key">${k}</span>
      <span class="tiquete-val">${v}</span>
    </div>
  `).join('');
}

/**
 * Re-dispatches a print job to the local network printer.
 */
function reprintTiquete(t, btnId) {
  const tipo = (t.tipo || 'turn').toLowerCase();
  sendPrintJob(btnId, { tipo, ...t });
}

/**
 * Removes a specific record from the local audit log.
 */
function deleteTiquete(id) {
  let list = getTiquetes();
  list = list.filter(t => t.id !== id);
  localStorage.setItem('apex_tiquetes', JSON.stringify(list));
  renderTiquetes();
  showToast('Record purged from local storage.', 'info');
}

/**
 * Wipes the entire local audit history.
 */
function clearAll() {
  if (!confirm('Permanent Action: Are you sure you want to purge the entire audit log?')) return;
  localStorage.removeItem('apex_tiquetes');
  renderTiquetes();
  showToast('Audit history purged.', 'info');
}
