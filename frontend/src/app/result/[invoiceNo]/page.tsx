import { Suspense } from "react";
import { ResultView } from "@/components/result/ResultView";
import { Spinner } from "@/components/ui/Spinner";

export default function ResultPage({ params }: PageProps<"/result/[invoiceNo]">) {
  return (
    <Suspense
      fallback={
        <div className="flex items-center gap-2 text-sm text-slate-500">
          <Spinner className="size-4" /> Loading payment…
        </div>
      }
    >
      {params.then(({ invoiceNo }) => (
        <ResultView invoiceNo={decodeURIComponent(invoiceNo)} />
      ))}
    </Suspense>
  );
}
