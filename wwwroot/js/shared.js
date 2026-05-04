// ════════════════════════════════════════
// SHARED UTILITIES — todas las páginas
// ════════════════════════════════════════

// ── TOASTS ──
function showToast(msg, type = 'info') {
  const container = document.getElementById('toastContainer');
  if (!container) return;
  const toast = document.createElement('div');
  toast.className = `toast-item ${type}`;
  const label = type === 'success' ? 'Éxito' : type === 'error' ? 'Error' : 'Info';
  toast.innerHTML = `<div class="toast-label">${label}</div>${msg}`;
  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transition = 'opacity 0.3s';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// ── MOBILE SIDEBAR ──
function toggleSidebar() {
  document.getElementById('sidebar')?.classList.toggle('mobile-open');
  document.getElementById('mobileOverlay')?.classList.toggle('active');
}
function closeSidebar() {
  document.getElementById('sidebar')?.classList.remove('mobile-open');
  document.getElementById('mobileOverlay')?.classList.remove('active');
}

// ════════════════════════════════════════
// IMPRESIÓN — KeplerPrint
// POST http://localhost:3000/api/print/...
// Corre: cd KeplerPrint && dotnet run
// ════════════════════════════════════════
const PRINT_BASE_URL = 'http://localhost:3000/api/print';

function getPrintServerUrl() {
  const ip  = document.getElementById('printServerIp');
  const val = ip ? ip.value.trim() : (localStorage.getItem('apex_print_ip') || 'localhost:3000');
  if (ip) localStorage.setItem('apex_print_ip', val);
  return `http://${val}/api/print`;
}

async function sendPrintJob(btnId, payload) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.classList.add('printing');
  btn.innerHTML = '<i class="bi bi-printer print-spinner"></i> Enviando...';

  const tipoEndpoint = { 'PILOTO':'piloto', 'VEHICULO':'vehiculo', 'TURNO':'turno', 'COMPLETO':'completo' };
  const endpoint = tipoEndpoint[(payload.tipo || '').toUpperCase()] || 'turno';

  const printPayload = {
    nombre:    payload.nombre    || payload.fullName  || '—',
    driverId:  payload.driverId  || payload.gridId    || '—',
    licencia:  payload.licencia  || payload.grade     || '—',
    email:     payload.email     || '—',
    fecha:     new Date().toLocaleString('es-CO'),
    modelo:    payload.modelo    || '—',
    categoria: payload.categoria || '—',
    vin:       payload.vin       || '—',
    garage:    payload.garage    || '—',
    turno:     payload.turno     || payload.gridId    || '—',
    vehiculo:  payload.vehiculo  || '—',
    duracion:  payload.duracion  || 0,
    createdAt: payload.createdAt || new Date().toLocaleTimeString('es-CO'),
  };

  try {
    const res  = await fetch(`${getPrintServerUrl()}/${endpoint}`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(printPayload), signal: AbortSignal.timeout(6000)
    });
    const data = await res.json();
    if (data.ok) {
      btn.classList.remove('printing'); btn.classList.add('success-print');
      btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Impreso';
      showToast('Ticket enviado a la impresora', 'success');
      setTimeout(() => { btn.classList.remove('success-print'); btn.innerHTML = '<i class="bi bi-printer-fill"></i> Reimprimir'; }, 4000);
    } else throw new Error(data.error || 'Error del servidor');
  } catch (err) {
    btn.classList.remove('printing'); btn.classList.add('error-print');
    btn.innerHTML = '<i class="bi bi-exclamation-triangle-fill"></i> Error — Reintentar';
    showToast(`Error impresión: ${err.message}`, 'error');
  }
}

// Impresión completa: Piloto + Vehículo + Turno en un ticket
async function printRegistroCompleto(payload) {
  try {
    const res  = await fetch(`${getPrintServerUrl()}/completo`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload), signal: AbortSignal.timeout(6000)
    });
    const data = await res.json();
    if (data.ok) showToast('Ticket completo impreso', 'success');
    else         showToast(`Error: ${data.error}`, 'error');
  } catch (err) {
    showToast(`KeplerPrint no responde: ${err.message}`, 'error');
  }
}

