(function () {
  "use strict";
  var raiz = document.documentElement;

  function guardarTema(valor) {
    try {
      if (valor === "system") localStorage.removeItem("arrendia-tema");
      else localStorage.setItem("arrendia-tema", valor);
    } catch (e) { }
  }

  function cerrarMenus() {
    document.querySelectorAll(".pop.on").forEach(function (p) { p.classList.remove("on"); });
  }

  document.addEventListener("click", function (evento) {
    var objetivo = evento.target;

    if (objetivo.closest("[data-tema]")) {
      var actual = raiz.getAttribute("data-theme") || "system";
      var siguiente = actual === "system" ? "light" : actual === "light" ? "dark" : "system";
      if (siguiente === "system") raiz.removeAttribute("data-theme");
      else raiz.setAttribute("data-theme", siguiente);
      guardarTema(siguiente);
      return;
    }

    var disparador = objetivo.closest("[data-pop]");
    if (disparador) {
      var pop = disparador.parentElement.querySelector(".pop");
      var abierto = pop.classList.contains("on");
      cerrarMenus();
      if (!abierto) pop.classList.add("on");
      return;
    }
    if (!objetivo.closest(".pop")) cerrarMenus();

    var shell = document.querySelector("[data-shell]");
    if (shell && objetivo.closest("[data-abrir-menu]")) { shell.classList.add("menu-abierto"); return; }
    if (shell && objetivo.closest("[data-cerrar-menu]")) { shell.classList.remove("menu-abierto"); return; }

    var ver = objetivo.closest("[data-ver-contrasena]");
    if (ver) {
      var campo = ver.parentElement.querySelector("input");
      var mostrar = campo.type === "password";
      campo.type = mostrar ? "text" : "password";
      ver.setAttribute("aria-label", mostrar ? "Ocultar contraseña" : "Mostrar contraseña");
    }
  });

  document.addEventListener("keydown", function (evento) {
    if (evento.key === "Escape") {
      cerrarMenus();
      var shell = document.querySelector("[data-shell]");
      if (shell) shell.classList.remove("menu-abierto");
    }
  });

  document.addEventListener("input", function (evento) {
    var campo = evento.target.closest("[data-medidor]");
    if (!campo) return;
    var barra = campo.closest(".fld").querySelector("[data-medidor-barra]");
    var v = campo.value;
    var nivel = 0;
    if (v.length >= 10) nivel++;
    if (/[a-z]/i.test(v) && /\d/.test(v)) nivel++;
    if (/[A-Z]/.test(v) && /[a-z]/.test(v)) nivel++;
    if (/[^A-Za-z0-9]/.test(v) || v.length >= 14) nivel++;
    barra.hidden = v.length === 0;
    barra.setAttribute("data-nivel", String(nivel));
    barra.firstElementChild.style.width = Math.max(10, nivel * 25) + "%";
  });

  document.addEventListener("submit", function (evento) {
    var formulario = evento.target.closest("[data-formulario]");
    if (!formulario) return;
    var boton = formulario.querySelector("button[type=submit]");
    if (boton) {
      if (boton.getAttribute("aria-busy") === "true") { evento.preventDefault(); return; }
      boton.setAttribute("aria-busy", "true");
    }
  });

  document.querySelectorAll("select[data-depende-de]").forEach(function (lista) {
    var origen = document.getElementById(lista.getAttribute("data-depende-de"));
    if (!origen) return;
    function filtrar() {
      var actual = lista.selectedOptions[0];
      var nombre = actual ? actual.value : "";
      var visible = null;
      lista.querySelectorAll("optgroup").forEach(function (grupo) {
        var ok = grupo.label === origen.value;
        grupo.hidden = !ok;
        grupo.disabled = !ok;
        if (ok) visible = grupo;
      });
      if (!visible || (actual && actual.parentElement === visible)) return;
      var opciones = Array.prototype.slice.call(visible.querySelectorAll("option"));
      var igual = opciones.filter(function (o) { return o.value === nombre; })[0];
      var elegida = igual || opciones.filter(function (o) { return o.value === origen.value; })[0] || opciones[0];
      if (elegida) elegida.selected = true;
    }
    origen.addEventListener("change", filtrar);
    filtrar();
  });

  document.querySelectorAll("[data-mostrar-si]").forEach(function (bloque) {
    var nombre = bloque.getAttribute("data-mostrar-si");
    var radios = document.querySelectorAll("input[type=radio][name='" + nombre + "']");
    function actualizar() {
      var marcado = document.querySelector("input[type=radio][name='" + nombre + "']:checked");
      bloque.hidden = !marcado || marcado.value !== "true";
    }
    radios.forEach(function (r) { r.addEventListener("change", actualizar); });
    actualizar();
  });

  var plano = document.querySelector("[data-plano]");
  if (plano) {
    var tipos = ["o", "o", "o", "o", "o", "o", "d", "d", "m", ""];
    for (var i = 0; i < 60; i++) {
      var t = tipos[(i * 7 + (i % 3) * 5) % tipos.length];
      var celda = document.createElement("i");
      if (t) celda.classList.add(t);
      if (t === "o" && i % 5 === 0) celda.classList.add("p");
      celda.style.setProperty("--retraso", ((i * 0.37) % 4.5).toFixed(2) + "s");
      plano.appendChild(celda);
    }
  }
})();
