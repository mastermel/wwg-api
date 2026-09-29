import { Autocomplete, Loader } from "@mantine/core";
import { useDebouncedValue } from "@mantine/hooks";
import { IconSearch } from "@tabler/icons-react";
import { useState } from "react";
import { useSearchPlaces } from "@/api/generated/endpoints/maps/maps";
import type { PlaceResult } from "@/api/generated/model";
import { errorMessage } from "@/lib/errors";

interface PlaceSearchProps {
  campaignId: string;
  onChoose: (place: PlaceResult) => void;
}

/** Finds a place by name (the API's place search), to fly the map to it. */
export function PlaceSearch({ campaignId, onChoose }: PlaceSearchProps) {
  const [text, setText] = useState("");
  const [search] = useDebouncedValue(text.trim(), 400);
  const places = useSearchPlaces(
    campaignId,
    { search },
    // Live answers, never saved; only once there's enough to search for.
    { query: { enabled: search.length >= 2, meta: { persist: false }, staleTime: 60_000 } },
  );
  const results = search.length >= 2 ? (places.data ?? []) : [];

  return (
    <Autocomplete
      label="Find a place"
      description="A town, region or country. Then pan and zoom to frame the campaign's area."
      placeholder="Leipzig"
      leftSection={<IconSearch size={16} aria-hidden />}
      rightSection={places.isFetching ? <Loader size="xs" /> : null}
      value={text}
      onChange={setText}
      data={results.map((place) => place.description)}
      // The API has already matched them; don't filter again.
      filter={({ options }) => options}
      onOptionSubmit={(description) => {
        const place = results.find((p) => p.description === description);
        if (place) onChoose(place);
      }}
      error={places.isError ? errorMessage(places.error, "Place search didn't work.") : undefined}
      comboboxProps={{ withinPortal: false }}
    />
  );
}
