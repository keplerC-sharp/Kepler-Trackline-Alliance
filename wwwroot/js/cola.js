/**
 * @file cola.js
 * @description Core logic for the Track Control Dashboard. 
 * Manages SignalR synchronization, real-time timers, and queue state transitions.
 */

let sessionId        = 0;
let queueTimer       = null;
let timerTick        = null;
let currentOnTrackId = null;

// Initialize layout and session context upon DOM readiness.
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

/**
 * Establishes a persistent SignalR connection to receive live broadcast updates.
 * This architectural choice eliminates the need for expensive polling.
 */
async function initSignalR() {
  connection = new signalR.HubConnectionBuilder()
    .withUrl("/queueHub")
    .withAutomaticReconnect()
    .build();

  connection.on("QueueUpdated", async () => {
    console.log("Queue synchronized via SignalR");
    await loadQueue();
    await loadAdvancedStats();
  });

  try {
    await connection.start();
    console.log("SignalR uplink established.");
    await connection.invoke("JoinSession", String(sessionId));
    await loadQueue();
  } catch (err) {
    console.error("SignalR connection failed. Falling back to polling.", err);
    // Fallback mechanism to ensure continuity in case of websocket failure.
    setInterval(loadQueue, 5000);
  }
}

/**
 * Fetches the current session state and triggers UI re-renders.
 */
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
    console.error('Data acquisition failed:', err);
    showToast('Failed to synchronize track data.', 'error');
  }
}

/**
 * Populates high-level performance metrics and session-wide statistics.
 */
async function loadAdvancedStats() {
  if (!sessionId) return;
  try {
    const res = await fetch(`/Queue/GetAdvancedStats?sessionId=${sessionId}`);
    if (!res.ok) return;
    const data = await res.json();
    
    // Update completion counter derived from advisor performance.
    const elDone = document.getElementById('statDone');
    if (elDone) {
      const total = data.byAdvisor.reduce((acc, curr) => acc + curr.count, 0);
      elDone.textContent = total;
    }

    const elAvg = document.getElementById('statAvgTime');
    if (elAvg) elAvg.textContent = data.avgTimeMinutes + 'm';

    const elQueued = document.getElementById('statQueued');
    if (elQueued) elQueued.textContent = data.totalQueued;

    const elRate = document.getElementById('statTurnRate');
    if (elRate) elRate.textContent = data.turnRate + '/h';

    const elBanner = document.getElementById('nextPilotBanner');
    const elNextName = document.getElementById('nextPilotName');
    if (elBanner && elNextName) {
      if (data.nextPilotName && data.nextPilotName !== 'None') {
        elNextName.textContent = data.nextPilotName;
        elBanner.style.display = 'block';
      } else {
        elBanner.style.display = 'none';
      }
    }
  } catch { /* Suppress silent failures for background updates */ }
}

/**
 * Renders the main queue list with status-specific styling and action buttons.
 */
function renderQueueList(entries) {
  const el = document.getElementById('queueList');
  if (!el) return;

  const visible = entries.filter(e => e.status !== 'COMPLETED' && e.status !== 'CANCELLED');

  if (!visible.length) {
    el.innerHTML = `
      <div style="text-align:center;padding:50px 20px;color:var(--text-muted);
        font-family:var(--font-mono);font-size:0.7rem;letter-spacing:0.15em;text-transform:uppercase;">
        Queue is currently empty —
        <a href="/Dashboard/Index" style="color:var(--cyan);">Register Pilot</a>
      </div>`;
    return;
  }

  el.innerHTML = visible.map((q, i) => {
    const isOn   = q.status === 'ON_TRACK';
    const isNext = q.status === 'UP_NEXT';
    const cls    = isOn ? 'active' : isNext ? 'upnext' : 'pending';
    const tagCls = isOn ? 'tag-green' : isNext ? 'tag-cyan' : 'tag-yellow';
    const tagTxt = isOn ? 'ON TRACK'  : isNext ? 'UP NEXT'   : 'PENDING';
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
          <div style="display:flex;gap:4px;">
            ${!isNext && q.participant?.grade !== 'S' ? `
              <button class="btn-icon" onclick="prioritizeEntry(${q.id})" title="Prioritize">
                <i class="bi bi-arrow-up-circle" style="color:var(--cyan);"></i>
              </button>` : ''}
            <button class="btn-icon" onclick="cancelEntry(${q.id})" title="Cancel turn">
              <i class="bi bi-x-circle" style="color:var(--red);"></i>
            </button>
          </div>` : ''}
      </div>`;
  }).join('');
}

function esc(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

/**
 * Manages the active turn panel and orchestrates the countdown timer.
 */
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
        No Active Turn
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
        <span class="tag tag-green">POSITION #${active.position}</span>
        ${active.participant?.grade === 'S' ? '<span class="tag tag-red">PRIORITY</span>' : ''}
      </div>
    </div>`;

  if (currentOnTrackId !== active.id) {
    clearInterval(timerTick); timerTick = null;
    currentOnTrackId = active.id;
  }

  // Timer logic for track stint monitoring.
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

/**
 * Updates the 'Next in Line' widget to provide upcoming pilot visibility.
 */
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
      <span class="tag tag-cyan ms-auto">${next.status === 'UP_NEXT' ? 'NEXT' : 'QUEUED'}</span>
    </div>`;
}

/**
 * Synchronizes client-side counters with the loaded queue dataset.
 */
function updateStatsFromQueue(entries) {
  const sq = document.getElementById('statQueued');
  if (sq) sq.textContent = entries.filter(e => e.status === 'QUEUED' || e.status === 'UP_NEXT').length;
}

/**
 * Dynamically adjusts control button states and labels based on remaining queue depth.
 * Fix: Start Turn is enabled when ANY entry is QUEUED or UP_NEXT, not just UP_NEXT.
 */
