import { useComputedColorScheme } from "@mantine/core";
import { useMemo } from "react";
import { Layer, Source } from "react-map-gl/maplibre";
import type { CampaignGridResponse } from "@/api/generated/model";
import type { HexGrid } from "@/features/maps/hex-grid";
import { mapPalettes } from "@/features/maps/map-style";
import { acrossStored, sideCorners, type Side } from "@/features/maps/terrain";

/** The parts of the terrain a viewer shows (the Map page's Layers panel). */
export interface TerrainParts {
  terrain: boolean;
  roads: boolean;
  rivers: boolean;
  towns: boolean;
  bridges: boolean;
}

const everyPart: TerrainParts = {
  terrain: true,
  roads: true,
  rivers: true,
  towns: true,
  bridges: true,
};

interface TerrainLayerProps {
  grid: HexGrid;
  terrain: CampaignGridResponse;
  show?: TerrainParts;
}

const visibility = (shown: boolean) => ({ visibility: shown ? "visible" : "none" }) as const;

type Feature = GeoJSON.Feature;

const collection = (features: Feature[]): GeoJSON.FeatureCollection => ({
  type: "FeatureCollection",
  features,
});

/** The stored side as the hex it's stored on sees it (N, NE or SE are its own). */
const asSide = (side: string) => side as Side;

/**
 * The campaign's terrain (decisions 0014 and 0016), drawn over the map inside a CampaignMap:
 * hexes tinted by their ground and forest, rivers along the hexsides, roads and waterways from
 * hex to hex, bridges where a road crosses a river, and towns, cities and fortresses at the
 * hexes' centres, with their names.
 */
export function TerrainLayer({ grid, terrain, show = everyPart }: TerrainLayerProps) {
  const p = mapPalettes[useComputedColorScheme("light")];
  const data = useMemo(() => {
    const hexes = terrain.cells.map((cell): Feature => ({
      type: "Feature",
      properties: { terrain: cell.terrain, forest: cell.forest },
      geometry: {
        type: "Polygon",
        coordinates: [[...grid.corners(cell), grid.corners(cell)[0] ?? [0, 0]]],
      },
    }));
    const places = terrain.cells
      .filter((cell) => cell.settlement.size !== "None" || cell.settlement.fortress)
      .map((cell): Feature => {
        const { longitude, latitude } = grid.centre(cell);
        return {
          type: "Feature",
          properties: { ...cell.settlement, name: cell.settlement.name ?? "" },
          geometry: { type: "Point", coordinates: [longitude, latitude] },
        };
      });
    const lines: Feature[] = [];
    const bridges: Feature[] = [];
    for (const edge of terrain.edges) {
      const from = grid.centre(edge);
      const to = grid.centre(acrossStored(edge));
      const across: [number, number][] = [
        [from.longitude, from.latitude],
        [to.longitude, to.latitude],
      ];
      if (edge.road !== "None") {
        lines.push({
          type: "Feature",
          properties: { kind: "road", road: edge.road },
          geometry: { type: "LineString", coordinates: across },
        });
      }
      if (edge.waterway !== "None") {
        lines.push({
          type: "Feature",
          properties: { kind: "waterway" },
          geometry: { type: "LineString", coordinates: across },
        });
      }
      if (edge.river) {
        const along = sideCorners(grid, edge, asSide(edge.side));
        lines.push({
          type: "Feature",
          properties: { kind: "river" },
          geometry: { type: "LineString", coordinates: along },
        });
        if (edge.bridge) {
          const [[x1, y1], [x2, y2]] = along as [[number, number], [number, number]];
          bridges.push({
            type: "Feature",
            properties: {},
            geometry: { type: "Point", coordinates: [(x1 + x2) / 2, (y1 + y2) / 2] },
          });
        }
      }
    }
    return {
      hexes: collection(hexes),
      lines: collection(lines),
      bridges: collection(bridges),
      places: collection(places),
    };
  }, [grid, terrain]);

  return (
    <>
      <Source id="terrain-hexes" type="geojson" data={data.hexes}>
        <Layer
          id="terrain-ground"
          layout={visibility(show.terrain)}
          type="fill"
          filter={["!=", ["get", "terrain"], "Flat"]}
          paint={{
            "fill-color": [
              "match",
              ["get", "terrain"],
              "LowHill",
              p.lowHill,
              "HighHill",
              p.highHill,
              "Mountain",
              p.mountain,
              p.waterHex,
            ],
            "fill-opacity": 0.45,
          }}
        />
        <Layer
          id="terrain-forest"
          layout={visibility(show.terrain)}
          type="fill"
          filter={["==", ["get", "forest"], true]}
          paint={{ "fill-color": p.forestHex, "fill-opacity": 0.35 }}
        />
      </Source>
      <Source id="terrain-lines" type="geojson" data={data.lines}>
        <Layer
          id="terrain-rivers"
          type="line"
          filter={["==", ["get", "kind"], "river"]}
          layout={{ ...visibility(show.rivers), "line-cap": "round" }}
          paint={{ "line-color": p.river, "line-width": 4 }}
        />
        <Layer
          id="terrain-waterways"
          layout={visibility(show.rivers)}
          type="line"
          filter={["==", ["get", "kind"], "waterway"]}
          paint={{ "line-color": p.river, "line-width": 2, "line-dasharray": [1, 1] }}
        />
        <Layer
          id="terrain-good-roads"
          type="line"
          filter={["all", ["==", ["get", "kind"], "road"], ["==", ["get", "road"], "Good"]]}
          layout={{ ...visibility(show.roads), "line-cap": "round" }}
          paint={{ "line-color": p.road, "line-width": 3 }}
        />
        <Layer
          id="terrain-poor-roads"
          layout={visibility(show.roads)}
          type="line"
          filter={["all", ["==", ["get", "kind"], "road"], ["==", ["get", "road"], "Poor"]]}
          paint={{ "line-color": p.road, "line-width": 2, "line-dasharray": [3, 2] }}
        />
      </Source>
      <Source id="terrain-bridges" type="geojson" data={data.bridges}>
        <Layer
          id="terrain-bridges"
          type="circle"
          layout={visibility(show.bridges)}
          paint={{
            "circle-radius": 4,
            "circle-color": p.road,
            "circle-stroke-color": p.label,
            "circle-stroke-width": 1,
          }}
        />
      </Source>
      <Source id="terrain-places" type="geojson" data={data.places}>
        <Layer
          id="terrain-places"
          type="circle"
          layout={visibility(show.towns)}
          paint={{
            "circle-radius": ["match", ["get", "size"], "City", 7, 5],
            "circle-color": [
              "case",
              ["all", ["get", "fortress"], ["==", ["get", "size"], "None"]],
              p.fortress,
              p.label,
            ],
            "circle-stroke-color": ["case", ["get", "fortress"], p.fortress, p.halo],
            "circle-stroke-width": ["case", ["get", "walled"], 3, ["get", "fortress"], 3, 1],
          }}
        />
        <Layer
          id="terrain-place-names"
          type="symbol"
          layout={{
            ...visibility(show.towns),
            "text-field": ["get", "name"],
            "text-font": ["Noto Sans Regular"],
            "text-size": 12,
            "text-offset": [0, 1.1],
            "text-anchor": "top",
          }}
          paint={{ "text-color": p.label, "text-halo-color": p.halo, "text-halo-width": 1.5 }}
        />
      </Source>
    </>
  );
}
