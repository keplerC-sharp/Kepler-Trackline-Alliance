// ════════════════════════════════════════
// MOCK DATA
// ════════════════════════════════════════
const PILOTS = [
  { id: 'ID-00042', name: 'S. RICCIARDO',   vehicle: 'BMW M4 Competition',    garage: 'G-08', status: 'HOT TRACK', hasQueue: false },
  { id: 'ID-00058', name: 'E. HOFFMAN',     vehicle: 'AUDI RS6 Vorsprung',    garage: 'G-12', status: 'IN PITS',   hasQueue: false },
  { id: 'ID-00011', name: 'L. HAMILTON',    vehicle: 'Porsche 911 GT3 RS',    garage: 'G-01', status: 'SCRUTINY',  hasQueue: false },
  { id: 'ID-00089', name: 'K. RAIKKONEN',   vehicle: 'AMG GT Black Series',   garage: 'G-22', status: 'OFF TRACK', hasQueue: false },
  { id: 'ID-00033', name: 'M. VANDERGRIFT', vehicle: 'Ferrari 488 GT3',       garage: 'G-05', status: 'HOT TRACK', hasQueue: true  },
  { id: 'ID-00017', name: 'P. DUPONT',      vehicle: 'Lamborghini Huracán',   garage: 'G-09', status: 'IN PITS',   hasQueue: false },
];

const REGISTERED_IDS = ['ID-00042', 'ID-00058', 'ID-00011', 'ID-00089', 'ID-00033', 'ID-00017'];

let queueData = [
  { turnNum: 'T-001', pilot: 'L. HAMILTON',    vehicle: 'Porsche 911 GT3 RS', duration: 20, elapsed: 15, status: 'active'  },
  { turnNum: 'T-002', pilot: 'S. RICCIARDO',   vehicle: 'BMW M4 Competition', duration: 15, elapsed: 0,  status: 'pending' },
  { turnNum: 'T-003', pilot: 'E. HOFFMAN',     vehicle: 'AUDI RS6 Vorsprung', duration: 30, elapsed: 0,  status: 'pending' },
  { turnNum: 'T-004', pilot: 'M. VANDERGRIFT', vehicle: 'Ferrari 488 GT3',    duration: 20, elapsed: 0,  status: 'pending' },
  { turnNum: 'T-005', pilot: 'P. DUPONT',      vehicle: 'Lamborghini Huracán',duration: 10, elapsed: 0,  status: 'pending' },
];

let currentTurnNum = 6;
let selectedDuration = 20;
let selectedPilot = null;
let queueTimer = null;

// ════════════════════════════════════════
// SCREEN NAVIGATION
// ════════════════════════════════════════
function showScreen(id) {
  document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
  document.getElementById(id).classList.add('active');
}

function handleLogin() {
  const id   = document.getElementById('loginId').value.trim();
  const pass = document.getElementById('loginPass').value.trim();

  if (!id || !pass) {
    showToast('Ingresa Marshal ID y Access Key', 'error');
    return;
  }

  const overlay = document.getElementById('loadingOverlay');
  overlay.classList.add('active');

  setTimeout(() => {
    overlay.classList.remove('active');
    showScreen('screen-dashboard');
    initDashboard();
    showToast('Acceso concedido — Bienvenido', 'success');
  }, 1400);
}

function handleLogout() {
  showScreen('screen-login');
  document.getElementById('loginId').value = '';
  document.getElementById('loginPass').value = '';
  if (queueTimer) clearInterval(queueTimer);
}

// Enter key on login
document.addEventListener('keydown', e => {
  if (e.key === 'Enter' && document.getElementById('screen-login').classList.contains('active')) {
    handleLogin();
  }
});

// ════════════════════════════════════════
// DASHBOARD INIT
// ════════════════════════════════════════
function initDashboard() {
  renderRecentEntries();
  renderQueueList();
  startSessionTimer();
  startQueueSimulation();
  updateQueueStats();
  updateCurrentTurn();
}

