'use strict';
// No tokens, tenant payloads or drafts in browser storage. GET never renews the session.
(() => {
  document.addEventListener('submit', event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() !== 'post') return;
    if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
    form.dataset.submitting = 'true'; form.setAttribute('aria-busy', 'true');
    const status = document.createElement('p'); status.setAttribute('role', 'status'); status.textContent = 'Processando a solicitação…'; form.append(status);
  });
  if (location.pathname !== '/app') return;
  let expiryTimer;
  async function validate() {
    try {
      const response = await fetch('/account/session', { cache: 'no-store', credentials: 'same-origin' });
      if (!response.ok) { document.getElementById('main').replaceChildren(); location.replace('/account/login'); return; }
      const session = await response.json();
      if (session.stamp !== document.body.dataset.session) { document.getElementById('main').replaceChildren(); location.reload(); return; }
      clearTimeout(expiryTimer);
      const remaining = Date.parse(session.expiresAtUtc) - Date.now();
      expiryTimer = setTimeout(() => {
        const message = document.createElement('p'); message.className = 'notice'; message.setAttribute('role', 'alert');
        message.textContent = 'Sua sessão está perto de expirar. Use Continuar sessão ou saia; alterações não salvas não serão enviadas automaticamente.';
        document.getElementById('main').prepend(message);
      }, Math.max(0, remaining - 120000));
    } catch { /* Offline does not renew authorization; all operations still require the server. */ }
  }
  addEventListener('pageshow', event => { if (event.persisted) { document.getElementById('main').replaceChildren(); location.reload(); } else validate(); });
  document.addEventListener('visibilitychange', () => { if (!document.hidden) validate(); });
})();