async function testPrintConnection() {
  const dot   = document.getElementById('printStatusDot');
  const label = document.getElementById('printStatusLabel');
  if (!dot || !label) return;
  dot.className = 'print-status-dot'; label.textContent = 'Probando...';
  try {
    const res = await fetch(`${getPrintServerUrl()}/health`, { signal: AbortSignal.timeout(3000) });
    if (res.ok) {
      dot.className = 'print-status-dot online'; label.textContent = 'Conectado';
      showToast('KeplerPrint online', 'success');
    } else throw new Error();
  } catch {
    dot.className = 'print-status-dot offline'; label.textContent = 'Sin conexión';
    showToast('KeplerPrint offline — corre: cd KeplerPrint && dotnet run', 'error');
  }
}

// ── SIDEBAR ──
function renderSidebar(activePage, opName) {
  const pages = [
    { id: 'registry',  icon: 'bi-person-lines-fill', label: 'Driver Registry', href: '/Dashboard/Index'   },
    { id: 'cola',      icon: 'bi-clock-history',     label: 'Cola de Turnos',  href: '/Queue/Index'       },
    { id: 'tiquetes',  icon: 'bi-ticket-perforated', label: 'Tiquetes',        href: '/Dashboard/Tickets' },
    { id: 'operators', icon: 'bi-person-plus',        label: 'Crear Operador', href: '/Auth/Register'     },
  ];
  const name     = opName || 'Operador';
  const initials = name.split(' ').map(w => w[0]).join('').slice(0,2).toUpperCase() || 'OP';
  return `
    <div class="sidebar-user">
      <div class="sidebar-avatar">${initials}</div>
      <div class="sidebar-user-info">
        <div class="sidebar-user-name">${name}</div>
        <div class="sidebar-user-role">Pit Wall Station</div>
      </div>
    </div>
    <nav class="sidebar-nav">
      ${pages.map(p => `
        <a class="nav-item ${activePage === p.id ? 'active' : ''}" href="${p.href}">
          <i class="bi ${p.icon}"></i>${p.label}
        </a>`).join('')}
    </nav>
    <div class="sidebar-bottom">
      <a class="btn-new-session" href="/Auth/Logout">
        <i class="bi bi-box-arrow-left me-1"></i> Cerrar Sesión
      </a>
    </div>`;
}

// ── TOPBAR ──
function renderTopbar() {
  return `
    <button class="hamburger" onclick="toggleSidebar()">
      <i class="bi bi-list"></i>
    </button>
    <div class="topbar-brand"><span>CIRCUIT PRO: </span>PIT CONTROL</div>
    <div class="topbar-search">
      <i class="bi bi-search"></i>
      <input type="text" placeholder="Búsqueda rápida...">
    </div>
    <div class="topbar-actions">
      <div class="btn-icon"><i class="bi bi-bell"></i></div>
      <div class="btn-icon"><i class="bi bi-gear"></i></div>
      <button class="btn-stop" onclick="showToast('Emergency stop activado', 'error')">
        <i class="bi bi-octagon-fill me-1"></i> Emergency Stop
      </button>
    </div>`;
}

// ── INIT LAYOUT ──
async function initLayout(activePage) {
  const topbar = document.getElementById('topbar');
  if (topbar) topbar.innerHTML = renderTopbar();

  const sidebar = document.getElementById('sidebar');
  if (sidebar) sidebar.innerHTML = renderSidebar(activePage, 'Operador');

  // Obtener nombre real del operador autenticado
  let opName = sessionStorage.getItem('apex_operator_name');
  if (!opName) {
    try {
      const res = await fetch('/Auth/Me');
      if (res.ok) {
        const data = await res.json();
        opName = data.name || 'Operador';
        sessionStorage.setItem('apex_operator_name', opName);
      }
    } catch { opName = 'Operador'; }
  }
  if (sidebar) sidebar.innerHTML = renderSidebar(activePage, opName);

  // Restaurar IP impresora
  const ipEl = document.getElementById('printServerIp');
  if (ipEl) ipEl.value = localStorage.getItem('apex_print_ip') || 'localhost:3000';
}
