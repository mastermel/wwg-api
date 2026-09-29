import { apiAs, waterlooMap } from "./api.ts";
import { createCampaign, join, joinLink } from "./campaigns.ts";
import type { User } from "./fixtures.ts";

/** Where the units start: the middle of the map's area, and away to the south-west. */
export const startingPlaces = {
  "Imperial Guard": { latitude: 50.7, longitude: 4.4 },
  "Reserve Artillery": { latitude: 50.64, longitude: 4.25 },
} as const;

/**
 * A campaign the Umpire has set up and started through the API (the tests aren't about that):
 * the map's area, a faction, and the commander's army "Armée du Nord" with the Imperial Guard
 * and the Reserve Artillery placed apart, each moving up to `limitMetres` a turn. Turn 1 is open.
 * Leaves the Umpire on the campaign.
 */
export async function startedCampaign(
  umpire: User,
  commander: User,
  name: string,
  limitMetres = 20_000,
) {
  await createCampaign(umpire.page, name);
  const campaignUrl = umpire.page.url();
  const campaignId = new URL(campaignUrl).pathname.split("/").at(-1) ?? "";
  await join(commander.page, await joinLink(umpire.page), name);

  const api = await apiAs(umpire.page);
  await api.put(`/api/campaigns/${campaignId}/map`, {
    ...waterlooMap,
    movementLimits: waterlooMap.movementLimits.map((l) => ({ ...l, metres: limitMetres })),
  });
  const faction = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/factions`, {
    name: "French Empire",
  });
  const members = await api.get<{ id: string; firstName: string }[]>(
    `/api/campaigns/${campaignId}/members`,
  );
  const army = await api.post<{ id: string }>(`/api/campaigns/${campaignId}/armies`, {
    name: "Armée du Nord",
    commanderMemberId: members.find((m) => m.firstName === commander.firstName)?.id ?? null,
    factionId: faction.id,
    nation: "France",
  });
  for (const [unitName, type] of [
    ["Imperial Guard", "HeavyInfantry"],
    ["Reserve Artillery", "FootArtillery"],
  ] as const) {
    const unit = await api.post<{ id: string }>(`/api/armies/${army.id}/units`, {
      name: unitName,
      type,
      fightingFactor: 6,
      points: 30,
    });
    await api.put(`/api/units/${unit.id}/placement`, startingPlaces[unitName]);
  }
  await api.post(`/api/campaigns/${campaignId}/start`, null);
  return { campaignUrl, campaignId, armyId: army.id };
}
