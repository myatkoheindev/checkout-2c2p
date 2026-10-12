/**
 * Formats an amount with its currency code, e.g. "SGD 199.00" — the code, because en-SG would
 * print a bare "$" that reads as US dollars.
 */
export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat("en-SG", { style: "currency", currency, currencyDisplay: "code" }).format(amount);
}

const CHANNELS: Record<string, string> = {
  CC: "Card",
  VI: "Visa",
  MA: "Mastercard",
  AM: "American Express",
  JC: "JCB",
  UP: "UnionPay",
  DI: "Discover",
  DC: "Diners Club",
};

/** A readable name for 2C2P's channel code ("VI" is "Visa"); unknown codes are shown as given. */
export function paymentMethodLabel(channelCode: string | null): string {
  if (!channelCode) return "Card";
  return CHANNELS[channelCode.toUpperCase()] ?? channelCode;
}

/** "Recurring" → "Recurring", "OneTime" → "One-time". */
export function paymentTypeLabel(paymentType: string): string {
  return paymentType === "OneTime" ? "One-time" : paymentType;
}
