import { screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { getGetHealthMockHandler } from "@/api/generated/endpoints/health/health.msw";
import { server } from "@/test/server";
import { expectNoAxeViolations, renderApp } from "@/test/render";

describe("About page", () => {
  it("shows the server as healthy when the API is", async () => {
    server.use(getGetHealthMockHandler({ status: "Healthy" }));

    await renderApp("/about");

    expect(await screen.findByText("Healthy")).toBeInTheDocument();
  });

  it("shows the server as unhealthy when the API answers 503", async () => {
    server.use(
      http.get("*/health", () => HttpResponse.json({ status: "Unhealthy" }, { status: 503 })),
    );

    await renderApp("/about");

    expect(await screen.findByText("Unhealthy")).toBeInTheDocument();
  });

  it("shows the server as unreachable when the request fails", async () => {
    server.use(http.get("*/health", () => HttpResponse.error()));

    await renderApp("/about");

    expect(await screen.findByText("Unreachable")).toBeInTheDocument();
  });

  it("has no detectable accessibility problems", async () => {
    server.use(getGetHealthMockHandler({ status: "Healthy" }));
    const { container } = await renderApp("/about");
    await screen.findByText("Healthy");

    await expectNoAxeViolations(container);
  });
});
