(function () {
  try {
    var tema = localStorage.getItem("arrendia-tema");
    if (tema === "light" || tema === "dark") {
      document.documentElement.setAttribute("data-theme", tema);
    }
  } catch (e) { }
})();
