// Pide al navegador que la tecla Esc llegue al juego aunque este en pantalla
// completa (Keyboard Lock: en Chrome/Edge, Esc solo sale de pantalla completa
// si se mantiene pulsada ~2 segundos). Firefox y Safari no lo admiten.
mergeInto(LibraryManager.library, {
  // Pasa los archivos de Application.persistentDataPath al IndexedDB del
  // navegador (sin esto, lo escrito se pierde al cerrar la pestana).
  WarriorGuardarDisco: function () {
    try {
      FS.syncfs(false, function (e) { if (e) console.warn('[Warrior] No se pudo guardar en el navegador', e); });
    } catch (e) {}
  },

  WarriorBloquearEscape: function () {
    try {
      if (window.__warriorEscape) return;
      window.__warriorEscape = true;
      var pedir = function () {
        try {
          if (navigator.keyboard && navigator.keyboard.lock) {
            navigator.keyboard.lock(['Escape']).catch(function () {});
          }
        } catch (e) {}
      };
      document.addEventListener('fullscreenchange', pedir);
      window.addEventListener('resize', pedir);
      window.addEventListener('pointerdown', pedir);
      pedir();
    } catch (e) {}
  }
});
