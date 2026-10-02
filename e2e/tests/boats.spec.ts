import { apiAs } from "./support/api.ts";
import { scan } from "./support/axe.ts";
import { expect, test } from "./support/fixtures.ts";
import { browserOf, libraryFaction } from "./support/library.ts";
import { clickMapPart } from "./support/map.ts";
import { approveAndStartNext, startedCampaign } from "./support/turns.ts";

test("a commander embarks a unit on its army's boats, and lands it across the river", async ({
  signUp,
}) => {
  test.slow();
  const umpire = await signUp("Ada");
  const bob = await signUp("Bob");
  const { campaignUrl, campaignId, armyId } = await startedCampaign(umpire, bob, "Namur");

  // A river course east from the Guard's hex, and a river along its north side.
  const api = await apiAs(umpire.page);
  await api.put(`/api/campaigns/${campaignId}/grid/edges/0/0/SE`, {
    road: "None",
    river: false,
    bridge: false,
    waterway: "Out",
  });
  await api.put(`/api/campaigns/${campaignId}/grid/edges/0/0/N`, {
    road: "None",
    river: true,
    bridge: false,
    waterway: "None",
  });
  // Three boats beside the Guard (30 points, 14 a boat), from a faction of the army's.
  const faction = await libraryFaction(browserOf(umpire.page), "Boatmen", "France", [
    { name: "Barge 1", type: "Boat" },
    { name: "Barge 2", type: "Boat" },
    { name: "Barge 3", type: "Boat" },
  ]);
  const army = await api.get<{
    name: string;
    side: { id: string };
    color: string;
    nation: string;
    factions: { id: string }[];
  }>(`/api/armies/${armyId}`);
  await api.put(`/api/armies/${armyId}`, {
    name: army.name,
    sideId: army.side.id,
    color: army.color,
    nation: army.nation,
    factionIds: [...army.factions.map((f) => f.id), faction.id],
  });
  const boats = await api.post<{ id: string }[]>(`/api/armies/${armyId}/units`, {
    unitIds: faction.units.map((unit) => unit.id),
  });
  for (const boat of boats) {
    await api.put(`/api/army-units/${boat.id}/placement`, { q: 0, r: 0 });
  }

  const page = bob.page;
  await page.goto(`${campaignUrl}/map`);
  await page.getByRole("button", { name: /^4 units: .*Imperial Guard/ }).click();
  await page.getByRole("button", { name: /^Imperial Guard, Line Infantry/ }).click();
  const drawer = page.getByRole("dialog", { name: "Imperial Guard" });
  await expect(drawer).toContainText("It needs 3 boats; 3 free here.");
  expect(await scan(page, "unit drawer, boats")).toEqual([]);
  await drawer.getByRole("button", { name: "Embark" }).click();
  await expect(page.getByText("Imperial Guard will embark.")).toBeVisible();

  // The Reserve Artillery holds; the boats need nothing: they go with the Guard.
  const bobs = await apiAs(page);
  const turns = await bobs.get<{ id: string; open: boolean }[]>(`/api/armies/${armyId}/turns`);
  const turn = turns.find((t) => t.open);
  const units = await bobs.get<{ id: string; name: string }[]>(
    `/api/campaigns/${campaignId}/units`,
  );
  const artillery = units.find((u) => u.name === "Reserve Artillery");
  await bobs.put(`/api/army-turns/${turn?.id ?? ""}/orders/${artillery?.id ?? ""}`, {
    kind: "Hold",
  });
  await bobs.post(`/api/army-turns/${turn?.id ?? ""}/submit`, null);
  await approveAndStartNext(umpire, campaignId, armyId);

  // On its boats now: one marker, marked as such.
  await page.reload();
  await page
    .getByRole("button", { name: /^Imperial Guard, Line Infantry, Armée du Nord, on boats/ })
    .click();
  await expect(drawer).toContainText("On 3 boats");
  await drawer.getByRole("button", { name: "Land" }).click();
  await expect(page.getByText(/Tap a shaded hex for where Imperial Guard lands/)).toBeVisible();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  // The hex north of the Guard's, across the river: a fifth of the map's height up.
  await clickMapPart(page, 0, -0.22);
  await expect(page.getByText("Imperial Guard will land.")).toBeVisible();
  await expect(
    page.getByRole("region", { name: "Turn 2" }).getByText("Lands in the next hex"),
  ).toBeVisible();
});
