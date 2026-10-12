"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { ApiError, getTransaction, inquireTransaction, type Transaction } from "@/lib/api";
import { formatMoney, paymentMethodLabel } from "@/lib/format";
import { Card } from "@/components/ui/Card";
import { Spinner } from "@/components/ui/Spinner";

/** How often a pending payment is re-read. */
export const POLL_INTERVAL_MS = 3000;

/**
 * When a hosted-page payment still has no backend notification after this long, the page also
 * asks 2C2P directly (Payment Inquiry). A saved card is always confirmed that way, at once.
 */
export const INQUIRY_FALLBACK_AFTER_MS = 10_000;

/** After this long the page says so, but keeps polling. */
const SLOW_AFTER_MS = 60_000;

export function ResultView({ invoiceNo }: { invoiceNo: string }) {
  const openedAt = useRef<number | null>(null);
  const last = useRef<Transaction | undefined>(undefined);
  const [slow, setSlow] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setSlow(true), SLOW_AFTER_MS);
    return () => clearTimeout(timer);
  }, []);

  const transaction = useQuery({
    queryKey: ["transaction", invoiceNo],
    queryFn: async () => {
      // Always the database's answer, never the redirect's query string. While pending, a saved
      // card (3DS return) or a late notification is settled by asking the API to inquire.
      openedAt.current ??= Date.now();
      const previous = last.current;
      const inquire =
        previous?.status === "Pending" &&
        (previous.flow === "SavedCard" || Date.now() - openedAt.current >= INQUIRY_FALLBACK_AFTER_MS);

      const result = inquire ? await inquireTransaction(invoiceNo) : await getTransaction(invoiceNo);
      last.current = result;
      return result;
    },
    staleTime: 0,
    retry: (count, error) => !(error instanceof ApiError && error.status === 404) && count < 3,
    refetchInterval: (query) => (query.state.data?.status === "Pending" ? POLL_INTERVAL_MS : false),
  });

  if (transaction.isPending) {
    return <Processing />;
  }

  if (transaction.isError) {
    const notFound = transaction.error instanceof ApiError && transaction.error.status === 404;
    return (
      <Card className="text-center">
        <h1 className="text-xl font-semibold">{notFound ? "Payment not found" : "Could not load your payment"}</h1>
        <p className="mt-2 text-sm text-slate-500">
          {notFound ? `There is no payment with invoice ${invoiceNo}.` : "Please refresh the page in a moment."}
        </p>
        <Link href="/" className="mt-6 inline-block text-sm font-medium text-indigo-600 hover:underline">
          Back to checkout
        </Link>
      </Card>
    );
  }

  const data = transaction.data;

  if (data.status === "Pending") {
    return <Processing slow={slow} invoiceNo={data.invoiceNo} />;
  }

  return data.status === "Succeeded" ? <Succeeded transaction={data} /> : <Failed transaction={data} />;
}

function Processing({ slow = false, invoiceNo }: { slow?: boolean; invoiceNo?: string }) {
  return (
    <Card className="flex flex-col items-center gap-4 py-12 text-center">
      <Spinner className="size-10 text-indigo-600" />
      <div role="status" aria-live="polite">
        <h1 className="text-xl font-semibold">Processing payment</h1>
        <p className="mt-2 text-sm text-slate-500">
          {slow
            ? "This is taking longer than usual. You can keep this page open — it updates by itself."
            : "Waiting for confirmation from the payment provider. Please don’t close this page."}
        </p>
        {invoiceNo && <p className="mt-4 font-mono text-xs text-slate-400">{invoiceNo}</p>}
      </div>
    </Card>
  );
}

function Succeeded({ transaction }: { transaction: Transaction }) {
  return (
    <Card>
      <div className="flex flex-col items-center text-center">
        <span
          aria-hidden="true"
          className="flex size-14 items-center justify-center rounded-full bg-emerald-100 text-2xl text-emerald-700"
        >
          ✓
        </span>
        <h1 className="mt-4 text-2xl font-semibold">Payment succeeded</h1>
        <p className="mt-1 text-3xl font-semibold tracking-tight">
          {formatMoney(transaction.amount, transaction.currency)}
        </p>
      </div>
      <dl className="mt-6 grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 border-t border-slate-200 pt-6 text-sm">
        <dt className="text-slate-500">Product</dt>
        <dd className="text-right font-medium">{transaction.productName}</dd>
        <dt className="text-slate-500">Invoice number</dt>
        <dd className="text-right font-mono">{transaction.invoiceNo}</dd>
        <dt className="text-slate-500">Payment method</dt>
        <dd className="text-right font-medium">{paymentMethodLabel(transaction.paymentChannel)}</dd>
        {transaction.maskedCardNo && (
          <>
            <dt className="text-slate-500">Card / account</dt>
            <dd className="text-right font-mono">{transaction.maskedCardNo}</dd>
          </>
        )}
      </dl>
      <Link
        href="/"
        className="mt-6 block rounded-xl border border-slate-300 px-4 py-3 text-center text-sm font-semibold hover:bg-slate-50"
      >
        New checkout
      </Link>
    </Card>
  );
}

function Failed({ transaction }: { transaction: Transaction }) {
  const retry = `/?customerId=${transaction.customerId}&productId=${transaction.productId}`;

  return (
    <Card>
      <div className="flex flex-col items-center text-center">
        <span
          aria-hidden="true"
          className="flex size-14 items-center justify-center rounded-full bg-red-100 text-2xl text-red-700"
        >
          ✕
        </span>
        <h1 className="mt-4 text-2xl font-semibold">Payment failed</h1>
        <p className="mt-2 text-sm text-slate-600" data-testid="failure-reason">
          {transaction.respDesc ?? "The payment could not be completed."}
        </p>
        <p className="mt-4 text-xs text-slate-400">
          {transaction.productName} · {formatMoney(transaction.amount, transaction.currency)} ·{" "}
          <span className="font-mono">{transaction.invoiceNo}</span>
        </p>
      </div>
      <Link
        href={retry}
        className="mt-6 block rounded-xl bg-indigo-600 px-4 py-3 text-center text-sm font-semibold text-white hover:bg-indigo-500"
      >
        Go back
      </Link>
    </Card>
  );
}
