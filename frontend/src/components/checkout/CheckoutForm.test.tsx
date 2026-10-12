import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Activity } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as api from "@/lib/api";
import { renderWithQuery } from "@/test/render";
import { CheckoutForm } from "./CheckoutForm";

const push = vi.fn();
let search = new URLSearchParams();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  useSearchParams: () => search,
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return {
    ...actual,
    getCustomers: vi.fn(),
    getProducts: vi.fn(),
    getSavedCards: vi.fn(),
    postCheckout: vi.fn(),
  };
});

const customers: api.Customer[] = [
  { id: 1, firstName: "Wei Ling", lastName: "Tan", email: "w@example.com", fullName: "Wei Ling Tan" },
  { id: 2, firstName: "Arjun", lastName: "Nair", email: "a@example.com", fullName: "Arjun Nair" },
];
const products: api.Product[] = [
  { id: 1, name: "Yoga Drop-in Class", description: "", price: 35, currency: "SGD", paymentType: "OneTime" },
  { id: 3, name: "Monthly Unlimited Membership", description: "", price: 199, currency: "SGD", paymentType: "Recurring" },
];

const assign = vi.fn();

beforeEach(() => {
  vi.resetAllMocks();
  search = new URLSearchParams();
  vi.mocked(api.getCustomers).mockResolvedValue(customers);
  vi.mocked(api.getProducts).mockResolvedValue(products);
  vi.mocked(api.getSavedCards).mockResolvedValue([]);
  Object.defineProperty(window, "location", { value: { assign }, writable: true });
});

/** Picks a customer and a product by id, once the dropdowns have loaded. */
async function choose(user: ReturnType<typeof userEvent.setup>, customerId: number, productId: number) {
  await screen.findByRole("option", { name: "Wei Ling Tan" });
  await screen.findByRole("option", { name: /Yoga Drop-in Class/ });
  await user.selectOptions(screen.getByLabelText("Customer"), String(customerId));
  await user.selectOptions(screen.getByLabelText("Product"), String(productId));
}

