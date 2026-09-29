import { setWorkerUrl } from "maplibre-gl";
import workerUrl from "maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url";

// MapLibre 6 looks for its worker beside its own module (new URL("./maplibre-gl-worker.mjs",
// import.meta.url)), which bundling moves. Vite builds the worker, with what it imports, as a
// file of its own; this is where it is.
setWorkerUrl(workerUrl);
