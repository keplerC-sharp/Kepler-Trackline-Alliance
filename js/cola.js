// ════════════════════════════════════════
// COLA — cola.html
// ════════════════════════════════════════

let queueData = [];
let queueTimer = null;

window.addEventListener('DOMContentLoaded', () => {
  initLayout('cola');
  loadQueue();
  renderQueueList();
  updateQueueStats();
  updateCurrentTurn();
  startQueueSimulation();
});

// ── PERSISTENCIA ──
function loadQueue() {
  const saved = localStorage.getItem('apex_queue');
  if (saved) {
    queueData = JSON.parse(saved);
  } else {
    // datos demo si no hay nada
    queueData = [
      { turnNum:'T-001', pilot:'L. HAMILTON',    vehicle:'Porsche 911 GT3 RS', duration:20, elapsed:15, status:'active'  },
      { turnNum:'T-002', pilot:'S. RICCIARDO',   vehicle:'BMW M4 Competition', duration:15, elapsed:0,  status:'pending' },
      { turnNum:'T-003', pilot:'E. HOFFMAN',     vehicle:'AUDI RS6 Vorsprung', duration:30, elapsed:0,  status:'pending' },
      { turnNum:'T-004', pilot:'M. VANDERGRIFT', vehicle:'Ferrari 488 GT3',    duration:20, elapsed:0,  status:'pending' },
    ];
    localStorage.setItem('apex_queue', JSON.stringify(queueData));
  }
}

function saveQueue() {
  localStorage.setItem('apex_queue', JSON.stringify(queueData));
}

// ── RENDER ──
function renderQueueList() {
  const el = document.getElementById('queueList');
  if (!el) return;

  if (!queueData.length) {
    el.innerHTML = `<div style="text-align:center; padding:40px; color:var(--text-muted); font-family:var(--font-mono); font-size:0.7rem; letter-spacing:0.15em; text-transform:uppercase;">
      Cola vacía — <a href="registro.html" style="color:var(--cyan);">agregar turno</a>
    </div>`;
    return;
  }

  el.innerHTML = queueData.map((q, i) => {
    const pct = q.status === 'active'
      ? Math.min(100, (q.elapsed / (q.duration * 60)) * 100)
      : (q.status === 'done' ? 100 : 0);
    return `
      <div class="queue-item ${q.status}" style="animation-delay:${i * 0.07}s;">
        <div class="queue-num">${q.turnNum.replace('T-','')}</div>
        <div class="queue-info">
          <div class="queue-driver-name">${q.pilot}</div>
          <div class="queue-vehicle">${q.vehicle}</div>
          <div class="queue-meta">
            <span class="tag ${q.status==='active'?'tag-green':q.status==='done'?'tag-gray':'tag-yellow'}">
              ${q.status==='active'?'EN PISTA':q.status==='done'?'COMPLETADO':'PENDIENTE'}
            </span>
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
    panel.innerHTML = '<div style="text-align:center; padding:20px 0; color:var(--text-muted); font-family:var(--font-mono); font-size:0.7rem; letter-spacing:0.15em; text-transform:uppercase;">Sin turno activo</div>';
    if (timer) { timer.textContent = '--:--'; timer.className = 'big-timer'; }
    if (bar) bar.style.width = '0%';
    return;
  }

  const totalSec   = active.duration * 60;
  const remaining  = Math.max(0, totalSec - active.elapsed);
  const pct        = Math.min(100, (active.elapsed / totalSec) * 100);
  const mins       = Math.floor(remaining / 60);
  const secs       = remaining % 60;
  const timeStr    = `${String(mins).padStart(2,'0')}:${String(secs).padStart(2,'0')}`;

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

// ── ADVANCE ──
function advanceQueue() {
  const activeIdx = queueData.findIndex(q => q.status === 'active');
  if (activeIdx >= 0) queueData[activeIdx].status = 'done';

  const nextIdx = queueData.findIndex(q => q.status === 'pending');
  if (nextIdx >= 0) {
    queueData[nextIdx].status  = 'active';
    queueData[nextIdx].elapsed = 0;
    showToast(`Turno ${queueData[nextIdx].turnNum} iniciado — ${queueData[nextIdx].pilot}`, 'success');
  } else {
    showToast('Cola vacía — no hay turnos pendientes', 'error');
  }

  saveQueue();
  renderQueueList();
  updateQueueStats();
  updateCurrentTurn();
}

// ── SIMULACIÓN EN TIEMPO REAL ──
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
          next.status  = 'active';
          next.elapsed = 0;
          showToast(`Turno ${next.turnNum} iniciado — ${next.pilot}`, 'success');
        }
        saveQueue();
      }
    } else {
      const next = queueData.find(q => q.status === 'pending');
      if (next) { next.status = 'active'; next.elapsed = 0; saveQueue(); }
    }
    renderQueueList();
    updateCurrentTurn();
    updateQueueStats();
  }, 1000);
}
