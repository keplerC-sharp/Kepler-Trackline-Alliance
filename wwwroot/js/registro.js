// ════════════════════════════════════════════════════════════════
// REGISTRO — /Dashboard/Index
// Endpoints: /Dashboard/GetParticipants, /Dashboard/RegisterParticipant
//            /Dashboard/AssignToQueue, /Queue/GetActiveSession
// ════════════════════════════════════════════════════════════════

let allParticipants   = [];
let selectedPilot     = null;
let selectedDuration  = 20;
let activeSessionId   = 0;
let lastRegisteredPilot   = null;
let lastRegisteredVehicle = null;

const STATUS_TAG = { 'S': 'tag-red', 'A': 'tag-cyan', 'B': 'tag-yellow' };

window.addEventListener('DOMContentLoaded', async () => {
  await initLayout('registry');          // shared.js — carga sidebar + nombre
  await loadSessionInfo();
  await loadParticipants();
});

// ── Sesión activa ─────────────────────────────────────────────────────────
async function loadSessionInfo() {
  try {
    const res  = await fetch('/Queue/GetActiveSession');
    if (!res.ok) return;
    const data = await res.json();
    const sc   = document.getElementById('sessionCode');
    if (data && data.id) {
      activeSessionId = data.id;
      if (sc) sc.textContent = data.sessionCode;
    } else {
      if (sc) sc.textContent = 'Sin sesión activa';
    }
  } catch {
    const sc = document.getElementById('sessionCode');
    if (sc) sc.textContent = 'Sin sesión activa';
  }
}

// ── Cargar participantes ──────────────────────────────────────────────────
async function loadParticipants() {
  const el = document.getElementById('recentEntriesList');
  try {
    const res = await fetch('/Dashboard/GetParticipants');
    if (!res.ok) throw new Error('HTTP ' + res.status);
    allParticipants = await res.json();
    renderRecentEntries();
    const total = document.getElementById('totalEntries');
    if (total) total.textContent = allParticipants.length;
  } catch (e) {
    console.error('Error cargando participantes:', e);
    if (el) el.innerHTML = `<div style="color:var(--red);font-family:var(--font-mono);font-size:0.7rem;text-align:center;padding:20px;">
      Error al cargar participantes.<br>Verifica la conexión a la base de datos.</div>`;
  }
}

// ── TABS ──────────────────────────────────────────────────────────────────
function switchTab(tab, btn) {
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.getElementById('tab-' + tab)?.classList.add('active');
  btn.classList.add('active');
}

// ── Lista de participantes registrados ────────────────────────────────────
function renderRecentEntries() {
  const el = document.getElementById('recentEntriesList');
  if (!el) return;

  if (!allParticipants.length) {
    el.innerHTML = `<div style="color:var(--text-muted);font-family:var(--font-mono);font-size:0.7rem;text-align:center;padding:20px;">
      Sin participantes registrados aún</div>`;
    return;
  }

  el.innerHTML = allParticipants.map(p => `
    <div class="driver-card" onclick="goToSearch('${escHtml(p.gridId)}')">
      <div class="driver-card-num">${escHtml(p.gridId)}</div>
      <div class="driver-card-info">
        <div style="display:flex;align-items:center;gap:8px;flex-wrap:wrap;">
          <div class="driver-card-name">${escHtml(p.fullName)}</div>
          <span class="tag ${STATUS_TAG[p.grade] || 'tag-gray'}">${escHtml(p.grade)}</span>
        </div>
        <div class="driver-card-vehicle">${p.seasonPoints} pts</div>
      </div>
    </div>`).join('');
}

