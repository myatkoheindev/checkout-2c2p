import { screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as api from "@/lib/api";
import { renderWithQuery } from "@/test/render";
import { ResultView } from "./ResultView";

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return { ...actual, getTransaction: vi.fn(), inquireTransaction: vi.fn() };
});

const base: api.Transaction = {
  invoiceNo: "INV202610090000000001",
  status: "Pending",
  amount: 199,
  currency: "SGD",
  customerId: 2,
  customerName: "Arjun Nair",
  productId: 3,
  productName: "Monthly Unlimited Membership",
  paymentType: "Recurring",
  flow: "HostedPage",
  paymentChannel: null,
  maskedCardNo: null,
  respCode: null,
  respDesc: null,
  createdAt: "2026-10-09T14:00:00Z",
  paidAt: null,
};

beforeEach(() => vi.clearAllMocks());

describe("ResultView", () => {
  it("shows a succeeded payment with its method and masked card", async () => {
    vi.mocked(api.getTransaction).mockResolvedValue({
      ...base,
      status: "Succeeded",
      paymentChannel: "VI",
      maskedCardNo: "411111XXXXXX1111",
    });

    renderWithQuery(<ResultView invoiceNo={base.invoiceNo} />);

    expect(await screen.findByText("Payment succeeded")).toBeInTheDocument();
    expect(screen.getByText("Monthly Unlimited Membership")).toBeInTheDocument();
    expect(screen.getByText(/^SGD\s199\.00$/)).toBeInTheDocument();
    expect(screen.getByText(base.invoiceNo)).toBeInTheDocument();
    expect(screen.getByText("Visa")).toBeInTheDocument();
    expect(screen.getByText("411111XXXXXX1111")).toBeInTheDocument();
  });

  it("shows the failure reason and a Go back link that preselects the same purchase", async () => {
    vi.mocked(api.getTransaction).mockResolvedValue({ ...base, status: "Failed", respDesc: "Unable to authenticate card holder" });

    renderWithQuery(<ResultView invoiceNo={base.invoiceNo} />);

    expect(await screen.findByTestId("failure-reason")).toHaveTextContent("Unable to authenticate card holder");
    expect(screen.getByRole("link", { name: "Go back" })).toHaveAttribute("href", "/?customerId=2&productId=3");
  });

  it("polls while pending until the outcome arrives", async () => {
    vi.mocked(api.getTransaction)
      .mockResolvedValueOnce(base)
      .mockResolvedValueOnce({ ...base, status: "Succeeded", paymentChannel: "VI" });

    renderWithQuery(<ResultView invoiceNo={base.invoiceNo} />);

    expect(await screen.findByText("Processing payment")).toBeInTheDocument();
    expect(await screen.findByText("Payment succeeded", {}, { timeout: 5000 })).toBeInTheDocument();
    expect(api.inquireTransaction).not.toHaveBeenCalled();
  });

  it("confirms a pending saved-card payment with Payment Inquiry", async () => {
    vi.mocked(api.getTransaction).mockResolvedValue({ ...base, flow: "SavedCard" });
    vi.mocked(api.inquireTransaction).mockResolvedValue({ ...base, flow: "SavedCard", status: "Succeeded" });

    renderWithQuery(<ResultView invoiceNo={base.invoiceNo} />);

    await waitFor(() => expect(api.inquireTransaction).toHaveBeenCalledWith(base.invoiceNo), { timeout: 5000 });
    expect(await screen.findByText("Payment succeeded")).toBeInTheDocument();
  });

  it("says so when the invoice does not exist", async () => {
    vi.mocked(api.getTransaction).mockRejectedValue(new api.ApiError(404, { detail: "not found" }));

    renderWithQuery(<ResultView invoiceNo="INV0" />);

    expect(await screen.findByText("Payment not found")).toBeInTheDocument();
  });
});