function updateActionButtons(entries) {
  const btnFinish  = document.getElementById('btnFinish');
  const btnStart   = document.getElementById('btnStart');
  const btnAdvance = document.getElementById('btnAdvance');
  
  if (!btnFinish || !btnStart || !btnAdvance) return;
  
  // Consider QUEUED entries too — backend handles QUEUED → ON_TRACK directly.
  const hasNext  = entries.some(e => e.status === 'QUEUED' || e.status === 'UP_NEXT');
  const onTrack  = entries.some(e => e.status === 'ON_TRACK');

  const setBtn = (btn, enabled) => {
    btn.disabled = !enabled;
    btn.style.opacity = enabled ? '1' : '0.5';
    btn.style.cursor  = enabled ? 'pointer' : 'not-allowed';
  };

  setBtn(btnFinish,  onTrack);
  setBtn(btnStart,   hasNext && !onTrack);
  setBtn(btnAdvance, onTrack && hasNext);
}

/**
 * Completes the active turn.
 */
async function finishTurn() {
  if (!sessionId) { showToast('No active session found.', 'error'); return; }
  try {
    const res = await fetch('/Queue/FinishTurn', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ sessionId })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (data.ok) {
      clearInterval(timerTick); timerTick = null; currentOnTrackId = null;
      showToast('Turn finished successfully.', 'success');
      await loadQueue();
      await loadAdvancedStats();
    } else {
      showToast(data.error || 'Failed to finish turn.', 'error');
    }
  } catch (err) {
    showToast('Communication error: ' + err.message, 'error');
  }
}

/**
 * Starts the next eligible turn in the queue.
 */
async function startTurn() {
  if (!sessionId) { showToast('No active session found.', 'error'); return; }
  try {
    const res = await fetch('/Queue/StartTurn', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ sessionId })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (data.ok) {
      // Play alert first; announce pilot AFTER the sound ends to avoid overlap.
      if (data.newOnTrack) {
        playTurnAlert(() => announceParticipant(data.newOnTrack.position, data.newOnTrack.fullName, 10));
      } else {
        playTurnAlert();
      }
      showToast('Turn started successfully.', 'success');
      await loadQueue();
      await loadAdvancedStats();
    } else {
      showToast(data.error || 'Failed to start turn.', 'error');
    }
  } catch (err) {
    showToast('Communication error: ' + err.message, 'error');
  }
}

/**
 * Executes the combined state transition (Finish & Next).
 */
async function advanceQueue() {
  if (!sessionId) { showToast('No active session found.', 'error'); return; }
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

      // Play alert first; announce pilot AFTER the sound ends to avoid overlap.
      if (data.newOnTrack) {
        playTurnAlert(() => announceParticipant(data.newOnTrack.position, data.newOnTrack.fullName, 10));
      } else {
        playTurnAlert();
      }

      showToast('Queue advanced successfully.', 'success');
      await loadQueue();
      await loadAdvancedStats();
    } else {
      showToast(data.error || 'Failed to advance queue.', 'error');
    }
  } catch (err) {
    showToast('Communication error: ' + err.message, 'error');
  }
}

// ── Singleton audio player — prevents overlapping sounds ─────────────────
let _alertAudio = null;

function playTurnAlert(onEnded) {
  try {
    // Stop and reuse the same audio instance to avoid overlap.
    if (_alertAudio) {
      _alertAudio.pause();
      _alertAudio.currentTime = 0;
    } else {
      _alertAudio = new Audio('/sounds/pase-a-la-pista.mp3');
      _alertAudio.volume = 0.8;
    }
    if (onEnded) {
      _alertAudio.onended = onEnded;
    } else {
      _alertAudio.onended = null;
    }
    _alertAudio.play().catch(err => console.warn('Audio playback inhibited by browser:', err));
  } catch (e) { /* Fail silently */ }
}

/**
 * Uses SpeechSynthesis API to announce the next pilot over the PA system.
 * Called AFTER the alert sound finishes to prevent audio collision.
 */
function announceParticipant(position, fullName, durationMinutes = 10) {
  if (!('speechSynthesis' in window)) return;
  window.speechSynthesis.cancel();
  const text = `Turn number ${position}, pilot ${fullName}. Approximate duration: ${durationMinutes} minutes.`;
  const msg  = new SpeechSynthesisUtterance(text);
  msg.lang  = 'en-US';
  msg.rate  = 0.9;
  msg.pitch = 1;
  window.speechSynthesis.speak(msg);
}

function reannounceCurrent() {
  const panel = document.getElementById('currentTurnPanel');
  if (!panel || panel.innerHTML.includes('No Active Turn')) {
    showToast('No active turn to re-announce.', 'info');
    return;
  }
  
  const name = panel.querySelector('div[style*="font-size:1.5rem"]').textContent.trim();
  const posText = panel.querySelector('.tag-green').textContent.trim();
  const position = posText.replace('POSITION #', '');
  
  playTurnAlert();
  setTimeout(() => {
    announceParticipant(position, name, 10);
    showToast('Re-calling pilot...', 'info');
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
      showToast('Entry prioritized.', 'success');
      await loadQueue();
    }
  } catch {
    showToast('Failed to prioritize entry.', 'error');
  }
}

async function cancelEntry(entryId) {
  if (!confirm('Are you sure you want to cancel this turn?')) return;
  try {
    const res = await fetch('/Queue/Cancel', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ entryId })
    });
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const data = await res.json();
    if (data.ok) {
      showToast('Entry cancelled.', 'info');
      await loadQueue();
      await loadAdvancedStats();
    } else {
      showToast(data.error || 'Cancellation error.', 'error');
    }
  } catch {
    showToast('Connection failure during cancellation.', 'error');
  }
}


