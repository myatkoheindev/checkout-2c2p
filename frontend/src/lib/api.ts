/**
 * The only module that talks to the Checkout API. Components call these functions through
 * TanStack Query; nothing else uses fetch.
 */

export const API_URL = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080").replace(/\/$/, "");

export type PaymentType = "OneTime" | "Recurring";
export type TransactionStatus = "Pending" | "Succeeded" | "Failed";
export type PaymentFlow = "HostedPage" | "SavedCard";

export interface Customer {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  fullName: string;
}

export interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  currency: string;
  paymentType: PaymentType;
}

export interface SavedCard {
  id: number;
  maskedCardNo: string;
  createdAt: string;
}

export interface CheckoutRequest {
  customerId: number;
  productId: number;
  customerCardId?: number;
}

export interface CheckoutResponse {
  invoiceNo: string;
  status: TransactionStatus;
  redirectUrl: string | null;
}

export interface Transaction {
  invoiceNo: string;
  status: TransactionStatus;
  amount: number;
  currency: string;
  customerId: number;
  customerName: string;
  productId: number;
  productName: string;
  paymentType: PaymentType;
  flow: PaymentFlow;
  paymentChannel: string | null;
  maskedCardNo: string | null;
  respCode: string | null;
  respDesc: string | null;
  createdAt: string;
  paidAt: string | null;
}

/** An RFC 7807 problem details body, as every API error is. */
export interface Problem {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** A non-2xx answer from the API, carrying its problem details. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: Problem | null,
  ) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
  }

  /** One readable sentence: the field errors if there are any, otherwise the detail. */
  get userMessage(): string {
    const fieldErrors = Object.values(this.problem?.errors ?? {}).flat();
    return fieldErrors.length > 0 ? fieldErrors.join(" ") : this.message;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...init,
    cache: "no-store",
    headers: { Accept: "application/json", ...init?.headers },
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as Problem | null;
    throw new ApiError(response.status, problem);
  }

  return (await response.json()) as T;
}

export const getCustomers = () => request<Customer[]>("/api/customers");

export const getProducts = () => request<Product[]>("/api/products");

export const getSavedCards = (customerId: number) => request<SavedCard[]>(`/api/customers/${customerId}/cards`);

/**
 * Starts a payment. The same idempotency key must be sent on every retry of one purchase; the API
 * then answers with the original invoice and redirect instead of charging again.
 */
export const postCheckout = (body: CheckoutRequest, idempotencyKey: string) =>
  request<CheckoutResponse>("/api/checkout", {
    method: "POST",
    headers: { "Content-Type": "application/json", "Idempotency-Key": idempotencyKey },
    body: JSON.stringify(body),
  });

/** Reads a transaction fresh from the database. */
export const getTransaction = (invoiceNo: string) =>
  request<Transaction>(`/api/transactions/${encodeURIComponent(invoiceNo)}`);

/** Asks the API to confirm a pending payment with 2C2P (throttled server-side). */
export const inquireTransaction = (invoiceNo: string) =>
  request<Transaction>(`/api/transactions/${encodeURIComponent(invoiceNo)}/inquiry`, { method: "POST" });
