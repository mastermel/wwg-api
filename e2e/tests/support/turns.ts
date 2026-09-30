import { apiAs, waterlooMap } from "./api.ts";
import { createCampaign, join, joinLink } from "./campaigns.ts";
import { browserOf, libraryFaction } from "./library.ts";
import type { User } from "./fixtures.ts";

/**
 * Where the units start: the middle of the map's area, and 5 km or so to the south-west, apart
 * but both in view on a phone (which shows the area's full height, and only part of its width).
 */
export const startingPlaces = {
  "Imperial Guard": { q: 0, r: 0 },
  "Reserve Artillery": { q: -1, r: 1 },
} as const;

/**
 * A campaign the Umpire has set up and started through the API (the tests aren't about that):
 * the map's area, a side, and the commander's army "Armée du Nord" with the Imperial Guard
 * and the Reserve Artillery (from a library faction of its own) placed apart, in hexes `hexSize` metres across (3 miles unless given;
 * both units are line infantry and foot artillery, which move two hexes a turn). Turn 1 is open.
 * Leaves the Umpire on the campaign.
 */
export async function startedCampaign(umpire: User, commander: User, name: string, hexSize = 4828) {
  await createCampaign(umpire.page, name);
  const campaignUrl = umpire.page.url();
  const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
  await join(commander.page, await joinLink(umpire.page), name);

  const api = await apiAs(umpire.page);
  await api.put(`/api/campaigns/${campaignId}/map`, {
    ...waterlooMap,
    hexSize,
  });
  const side = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/sides`, {
    name: "French Empire",
  });
  const members = await api.get<{ id: string; firstName: string }[]>(
    `/api/campaigns/${campaignId}/members`,
  );
  const faction = await libraryFaction(browserOf(umpire.page), "French", "France", [
    { name: "Imperial Guard", type: "LineInfantry" },
    { name: "Reserve Artillery", type: "FootArtillery" },
  ]);
  const army = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/armies`, {
    name: "Armée du Nord",
    commanderMemberId: members.find((m) => m.firstName === commander.firstName)?.id ?? null,
    sideId: side.id,
    nation: "France",
    factionIds: [faction.id],
  });
  const units = await api.post<{ id: string; name: keyof typeof startingPlaces }[]>(
    `/api/armies/${army.id}/units`,
    { unitIds: faction.units.map((unit) => unit.id) },
  );
  for (const unit of units) {
    await api.put(`/api/army-units/${unit.id}/placement`, startingPlaces[unit.name]);
  }
  // French infantry would march a hex further in turn 1's Morning (step 45): not in these tests,
  // which are about the moves themselves.
  await api.put(`/api/campaigns/${campaignId}/calendar`, {
    startDate: null,
    firstTurnPart: "Morning",
    morningNations: [],
    afternoonNations: [],
  });
  await api.post(`/api/campaigns/${campaignId}/start`, null);
  return { campaignUrl, campaignId, armyId: army.id };
}

/**
 * The commander gives every unit an order and submits the open turn, through the API: Hold, or
 * for units named in `moves`, a Move along those hexes.
 */
export async function holdAndSubmit(
  commander: User,
  campaignId: string,
  armyId: string,
  moves: Partial<Record<string, { q: number; r: number }[]>> = {},
) {
  const api = await apiAs(commander.page);
  const turns = await api.get<{ id: string; open: boolean }[]>(`/api/armies/${armyId}/turns`);
  const turn = turns.find((t) => t.open);
  if (!turn) throw new Error("The army has no open turn.");
  const units = await api.get<{ id: string; armyId: string; name: string }[]>(
    `/api/campaigns/${campaignId}/units`,
  );
  for (const unit of units.filter((u) => u.armyId === armyId)) {
    const path = moves[unit.name];
    await api.put(
      `/api/army-turns/${turn.id}/orders/${unit.id}`,
      path ? { kind: "Move", path } : { kind: "Hold" },
    );
  }
  await api.post(`/api/army-turns/${turn.id}/submit`, null);
}

/** The Umpire approves the army's open turn and starts the next, through the API. */
export async function approveAndStartNext(umpire: User, campaignId: string, armyId: string) {
  const api = await apiAs(umpire.page);
  const turns = await api.get<{ id: string; open: boolean }[]>(`/api/armies/${armyId}/turns`);
  const turn = turns.find((t) => t.open);
  if (!turn) throw new Error("The army has no open turn.");
  await api.post(`/api/army-turns/${turn.id}/approve`, null);
  await api.post(`/api/campaigns/${campaignId}/turns`, null);
}
