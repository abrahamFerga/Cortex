// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { GenericTab } from "./GenericTab";
import type { ModuleTab } from "../lib/api";

const foodsTab: ModuleTab = {
  id: "foods",
  label: "Foods",
  route: "/nutrition/foods",
  dataEndpoint: "/api/nutrition/foods",
  columns: [
    { field: "name", header: "Food" },
    { field: "kcalPer100g", header: "kcal/100g" },
  ],
};

function renderTab(tab: ModuleTab, rows: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(rows) } as unknown as Response),
  );
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <GenericTab tab={tab} />
    </QueryClientProvider>,
  );
}

describe("GenericTab (server-driven table)", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("renders the declared column headers and the endpoint's rows", async () => {
    renderTab(foodsTab, [{ name: "Chicken breast", kcalPer100g: 165 }]);

    expect(await screen.findByText("Chicken breast")).toBeTruthy();
    expect(screen.getByText("Food")).toBeTruthy(); // a column header from the manifest
    expect(screen.getByText("165")).toBeTruthy(); // a numeric cell, stringified
  });

  it("shows an empty state when the endpoint returns no rows", async () => {
    renderTab(foodsTab, []);

    expect(await screen.findByText("No data yet.")).toBeTruthy();
  });
});
