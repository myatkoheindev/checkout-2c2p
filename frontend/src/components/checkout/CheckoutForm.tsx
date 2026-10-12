"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { useLayoutEffect, useRef, useState } from "react";
import {
  ApiError,
  getCustomers,
  getProducts,
  getSavedCards,
  postCheckout,
  type CheckoutRequest,
  type CheckoutResponse,
} from "@/lib/api";
import { formatMoney, paymentTypeLabel } from "@/lib/format";
import { Card } from "@/components/ui/Card";
import { Select } from "@/components/ui/Select";
import { Spinner } from "@/components/ui/Spinner";
import { useIdempotencyKey } from "./useIdempotencyKey";

const NEW_METHOD = "new";

/** How often, and how many times, a 409 (same key still in progress) is retried with the same key. */
const CONFLICT_RETRY_DELAY_MS = 2000;
const CONFLICT_RETRIES = 5;

function idFrom(value: string | null): number | null {
  const id = Number(value);
  return Number.isInteger(id) && id > 0 ? id : null;
}

export function CheckoutForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const idempotencyKey = useIdempotencyKey();
  const sentWithCurrentKey = useRef(false);
  // Set synchronously on submit: a second click can land before React re-renders the button as
  // disabled (seen in a real browser), and must not send a second request.
  const submitting = useRef(false);

  // "Go back" from a failed payment preselects the same customer and product.
  const [initialCustomerId] = useState(() => idFrom(searchParams.get("customerId")));
  const [initialProductId] = useState(() => idFrom(searchParams.get("productId")));
  const [customerId, setCustomerId] = useState<number | null>(initialCustomerId);
  const [productId, setProductId] = useState<number | null>(initialProductId);
  const [method, setMethod] = useState<string>(NEW_METHOD);
  const [waitingForEarlierAttempt, setWaitingForEarlierAttempt] = useState(false);

  const customers = useQuery({ queryKey: ["customers"], queryFn: getCustomers });
  const products = useQuery({ queryKey: ["products"], queryFn: getProducts });
  const cards = useQuery({
    queryKey: ["customers", customerId, "cards"],
    queryFn: () => getSavedCards(customerId!),
    enabled: customerId !== null,
  });

  const customer = customers.data?.find((c) => c.id === customerId);
  const product = products.data?.find((p) => p.id === productId);
  const savedCards = cards.data ?? [];

  const checkout = useMutation({
    mutationFn: async (body: CheckoutRequest): Promise<CheckoutResponse> => {
      const key = idempotencyKey.current();
      sentWithCurrentKey.current = true;

      // A 409 means an earlier click with this key is still being processed: wait and ask again
      // with the same key, which returns that attempt's result rather than starting a new one.
      for (let attempt = 0; ; attempt++) {
        try {
          return await postCheckout(body, key);
        } catch (error) {
          if (!(error instanceof ApiError) || error.status !== 409 || attempt >= CONFLICT_RETRIES) throw error;
          setWaitingForEarlierAttempt(true);
          await new Promise((resolve) => setTimeout(resolve, CONFLICT_RETRY_DELAY_MS));
        }
      }
    },
    onSettled: () => setWaitingForEarlierAttempt(false),
    onError: () => {
      submitting.current = false;
    },
    onSuccess: (result) => {
      if (result.redirectUrl) {
        // The 2C2P hosted page, or a 3DS page for a saved card.
        window.location.assign(result.redirectUrl);
      } else {
        router.push(`/result/${encodeURIComponent(result.invoiceNo)}`);
      }
    },
  });

  // Next.js (Cache Components) keeps this page alive but hidden after navigating away, e.g. to the
  // result page after a saved-card payment, and shows the same instance on "New checkout". Coming
  // back is a new purchase, so reset when hidden: otherwise the button stays on "Redirecting…" and
  // the old Idempotency-Key would replay the last payment instead of starting a new one.
  const { reset: resetCheckout } = checkout;
  const { renew: renewKey } = idempotencyKey;
  useLayoutEffect(
    () => () => {
      submitting.current = false;
      sentWithCurrentKey.current = false;
      renewKey();
      resetCheckout();
      setWaitingForEarlierAttempt(false);
      setCustomerId(initialCustomerId);
      setProductId(initialProductId);
      setMethod(NEW_METHOD);
    },
    [resetCheckout, renewKey, initialCustomerId, initialProductId],
  );

  /** A different selection is a different purchase: it must not reuse a key already sent. */
  function selectionChanged() {
    if (sentWithCurrentKey.current) {
      idempotencyKey.renew();
      sentWithCurrentKey.current = false;
    }
    submitting.current = false;
    checkout.reset();
  }

  const busy = checkout.isPending || checkout.isSuccess; // success means the browser is leaving
  const canCheckout = customer !== undefined && product !== undefined && !busy;

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!customer || !product || busy || submitting.current) return;
    submitting.current = true;

    checkout.mutate({
      customerId: customer.id,
      productId: product.id,
      ...(method !== NEW_METHOD && { customerCardId: Number(method) }),
    });
  }

  const loadError = customers.error ?? products.error;

  return (
    <form onSubmit={submit} className="flex flex-col gap-6" aria-busy={busy}>
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Checkout</h1>
        <p className="mt-1 text-sm text-slate-500">Secure payment with 2C2P · Singapore dollars</p>
      </header>

      {loadError && (
        <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-700">
          Could not load the checkout. Is the API running at the configured address?
        </p>
      )}

      <Card className="flex flex-col gap-4">
        <Select
          id="customer"
          label="Customer"
          value={customerId ?? ""}
          disabled={busy || !customers.data}
          onChange={(event) => {
            setCustomerId(idFrom(event.target.value));
            setMethod(NEW_METHOD);
            selectionChanged();
          }}
        >
          <option value="" disabled>
            {customers.isLoading ? "Loading customers…" : "Select a customer"}
          </option>
          {customers.data?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.fullName}
            </option>
          ))}
        </Select>

        <Select
          id="product"
          label="Product"
          value={productId ?? ""}
          disabled={busy || !products.data}
          onChange={(event) => {
            setProductId(idFrom(event.target.value));
            selectionChanged();
          }}
        >
          <option value="" disabled>
            {products.isLoading ? "Loading products…" : "Select a product"}
          </option>
          {products.data?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name} — {formatMoney(p.price, p.currency)}
            </option>
          ))}
        </Select>
      </Card>

      {customer && product && (
        <Card>
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">
            Payment summary
          </h2>
          <dl className="mt-4 grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
            <dt className="text-slate-500">Customer</dt>
            <dd className="text-right font-medium">{customer.fullName}</dd>
            <dt className="text-slate-500">Product</dt>
            <dd className="text-right font-medium">{product.name}</dd>
            <dt className="text-slate-500">Payment type</dt>
            <dd className="text-right font-medium">{paymentTypeLabel(product.paymentType)}</dd>
            <dt className="text-slate-500">Currency</dt>
            <dd className="text-right font-medium">{product.currency}</dd>
          </dl>
          <div className="mt-4 flex items-baseline justify-between border-t border-slate-200 pt-4">
            <span className="text-sm text-slate-500">Total</span>
            <span className="text-2xl font-semibold" data-testid="summary-total">
              {formatMoney(product.price, product.currency)}
            </span>
          </div>
        </Card>
      )}

      {customer && savedCards.length > 0 && (
        <Card>
          <fieldset disabled={busy}>
            <legend className="text-sm font-semibold uppercase tracking-wide text-slate-500">
              Payment method
            </legend>
            <div className="mt-3 flex flex-col gap-2">
              {savedCards.map((card) => (
                <MethodOption
                  key={card.id}
                  value={String(card.id)}
                  checked={method === String(card.id)}
                  onSelect={(value) => {
                    setMethod(value);
                    selectionChanged();
                  }}
                  label={card.maskedCardNo}
                  mono
                  hint="Saved card · no redirect"
                />
              ))}
              <MethodOption
                value={NEW_METHOD}
                checked={method === NEW_METHOD}
                onSelect={(value) => {
                  setMethod(value);
                  selectionChanged();
                }}
                label="Pay with a new payment method"
                hint="Continue on the 2C2P payment page"
              />
            </div>
          </fieldset>
        </Card>
      )}

      {checkout.error && (
        <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-700">
          {checkout.error instanceof ApiError
            ? checkout.error.userMessage
            : "Could not reach the payment service. Check your connection and try again — you will not be charged twice."}
        </p>
      )}

      <button
        type="submit"
        disabled={!canCheckout}
        className="flex items-center justify-center gap-2 rounded-xl bg-indigo-600 px-4 py-3.5 text-base font-semibold text-white shadow-sm transition hover:bg-indigo-500 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-500"
      >
        {busy && <Spinner className="size-4" />}
        {checkout.isSuccess
          ? "Redirecting…"
          : waitingForEarlierAttempt
            ? "Finishing your earlier attempt…"
            : checkout.isPending
              ? "Processing…"
              : product
                ? `Checkout · ${formatMoney(product.price, product.currency)}`
                : "Checkout"}
      </button>
    </form>
  );
}

function MethodOption(props: {
  value: string;
  checked: boolean;
  onSelect: (value: string) => void;
  label: string;
  hint: string;
  mono?: boolean;
}) {
  return (
    <label
      className={`flex cursor-pointer items-center gap-3 rounded-lg border p-3 text-sm transition ${
        props.checked
          ? "border-indigo-500 bg-indigo-50"
          : "border-slate-200 hover:border-slate-300"
      }`}
    >
      <input
        type="radio"
        name="payment-method"
        value={props.value}
        checked={props.checked}
        onChange={() => props.onSelect(props.value)}
        className="size-4 accent-indigo-600"
      />
      <span className="flex flex-col">
        <span className={`font-medium ${props.mono ? "font-mono tracking-wide" : ""}`}>{props.label}</span>
        <span className="text-xs text-slate-500">{props.hint}</span>
      </span>
    </label>
  );
}
