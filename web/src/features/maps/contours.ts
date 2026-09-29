import { addProtocol } from "maplibre-gl";
import mlcontour from "maplibre-contour";
import { tileHosts } from "@/features/maps/map-style";

let demSource: InstanceType<typeof mlcontour.DemSource> | undefined;

/**
 * The tile URL of contour lines drawn in the browser from the elevation tiles (maplibre-contour,
 * in a web worker), every 50 m (or 200 ft) and closer at high zooms; major lines each 5th.
 */
export function contourTiles(feet: boolean): string {
  if (!demSource) {
    demSource = new mlcontour.DemSource({
      url: tileHosts.elevation,
      encoding: "terrarium",
      maxzoom: 12,
      worker: true,
    });
    demSource.setupMaplibre({ addProtocol });
  }
  return demSource.contourProtocolUrl({
    multiplier: feet ? 3.28084 : 1,
    thresholds: feet
      ? { 9: [1000, 5000], 11: [500, 2500], 12: [200, 1000], 14: [100, 500] }
      : { 9: [250, 1000], 11: [100, 500], 12: [50, 250], 14: [20, 100] },
  });
}
