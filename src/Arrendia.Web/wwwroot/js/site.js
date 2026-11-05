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

    var abrir = objetivo.closest("[data-abrir-dialogo]");
    if (abrir) {
      var dialogo = document.getElementById(abrir.getAttribute("data-abrir-dialogo"));
      if (dialogo && dialogo.showModal) dialogo.showModal();
      return;
    }
    if (objetivo.closest("[data-cerrar-dialogo]") || objetivo.matches("dialog.modal")) {
      var ventana = objetivo.closest("dialog");
      if (ventana) ventana.close();
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
    var regla = bloque.getAttribute("data-mostrar-si").split("=");
    var nombre = regla[0];
    var valor = regla.length > 1 ? regla[1] : "true";
    var radios = document.querySelectorAll("input[type=radio][name='" + nombre + "']");
    function actualizar() {
      var marcado = document.querySelector("input[type=radio][name='" + nombre + "']:checked");
      bloque.hidden = !marcado || marcado.value !== valor;
    }
    radios.forEach(function (r) { r.addEventListener("change", actualizar); });
    actualizar();
  });

  document.addEventListener("click", function (evento) {
    var boton = evento.target.closest("[data-copiar]");
    if (!boton) return;
    var campo = boton.parentElement.querySelector("[data-copiable]");
    if (!campo) return;
    campo.select();
    var listo = function () {
      var texto = boton.textContent;
      boton.textContent = "Copiado";
      setTimeout(function () { boton.textContent = texto; }, 1600);
    };
    if (navigator.clipboard) navigator.clipboard.writeText(campo.value).then(listo, listo);
    else { document.execCommand("copy"); listo(); }
  });

  document.querySelectorAll("[data-total-cargos]").forEach(function (formulario) {
    var monto = formulario.querySelector("[data-monto]");
    var resumen = formulario.querySelector("[data-resumen-cargos]");
    function recalcular() {
      var total = 0, cuantos = 0;
      formulario.querySelectorAll("input[data-saldo]:checked").forEach(function (c) {
        total += parseFloat(c.getAttribute("data-saldo")) || 0;
        cuantos++;
      });
      if (resumen) resumen.textContent = cuantos === 0 ? "Se aplicará a los cargos más antiguos primero." : cuantos + (cuantos === 1 ? " cargo elegido: $" : " cargos elegidos: $") + total.toFixed(2);
      if (monto && cuantos > 0) monto.value = total.toFixed(2);
    }
    formulario.addEventListener("change", function (e) { if (e.target.matches("input[data-saldo]")) recalcular(); });
    recalcular();
  });

  document.querySelectorAll("select[data-sugerir]").forEach(function (lista) {
    var destino = document.getElementById(lista.getAttribute("data-sugerir"));
    if (!destino) return;
    lista.addEventListener("change", function () {
      var opcion = lista.selectedOptions[0];
      var valor = opcion ? opcion.getAttribute("data-valor") : null;
      if (valor && !destino.value) destino.value = valor;
    });
  });

  document.querySelectorAll("input[type=file][multiple]").forEach(function (campo) {
    if (typeof DataTransfer === "undefined") return;
    var juntos = new DataTransfer();
    var aviso = document.createElement("span");
    aviso.className = "hint";
    campo.insertAdjacentElement("afterend", aviso);

    function mostrar() {
      var n = juntos.files.length;
      aviso.textContent = n === 0 ? "" : n === 1 ? "1 archivo elegido: " + juntos.files[0].name : n + " archivos elegidos. Puedes agregar más.";
    }

    campo.addEventListener("change", function () {
      Array.prototype.forEach.call(campo.files, function (archivo) {
        var repetido = Array.prototype.some.call(juntos.files, function (f) { return f.name === archivo.name && f.size === archivo.size; });
        if (!repetido) juntos.items.add(archivo);
      });
      campo.files = juntos.files;
      mostrar();
    });

    if (campo.form) campo.form.addEventListener("reset", function () { juntos = new DataTransfer(); mostrar(); });
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
