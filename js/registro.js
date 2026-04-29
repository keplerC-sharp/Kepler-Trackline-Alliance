// ════════════════════════════════════════
// REGISTRO — registro.html
// ════════════════════════════════════════

const REGISTERED_IDS = ['ID-00042','ID-00058','ID-00011','ID-00089','ID-00033','ID-00017'];
const PILOTS = [
  { id:'ID-00042', name:'S. RICCIARDO',   vehicle:'BMW M4 Competition',  garage:'G-08', status:'HOT TRACK', hasQueue:false },
  { id:'ID-00058', name:'E. HOFFMAN',     vehicle:'AUDI RS6 Vorsprung',  garage:'G-12', status:'IN PITS',   hasQueue:false },
  { id:'ID-00011', name:'L. HAMILTON',    vehicle:'Porsche 911 GT3 RS',  garage:'G-01', status:'SCRUTINY',  hasQueue:false },
  { id:'ID-00089', name:'K. RAIKKONEN',   vehicle:'AMG GT Black Series', garage:'G-22', status:'OFF TRACK', hasQueue:false },
  { id:'ID-00033', name:'M. VANDERGRIFT', vehicle:'Ferrari 488 GT3',     garage:'G-05', status:'HOT TRACK', hasQueue:true  },
  { id:'ID-00017', name:'P. DUPONT',      vehicle:'Lamborghini Huracán', garage:'G-09', status:'IN PITS',   hasQueue:false },
];

const STATUS_TAG = {
  'HOT TRACK':'tag-red', 'IN PITS':'tag-cyan',
  'SCRUTINY':'tag-yellow', 'OFF TRACK':'tag-gray', 'VERIFIED':'tag-green'
};

let lastRegisteredPilot   = null;
let lastRegisteredVehicle = null;
let selectedPilot         = null;
let selectedDuration      = 20;
let currentTurnNum        = 6;

window.addEventListener('DOMContentLoaded', () => {
  initLayout('registry');
  renderRecentEntries();
});

// ── TABS ──
function switchTab(tab, btn) {
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  document.getElementById('tab-' + tab).classList.add('active');
  btn.classList.add('active');
}

