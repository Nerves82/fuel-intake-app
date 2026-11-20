// Small helpers the Blazor screens can't do on their own (both hosts load this file).
window.fuelIntake = {
  // Right-click on a field (not Shift+right-click) opens the app's "Add note" menu instead of the browser menu.
  init(dotnet) {
    if (window.fuelIntake._ready) return;
    window.fuelIntake._ready = true;
    document.addEventListener('contextmenu', e => {
      if (e.shiftKey) return;
      const f = e.target.closest('[data-field-key]');
      if (!f) return;
      e.preventDefault();
      dotnet.invokeMethodAsync('OpenFieldMenu', f.dataset.fieldScreen, f.dataset.fieldKey, f.dataset.fieldLabel, e.clientX, e.clientY);
    });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') dotnet.invokeMethodAsync('CloseFieldMenu'); });
  },
  // Scroll to a field, highlight it briefly and put the cursor in it. Returns false if it isn't on screen.
  reveal(id) {
    const el = document.getElementById(id);
    if (!el) return false;
    el.scrollIntoView({ block: 'center', behavior: 'smooth' });
    el.classList.remove('flash'); void el.offsetWidth; el.classList.add('flash');
    const input = el.querySelector('input:not([type=radio]), select, textarea, input');
    if (input) setTimeout(() => input.focus({ preventScroll: true }), 350);
    return true;
  },
  focus(id) { const el = document.getElementById(id); if (el) el.focus(); }
};
