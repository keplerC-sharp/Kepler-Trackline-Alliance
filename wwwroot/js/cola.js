// ════════════════════════════════════════
// COLA — /Queue/Index
// ════════════════════════════════════════

let sessionId        = 0;
let queueTimer       = null;
let timerTick        = null;
let currentOnTrackId = null;

window.addEventListener('DOMContentLoaded', async () => {
  await initLayout('cola');

  const sid = document.getElementById('activeSessionId');
  if (sid) sessionId = parseInt(sid.value) || 0;

  if (sessionId > 0) {
    initSignalR();
    loadAdvancedStats();
  }
});

let connection = null;

async function initSignalR() {
  connection = new signalR.HubConnectionBuilder()
    .withUrl("/queueHub")
    .withAutomaticReconnect()
    .build();

  connection.on("QueueUpdated", async () => {
    console.log("Cola actualizada vía SignalR");
    await loadQueue();
    await loadAdvancedStats();
  });

  try {
    await connection.start();
    console.log("SignalR conectado en Dashboard");
    await connection.invoke("JoinSession", String(sessionId));
    await loadQueue();
  } catch (err) {
    console.error("Error SignalR Dashboard:", err);
    setInterval(loadQueue, 5000);
  }
}

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
    updateActionButtons(data);
  } catch (err) {
    console.error('Error cargando cola:', err);
    showToast('Error al cargar la cola', 'error');
  }
}

