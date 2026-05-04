// ════════════════════════════════════════
// COLA — /Queue/Index
// ════════════════════════════════════════

let sessionId        = 0;
let queueTimer       = null;
let timerTick        = null;
let currentOnTrackId = null;

window.addEventListener('DOMContentLoaded', async () => {
  await initLayout('cola');   // shared.js — async, espera el nombre del operador

  const sid = document.getElementById('activeSessionId');
  if (sid) sessionId = parseInt(sid.value) || 0;

  if (sessionId > 0) {
    await loadQueue();
    await loadStats();
    queueTimer = setInterval(async () => {
      await loadQueue();
      await loadStats();
    }, 5000);
  }
});

// ── Cola ──────────────────────────────────────────────────────────────────
async function loadQueue() {
  if (!sessionId) return;
  try {
    const res = await fetch(`/Queue/GetQueue?sessionId=${sessionId}`);
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    renderQueueList(data);
    updateCurrentTurn(data);
    updateUpNext(data);
    updateStatsFromQueue(data);
  } catch (err) {
    console.error('Error cargando cola:', err);
  }
}

// ── Stats (completados/cancelados vienen del servidor) ────────────────────
async function loadStats() {
  if (!sessionId) return;
  try {
    const res = await fetch(`/Queue/GetStats?sessionId=${sessionId}`);
    if (!res.ok) return;
    const data = await res.json();
    const el = document.getElementById('statDone');
    if (el) el.textContent = data.completed ?? 0;
  } catch { /* silencioso */ }
}

// ── Render lista ──────────────────────────────────────────────────────────
function renderQueueList(entries) {
  const el = document.getElementById('queueList');
  if (!el) return;

  const visible = entries.filter(e => e.status !== 'COMPLETED' && e.status !== 'CANCELLED');

  if (!visible.length) {
    el.innerHTML = `
      <div style="text-align:center;padding:50px 20px;color:var(--text-muted);
        font-family:var(--font-mono);font-size:0.7rem;letter-spacing:0.15em;text-transform:uppercase;">
        Cola vacía —
        <a href="/Dashboard/Index" style="color:var(--cyan);">agregar participante</a>
      </div>`;
    return;
  }

  el.innerHTML = visible.map((q, i) => {
    const isOn   = q.status === 'ON_TRACK';
    const isNext = q.status === 'UP_NEXT';
    const cls    = isOn ? 'active' : isNext ? 'upnext' : 'pending';
    const tagCls = isOn ? 'tag-green' : isNext ? 'tag-cyan' : 'tag-yellow';
    const tagTxt = isOn ? 'EN PISTA'  : isNext ? 'PRÓXIMO'  : 'PENDIENTE';
    const prio   = q.participant?.grade === 'S'
      ? '<span class="tag tag-red" style="font-size:0.55rem;margin-left:4px;">PRIO</span>' : '';

    return `
      <div class="queue-item ${cls}" style="animation-delay:${i * 0.06}s;">
        <div class="queue-num">${String(q.position).padStart(2,'0')}</div>
        <div class="queue-info">
          <div class="queue-driver-name">
            ${esc(q.participant?.fullName ?? '—')} ${prio}
          </div>
          <div class="queue-vehicle">
            ${esc(q.participant?.gridId ?? '—')} ·
            Grade ${esc(q.participant?.grade ?? '?')} ·
            ${q.participant?.seasonPoints ?? 0} pts
          </div>
          <div class="queue-meta">
            <span class="tag ${tagCls}">${tagTxt}</span>
          </div>
        </div>
        ${!isOn ? `
          <button class="btn-icon" onclick="cancelEntry(${q.id})" title="Cancelar turno">
            <i class="bi bi-x-circle" style="color:var(--red);"></i>
          </button>` : ''}
      </div>`;
  }).join('');
}

