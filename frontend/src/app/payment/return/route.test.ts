import { NextRequest } from "next/server";
import { describe, expect, it } from "vitest";
import { GET, POST } from "./route";

const at = (path: string, method = "GET") => new NextRequest(new URL(path, "http://localhost:3000"), { method });

describe("payment return", () => {
  it("sends the browser to the result page for its invoice", () => {
    const response = GET(at("/payment/return?invoiceNo=INV202610090000000001"));

    expect(response.status).toBe(303);
    expect(response.headers.get("location")).toBe("/result/INV202610090000000001");
  });

  it("accepts 2C2P's form POST the same way", () => {
    const response = POST(at("/payment/return?invoiceNo=INV1", "POST"));

    expect(response.status).toBe(303);
    expect(response.headers.get("location")).toBe("/result/INV1");
  });

  it("refuses anything that is not an invoice number", () => {
    const response = GET(at("/payment/return?invoiceNo=../../evil"));

    expect(response.headers.get("location")).toBe("/");
  });
});
