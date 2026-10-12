import type { NextRequest } from "next/server";

/**
 * frontendReturnUrl: where 2C2P sends the browser after the hosted page or 3DS. The API sets it to
 * `/payment/return?invoiceNo=…`. 2C2P may arrive with a GET or a form POST carrying its own
 * response; either way we only take the invoice number and show the result from our database —
 * the redirect can arrive before the backend notification, so its content is never trusted.
 */
function toResult(request: NextRequest): Response {
  const invoiceNo = request.nextUrl.searchParams.get("invoiceNo");
  const target = invoiceNo && /^[A-Za-z0-9]{1,50}$/.test(invoiceNo) ? `/result/${invoiceNo}` : "/";

  // 303 turns a POST into a GET of the result page. The Location is relative: behind Docker the
  // server's own URL is its bind address (0.0.0.0), not the host the browser used.
  return new Response(null, { status: 303, headers: { Location: target } });
}

export function GET(request: NextRequest) {
  return toResult(request);
}

export function POST(request: NextRequest) {
  return toResult(request);
}
