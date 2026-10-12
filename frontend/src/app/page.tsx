import { Suspense } from "react";
import { CheckoutForm } from "@/components/checkout/CheckoutForm";
import { Spinner } from "@/components/ui/Spinner";

export default function CheckoutPage() {
  // The form reads ?customerId&productId, so it renders in the browser behind this boundary.
  return (
    <Suspense
      fallback={
        <div className="flex items-center gap-2 text-sm text-slate-500">
          <Spinner className="size-4" /> Loading checkout…
        </div>
      }
    >
      <CheckoutForm />
    </Suspense>
  );
}