function esc(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

// ── Turno actual + timer ──────────────────────────────────────────────────
function updateCurrentTurn(entries) {
  const active = entries.find(e => e.status === 'ON_TRACK');
  const panel  = document.getElementById('currentTurnPanel');
  const timer  = document.getElementById('bigTimer');
  const bar    = document.getElementById('mainProgressBar');
  if (!panel) return;

  if (!active) {
    panel.innerHTML = `
      <div style="text-align:center;padding:20px 0;color:var(--text-muted);
        font-family:var(--font-mono);font-size:0.7rem;letter-spacing:0.15em;text-transform:uppercase;">
        Sin turno activo
      </div>`;
    if (timer) { timer.textContent = '--:--'; timer.className = 'big-timer'; }
    if (bar)   bar.style.width = '0%';
    clearInterval(timerTick); timerTick = null; currentOnTrackId = null;
    return;
  }

  panel.innerHTML = `
    <div style="text-align:center;">
      <div style="font-family:var(--font-display);font-size:1.5rem;font-weight:900;text-transform:uppercase;">
        ${esc(active.participant?.fullName ?? '—')}
      </div>
      <div style="color:var(--text-dim);font-size:0.85rem;margin-top:4px;text-transform:uppercase;">
        ${esc(active.participant?.gridId ?? '—')} · Grade ${esc(active.participant?.grade ?? '?')}
      </div>
      <div style="margin-top:12px;display:flex;justify-content:center;gap:8px;flex-wrap:wrap;">
        <span class="tag tag-green">POSICIÓN #${active.position}</span>
        ${active.participant?.grade === 'S'
          ? '<span class="tag tag-red">PRIORIDAD</span>' : ''}
      </div>
    </div>`;

  // Reiniciar timer si cambia el turno activo
  if (currentOnTrackId !== active.id) {
    clearInterval(timerTick); timerTick = null;
    currentOnTrackId = active.id;
  }

  if (active.startedAt && !timerTick) {
    const startMs = new Date(active.startedAt).getTime();
    timerTick = setInterval(() => {
      const elapsed = Math.floor((Date.now() - startMs) / 1000);
      const m = String(Math.floor(elapsed / 60)).padStart(2,'0');
      const s = String(elapsed % 60).padStart(2,'0');
      if (timer) {
        timer.textContent = `${m}:${s}`;
        timer.className   = 'big-timer' +
          (elapsed > 2400 ? ' critical' : elapsed > 1500 ? ' warning' : '');
      }
      if (bar) bar.style.width = Math.min(100, (elapsed / 3600) * 100) + '%';
    }, 1000);
  }
}

// ── Próximo en cola ───────────────────────────────────────────────────────
function updateUpNext(entries) {
  const panel = document.getElementById('upNextPanel');
  if (!panel) return;
  const next = entries.find(e => e.status === 'UP_NEXT')
            ?? entries.find(e => e.status === 'QUEUED');
  if (!next) {
    panel.innerHTML = `<div style="text-align:center;padding:10px 0;color:var(--text-muted);
      font-family:var(--font-mono);font-size:0.65rem;letter-spacing:0.12em;">—</div>`;
    return;
  }
  panel.innerHTML = `
    <div style="display:flex;align-items:center;gap:12px;">
      <div style="font-family:var(--font-display);font-size:1.8rem;font-weight:900;
        color:var(--cyan);min-width:40px;">${String(next.position).padStart(2,'0')}</div>
      <div>
        <div style="font-family:var(--font-display);font-size:1rem;font-weight:700;text-transform:uppercase;">
          ${esc(next.participant?.fullName ?? '—')}
        </div>
        <div style="font-family:var(--font-mono);font-size:0.65rem;color:var(--text-dim);">
          ${esc(next.participant?.gridId ?? '—')} · Grade ${esc(next.participant?.grade ?? '?')}
        </div>
      </div>
      <span class="tag tag-cyan ms-auto">${next.status === 'UP_NEXT' ? 'PRÓXIMO' : 'EN COLA'}</span>
    </div>`;
}

// ── Stats en tiempo real desde la cola ───────────────────────────────────
function updateStatsFromQueue(entries) {
  const sp = document.getElementById('statPending');
  const sa = document.getElementById('statActive');
  if (sp) sp.textContent = entries.filter(e => e.status === 'QUEUED').length;
  if (sa) sa.textContent = entries.filter(e => e.status === 'ON_TRACK').length;
}

// ── Avanzar cola ──────────────────────────────────────────────────────────
async function advanceQueue() {
  if (!sessionId) { showToast('No hay sesión activa', 'error'); return; }
  try {
    const res = await fetch('/Queue/Advance', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ sessionId })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (data.ok) {
      clearInterval(timerTick); timerTick = null; currentOnTrackId = null;
      showToast('Cola avanzada', 'success');
      await loadQueue();
      await loadStats();
    } else {
      showToast(data.error || 'Error al avanzar', 'error');
    }
  } catch (err) {
    showToast('Error de conexión: ' + err.message, 'error');
  }
}

// ── Cancelar entrada ──────────────────────────────────────────────────────
async function cancelEntry(entryId) {
  if (!confirm('¿Cancelar este turno?')) return;
  try {
    const res = await fetch('/Queue/Cancel', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ entryId })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (data.ok) {
      showToast('Turno cancelado', 'info');
      await loadQueue();
      await loadStats();
    } else {
      showToast(data.error || 'Error', 'error');
    }
  } catch {
    showToast('Error de conexión', 'error');
  }
}
