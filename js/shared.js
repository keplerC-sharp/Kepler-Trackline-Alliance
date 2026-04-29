// ════════════════════════════════════════
// SHARED UTILITIES — todas las páginas
// ════════════════════════════════════════

// ── AUTH GUARD ──
function requireAuth() {
  if (!sessionStorage.getItem('apex_user')) {
    window.location.href = 'index.html';
  }
}

function handleLogout() {
  sessionStorage.removeItem('apex_user');
  window.location.href = 'index.html';
}

function getUser() {
  const u = sessionStorage.getItem('apex_user');
  return u ? JSON.parse(u) : null;
}

// ── SIDEBAR ACTIVE STATE ──
function setActivePage(pageId) {
  document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
  const el = document.getElementById('nav-' + pageId);
  if (el) el.classList.add('active');
}

// ── MOBILE SIDEBAR ──
function toggleSidebar() {
  document.getElementById('sidebar').classList.toggle('mobile-open');
  document.getElementById('mobileOverlay').classList.toggle('active');
}
function closeSidebar() {
  document.getElementById('sidebar')?.classList.remove('mobile-open');
  document.getElementById('mobileOverlay')?.classList.remove('active');
}

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
  }, 3500);
}

// ── PRINT SERVER ──
function getPrintServerUrl() {
  const ip = document.getElementById('printServerIp');
  const val = ip ? ip.value.trim() : localStorage.getItem('apex_print_ip') || '192.168.1.105:3000';
  if (ip) localStorage.setItem('apex_print_ip', ip.value.trim());
  return `http://${val}`;
}

async function sendPrintJob(btnId, payload) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.classList.add('printing');
  btn.innerHTML = '<i class="bi bi-printer print-spinner"></i> Enviando...';
  try {
    const res = await fetch(`${getPrintServerUrl()}/api/print/${payload.tipo.toLowerCase()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(6000)
    });
    const data = await res.json();
    if (data.ok) {
      btn.classList.remove('printing');
      btn.classList.add('success-print');
      btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Impreso';
      showToast('Ticket enviado a la impresora', 'success');
      setTimeout(() => {
        btn.classList.remove('success-print');
        btn.innerHTML = '<i class="bi bi-printer-fill"></i> Reimprimir';
      }, 4000);
    } else { throw new Error(data.error || 'Error del servidor'); }
  } catch (err) {
    btn.classList.remove('printing');
    btn.classList.add('error-print');
    btn.innerHTML = '<i class="bi bi-exclamation-triangle-fill"></i> Error — Reintentar';
    showToast(`Error de impresión: ${err.message}`, 'error');
  }
}

async function testPrintConnection() {
  const dot   = document.getElementById('printStatusDot');
  const label = document.getElementById('printStatusLabel');
  if (!dot || !label) return;
  dot.className = 'print-status-dot';
  label.textContent = 'Probando...';
  try {
    const res = await fetch(`${getPrintServerUrl()}/api/print/health`, { signal: AbortSignal.timeout(3000) });
    if (res.ok) {
      dot.className = 'print-status-dot online';
      label.textContent = 'Conectado';
      showToast('Servidor de impresión online', 'success');
    } else { throw new Error(); }
  } catch {
    dot.className = 'print-status-dot offline';
    label.textContent = 'Sin conexión';
    showToast('No se pudo conectar al servidor', 'error');
  }
}

// ── SIDEBAR HTML (inyectado en cada página) ──
function renderSidebar(activePage) {
  const user = getUser();
  const initials = user ? user.id.substring(0, 2).toUpperCase() : 'S1';
  const name     = user ? user.id : 'Sector 1 Admin';

  const pages = [
    { id: 'registry', icon: 'bi-person-lines-fill', label: 'Driver Registry',  href: 'registro.html' },
    { id: 'cola',     icon: 'bi-clock-history',     label: 'Cola de Turnos',   href: 'cola.html'     },
    { id: 'tiquetes', icon: 'bi-ticket-perforated', label: 'Tiquetes',         href: 'tiquetes.html' },
  ];

  return `
    <div class="sidebar-user">
      <div class="sidebar-avatar">${initials}</div>
      <div class="sidebar-user-info">
        <div class="sidebar-user-name">${name}</div>
        <div class="sidebar-user-role">Pit Wall Station 04</div>
      </div>
    </div>
    <nav class="sidebar-nav">
      ${pages.map(p => `
        <a class="nav-item ${activePage === p.id ? 'active' : ''}" href="${p.href}" id="nav-${p.id}">
          <i class="bi ${p.icon}"></i>
          ${p.label}
        </a>
      `).join('')}
    </nav>
    <div class="sidebar-bottom">
      <button class="btn-new-session" onclick="handleLogout()">
        <i class="bi bi-box-arrow-left me-1"></i> Cerrar Sesión
      </button>
    </div>
  `;
}

// ── TOPBAR HTML ──
function renderTopbar(pageTitle) {
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
    </div>
  `;
}

// ── INIT LAYOUT (call on each dashboard page) ──
function initLayout(activePage) {
  requireAuth();
  const sidebar = document.getElementById('sidebar');
  const topbar  = document.getElementById('topbar');
  if (sidebar) sidebar.innerHTML = renderSidebar(activePage);
  if (topbar)  topbar.innerHTML  = renderTopbar(activePage);

  // Restore print IP
  const ipEl = document.getElementById('printServerIp');
  if (ipEl) ipEl.value = localStorage.getItem('apex_print_ip') || '192.168.1.105:3000';
}
