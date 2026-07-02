// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { AdminErrorBoundary } from "./AdminErrorBoundary";

afterEach(cleanup);

function Boom(): never {
  throw new Error("kaboom in a section");
}

describe("AdminErrorBoundary", () => {
  it("renders its children when they do not throw", () => {
    render(
      <AdminErrorBoundary>
        <p>all good</p>
      </AdminErrorBoundary>,
    );

    // getByText throws if absent, so reaching the assertion already proves it rendered.
    expect(screen.getByText("all good")).toBeTruthy();
  });

  it("shows a recoverable fallback (not a white screen) when a child throws", () => {
    // React re-throws to console.error even when a boundary catches it; silence it for a clean run.
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    try {
      render(
        <AdminErrorBoundary>
          <Boom />
        </AdminErrorBoundary>,
      );

      expect(screen.getByRole("alert")).toBeTruthy();
      // The specific failure message reaches the operator, not just a generic screen.
      expect(screen.getByText("kaboom in a section")).toBeTruthy();
      expect(screen.getByRole("button", { name: /reload console/i })).toBeTruthy();
    } finally {
      spy.mockRestore();
    }
  });
});
