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

  // Copia del progreso: baja un archivo de texto a la carpeta de descargas.
  WarriorDescargar: function (nombrePtr, textoPtr) {
    try {
      var nombre = UTF8ToString(nombrePtr), texto = UTF8ToString(textoPtr);
      var url = URL.createObjectURL(new Blob([texto], { type: 'application/json' }));
      var a = document.createElement('a');
      a.href = url;
      a.download = nombre;
      document.body.appendChild(a);
      a.click();
      setTimeout(function () { document.body.removeChild(a); URL.revokeObjectURL(url); }, 2000);
      return 1;
    } catch (e) { return 0; }
  },

  // Copia del progreso: abre "Abrir archivo" y manda el texto al juego (SendMessage).
  WarriorElegirArchivo: function (objPtr, metodoPtr) {
    var obj = UTF8ToString(objPtr), metodo = UTF8ToString(metodoPtr);
    var input = document.getElementById('warrior-archivo');
    if (!input) {
      input = document.createElement('input');
      input.type = 'file';
      input.id = 'warrior-archivo';
      input.accept = '.json,application/json';
      input.style.display = 'none';
      document.body.appendChild(input);
    }
    input.value = '';
    input.onchange = function () {
      var f = input.files && input.files[0];
      if (!f) return;
      var r = new FileReader();
      r.onload = function () {
        if (typeof SendMessage === 'function') SendMessage(obj, metodo, r.result);
        else Module.SendMessage(obj, metodo, r.result);
      };
      r.readAsText(f);
    };
    input.click();
  },

  // 1 si el navegador es Brave (puede borrar lo guardado al cerrar).
  WarriorEsBrave: function () {
    return (navigator.brave && typeof navigator.brave.isBrave === 'function') ? 1 : 0;
  },

  // Pide al navegador que no borre lo guardado cuando le falte espacio.
  WarriorPedirPersistente: function () {
    try { if (navigator.storage && navigator.storage.persist) navigator.storage.persist(); } catch (e) {}
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
