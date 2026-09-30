import { VectorTile } from "@mapbox/vector-tile";
import { PbfReader } from "pbf";
import type { MapBounds } from "@/api/generated/model";
import type { ElevationTileData, VectorTileData } from "@/features/maps/inference/sources";
import { tileAt } from "@/features/maps/inference/sources";
import { tileHosts } from "@/features/maps/map-style";

/**
 * Fetching the map's tiles for inference, in the browser (the same hosts the map draws from;
 * the CSP allows them). Not unit tested: jsdom can't decode images, and this is the network.
 */

/** The layers inference reads (the rest of each tile is skipped). */
const vectorLayers = ["landcover", "water", "waterway", "transportation", "place"] as const;

/** At most this many tiles of each kind: beyond, a lower zoom (coarser, but it finishes). */
export const maxTiles = 400;

// The Earth's circumference at the equator, in metres (Web Mercator's).
const circumference = 40_075_016.7;

/** Every tile of zoom `z` covering the area. */
export function tilesCovering(bounds: MapBounds, z: number) {
  const topLeft = tileAt({ longitude: bounds.west, latitude: bounds.north }, z);
  const bottomRight = tileAt({ longitude: bounds.east, latitude: bounds.south }, z);
  const tiles: { x: number; y: number; z: number }[] = [];
  for (let x = topLeft.x; x <= bottomRight.x; x++) {
    for (let y = topLeft.y; y <= bottomRight.y; y++) tiles.push({ x, y, z });
  }
  return tiles;
}

/**
 * The zoom whose tiles are about `tileWidth` metres across (at the area's middle), within
 * `[min, max]`, lowered until there are no more than `maxTiles`.
 */
export function zoomFor(bounds: MapBounds, tileWidth: number, [min, max]: [number, number]) {
  const latitude = (bounds.south + bounds.north) / 2;
  const world = circumference * Math.cos((latitude * Math.PI) / 180);
  let z = Math.min(max, Math.max(min, Math.round(Math.log2(world / tileWidth))));
  while (z > 0 && tilesCovering(bounds, z).length > maxTiles) z--;
  return z;
}

/** Runs `work` over `items`, a few at a time, reporting as each finishes. */
async function inBatches<T, R>(
  items: T[],
  work: (item: T) => Promise<R | null>,
  onDone: () => void,
): Promise<R[]> {
  const results: R[] = [];
  let next = 0;
  const worker = async () => {
    while (next < items.length) {
      const item = items[next++];
      if (item === undefined) break;
      const result = await work(item);
      if (result !== null) results.push(result);
      onDone();
    }
  };
  await Promise.all(Array.from({ length: 6 }, worker));
  return results;
}

/** The vector tiles' URL template, from OpenFreeMap's TileJSON (it names the current build). */
async function vectorTemplate(): Promise<string> {
  const response = await fetch(tileHosts.vector);
  if (!response.ok) throw new Error("The map's data couldn't be loaded.");
  const tileJson = (await response.json()) as { tiles?: string[] };
  const template = tileJson.tiles?.[0];
  if (!template) throw new Error("The map's data couldn't be loaded.");
  return template;
}

const fill = (template: string, { x, y, z }: { x: number; y: number; z: number }) =>
  template.replace("{z}", String(z)).replace("{x}", String(x)).replace("{y}", String(y));

async function vectorTile(
  template: string,
  tile: { x: number; y: number; z: number },
): Promise<VectorTileData | null> {
  const response = await fetch(fill(template, tile));
  // No tile: nothing there (open sea, say).
  if (response.status === 404 || response.status === 204) return null;
  if (!response.ok) throw new Error("Some of the map's data couldn't be loaded.");
  const decoded = new VectorTile(new PbfReader(new Uint8Array(await response.arrayBuffer())));
  const features: VectorTileData["features"] = [];
  for (const name of vectorLayers) {
    // A tile without the layer (no roads at sea, say) hasn't got its key.
    if (!Object.hasOwn(decoded.layers, name)) continue;
    const layer = decoded.layers[name];
    for (let i = 0; i < layer.length; i++) {
      const feature = layer.feature(i);
      features.push({
        layer: name,
        properties: feature.properties,
        geometry: feature.toGeoJSON(tile.x, tile.y, tile.z).geometry,
      });
    }
  }
  return { ...tile, features };
}

async function elevationTile(tile: {
  x: number;
  y: number;
  z: number;
}): Promise<ElevationTileData | null> {
  const response = await fetch(fill(tileHosts.elevation, tile));
  if (response.status === 404 || response.status === 204) return null;
  if (!response.ok) throw new Error("Some of the map's heights couldn't be loaded.");
  const image = await createImageBitmap(await response.blob());
  const canvas = new OffscreenCanvas(image.width, image.height);
  const context = canvas.getContext("2d");
  if (!context) throw new Error("This browser can't read the map's heights.");
  context.drawImage(image, 0, 0);
  const { data } = context.getImageData(0, 0, image.width, image.height);
  return { ...tile, width: image.width, height: image.height, pixels: data };
}

/** The tiles inference reads, fetched and decoded; `onProgress` as each arrives. */
export async function loadTiles(
  bounds: MapBounds,
  hexSize: number,
  onProgress: (done: number, total: number) => void,
) {
  // Vector tiles about 5 hexes across (OpenFreeMap's zooms go to 14; secondary roads start at 9):
  // 3-mile hexes read zoom 10. Heights at about 25 pixels to a hex (Mapterhorn's 512-pixel
  // Terrarium tiles go to 12): 3-mile hexes read zoom 8, about 200 m a pixel.
  const vector = tilesCovering(bounds, zoomFor(bounds, 5 * hexSize, [9, 14]));
  const heights = tilesCovering(bounds, zoomFor(bounds, (512 * hexSize) / 25, [5, 12]));
  const total = vector.length + heights.length;
  let done = 0;
  const tick = () => {
    done++;
    onProgress(done, total);
  };
  const template = await vectorTemplate();
  const [vectorTiles, elevationTiles] = await Promise.all([
    inBatches(vector, (tile) => vectorTile(template, tile), tick),
    inBatches(heights, elevationTile, tick),
  ]);
  return { vectorTiles, elevationTiles };
}
