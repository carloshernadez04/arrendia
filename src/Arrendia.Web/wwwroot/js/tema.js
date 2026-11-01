(function () {
  try {
    var guardado = document.querySelector("meta[name='arrendia-tema']");
    if (guardado) {
      var valor = guardado.getAttribute("content");
      if (valor === "light" || valor === "dark") localStorage.setItem("arrendia-tema", valor);
      else localStorage.removeItem("arrendia-tema");
    }
    var tema = localStorage.getItem("arrendia-tema");
    if (tema === "light" || tema === "dark") {
      document.documentElement.setAttribute("data-theme", tema);
    }
  } catch (e) { }
})();
