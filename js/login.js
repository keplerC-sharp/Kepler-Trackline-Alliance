// ════════════════════════════════════════
// LOGIN — index.html
// ════════════════════════════════════════

// Si ya está autenticado, redirigir directo
if (sessionStorage.getItem('apex_user')) {
  window.location.href = 'registro.html';
}

// Restaurar ID si estaba guardado
window.addEventListener('DOMContentLoaded', () => {
  const saved = localStorage.getItem('apex_remember_id');
  if (saved) {
    document.getElementById('loginId').value = saved;
    document.getElementById('rememberCheck').checked = true;
  }
});

document.addEventListener('keydown', e => {
  if (e.key === 'Enter') handleLogin();
});

function handleLogin() {
  const id   = document.getElementById('loginId').value.trim();
  const pass = document.getElementById('loginPass').value.trim();
  const remember = document.getElementById('rememberCheck').checked;

  if (!id || !pass) {
    showToast('Ingresa Marshal ID y Access Key', 'error');
    return;
  }

  const overlay = document.getElementById('loadingOverlay');
  overlay.classList.add('active');

  // — Aquí conectas con tu API real: POST /api/auth/login —
  // Por ahora: mock con cualquier credencial
  setTimeout(() => {
    overlay.classList.remove('active');

    // Guardar sesión
    sessionStorage.setItem('apex_user', JSON.stringify({ id, role: 'admin' }));

    // Recordar ID si está marcado
    if (remember) {
      localStorage.setItem('apex_remember_id', id);
    } else {
      localStorage.removeItem('apex_remember_id');
    }

    window.location.href = 'registro.html';
  }, 1200);
}