// ════════════════════════════════════════
// PAGE SWITCHING
// ════════════════════════════════════════
function switchPage(page) {
  document.querySelectorAll('.page').forEach(p => p.style.display = 'none');
  document.getElementById(`page-${page}`).style.display = 'block';
  document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));

  const map = { race: 0, pit: 1, registry: 2, queue: 3, heat: 4, telemetry: 5 };
  const items = document.querySelectorAll('.nav-item');
  if (items[map[page]]) items[map[page]].classList.add('active');

  closeSidebar();
  if (page === 'queue') { renderQueueList(); updateQueueStats(); updateCurrentTurn(); }
}

// ════════════════════════════════════════
// TAB SWITCHING (Registry)
// ════════════════════════════════════════
function switchTab(tab) {
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.getElementById(`tab-${tab}`).classList.add('active');
  event.target.classList.add('active');
}

// ════════════════════════════════════════
// RECENT ENTRIES
// ════════════════════════════════════════
const statusTagClass = { 'HOT TRACK': 'tag-red', 'IN PITS': 'tag-cyan', 'SCRUTINY': 'tag-yellow', 'OFF TRACK': 'tag-gray', 'VERIFIED': 'tag-green' };

function renderRecentEntries() {
  const el = document.getElementById('recentEntriesList');
  el.innerHTML = PILOTS.map((p, i) => `
    <div class="driver-card" onclick="selectPilotFromEntry('${p.id}')">
      <div class="driver-card-num" style="color:var(--text);">#${p.id.split('-')[1]}</div>
      <div class="driver-card-info">
        <div style="display:flex; align-items:center; gap:8px; flex-wrap:wrap;">
          <div class="driver-card-name">${p.name}</div>
          <span class="tag ${statusTagClass[p.status] || 'tag-gray'}">${p.status}</span>
        </div>
        <div class="driver-card-vehicle">${p.vehicle}</div>
        <div class="driver-card-meta">
          <span>GARAGE: ${p.garage}</span>
          <span>STATUS: ${p.hasQueue ? '<span style="color:var(--red)">HAS QUEUE</span>' : 'VERIFIED'}</span>
        </div>
      </div>
    </div>
  `).join('');
}

// ════════════════════════════════════════
// PILOT FORM VALIDATION
// ════════════════════════════════════════
function validatePilotName(el) {
  const err = document.getElementById('pilotNameErr');
  if (el.value.trim().length < 2) {
    el.classList.add('error'); el.classList.remove('success');
    err.style.display = 'flex';
  } else {
    el.classList.remove('error'); el.classList.add('success');
    err.style.display = 'none';
  }
}

function validateEmail(el) {
  const err = document.getElementById('pilotEmailErr');
  const valid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(el.value);
  if (!valid && el.value) {
    el.classList.add('error'); el.classList.remove('success');
    err.style.display = 'flex';
  } else {
    el.classList.remove('error'); if (valid) el.classList.add('success');
    err.style.display = 'none';
  }
}

function checkDuplicateId(el) {
  const err = document.getElementById('driverIdErr');
  const ok  = document.getElementById('driverIdOk');
  const val = el.value.trim().toUpperCase();
  if (!val) { err.style.display='none'; ok.style.display='none'; el.classList.remove('error','success'); return; }

  if (REGISTERED_IDS.includes(val)) {
    el.classList.add('error'); el.classList.remove('success');
    err.style.display = 'flex'; ok.style.display = 'none';
  } else {
    el.classList.remove('error'); el.classList.add('success');
    err.style.display = 'none'; ok.style.display = 'flex';
  }
}

function verifyId() {
  const val = document.getElementById('driverId').value.trim().toUpperCase();
  if (!val) { showToast('Ingresa un Driver ID para verificar', 'error'); return; }
  const exists = REGISTERED_IDS.includes(val);
  showToast(exists ? `ID ${val} ya registrado en base de datos` : `ID ${val} disponible para registro`, exists ? 'error' : 'success');
  updateSystemLog(`Verificación de ID ${val}: ${exists ? 'DUPLICADO DETECTADO' : 'disponible'}. ${new Date().toLocaleTimeString()}`);
}

