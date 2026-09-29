import type { Nation } from "@/api/generated/model";

/** Each nation's name, as the app shows it. */
const labels: Record<Nation, string> = {
  None: "No nation",
  France: "France",
  Britain: "Britain",
  Prussia: "Prussia",
  Austria: "Austria",
  Russia: "Russia",
  Spain: "Spain",
  Portugal: "Portugal",
  Sweden: "Sweden",
  Denmark: "Denmark",
  Holland: "Holland",
  Bavaria: "Bavaria",
  Saxony: "Saxony",
  Wurttemberg: "Württemberg",
  Westphalia: "Westphalia",
  Baden: "Baden",
  Warsaw: "Duchy of Warsaw",
  Italy: "Kingdom of Italy",
  Naples: "Naples",
  Brunswick: "Brunswick",
  Hanover: "Hanover",
  Ottoman: "Ottoman Empire",
};

export const nationLabel = (nation: Nation) => labels[nation];

/** Every nation, for choosing one: "No nation" first, then by name. */
export const nationOptions: { value: Nation; label: string }[] = [
  { value: "None", label: labels.None },
  ...(Object.keys(labels) as Nation[])
    .filter((nation) => nation !== "None")
    .map((nation) => ({ value: nation, label: labels[nation] }))
    .sort((a, b) => a.label.localeCompare(b.label)),
];