function escHtml(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function goToSearch(gridId) {
  document.querySelectorAll('.tab-btn').forEach((b, i) => {
    b.classList.remove('active');
    if (i === 2) b.classList.add('active');
  });
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.getElementById('tab-search')?.classList.add('active');
  setTimeout(() => selectPilotByGridId(gridId), 80);
}

// ── Validaciones ──────────────────────────────────────────────────────────
function validatePilotName(el) {
  const err = document.getElementById('pilotNameErr');
  const ok  = el.value.trim().length >= 2;
  el.classList.toggle('error', !ok);
  el.classList.toggle('success', ok);
  if (err) err.style.display = ok ? 'none' : 'flex';
}

function checkDuplicateGridId(el) {
  const err  = document.getElementById('driverIdErr');
  const okEl = document.getElementById('driverIdOk');
  const val  = el.value.trim().toUpperCase();
  if (!val) {
    if (err)  err.style.display  = 'none';
    if (okEl) okEl.style.display = 'none';
    el.classList.remove('error', 'success');
    return;
  }
  const exists = allParticipants.some(p => p.gridId === val);
  el.classList.toggle('error', exists);
  el.classList.toggle('success', !exists);
  if (err)  err.style.display  = exists ? 'flex' : 'none';
  if (okEl) okEl.style.display = exists ? 'none' : 'flex';
}

function verifyGridId() {
  const val = document.getElementById('driverId')?.value.trim().toUpperCase();
  if (!val) { showToast('Ingresa un Grid ID para verificar', 'error'); return; }
  const exists = allParticipants.some(p => p.gridId === val);
  showToast(exists ? `Grid ID ${val} ya registrado` : `Grid ID ${val} disponible`,
            exists ? 'error' : 'success');
}

// ── REGISTRAR PARTICIPANTE (sin necesidad de sesión activa) ───────────────
async function commitPilotRegistration() {
  const nameEl   = document.getElementById('pilotName');
  const gridEl   = document.getElementById('driverId');
  const gradeEl  = document.getElementById('licenseGrade');
  const pointsEl = document.getElementById('seasonPoints');

  const name   = nameEl?.value.trim() ?? '';
  const gridId = gridEl?.value.trim().toUpperCase() ?? '';
  const grade  = gradeEl?.value ?? '';
  const points = parseInt(pointsEl?.value) || 0;

  if (!name)   { showToast('Nombre requerido', 'error');            return; }
  if (!gridId) { showToast('Grid ID requerido', 'error');           return; }
  if (!grade)  { showToast('Selecciona License Grade', 'error');    return; }
  if (allParticipants.some(p => p.gridId === gridId)) {
    showToast('Grid ID duplicado — ya existe ese participante', 'error'); return;
  }

  try {
    const res = await fetch('/Dashboard/RegisterParticipant', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ fullName: name, gridId, grade, seasonPoints: points })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (!data.ok) { showToast(data.error || 'Error al registrar', 'error'); return; }

    const newPilot = { id: data.participantId, fullName: name, gridId, grade, seasonPoints: points };
    allParticipants.push(newPilot);
    lastRegisteredPilot = { nombre: name, driverId: gridId, licencia: grade };
    renderRecentEntries();

    const total = document.getElementById('totalEntries');
    if (total) total.textContent = allParticipants.length;

    // Log
    const sl = document.getElementById('systemStatusLog');
    if (sl) sl.textContent = `Registro: ${name} / ${gridId} / ${grade} — ${new Date().toLocaleTimeString()}`;

    saveTiquete({ tipo: 'PILOTO', nombre: name, driverId: gridId, licencia: grade, fecha: new Date().toLocaleString('es-CO') });

    // Mostrar botón de impresión
    const pa = document.getElementById('pilotPrintArea');
    if (pa) pa.style.display = 'block';
    const btn = document.getElementById('pilotPrintBtn');
    if (btn) { btn.classList.remove('success-print','error-print','printing'); btn.innerHTML = '<i class="bi bi-printer-fill"></i> Imprimir Ticket Piloto'; }

    clearPilotForm();

    // Si hay sesión activa, ofrecer asignar inmediatamente a la cola
    if (activeSessionId) {
      showToastWithAction(
        `${name} registrado. ¿Asignar a la cola ahora?`,
        'Asignar',
        () => assignTurnByParticipant(newPilot)
      );
    } else {
      showToast(`✓ ${name} registrado. Inicia una sesión en Cola para asignar turno.`, 'success');
    }
  } catch (err) {
    showToast('Error de conexión: ' + err.message, 'error');
  }
}

// Toast con botón de acción
function showToastWithAction(msg, btnLabel, action) {
  const container = document.getElementById('toastContainer');
  if (!container) { showToast(msg, 'success'); return; }
  const id    = 'ta_' + Date.now();
  const toast = document.createElement('div');
  toast.className = 'toast-item success';
  toast.style.cssText = 'display:flex;flex-direction:column;gap:8px;';
  toast.innerHTML = `
    <div class="toast-label">Éxito</div>
    <div>${msg}</div>
    <button id="${id}" style="background:var(--cyan);color:#000;border:none;padding:4px 10px;
      font-family:var(--font-mono);font-size:0.65rem;letter-spacing:0.1em;cursor:pointer;border-radius:2px;">
      ${btnLabel}
    </button>`;
  container.appendChild(toast);
  document.getElementById(id)?.addEventListener('click', () => { toast.remove(); action(); });
  setTimeout(() => { toast.style.opacity='0'; toast.style.transition='opacity 0.3s'; setTimeout(()=>toast.remove(),300); }, 6000);
}