function commitPilotRegistration() {
  const name  = document.getElementById('pilotName').value.trim();
  const idVal = document.getElementById('driverId').value.trim().toUpperCase();
  const lic   = document.getElementById('licenseClass').value;
  const email = document.getElementById('pilotEmail').value.trim();

  if (!name)  { showToast('Nombre requerido', 'error'); return; }
  if (!idVal) { showToast('Driver ID requerido', 'error'); return; }
  if (REGISTERED_IDS.includes(idVal)) { showToast('ID duplicado — registro bloqueado', 'error'); return; }
  if (!lic)   { showToast('Selecciona License Class', 'error'); return; }

  // Save
  REGISTERED_IDS.push(idVal);
  const newPilot = { id: idVal, name: name.toUpperCase(), vehicle: '—', garage: '—', status: 'OFF TRACK', hasQueue: false };
  PILOTS.push(newPilot);
  lastRegisteredPilot = { nombre: name, driverId: idVal, licencia: lic, email };
  document.getElementById('totalEntries').textContent = parseInt(document.getElementById('totalEntries').textContent) + 1;
  renderRecentEntries();
  showToast(`Piloto ${name} registrado exitosamente`, 'success');
  updateSystemLog(`Registro completado: ${name} / ${idVal} / ${lic}. ${new Date().toLocaleTimeString()}`);

  // Show print button
  document.getElementById('pilotPrintArea').style.display = 'block';
  const btn = document.getElementById('pilotPrintBtn');
  btn.classList.remove('success-print','error-print','printing');
  btn.innerHTML = '<i class="bi bi-printer-fill"></i> Imprimir Ticket Piloto';
}

function commitVehicleRegistration() {
  const model = document.getElementById('carModel').value.trim();
  const cat   = document.getElementById('carCategory').value;
  const vin   = document.getElementById('chassisNum').value.trim();
  const garage= document.getElementById('pitGarage').value.trim();
  if (!model) { showToast('Ingresa marca y modelo del vehículo', 'error'); return; }
  if (!cat)   { showToast('Selecciona una categoría', 'error'); return; }
  lastRegisteredVehicle = { modelo: model, categoria: cat, vin: vin || 'N/A', garage: garage || 'N/A' };
  showToast(`Vehículo ${model} (${cat}) registrado`, 'success');
  updateSystemLog(`Vehículo registrado: ${model} — Categoría ${cat}. ${new Date().toLocaleTimeString()}`);

  // Show print button
  document.getElementById('vehiclePrintArea').style.display = 'block';
  const btn = document.getElementById('vehiclePrintBtn');
  btn.classList.remove('success-print','error-print','printing');
  btn.innerHTML = '<i class="bi bi-printer-fill"></i> Imprimir Ticket Vehículo';
}

// ════════════════════════════════════════
// PRINT FUNCTIONS
// ════════════════════════════════════════
let lastRegisteredPilot  = null;
let lastRegisteredVehicle = null;

function getPrintServerUrl() {
  const ip = document.getElementById('printServerIp');
  const val = ip ? ip.value.trim() : '192.168.1.105:3000';
  return `http://${val}`;
}

async function sendPrintJob(btnId, payload) {
  const btn = document.getElementById(btnId);
  if (!btn) return;
  btn.classList.add('printing');
  btn.innerHTML = '<i class="bi bi-printer print-spinner"></i> Enviando...';

  try {
    const res = await fetch(`${getPrintServerUrl()}/print`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal: AbortSignal.timeout(6000)
    });
    const data = await res.json();
    if (data.ok) {
      btn.classList.remove('printing');
      btn.classList.add('success-print');
      btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Impreso correctamente';
      showToast('Ticket enviado a la impresora', 'success');
      setTimeout(() => {
        btn.classList.remove('success-print');
        btn.innerHTML = '<i class="bi bi-printer-fill"></i> Reimprimir';
      }, 4000);
    } else {
      throw new Error(data.error || 'Error del servidor');
    }
  } catch (err) {
    btn.classList.remove('printing');
    btn.classList.add('error-print');
    btn.innerHTML = '<i class="bi bi-exclamation-triangle-fill"></i> Error — Reintentar';
    showToast(`Error de impresión: ${err.message}`, 'error');
    console.error('Print error:', err);
  }
}