// ── Stats ─────────────────────────────────────────────────────────────────
async function loadAdvancedStats() {
  if (!sessionId) return;
  try {
    const res = await fetch(`/Queue/GetAdvancedStats?sessionId=${sessionId}`);
    if (!res.ok) return;
    const data = await res.json();
    
    // Actualizar contador completados (del método anterior)
    const elDone = document.getElementById('statDone');
    if (elDone) {
      const total = data.byAdvisor.reduce((acc, curr) => acc + curr.count, 0);
      elDone.textContent = total;
    }

    // Mostrar tiempo promedio si existe un contenedor
    const elAvg = document.getElementById('statAvgTime');
    if (elAvg) elAvg.textContent = data.avgTimeMinutes + 'm';

    const elQueued = document.getElementById('statQueued');
    if (elQueued) elQueued.textContent = data.totalQueued;

    const elRate = document.getElementById('statTurnRate');
    if (elRate) elRate.textContent = data.turnRate + '/h';

    const elBanner = document.getElementById('nextPilotBanner');
    const elNextName = document.getElementById('nextPilotName');
    if (elBanner && elNextName) {
      if (data.nextPilotName && data.nextPilotName !== 'Nadie') {
        elNextName.textContent = data.nextPilotName;
        elBanner.style.display = 'block';
      } else {
        elBanner.style.display = 'none';
      }
    }
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
            ${isOn ? `
              <button class="tag tag-blue ms-auto" onclick="openComment(${q.id})" style="border:none;cursor:pointer;">
                <i class="bi bi-chat-dots-fill"></i> Comentario
              </button>` : ''}
          </div>
        </div>
        ${!isOn ? `
          <div style="display:flex;gap:4px;">
            ${!isNext && q.participant?.grade !== 'S' ? `
              <button class="btn-icon" onclick="prioritizeEntry(${q.id})" title="Priorizar">
                <i class="bi bi-arrow-up-circle" style="color:var(--cyan);"></i>
              </button>` : ''}
            <button class="btn-icon" onclick="cancelEntry(${q.id})" title="Cancelar turno">
              <i class="bi bi-x-circle" style="color:var(--red);"></i>
            </button>
          </div>` : ''}
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
        ${active.participant?.grade === 'S' ? '<span class="tag tag-red">PRIORIDAD</span>' : ''}
      </div>
    </div>`;

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

// ── Próximo ───────────────────────────────────────────────────────────────
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

// ── Stats ─────────────────────────────────────────────────────────────────
function updateStatsFromQueue(entries) {
  const sq = document.getElementById('statQueued');
  if (sq) sq.textContent = entries.filter(e => e.status === 'QUEUED' || e.status === 'UP_NEXT').length;
  
  // statDone se actualiza vía loadAdvancedStats para mayor precisión histórica
}

function updateActionButtons(entries) {
  const btn = document.getElementById('btnAdvance');
  if (!btn) return;
  
  const hasMore = entries.some(e => e.status === 'QUEUED' || e.status === 'UP_NEXT');
  const onTrack = entries.some(e => e.status === 'ON_TRACK');

  if (!onTrack && !hasMore) {
    btn.disabled = true;
    btn.innerHTML = '<i class="bi bi-slash-circle"></i> No hay turnos';
    btn.style.opacity = '0.5';
  } else if (!hasMore) {
    btn.disabled = false;
    btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Finalizar Turno';
    btn.style.opacity = '1';
  } else {
    btn.disabled = false;
    btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Finalizar y Siguiente';
    btn.style.opacity = '1';
  }
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

      // ── Sonido y voz ──────────────────────────────────────────────────
      playTurnAlert();
      if (data.newOnTrack) {
        announceParticipant(data.newOnTrack.position, data.newOnTrack.fullName, 10);
      }

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

// ── Audio alert ───────────────────────────────────────────────────────────
function playTurnAlert() {
  try {
    const audio = new Audio('/sounds/pase-a-la-pista.mp3');
    audio.volume = 0.8;
    audio.play().catch(err => console.warn('Audio bloqueado por el navegador:', err));
  } catch (e) { /* silencioso */ }
}

// ── Text-to-Speech ────────────────────────────────────────────────────────
function announceParticipant(position, fullName, durationMinutes = 10) {
  if (!('speechSynthesis' in window)) return;
  window.speechSynthesis.cancel();
  const text = `Turno número ${position}, piloto ${fullName}. Duración aproximada: ${durationMinutes} minutos.`;
  const msg  = new SpeechSynthesisUtterance(text);
  msg.lang  = 'es-ES';
  msg.rate  = 0.9;
  msg.pitch = 1;
  window.speechSynthesis.speak(msg);
}

function reannounceCurrent() {
  const panel = document.getElementById('currentTurnPanel');
  if (!panel || panel.innerHTML.includes('Sin turno activo')) {
    showToast('No hay turno activo para llamar', 'info');
    return;
  }
  
  // Obtener datos del panel (un poco hacky pero efectivo sin refactorizar todo)
  const name = panel.querySelector('div[style*="font-size:1.5rem"]').textContent.trim();
  const posText = panel.querySelector('.tag-green').textContent.trim();
  const position = posText.replace('POSICIÓN #', '');
  
  playTurnAlert();
  setTimeout(() => {
    announceParticipant(position, name, 10);
    showToast('Llamando de nuevo...', 'info');
  }, 1000);
}

async function prioritizeEntry(entryId) {
  try {
    const res = await fetch('/Queue/Promote', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ entryId })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (data.ok) {
      showToast('Turno priorizado', 'success');
      // No hace falta recargar manualmente si SignalR está activo, 
      // pero por si acaso lo hacemos para feedback instantáneo
      await loadQueue();
    }
  } catch {
    showToast('Error al priorizar', 'error');
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

// ── Modal comentario del asesor ───────────────────────────────────────────
function openComment(entryId) {
  // Crear modal inline si no existe
  let modal = document.getElementById('commentModal');
  if (!modal) {
    modal = document.createElement('div');
    modal.id = 'commentModal';
    modal.style.cssText = `
      position:fixed;inset:0;background:rgba(0,0,0,0.7);z-index:9000;
      display:flex;align-items:center;justify-content:center;`;
    document.body.appendChild(modal);
  }
  modal.innerHTML = `
    <div style="background:var(--bg-card);padding:25px;border-radius:12px;width:90%;max-width:400px;
      border:1px solid var(--border);box-shadow:0 10px 40px rgba(0,0,0,0.5);">
      <div style="font-family:var(--font-display);font-size:1.1rem;font-weight:700;margin-bottom:15px;color:var(--cyan);">
        <i class="bi bi-chat-dots me-2"></i> Comentario del Asesor
      </div>
      <textarea id="commentText" style="width:100%;height:100px;background:var(--bg-body);color:var(--text);
        border:1px solid var(--border);border-radius:6px;padding:10px;font-family:inherit;font-size:0.9rem;resize:none;"
        placeholder="Escribe un comentario o nota..."></textarea>
      <div style="display:flex;justify-content:flex-end;gap:10px;margin-top:20px;">
        <button class="btn-secondary" onclick="closeComment()" style="padding:6px 15px;font-size:0.8rem;">Cancelar</button>
        <button class="btn-primary" onclick="saveComment(${entryId})" style="padding:6px 15px;font-size:0.8rem;">Guardar</button>
      </div>
    </div>`;
  modal.style.display = 'flex';
  setTimeout(() => document.getElementById('commentText')?.focus(), 100);
}

function closeComment() {
  const modal = document.getElementById('commentModal');
  if (modal) modal.style.display = 'none';
}

async function saveComment(entryId) {
  const text = document.getElementById('commentText')?.value.trim();
  if (!text) { showToast('Escribe un comentario', 'error'); return; }
  try {
    const res = await fetch('/Queue/AddComment', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ entryId, comment: text })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (data.ok) {
      showToast('Comentario guardado', 'success');
      closeComment();
    } else {
      showToast(data.error || 'Error al guardar', 'error');
    }
  } catch (err) {
    showToast('Error de conexión: ' + err.message, 'error');
  }
}

// Cerrar modal con Escape
document.addEventListener('keydown', e => {
  if (e.key === 'Escape') closeComment();
});
