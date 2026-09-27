// Sets Mantine's colour scheme before the first paint, so dark-mode users don't see a white
// flash while the app loads. The app follows the OS setting (no toggle), which is what Mantine
// resolves "auto" to once it starts. A file rather than an inline script, so the CSP can keep
// script-src 'self'.
(function () {
  var dark = window.matchMedia("(prefers-color-scheme: dark)").matches;
  var root = document.documentElement;
  root.setAttribute("data-mantine-color-scheme", dark ? "dark" : "light");
  // Mantine's body background (dark[7] / white), until its stylesheet loads.
  root.style.backgroundColor = dark ? "#242424" : "#ffffff";
})();