function printPilotTicket() {
  if (!lastRegisteredPilot) { showToast('No hay piloto registrado aún', 'error'); return; }
  sendPrintJob('pilotPrintBtn', {
    tipo: 'PILOTO',
    nombre: lastRegisteredPilot.nombre,
    driverId: lastRegisteredPilot.driverId,
    licencia: lastRegisteredPilot.licencia,
    email: lastRegisteredPilot.email,
    fecha: new Date().toLocaleString('es-CO')
  });
}

function printVehicleTicket() {
  if (!lastRegisteredVehicle) { showToast('No hay vehículo registrado aún', 'error'); return; }
  sendPrintJob('vehiclePrintBtn', {
    tipo: 'VEHICULO',
    modelo: lastRegisteredVehicle.modelo,
    categoria: lastRegisteredVehicle.categoria,
    vin: lastRegisteredVehicle.vin,
    garage: lastRegisteredVehicle.garage,
    fecha: new Date().toLocaleString('es-CO')
  });
}

function printTurnTicket(entry) {
  sendPrintJob('turnPrintBtn', {
    tipo: 'TURNO',
    turno: entry.turnNum,
    nombre: entry.pilot,
    vehiculo: entry.vehicle,
    duracion: entry.duration,
    createdAt: entry.createdAt,
    fecha: new Date().toLocaleString('es-CO')
  });
}

async function testPrintConnection() {
  const dot   = document.getElementById('printStatusDot');
  const label = document.getElementById('printStatusLabel');
  dot.className = 'print-status-dot';
  label.textContent = 'Probando...';
  try {
    const res = await fetch(`${getPrintServerUrl()}/health`, { signal: AbortSignal.timeout(3000) });
    if (res.ok) {
      dot.className = 'print-status-dot online';
      label.textContent = 'Conectado';
      showToast('Servidor de impresión online', 'success');
    } else { throw new Error('HTTP ' + res.status); }
  } catch {
    dot.className = 'print-status-dot offline';
    label.textContent = 'Sin conexión';
    showToast('No se pudo conectar al servidor de impresión', 'error');
  }
}

function clearPilotForm() {
  ['pilotName','driverId','pilotEmail','licenseClass'].forEach(id => {
    const el = document.getElementById(id);
    if (el) { el.value = ''; el.classList.remove('error','success'); }
  });
  ['pilotNameErr','driverIdErr','driverIdOk','pilotEmailErr'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.style.display = 'none';
  });
  document.getElementById('pilotPrintArea').style.display = 'none';
}

function clearVehicleForm() {
  ['carModel','chassisNum','pitGarage','carCategory'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.value = '';
  });
  document.getElementById('vehiclePrintArea').style.display = 'none';
}

// ════════════════════════════════════════
// PILOT SEARCH / AUTOCOMPLETE
// ════════════════════════════════════════
function handlePilotSearch(el) {
  const q = el.value.trim().toLowerCase();
  const dd = document.getElementById('autocompleteDropdown');

  if (!q) { dd.classList.remove('open'); dd.innerHTML = ''; return; }

  const results = PILOTS.filter(p =>
    p.name.toLowerCase().includes(q) || p.id.toLowerCase().includes(q)
  );

  if (!results.length) { dd.innerHTML = '<div class="autocomplete-item" style="color:var(--text-muted); cursor:default;">Sin resultados</div>'; dd.classList.add('open'); return; }

  dd.innerHTML = results.map(p => `
    <div class="autocomplete-item" onclick="selectPilot('${p.id}')">
      <span class="ac-num">${p.id}</span>
      <span class="ac-name">${p.name}</span>
      <span class="ac-vehicle">${p.vehicle}</span>
    </div>
  `).join('');
  dd.classList.add('open');
}

function selectPilotFromEntry(id) {
  switchTab({ target: document.querySelectorAll('.tab-btn')[2] });
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.querySelectorAll('.tab-btn')[2].classList.add('active');
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.getElementById('tab-search').classList.add('active');
  setTimeout(() => selectPilot(id), 100);
}

