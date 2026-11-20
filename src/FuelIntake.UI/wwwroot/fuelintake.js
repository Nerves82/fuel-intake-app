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
    const input = el.querySelector('input:not([type=radio]), select, .picker-button, textarea, input');
    if (input) setTimeout(() => input.focus({ preventScroll: true }), 350);
    return true;
  },
  focus(id) { const el = document.getElementById(id); if (el) el.focus(); },
  // Opens a dropdown list upward when there's no room below, and keeps the highlighted option in view.
  placeList(id, index) {
    const list = document.getElementById(id);
    if (!list) return;
    if (!list.dataset.placed) {
      list.dataset.placed = '1';
      const box = list.parentElement.getBoundingClientRect();
      const below = window.innerHeight - box.bottom, above = box.top;
      const needed = Math.min(list.scrollHeight, 320);
      list.classList.toggle('up', below < needed + 8 && above > below);
      list.classList.toggle('right', list.getBoundingClientRect().right > window.innerWidth - 8);
    }
    const item = list.children[index];
    if (item) item.scrollIntoView({ block: 'nearest' });
  },
  // Scrolls the notes panel so these notes are in view (the panel lists newest first: the last id is topmost).
  scrollToNotes(ids) {
    const els = ids.map(id => document.getElementById(id)).filter(Boolean);
    if (els.length > 0) els[els.length - 1].scrollIntoView({ block: 'nearest', behavior: 'smooth' });
  }
};
