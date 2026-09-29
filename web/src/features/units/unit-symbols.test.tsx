import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { UnitType } from "@/api/generated/model";
import { AppProviders } from "@/app/AppProviders";
import { createQueryClient } from "@/app/query-client";
import { UnitLegend } from "@/features/units/UnitLegend";
import { unitTypeLabels } from "@/features/units/unit-types";
import { expectNoAxeViolations } from "@/test/render";

function renderLegend() {
  return render(
    <AppProviders queryClient={createQueryClient()}>
      <UnitLegend />
    </AppProviders>,
  );
}

describe("unit symbols", () => {
  it("explains every type's symbol in the legend, in words", () => {
    renderLegend();

    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(Object.values(UnitType).length);
    // In the API's order, each with its name (the symbol itself is hidden from screen readers).
    Object.values(UnitType).forEach((type, index) => {
      expect(within(items[index] ?? document.body).getByText(unitTypeLabels[type])).toBeVisible();
    });
  });

  it("has no detectable accessibility problems", async () => {
    const { container } = renderLegend();

    await expectNoAxeViolations(container);
  });
});