function selectPilot(id) {
  const p = PILOTS.find(x => x.id === id);
  if (!p) return;
  selectedPilot = p;
  document.getElementById('autocompleteDropdown').classList.remove('open');
  document.getElementById('pilotSearch').value = `${p.name} — ${p.id}`;

  const card = document.getElementById('selectedPilotCard');
  card.style.display = 'block';
  card.innerHTML = `
    <div class="pilot-selected-card">
      <div class="psc-num">#${p.id.split('-')[1]}</div>
      <div>
        <div class="psc-name">${p.name}</div>
        <div class="psc-vehicle">${p.vehicle}</div>
        ${p.hasQueue ? '<div class="psc-alert"><i class="bi bi-exclamation-triangle-fill"></i> Piloto ya tiene turno asignado</div>' : ''}
      </div>
      <span class="tag ${statusTagClass[p.status] || 'tag-gray'} ms-auto">${p.status}</span>
    </div>
  `;

  document.getElementById('assignSection').style.display = p.hasQueue ? 'none' : 'block';
  document.getElementById('ticketDisplay').style.display = 'none';

  if (p.hasQueue) {
    showToast(`${p.name} ya tiene turno activo`, 'error');
  }
}

// ════════════════════════════════════════
// TURN ASSIGNMENT
// ════════════════════════════════════════
function selectDuration(btn, min) {
  document.querySelectorAll('.duration-btn').forEach(b => b.classList.remove('selected'));
  btn.classList.add('selected');
  selectedDuration = min;
}

function assignTurn() {
  if (!selectedPilot) { showToast('Selecciona un piloto primero', 'error'); return; }

  const turnNum = `T-${String(currentTurnNum++).padStart(3,'0')}`;
  const entry = {
    turnNum,
    pilot: selectedPilot.name,
    vehicle: selectedPilot.vehicle,
    duration: selectedDuration,
    elapsed: 0,
    status: 'pending',
    createdAt: new Date().toLocaleTimeString()
  };

  queueData.push(entry);
  selectedPilot.hasQueue = true;
  renderQueueList();
  updateQueueStats();
  renderRecentEntries();

  const ticketEl = document.getElementById('ticketDisplay');
  ticketEl.style.display = 'block';
  ticketEl.innerHTML = `
    <div class="ticket">
      <div class="ticket-header">
        <div>
          <div style="font-family:var(--font-mono); font-size:0.58rem; letter-spacing:0.15em; color:var(--text-muted); text-transform:uppercase;">Turno Asignado</div>
          <div class="ticket-num-big">${turnNum}</div>
        </div>
        <span class="tag tag-yellow">PENDIENTE</span>
      </div>
      <div class="ticket-body">
        <div class="ticket-row"><span class="ticket-key">Piloto</span><span class="ticket-val">${entry.pilot}</span></div>
        <div class="ticket-row"><span class="ticket-key">Vehículo</span><span class="ticket-val">${entry.vehicle}</span></div>
        <div class="ticket-row"><span class="ticket-key">Duración</span><span class="ticket-val">${selectedDuration} min</span></div>
        <div class="ticket-row"><span class="ticket-key">Posición</span><span class="ticket-val">#${queueData.filter(q=>q.status==='pending').length} en cola</span></div>
        <div class="ticket-row"><span class="ticket-key">Creado</span><span class="ticket-val">${entry.createdAt}</span></div>
      </div>
    </div>
    <div style="margin-top:12px;">
      <button class="btn-print" id="turnPrintBtn" onclick="printTurnTicket(${JSON.stringify(entry).replace(/"/g,'&quot;')})">
        <i class="bi bi-printer-fill"></i> Imprimir Ticket de Turno
      </button>
    </div>
  `;

  document.getElementById('assignSection').style.display = 'none';
  showToast(`Turno ${turnNum} asignado a ${selectedPilot.name}`, 'success');
  updateSystemLog(`Turno ${turnNum} creado para ${selectedPilot.name} — ${selectedDuration} min. PENDIENTE. ${new Date().toLocaleTimeString()}`);
  selectedPilot = null;
  document.getElementById('selectedPilotCard').style.display = 'none';
  document.getElementById('pilotSearch').value = '';
}

