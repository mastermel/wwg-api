// Sets Mantine's colour scheme before the first paint, so dark-mode users don't see a white
// flash while the app loads: the one chosen in the account menu (Mantine keeps it in
// localStorage), or else the OS setting, which is what Mantine resolves "auto" to once it starts.
// A file rather than an inline script, so the CSP can keep script-src 'self'.
(function () {
  var chosen = null;
  try {
    chosen = window.localStorage.getItem("mantine-color-scheme-value");
  } catch {
    // Storage blocked: follow the OS.
  }
  var dark =
    chosen === "dark" ||
    (chosen !== "light" && window.matchMedia("(prefers-color-scheme: dark)").matches);
  var root = document.documentElement;
  root.setAttribute("data-mantine-color-scheme", dark ? "dark" : "light");
  // The app's page canvas (--app-canvas in src/app/theme.ts), until its stylesheet loads.
  root.style.backgroundColor = dark ? "#1b1e25" : "#f1f3f7";
})();