function clearPilotForm() {
  ['pilotName','driverId','seasonPoints'].forEach(id => {
    const el = document.getElementById(id);
    if (el) { el.value = ''; el.classList.remove('error','success'); }
  });
  const lg = document.getElementById('licenseGrade');
  if (lg) lg.value = '';
  ['pilotNameErr','driverIdErr','driverIdOk'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.style.display = 'none';
  });
}

// ── VEHÍCULO (solo local) ─────────────────────────────────────────────────
function commitVehicleRegistration() {
  const model  = document.getElementById('carModel')?.value.trim() ?? '';
  const cat    = document.getElementById('carCategory')?.value ?? '';
  const vin    = document.getElementById('chassisNum')?.value.trim() ?? '';
  const garage = document.getElementById('pitGarage')?.value.trim() ?? '';
  if (!model) { showToast('Ingresa marca y modelo', 'error'); return; }
  if (!cat)   { showToast('Selecciona una categoría', 'error'); return; }
  lastRegisteredVehicle = { modelo: model, categoria: cat, vin: vin || 'N/A', garage: garage || 'N/A' };
  showToast(`Vehículo ${model} registrado`, 'success');
  const vpa = document.getElementById('vehiclePrintArea');
  if (vpa) vpa.style.display = 'block';
  const btn = document.getElementById('vehiclePrintBtn');
  if (btn) { btn.classList.remove('success-print','error-print','printing'); btn.innerHTML = '<i class="bi bi-printer-fill"></i> Imprimir Ticket Vehículo'; }
  saveTiquete({ tipo: 'VEHICULO', modelo: model, categoria: cat, vin: vin||'N/A', garage: garage||'N/A', fecha: new Date().toLocaleString('es-CO') });
}

function clearVehicleForm() {
  ['carModel','chassisNum','pitGarage','carCategory','carDriverId'].forEach(id => {
    const el = document.getElementById(id); if (el) el.value = '';
  });
  const vpa = document.getElementById('vehiclePrintArea');
  if (vpa) vpa.style.display = 'none';
}

// ── AUTOCOMPLETE ──────────────────────────────────────────────────────────
function handlePilotSearch(el) {
  const q  = el.value.trim().toLowerCase();
  const dd = document.getElementById('autocompleteDropdown');
  if (!dd) return;
  if (!q) { dd.classList.remove('open'); dd.innerHTML = ''; return; }

  const results = allParticipants.filter(p =>
    p.fullName.toLowerCase().includes(q) || p.gridId.toLowerCase().includes(q));

  if (!results.length) {
    dd.innerHTML = '<div class="autocomplete-item" style="color:var(--text-muted);cursor:default;">Sin resultados</div>';
    dd.classList.add('open');
    return;
  }
  dd.innerHTML = results.map(p => `
    <div class="autocomplete-item" onclick="selectPilotByGridId('${escHtml(p.gridId)}')">
      <span class="ac-num">${escHtml(p.gridId)}</span>
      <span class="ac-name">${escHtml(p.fullName)}</span>
      <span class="ac-vehicle">${escHtml(p.grade)} · ${p.seasonPoints} pts</span>
    </div>`).join('');
  dd.classList.add('open');
}

document.addEventListener('click', e => {
  if (!e.target.closest('#pilotSearch') && !e.target.closest('#autocompleteDropdown'))
    document.getElementById('autocompleteDropdown')?.classList.remove('open');
});

function selectPilotByGridId(gridId) {
  const p = allParticipants.find(x => x.gridId === gridId);
  if (!p) return;
  selectedPilot = p;

  document.getElementById('autocompleteDropdown')?.classList.remove('open');
  const ps = document.getElementById('pilotSearch');
  if (ps) ps.value = `${p.fullName} — ${p.gridId}`;

  const card = document.getElementById('selectedPilotCard');
  if (card) {
    card.style.display = 'block';
    card.innerHTML = `
      <div class="pilot-selected-card">
        <div class="psc-num">${escHtml(p.gridId)}</div>
        <div>
          <div class="psc-name">${escHtml(p.fullName)}</div>
          <div class="psc-vehicle">${p.seasonPoints} pts</div>
        </div>
        <span class="tag ${STATUS_TAG[p.grade]||'tag-gray'} ms-auto">${escHtml(p.grade)}</span>
      </div>`;
  }
  document.getElementById('assignSection')?.style && (document.getElementById('assignSection').style.display = 'block');
  document.getElementById('ticketDisplay')?.style  && (document.getElementById('ticketDisplay').style.display  = 'none');
}

