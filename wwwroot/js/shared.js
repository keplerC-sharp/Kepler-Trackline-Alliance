/**
 * @file shared.js
 * @description Global utilities and layout rendering for the Circuit Pro system.
 * Handles notification toasts, sidebar navigation, and print server connectivity.
 */

// ── NOTIFICATION SYSTEM ───────────────────────────────────────────────────

/**
 * Displays a non-intrusive toast notification.
 * Chosen for high-visibility UI feedback without breaking user workflow.
 */
function showToast(msg, type = 'info') {
  const container = document.getElementById('toastContainer');
  if (!container) return;
  const toast = document.createElement('div');
  toast.className = `toast-item ${type}`;
  const label = type === 'success' ? 'Success' : type === 'error' ? 'Error' : 'Info';
  toast.innerHTML = `<div class="toast-label">${label}</div>${msg}`;
  container.appendChild(toast);
  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transition = 'opacity 0.3s';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// ── MOBILE NAVIGATION ─────────────────────────────────────────────────────

function toggleSidebar() {
  document.getElementById('sidebar')?.classList.toggle('mobile-open');
  document.getElementById('mobileOverlay')?.classList.toggle('active');
}
function closeSidebar() {
  document.getElementById('sidebar')?.classList.remove('mobile-open');
  document.getElementById('mobileOverlay')?.classList.remove('active');
}

// ── PRINT INFRASTRUCTURE ──────────────────────────────────────────────────

/**
 * Retrieves the configured print server address from local storage or defaults.
 */
function getPrintServerUrl() {
  const ip  = document.getElementById('printServerIp');
  const val = ip ? ip.value.trim() : (localStorage.getItem('apex_print_ip') || '192.168.1.105:3000');
  if (ip) localStorage.setItem('apex_print_ip', ip.value.trim());
  return `http://${val}`;
}

/**
 * Asynchronously dispatches a print job to the local network print server.
 * Uses a timeout to prevent hanging UI on network latency.
 */
async function sendPrintJob(btnId, payload) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.classList.add('printing');
  btn.innerHTML = '<i class="bi bi-printer print-spinner"></i> Dispatching...';
  try {
    const res  = await fetch(`${getPrintServerUrl()}/api/print/${(payload.tipo||'').toLowerCase()}`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload), signal: AbortSignal.timeout(6000)
    });
    const data = await res.json();
    if (data.ok) {
      btn.classList.remove('printing'); btn.classList.add('success-print');
      btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Printed';
      showToast('Ticket dispatched to printer', 'success');
      setTimeout(() => { 
        btn.classList.remove('success-print'); 
        btn.innerHTML = '<i class="bi bi-printer-fill"></i> Reprint'; 
      }, 4000);
    } else throw new Error(data.error || 'Server rejected job');
  } catch (err) {
    btn.classList.remove('printing'); btn.classList.add('error-print');
    btn.innerHTML = '<i class="bi bi-exclamation-triangle-fill"></i> Retry';
    showToast(`Print Error: ${err.message}`, 'error');
  }
}

/**
 * Verifies connectivity with the print server to ensure hardware readiness.
 */
async function testPrintConnection() {
  const dot   = document.getElementById('printStatusDot');
  const label = document.getElementById('printStatusLabel');
  if (!dot || !label) return;
  dot.className = 'print-status-dot'; label.textContent = 'Testing...';
  try {
    const res = await fetch(`${getPrintServerUrl()}/api/print/health`, { signal: AbortSignal.timeout(3000) });
    if (res.ok) {
      dot.className = 'print-status-dot online'; label.textContent = 'Connected';
      showToast('Print server is online', 'success');
    } else throw new Error();
  } catch {
    dot.className = 'print-status-dot offline'; label.textContent = 'Disconnected';
    showToast('Could not reach print server', 'error');
  }
}

// ── LAYOUT RENDERING ──────────────────────────────────────────────────────

/**
 * Dynamically generates the sidebar navigation menu.
 * Uses a configuration object to easily manage application routes.
 */
function renderSidebar(activePage, opName) {
    const pages = [
      { id: 'registry',    icon: 'bi-person-lines-fill', label: 'Driver Registry', href: '/Dashboard/Index'   },
      { id: 'cola',        icon: 'bi-clock-history',     label: 'Track Queue',     href: '/Queue/Index'       },
      { id: 'waitingroom', icon: 'bi-display',           label: 'Waiting Room',    href: '/Queue/WaitingRoom', target: '_blank' },
      { id: 'tiquetes',    icon: 'bi-ticket-perforated', label: 'Tickets',          href: '/Dashboard/Tickets' },
      { id: 'operators',   icon: 'bi-person-plus',       label: 'New Operator',    href: '/Auth/Register'     },
    ];
  const name     = opName || 'Operator';
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
        <a class="nav-item ${activePage === p.id ? 'active' : ''}" href="${p.href}" ${p.target ? `target="${p.target}"` : ''}>
          <i class="bi ${p.icon}"></i>${p.label}
        </a>`).join('')}
    </nav>
    <div class="sidebar-bottom">
      <a class="btn-new-session" href="/Auth/Logout">
        <i class="bi bi-box-arrow-left me-1"></i> Sign Out
      </a>
    </div>`;
}

/**
 * Renders the top navigation bar.
 * Designed to provide quick access to search and critical safety controls.
 */
function renderTopbar() {
  return `
    <button class="hamburger" onclick="toggleSidebar()">
      <i class="bi bi-list"></i>
    </button>
    <div class="topbar-brand"><span>CIRCUIT PRO: </span>PIT CONTROL</div>
    <div class="topbar-search">
      <i class="bi bi-search"></i>
      <input type="text" placeholder="Quick search...">
    </div>
    <div class="topbar-actions">
      <div class="btn-icon"><i class="bi bi-bell"></i></div>
      <div class="btn-icon"><i class="bi bi-gear"></i></div>
      <button class="btn-stop" onclick="showToast('Emergency Stop Triggered', 'error')">
        <i class="bi bi-octagon-fill me-1"></i> Emergency Stop
      </button>
    </div>`;
}

/**
 * Entry point for layout initialization.
 * Synchronizes the UI with the current authenticated user context.
 */
async function initLayout(activePage) {
  const topbar = document.getElementById('topbar');
  if (topbar) topbar.innerHTML = renderTopbar();

  const sidebar = document.getElementById('sidebar');
  if (sidebar) sidebar.innerHTML = renderSidebar(activePage, 'Operator');

  let opName = sessionStorage.getItem('apex_operator_name');
  if (!opName) {
    try {
      const res = await fetch('/Auth/Me');
      if (res.ok) {
        const data = await res.json();
        opName = data.name || 'Operator';
        sessionStorage.setItem('apex_operator_name', opName);
      }
    } catch { opName = 'Operator'; }
  }

  if (sidebar) sidebar.innerHTML = renderSidebar(activePage, opName);

  const ipEl = document.getElementById('printServerIp');
  if (ipEl) ipEl.value = localStorage.getItem('apex_print_ip') || '192.168.1.105:3000';
}
