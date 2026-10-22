(function () {
  "use strict";

  var LIMITES = { latMin: 13.1, latMax: 14.5, lngMin: -90.2, lngMax: -87.6 };
  var CENTRO_PAIS = [13.75, -88.9];
  var PATRONES = [
    /@(-?\d+\.\d+),(-?\d+\.\d+)/,
    /!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)/,
    /[?&](?:q|ll|query)=(-?\d+\.\d+)(?:,|%2C)\s*(-?\d+\.\d+)/i,
    /^\s*(-?\d{1,3}\.\d+)\s*[,;\s]\s*(-?\d{1,3}\.\d+)\s*$/
  ];

  function leer(texto) {
    if (!texto) return null;
    for (var i = 0; i < PATRONES.length; i++) {
      var m = texto.trim().match(PATRONES[i]);
      if (m) return [parseFloat(m[1]), parseFloat(m[2])];
    }
    return null;
  }

  function dentro(p) {
    return p && p[0] >= LIMITES.latMin && p[0] <= LIMITES.latMax && p[1] >= LIMITES.lngMin && p[1] <= LIMITES.lngMax;
  }

  function texto(p) {
    return p[0].toFixed(6).replace(/0+$/, "").replace(/\.$/, "") + ", " + p[1].toFixed(6).replace(/0+$/, "").replace(/\.$/, "");
  }

  function crearMapa(el, interactivo) {
    var mapa = L.map(el, {
      scrollWheelZoom: false,
      minZoom: 8,
      maxBounds: [[12.6, -91], [15, -86.8]],
      maxBoundsViscosity: 0.8,
      dragging: interactivo || !L.Browser.mobile,
      tap: false
    });
    mapa.attributionControl.setPrefix('<a href="https://leafletjs.com" target="_blank" rel="noopener">Leaflet</a>');
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      referrerPolicy: "strict-origin-when-cross-origin",
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">OpenStreetMap</a>'
    }).addTo(mapa);
    el.addEventListener("focusin", function () { mapa.scrollWheelZoom.enable(); });
    el.addEventListener("mouseleave", function () { mapa.scrollWheelZoom.disable(); });
    mapa.on("click", function () { mapa.scrollWheelZoom.enable(); });
    return mapa;
  }

  function selector(el) {
    var campo = document.getElementById(el.getAttribute("data-campo"));
    var aviso = el.parentElement.querySelector("[data-mapa-aviso]");
    var botonUbicacion = el.parentElement.querySelector("[data-mi-ubicacion]");
    var botonQuitar = el.parentElement.querySelector("[data-quitar-ubicacion]");
    var mapa = crearMapa(el, true);
    var pin = null;

    function avisar(mensaje) {
      if (!aviso) return;
      aviso.textContent = mensaje || "";
    }

    function poner(p, centrar) {
      if (pin) {
        pin.setLatLng(p);
      } else {
        pin = L.marker(p, { draggable: true, autoPan: true, keyboard: true, title: "Ubicación de la propiedad" }).addTo(mapa);
        pin.on("dragend", function () {
          var ll = pin.getLatLng();
          elegir([ll.lat, ll.lng], false);
        });
      }
      if (centrar) mapa.setView(p, Math.max(mapa.getZoom() || 0, 17));
      if (botonQuitar) botonQuitar.hidden = false;
    }

    function quitar() {
      if (pin) { mapa.removeLayer(pin); pin = null; }
      if (botonQuitar) botonQuitar.hidden = true;
    }

    function elegir(p, centrar) {
      if (!dentro(p)) {
        avisar("Ese punto queda fuera de El Salvador.");
        if (pin && campo.value) poner(leer(campo.value), false);
        return;
      }
      avisar("");
      campo.value = texto(p);
      campo.dispatchEvent(new Event("change", { bubbles: true }));
      poner(p, centrar);
    }

    var inicial = leer(campo.value);
    if (dentro(inicial)) {
      mapa.setView(inicial, 17);
      poner(inicial, false);
    } else {
      mapa.setView(CENTRO_PAIS, 8);
      if (botonQuitar) botonQuitar.hidden = true;
    }

    mapa.on("click", function (e) { elegir([e.latlng.lat, e.latlng.lng], false); });

    var espera;
    campo.addEventListener("input", function () {
      clearTimeout(espera);
      espera = setTimeout(function () {
        var p = leer(campo.value);
        if (!campo.value.trim()) { quitar(); avisar(""); return; }
        if (dentro(p)) { avisar(""); poner(p, true); }
      }, 350);
    });

    if (botonQuitar) {
      botonQuitar.addEventListener("click", function () {
        campo.value = "";
        quitar();
        avisar("");
        mapa.setView(CENTRO_PAIS, 8);
      });
    }

    if (botonUbicacion) {
      if (!("geolocation" in navigator)) {
        botonUbicacion.hidden = true;
      } else {
        botonUbicacion.addEventListener("click", function () {
          botonUbicacion.disabled = true;
          avisar("Buscando tu ubicación…");
          navigator.geolocation.getCurrentPosition(function (pos) {
            botonUbicacion.disabled = false;
            elegir([pos.coords.latitude, pos.coords.longitude], true);
          }, function () {
            botonUbicacion.disabled = false;
            avisar("No pudimos obtener tu ubicación. Revisa el permiso del navegador o marca el punto en el mapa.");
          }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
        });
      }
    }

    requestAnimationFrame(function () { mapa.invalidateSize(); });
  }

  function vista(el) {
    var p = leer(el.getAttribute("data-ubicacion"));
    if (!dentro(p)) return;
    var mapa = crearMapa(el, false);
    mapa.setView(p, 16);
    L.marker(p, { keyboard: false, title: el.getAttribute("data-titulo") || "" }).addTo(mapa);
    requestAnimationFrame(function () { mapa.invalidateSize(); });
  }

  function iniciar() {
    if (!window.L) return;
    document.querySelectorAll("[data-mapa]").forEach(function (el) {
      if (el.hasAttribute("data-campo")) selector(el); else vista(el);
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", iniciar);
  } else {
    iniciar();
  }
})();
