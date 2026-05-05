// ════════════════════════════════════════
// LOGIN — /Auth/Login
// El form usa POST nativo ASP.NET MVC
// Este script solo maneja el UX (overlay)
// ════════════════════════════════════════

window.addEventListener('DOMContentLoaded', () => {
  const saved = localStorage.getItem('apex_remember_id');
  if (saved) {
    const idInput = document.getElementById('Identifier');
    if (idInput) idInput.value = saved;
  }
});

document.addEventListener('keydown', e => {
  if (e.key === 'Enter') {
    const form = document.getElementById('loginForm');
    if (form) form.requestSubmit();
  }
});