describe("CheckoutForm", () => {
  it("shows the payment summary for the selection", async () => {
    const user = userEvent.setup();
    renderWithQuery(<CheckoutForm />);

    await choose(user, 1, 3);

    expect(screen.getByText("Payment summary")).toBeInTheDocument();
    expect(screen.getByText("Recurring")).toBeInTheDocument();
    expect(screen.getByTestId("summary-total")).toHaveTextContent(/SGD\s199\.00/);
  });

  it("preselects the customer and product from Go back", async () => {
    search = new URLSearchParams("customerId=2&productId=1");
    renderWithQuery(<CheckoutForm />);

    await waitFor(() => expect(screen.getByLabelText("Customer")).toHaveValue("2"));
    expect(screen.getByLabelText("Product")).toHaveValue("1");
  });

  it("disables Checkout while the request is in flight, so a double click sends one request", async () => {
    let finish!: (value: api.CheckoutResponse) => void;
    vi.mocked(api.postCheckout).mockReturnValue(new Promise((resolve) => (finish = resolve)));
    const user = userEvent.setup();
    renderWithQuery(<CheckoutForm />);
    await choose(user, 1, 1);

    const button = screen.getByRole("button", { name: /checkout/i });
    await user.dblClick(button);

    expect(screen.getByRole("button", { name: /processing/i })).toBeDisabled();
    expect(api.postCheckout).toHaveBeenCalledTimes(1);

    finish({ invoiceNo: "INV1", status: "Pending", redirectUrl: "https://sandbox-pgw-ui.2c2p.com/x" });
    await waitFor(() => expect(assign).toHaveBeenCalledWith("https://sandbox-pgw-ui.2c2p.com/x"));
    expect(screen.getByRole("button", { name: /redirecting/i })).toBeDisabled();
  });

  it("ignores a second click that lands before the button re-renders as disabled", async () => {
    vi.mocked(api.postCheckout).mockReturnValue(new Promise(() => {}));
    const user = userEvent.setup();
    renderWithQuery(<CheckoutForm />);
    await choose(user, 1, 1);

    // Two submits in the same tick, as a fast double click in a real browser produces.
    const form = screen.getByRole("button", { name: /checkout/i }).closest("form")!;
    form.requestSubmit();
    form.requestSubmit();

    await waitFor(() => expect(api.postCheckout).toHaveBeenCalledTimes(1));
  });

  it("sends a UUID idempotency key and the selected saved card", async () => {
    vi.mocked(api.getSavedCards).mockResolvedValue([{ id: 7, maskedCardNo: "411111XXXXXX1111", createdAt: "" }]);
    vi.mocked(api.postCheckout).mockResolvedValue({ invoiceNo: "INV2", status: "Succeeded", redirectUrl: null });
    const user = userEvent.setup();
    renderWithQuery(<CheckoutForm />);
    await choose(user, 2, 1);

    await user.click(await screen.findByLabelText(/411111XXXXXX1111/));
    await user.click(screen.getByRole("button", { name: /checkout/i }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/result/INV2"));
    const [body, key] = vi.mocked(api.postCheckout).mock.calls[0];
    expect(body).toEqual({ customerId: 2, productId: 1, customerCardId: 7 });
    expect(key).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });

  it("offers saved cards only when the customer has some", async () => {
    const user = userEvent.setup();
    renderWithQuery(<CheckoutForm />);
    await choose(user, 1, 1);

    await waitFor(() => expect(api.getSavedCards).toHaveBeenCalledWith(1));
    expect(screen.queryByText("Pay with a new payment method")).not.toBeInTheDocument();
  });

  it("retries a 409 with the same key instead of starting a new purchase", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.mocked(api.postCheckout)
      .mockRejectedValueOnce(new api.ApiError(409, { detail: "still in progress" }))
      .mockResolvedValueOnce({ invoiceNo: "INV3", status: "Failed", redirectUrl: null });
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    renderWithQuery(<CheckoutForm />);
    await choose(user, 1, 1);

    await user.click(screen.getByRole("button", { name: /checkout/i }));
    expect(await screen.findByRole("button", { name: /finishing your earlier attempt/i })).toBeDisabled();
    await vi.advanceTimersByTimeAsync(2500);
    await waitFor(() => expect(push).toHaveBeenCalledWith("/result/INV3"));

    const [first, retry] = vi.mocked(api.postCheckout).mock.calls.map(([, key]) => key);
    expect(retry).toBe(first);

    vi.useRealTimers();
  });

  it("uses a new key once the selection changes after an attempt", async () => {
    vi.mocked(api.postCheckout).mockRejectedValue(new api.ApiError(422, { detail: "refused" }));
    const user = userEvent.setup();
    renderWithQuery(<CheckoutForm />);
    await choose(user, 1, 1);

    await user.click(screen.getByRole("button", { name: /checkout/i }));
    await screen.findByRole("alert");
    await user.click(screen.getByRole("button", { name: /checkout/i }));
    await waitFor(() => expect(api.postCheckout).toHaveBeenCalledTimes(2));

    await user.selectOptions(screen.getByLabelText("Product"), "3");
    await user.click(screen.getByRole("button", { name: /checkout/i }));
    await waitFor(() => expect(api.postCheckout).toHaveBeenCalledTimes(3));

    const [first, sameAgain, afterChange] = vi.mocked(api.postCheckout).mock.calls.map(([, key]) => key);
    expect(sameAgain).toBe(first);
    expect(afterChange).not.toBe(first);
  });

  it("shows the API's reason when checkout is refused", async () => {
    vi.mocked(api.postCheckout).mockRejectedValue(
      new api.ApiError(422, { errors: { customerCardId: ["Card 7 is not a saved card of customer 1."] } }),
    );
    const user = userEvent.setup();
    renderWithQuery(<CheckoutForm />);
    await choose(user, 1, 1);

    await user.click(screen.getByRole("button", { name: /checkout/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Card 7 is not a saved card of customer 1.");
    expect(screen.getByRole("button", { name: /checkout/i })).toBeEnabled();
  });

  it("starts a new purchase when the page is shown again after a payment (Next.js keeps it alive)", async () => {
    vi.mocked(api.postCheckout).mockResolvedValue({ invoiceNo: "INV1", status: "Succeeded", redirectUrl: null });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const page = (mode: "visible" | "hidden") => (
      <QueryClientProvider client={client}>
        <Activity mode={mode}>
          <CheckoutForm />
        </Activity>
      </QueryClientProvider>
    );
    const user = userEvent.setup();
    const { rerender } = render(page("visible"));

    // Pay: no redirect URL, so the form navigates to the result page and says "Redirecting…".
    await choose(user, 1, 1);
    await user.click(screen.getByRole("button", { name: /checkout/i }));
    await waitFor(() => expect(push).toHaveBeenCalledWith("/result/INV1"));
    expect(screen.getByRole("button", { name: /redirecting/i })).toBeDisabled();

    // Next.js hides the page instead of unmounting it, then shows it again on "New checkout".
    rerender(page("hidden"));
    rerender(page("visible"));

    expect(screen.queryByRole("button", { name: /redirecting/i })).not.toBeInTheDocument();
    expect(screen.getByLabelText("Customer")).toHaveValue("");

    // The next purchase gets its own Idempotency-Key, so it is not a replay of the last one.
    await choose(user, 1, 1);
    await user.click(screen.getByRole("button", { name: /checkout/i }));
    await waitFor(() => expect(api.postCheckout).toHaveBeenCalledTimes(2));
    const [first, second] = vi.mocked(api.postCheckout).mock.calls.map(([, key]) => key);
    expect(second).not.toBe(first);
  });
});