// ── ASIGNAR TURNO a la cola ───────────────────────────────────────────────
async function assignTurn() {
  if (!selectedPilot) { showToast('Selecciona un participante primero', 'error'); return; }
  if (!activeSessionId) {
    showToast('No hay sesión activa. Ve a "Cola de Turnos" e inicia una sesión.', 'error');
    return;
  }
  await assignTurnByParticipant(selectedPilot);
}

async function assignTurnByParticipant(pilot) {
  if (!activeSessionId) {
    showToast('No hay sesión activa para asignar el turno.', 'error');
    return;
  }
  try {
    const res = await fetch('/Dashboard/AssignToQueue', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ participantId: pilot.id, sessionId: activeSessionId })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (!data.ok) { showToast(data.error || 'Error al asignar turno', 'error'); return; }

    // Ticket visual
    const ticketEl = document.getElementById('ticketDisplay');
    if (ticketEl) {
      ticketEl.style.display = 'block';
      ticketEl.innerHTML = `
        <div class="ticket">
          <div class="ticket-header">
            <div>
              <div style="font-family:var(--font-mono);font-size:0.58rem;letter-spacing:0.15em;color:var(--text-muted);text-transform:uppercase;">Turno Asignado</div>
              <div class="ticket-num-big">${escHtml(pilot.gridId)}</div>
            </div>
            <span class="tag tag-yellow">EN COLA</span>
          </div>
          <div class="ticket-body">
            <div class="ticket-row"><span class="ticket-key">Participante</span><span class="ticket-val">${escHtml(pilot.fullName)}</span></div>
            <div class="ticket-row"><span class="ticket-key">Grade</span><span class="ticket-val">${escHtml(pilot.grade)}</span></div>
            <div class="ticket-row"><span class="ticket-key">Posición</span><span class="ticket-val">#${data.position}</span></div>
            <div class="ticket-row"><span class="ticket-key">Sesión</span><span class="ticket-val">#${activeSessionId}</span></div>
            <div class="ticket-row"><span class="ticket-key">Hora</span><span class="ticket-val">${new Date().toLocaleTimeString()}</span></div>
          </div>
        </div>
        <div style="margin-top:12px;display:flex;gap:8px;flex-wrap:wrap;">
          <a href="/Queue/Index" class="btn-primary" style="text-decoration:none;">
            <i class="bi bi-clock-history"></i> Ver Cola
          </a>
        </div>`;
    }

    saveTiquete({
      tipo: 'TURNO', turno: pilot.gridId, nombre: pilot.fullName,
      duracion: selectedDuration, createdAt: new Date().toLocaleTimeString(),
      fecha: new Date().toLocaleString('es-CO')
    });

    showToast(`✓ Turno asignado a ${pilot.fullName} — posición #${data.position}`, 'success');

    // Limpiar selección
    selectedPilot = null;
    document.getElementById('selectedPilotCard')?.style && (document.getElementById('selectedPilotCard').style.display = 'none');
    document.getElementById('assignSection')?.style     && (document.getElementById('assignSection').style.display = 'none');
    const ps = document.getElementById('pilotSearch');
    if (ps) ps.value = '';
  } catch (err) {
    showToast('Error de conexión: ' + err.message, 'error');
  }
}

// ── Duration ──────────────────────────────────────────────────────────────
function selectDuration(btn, min) {
  document.querySelectorAll('.duration-btn').forEach(b => b.classList.remove('selected'));
  btn.classList.add('selected');
  selectedDuration = min;
}

// ── Print ─────────────────────────────────────────────────────────────────
function printPilotTicket() {
  if (!lastRegisteredPilot) { showToast('No hay piloto registrado aún', 'error'); return; }
  sendPrintJob('pilotPrintBtn', { tipo: 'piloto', ...lastRegisteredPilot });
}
function printVehicleTicket() {
  if (!lastRegisteredVehicle) { showToast('No hay vehículo registrado aún', 'error'); return; }
  sendPrintJob('vehiclePrintBtn', { tipo: 'vehiculo', ...lastRegisteredVehicle });
}

// ── localStorage tiquetes ─────────────────────────────────────────────────
function saveTiquete(data) {
  try {
    const list = JSON.parse(localStorage.getItem('apex_tiquetes') || '[]');
    list.unshift({ ...data, id: Date.now() });
    localStorage.setItem('apex_tiquetes', JSON.stringify(list));
  } catch (e) { console.warn('No se pudo guardar tiquete:', e.message); }
}