// ── RECENT ENTRIES ──
function renderRecentEntries() {
  const el = document.getElementById('recentEntriesList');
  if (!el) return;
  document.getElementById('totalEntries').textContent = PILOTS.length;
  el.innerHTML = PILOTS.map(p => `
    <div class="driver-card" onclick="goToSearch('${p.id}')">
      <div class="driver-card-num">#${p.id.split('-')[1]}</div>
      <div class="driver-card-info">
        <div style="display:flex; align-items:center; gap:8px; flex-wrap:wrap;">
          <div class="driver-card-name">${p.name}</div>
          <span class="tag ${STATUS_TAG[p.status] || 'tag-gray'}">${p.status}</span>
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

function goToSearch(id) {
  document.querySelectorAll('.tab-btn').forEach((b,i) => { b.classList.remove('active'); if(i===2) b.classList.add('active'); });
  document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
  document.getElementById('tab-search').classList.add('active');
  setTimeout(() => selectPilot(id), 80);
}

// ── PILOT FORM VALIDATION ──
function validatePilotName(el) {
  const err = document.getElementById('pilotNameErr');
  if (el.value.trim().length < 2) {
    el.classList.add('error'); el.classList.remove('success'); err.style.display = 'flex';
  } else {
    el.classList.remove('error'); el.classList.add('success'); err.style.display = 'none';
  }
}

function validateEmail(el) {
  const err = document.getElementById('pilotEmailErr');
  const ok  = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(el.value);
  if (!ok && el.value) {
    el.classList.add('error'); el.classList.remove('success'); err.style.display = 'flex';
  } else {
    el.classList.remove('error'); if (ok) el.classList.add('success'); err.style.display = 'none';
  }
}

function checkDuplicateId(el) {
  const err = document.getElementById('driverIdErr');
  const ok  = document.getElementById('driverIdOk');
  const val = el.value.trim().toUpperCase();
  if (!val) { err.style.display='none'; ok.style.display='none'; el.classList.remove('error','success'); return; }
  if (REGISTERED_IDS.includes(val)) {
    el.classList.add('error'); el.classList.remove('success'); err.style.display='flex'; ok.style.display='none';
  } else {
    el.classList.remove('error'); el.classList.add('success'); err.style.display='none'; ok.style.display='flex';
  }
}

function verifyId() {
  const val = document.getElementById('driverId').value.trim().toUpperCase();
  if (!val) { showToast('Ingresa un Driver ID para verificar', 'error'); return; }
  const exists = REGISTERED_IDS.includes(val);
  showToast(exists ? `ID ${val} ya registrado` : `ID ${val} disponible`, exists ? 'error' : 'success');
}

function commitPilotRegistration() {
  const name = document.getElementById('pilotName').value.trim();
  const id   = document.getElementById('driverId').value.trim().toUpperCase();
  const lic  = document.getElementById('licenseClass').value;
  const email= document.getElementById('pilotEmail').value.trim();

  if (!name) { showToast('Nombre requerido', 'error'); return; }
  if (!id)   { showToast('Driver ID requerido', 'error'); return; }
  if (REGISTERED_IDS.includes(id)) { showToast('ID duplicado — registro bloqueado', 'error'); return; }
  if (!lic)  { showToast('Selecciona License Class', 'error'); return; }

  REGISTERED_IDS.push(id);
  PILOTS.push({ id, name: name.toUpperCase(), vehicle:'—', garage:'—', status:'OFF TRACK', hasQueue:false });
  lastRegisteredPilot = { nombre:name, driverId:id, licencia:lic, email };

  renderRecentEntries();
  showToast(`Piloto ${name} registrado exitosamente`, 'success');
  document.getElementById('systemStatusLog').textContent =
    `Registro completado: ${name} / ${id} / ${lic}. ${new Date().toLocaleTimeString()}`;

  document.getElementById('pilotPrintArea').style.display = 'block';
  const btn = document.getElementById('pilotPrintBtn');
  btn.classList.remove('success-print','error-print','printing');
  btn.innerHTML = '<i class="bi bi-printer-fill"></i> Imprimir Ticket Piloto';

  // Guardar en tiquetes
  saveTiquete({ tipo:'PILOTO', nombre:name, driverId:id, licencia:lic, email, fecha: new Date().toLocaleString('es-CO') });
}

function clearPilotForm() {
  ['pilotName','driverId','pilotEmail','licenseClass','pilotPhone','pilotNac'].forEach(id => {
    const el = document.getElementById(id);
    if (el) { el.value = ''; el.classList.remove('error','success'); }
  });
  ['pilotNameErr','driverIdErr','driverIdOk','pilotEmailErr'].forEach(id => {
    const el = document.getElementById(id); if (el) el.style.display = 'none';
  });
  document.getElementById('pilotPrintArea').style.display = 'none';
}

function commitVehicleRegistration() {
  const model  = document.getElementById('carModel').value.trim();
  const cat    = document.getElementById('carCategory').value;
  const vin    = document.getElementById('chassisNum').value.trim();
  const garage = document.getElementById('pitGarage').value.trim();
  if (!model) { showToast('Ingresa marca y modelo', 'error'); return; }
  if (!cat)   { showToast('Selecciona una categoría', 'error'); return; }

  lastRegisteredVehicle = { modelo:model, categoria:cat, vin:vin||'N/A', garage:garage||'N/A' };
  showToast(`Vehículo ${model} registrado`, 'success');
  document.getElementById('vehiclePrintArea').style.display = 'block';
  const btn = document.getElementById('vehiclePrintBtn');
  btn.classList.remove('success-print','error-print','printing');
  btn.innerHTML = '<i class="bi bi-printer-fill"></i> Imprimir Ticket Vehículo';
  saveTiquete({ tipo:'VEHICULO', modelo:model, categoria:cat, vin:vin||'N/A', garage:garage||'N/A', fecha:new Date().toLocaleString('es-CO') });
}

function clearVehicleForm() {
  ['carModel','chassisNum','pitGarage','carCategory','carDriverId'].forEach(id => {
    const el = document.getElementById(id); if (el) el.value = '';
  });
  document.getElementById('vehiclePrintArea').style.display = 'none';
}

// ── AUTOCOMPLETE ──
function handlePilotSearch(el) {
  const q  = el.value.trim().toLowerCase();
  const dd = document.getElementById('autocompleteDropdown');
  if (!q) { dd.classList.remove('open'); dd.innerHTML=''; return; }

  const results = PILOTS.filter(p => p.name.toLowerCase().includes(q) || p.id.toLowerCase().includes(q));
  if (!results.length) {
    dd.innerHTML = '<div class="autocomplete-item" style="color:var(--text-muted);cursor:default;">Sin resultados</div>';
    dd.classList.add('open'); return;
  }
  dd.innerHTML = results.map(p => `
    <div class="autocomplete-item" onclick="selectPilot('${p.id}')">
      <span class="ac-num">${p.id}</span>
      <span class="ac-name">${p.name}</span>
      <span class="ac-vehicle">${p.vehicle}</span>
    </div>
  `).join('');
  dd.classList.add('open');
}

document.addEventListener('click', e => {
  if (!e.target.closest('#pilotSearch') && !e.target.closest('#autocompleteDropdown')) {
    document.getElementById('autocompleteDropdown')?.classList.remove('open');
  }
});

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
      <span class="tag ${STATUS_TAG[p.status]||'tag-gray'} ms-auto">${p.status}</span>
    </div>
  `;
  document.getElementById('assignSection').style.display = p.hasQueue ? 'none' : 'block';
  document.getElementById('ticketDisplay').style.display = 'none';
  if (p.hasQueue) showToast(`${p.name} ya tiene turno activo`, 'error');
}

// ── TURN ASSIGNMENT ──
function selectDuration(btn, min) {
  document.querySelectorAll('.duration-btn').forEach(b => b.classList.remove('selected'));
  btn.classList.add('selected');
  selectedDuration = min;
}

function assignTurn() {
  if (!selectedPilot) { showToast('Selecciona un piloto primero', 'error'); return; }

  const turnNum = `T-${String(currentTurnNum++).padStart(3,'0')}`;
  const entry = {
    turnNum, pilot: selectedPilot.name, vehicle: selectedPilot.vehicle,
    duration: selectedDuration, elapsed:0, status:'pending',
    createdAt: new Date().toLocaleTimeString()
  };

  // Guardar en localStorage para la cola y tiquetes
  const queue = JSON.parse(localStorage.getItem('apex_queue') || '[]');
  queue.push(entry);
  localStorage.setItem('apex_queue', JSON.stringify(queue));

  saveTiquete({
    tipo:'TURNO', turno:turnNum, nombre:entry.pilot, vehiculo:entry.vehicle,
    duracion:entry.duration, createdAt:entry.createdAt, fecha:new Date().toLocaleString('es-CO')
  });

  selectedPilot.hasQueue = true;
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
        <div class="ticket-row"><span class="ticket-key">Creado</span><span class="ticket-val">${entry.createdAt}</span></div>
      </div>
    </div>
    <div style="margin-top:12px; display:flex; gap:8px; flex-wrap:wrap;">
      <button class="btn-print" id="turnPrintBtn" onclick='printTurnTicket(${JSON.stringify(entry).replace(/'/g,"&#39;")})'>
        <i class="bi bi-printer-fill"></i> Imprimir Ticket
      </button>
      <a href="cola.html" class="btn-outline" style="text-decoration:none;">
        <i class="bi bi-clock-history"></i> Ver Cola
      </a>
    </div>
  `;

  document.getElementById('assignSection').style.display = 'none';
  showToast(`Turno ${turnNum} asignado a ${selectedPilot.name}`, 'success');
  selectedPilot = null;
  document.getElementById('selectedPilotCard').style.display = 'none';
  document.getElementById('pilotSearch').value = '';
}

// ── PRINT ──
function printPilotTicket() {
  if (!lastRegisteredPilot) { showToast('No hay piloto registrado aún', 'error'); return; }
  sendPrintJob('pilotPrintBtn', { tipo:'piloto', ...lastRegisteredPilot });
}
function printVehicleTicket() {
  if (!lastRegisteredVehicle) { showToast('No hay vehículo registrado aún', 'error'); return; }
  sendPrintJob('vehiclePrintBtn', { tipo:'vehiculo', ...lastRegisteredVehicle });
}
function printTurnTicket(entry) {
  sendPrintJob('turnPrintBtn', { tipo:'turno', ...entry });
}

// ── TIQUETES (localStorage) ──
function saveTiquete(data) {
  const list = JSON.parse(localStorage.getItem('apex_tiquetes') || '[]');
  list.unshift({ ...data, id: Date.now() });
  localStorage.setItem('apex_tiquetes', JSON.stringify(list));
}