// ════════════════════════════════════════
// QUEUE RENDERING
// ════════════════════════════════════════
function renderQueueList() {
  const el = document.getElementById('queueList');
  if (!el) return;

  el.innerHTML = queueData.map((q, i) => {
    const pct = q.status === 'active' ? Math.min(100, (q.elapsed / (q.duration * 60)) * 100) : (q.status === 'done' ? 100 : 0);
    const delayStyle = `animation-delay: ${i * 0.07}s;`;
    return `
      <div class="queue-item ${q.status}" style="${delayStyle}">
        <div class="queue-num">${q.turnNum.replace('T-','')}</div>
        <div class="queue-info">
          <div class="queue-driver-name">${q.pilot}</div>
          <div class="queue-vehicle">${q.vehicle}</div>
          <div class="queue-meta">
            <span class="tag ${q.status==='active'?'tag-green':q.status==='done'?'tag-gray':'tag-yellow'}">${q.status==='active'?'EN PISTA':q.status==='done'?'COMPLETADO':'PENDIENTE'}</span>
            <span class="queue-time">${q.duration} min</span>
          </div>
        </div>
        <div class="queue-progress">
          <div class="progress-bar-wrap">
            <div class="progress-bar-fill" style="width:${pct}%;"></div>
          </div>
          <div class="queue-status-text">${Math.round(pct)}% completado</div>
        </div>
      </div>
    `;
  }).join('');
}

function updateQueueStats() {
  const pending = queueData.filter(q => q.status === 'pending').length;
  const active  = queueData.filter(q => q.status === 'active').length;
  const done    = queueData.filter(q => q.status === 'done').length;
  const sp = document.getElementById('statPending');
  const sa = document.getElementById('statActive');
  const sd = document.getElementById('statDone');
  if (sp) sp.textContent = pending;
  if (sa) sa.textContent = active;
  if (sd) sd.textContent = done;
}

function updateCurrentTurn() {
  const active = queueData.find(q => q.status === 'active');
  const panel  = document.getElementById('currentTurnPanel');
  const timer  = document.getElementById('bigTimer');
  const bar    = document.getElementById('mainProgressBar');
  if (!panel) return;

  if (!active) {
    panel.innerHTML = '<div style="text-align:center; padding: 20px 0; color: var(--text-muted); font-family: var(--font-mono); font-size:0.7rem; letter-spacing:0.15em; text-transform:uppercase;">Sin turno activo</div>';
    if (timer) { timer.textContent = '--:--'; timer.className = 'big-timer'; }
    if (bar) bar.style.width = '0%';
    return;
  }

  const totalSec = active.duration * 60;
  const remaining = Math.max(0, totalSec - active.elapsed);
  const pct = Math.min(100, (active.elapsed / totalSec) * 100);
  const mins = Math.floor(remaining / 60);
  const secs = remaining % 60;
  const timeStr = `${String(mins).padStart(2,'0')}:${String(secs).padStart(2,'0')}`;

  panel.innerHTML = `
    <div style="text-align:center;">
      <div style="font-family:var(--font-display); font-size:1.5rem; font-weight:900; text-transform:uppercase;">${active.pilot}</div>
      <div style="color:var(--text-dim); font-size:0.8rem; margin-top:4px; text-transform:uppercase;">${active.vehicle}</div>
      <div style="margin-top:12px;"><span class="tag tag-green">${active.turnNum}</span></div>
    </div>
  `;

  if (timer) {
    timer.textContent = timeStr;
    timer.className = 'big-timer' + (pct > 85 ? ' critical' : pct > 70 ? ' warning' : '');
  }
  if (bar) bar.style.width = pct + '%';
}

function advanceQueue() {
  const activeIdx = queueData.findIndex(q => q.status === 'active');
  if (activeIdx >= 0) { queueData[activeIdx].status = 'done'; }

  const nextIdx = queueData.findIndex(q => q.status === 'pending');
  if (nextIdx >= 0) {
    queueData[nextIdx].status = 'active';
    queueData[nextIdx].elapsed = 0;
    showToast(`Turno ${queueData[nextIdx].turnNum} iniciado — ${queueData[nextIdx].pilot}`, 'success');
  } else {
    showToast('Cola vacía — no hay turnos pendientes', 'error');
  }

  renderQueueList();
  updateQueueStats();
  updateCurrentTurn();
}

