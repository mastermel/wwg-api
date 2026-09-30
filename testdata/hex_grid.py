"""Writes hex-grid.json: expected figures for the campaign hex grid (decision 0014).

An independent reference for HexGrid.cs and hex-grid.ts, which are both tested against its
output. Run it from this folder (python3 hex_grid.py) after changing the grid's arithmetic.
"""

import json
import math

R = 6_371_008.8  # The mean Earth radius, in metres.


def grid(b, size):
    lat0 = (b["south"] + b["north"]) / 2
    lon0 = (b["west"] + b["east"]) / 2
    k = R * math.cos(math.radians(lat0))
    s = size / math.sqrt(3)  # Corner to centre.

    def project(lat, lon):
        return k * math.radians(lon - lon0), R * math.radians(lat0 - lat)

    def unproject(x, y):
        return lat0 - math.degrees(y / R), lon0 + math.degrees(x / k)

    def rnd(v):
        return math.floor(v + 0.5)

    def hex_at(lat, lon):
        x, y = project(lat, lon)
        qf = (2 / 3 * x) / s
        rf = (-1 / 3 * x + math.sqrt(3) / 3 * y) / s
        xf, zf = qf, rf
        yf = -xf - zf
        rx, ry, rz = rnd(xf), rnd(yf), rnd(zf)
        dx, dy, dz = abs(rx - xf), abs(ry - yf), abs(rz - zf)
        if dx > dy and dx > dz:
            rx = -ry - rz
        elif dy > dz:
            ry = -rx - rz
        else:
            rz = -rx - ry
        return int(rx), int(rz)

    def centre(q, r):
        return unproject(1.5 * s * q, size * (r + q / 2))

    def contains(q, r):
        lat, lon = centre(q, r)
        return b["south"] <= lat <= b["north"] and b["west"] <= lon <= b["east"]

    def all_hexes():
        xw, _ = project(lat0, b["west"])
        xe, _ = project(lat0, b["east"])
        _, yn = project(b["north"], lon0)
        _, ys = project(b["south"], lon0)
        return [
            (q, r)
            for q in range(math.floor(xw / (1.5 * s)) - 1, math.ceil(xe / (1.5 * s)) + 2)
            for r in range(math.floor(yn / size - q / 2) - 1, math.ceil(ys / size - q / 2) + 2)
            if contains(q, r)
        ]

    return hex_at, centre, contains, all_hexes


CASES = [
    (
        "Waterloo, 3 miles",
        {"west": 4.2, "south": 50.6, "east": 4.6, "north": 50.8},
        4828,
        [(50.7, 4.4), (50.72, 4.4), (50.73, 4.45), (50.66, 4.34), (50.61, 4.21), (50.79, 4.59), (50.75, 4.52)],
    ),
    (
        "Peninsula, 5 km",
        {"west": -9.5, "south": 36.0, "east": 3.3, "north": 43.8},
        5000,
        [(40.4, -3.7), (38.7, -9.1), (41.4, 2.2), (37.4, -5.98)],
    ),
]
OUTSIDE = [(1000, 0), (0, -1000), (-1000, 1000)]

cases = []
for name, bounds, size, points in CASES:
    hex_at, centre, contains, all_hexes = grid(bounds, size)
    expected_points = []
    for lat, lon in points:
        q, r = hex_at(lat, lon)
        clat, clon = centre(q, r)
        expected_points.append(
            {
                "latitude": lat,
                "longitude": lon,
                "q": q,
                "r": r,
                "centre": {"latitude": round(clat, 7), "longitude": round(clon, 7)},
            }
        )
    assert not any(contains(q, r) for q, r in OUTSIDE)
    cases.append(
        {
            "name": name,
            "bounds": bounds,
            "hexSize": size,
            "points": expected_points,
            "hexCount": len(all_hexes()),
            "outside": [{"q": q, "r": r} for q, r in OUTSIDE],
        }
    )

about = (
    "Expected figures for the hex grid (decision 0014), from an independent Python "
    "implementation (hex_grid.py). HexGrid.cs and hex-grid.ts are both tested against them."
)
with open("hex-grid.json", "w") as out:
    json.dump({"about": about, "cases": cases}, out, indent=2)