const mockNames = ['C. ALBON','V. BOTTAS','N. HULKENBERG','A. ALONSO','O. PIASTRI','Y. TSUNODA'];
let mockIdx = 0;
function addMockEntry() {
  const name = mockNames[mockIdx % mockNames.length];
  mockIdx++;
  const entry = {
    turnNum: `T-${String(currentTurnNum++).padStart(3,'0')}`,
    pilot: name,
    vehicle: 'Track Day Vehicle',
    duration: [10,15,20][Math.floor(Math.random()*3)],
    elapsed: 0,
    status: 'pending'
  };
  queueData.push(entry);
  renderQueueList();
  updateQueueStats();
  showToast(`Turno ${entry.turnNum} agregado — ${name}`, 'success');
}

// ════════════════════════════════════════
// QUEUE SIMULATION (SignalR mock)
// ════════════════════════════════════════
function startQueueSimulation() {
  if (queueTimer) clearInterval(queueTimer);
  queueTimer = setInterval(() => {
    const active = queueData.find(q => q.status === 'active');
    if (active) {
      active.elapsed += 1;
      if (active.elapsed >= active.duration * 60) {
        active.status = 'done';
        const next = queueData.find(q => q.status === 'pending');
        if (next) {
          next.status = 'active';
          showToast(`Turno ${next.turnNum} iniciado automáticamente — ${next.pilot}`, 'success');
        }
      }
    } else {
      const next = queueData.find(q => q.status === 'pending');
      if (next) { next.status = 'active'; }
    }

    renderQueueList();
    updateCurrentTurn();
    updateQueueStats();
  }, 1000);
}

// ════════════════════════════════════════
// SESSION TIMER
// ════════════════════════════════════════
let sessionSec = 4364;
function startSessionTimer() {
  setInterval(() => {
    sessionSec++;
    const h = Math.floor(sessionSec / 3600);
    const m = Math.floor((sessionSec % 3600) / 60);
    const s = sessionSec % 60;
    const el = document.getElementById('sessionTimer');
    if (el) el.innerHTML = `${String(h).padStart(2,'0')}:${String(m).padStart(2,'0')}:${String(s).padStart(2,'0')}<span>.0${Math.floor(Math.random()*9)}</span>`;
  }, 1000);
}

// ════════════════════════════════════════
// SYSTEM LOG
// ════════════════════════════════════════
function updateSystemLog(msg) {
  const el = document.getElementById('systemStatusLog');
  if (el) el.textContent = msg;
}

// ════════════════════════════════════════
// TOASTS
// ════════════════════════════════════════
function showToast(msg, type = 'info') {
  const container = document.getElementById('toastContainer');
  const toast = document.createElement('div');
  toast.className = `toast-item ${type}`;
  const label = type === 'success' ? 'Éxito' : type === 'error' ? 'Error' : 'Info';
  toast.innerHTML = `<div class="toast-label">${label}</div>${msg}`;
  container.appendChild(toast);
  setTimeout(() => { toast.style.opacity = '0'; toast.style.transition = 'opacity 0.3s'; setTimeout(() => toast.remove(), 300); }, 3500);
}

// ════════════════════════════════════════
// MOBILE SIDEBAR
// ════════════════════════════════════════
function toggleSidebar() {
  const sb = document.getElementById('sidebar');
  const ov = document.getElementById('mobileOverlay');
  sb.classList.toggle('mobile-open');
  ov.classList.toggle('active');
}
function closeSidebar() {
  document.getElementById('sidebar').classList.remove('mobile-open');
  document.getElementById('mobileOverlay').classList.remove('active');
}

// Close autocomplete on outside click
document.addEventListener('click', e => {
  if (!e.target.closest('#pilotSearch') && !e.target.closest('#autocompleteDropdown')) {
    const dd = document.getElementById('autocompleteDropdown');
    if (dd) dd.classList.remove('open');
  }
});
