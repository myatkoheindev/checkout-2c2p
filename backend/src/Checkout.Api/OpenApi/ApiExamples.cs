using Checkout.Api.Contracts.Requests;
using Checkout.Api.Contracts.Responses;
using Checkout.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Checkout.Api.OpenApi;

/// <summary>The example requests and responses shown in Swagger, named by the example attributes.</summary>
internal static class ApiExamples
{
    private const string Invoice = "INV202610090482913374";

    public static HealthResponse Health { get; } =
        new("ok", "Checkout API", "Development", new DateTimeOffset(2026, 10, 9, 13, 0, 0, TimeSpan.Zero));

    public static IReadOnlyList<CustomerResponse> Customers { get; } =
    [
        new(1, "Wei Ling", "Tan", "weiling.tan@example.com"),
        new(2, "Arjun", "Nair", "arjun.nair@example.com"),
    ];

    public static IReadOnlyList<SavedCardResponse> SavedCards { get; } =
    [
        new(7, "411111XXXXXX1111", new DateTime(2026, 10, 7, 9, 30, 0, DateTimeKind.Utc)),
    ];

    public static ProblemDetails CustomerNotFound { get; } =
        ProblemExamples.NotFound("Customer 99 does not exist.", "/api/customers/99/cards");

    public static IReadOnlyList<ProductResponse> Products { get; } =
    [
        new(1, "Yoga Drop-in Class", "A single 60-minute yoga class at any studio.", 35.00m, "SGD", PaymentType.OneTime),
        new(3, "Monthly Unlimited Membership", "Unlimited classes, billed every month.", 199.00m, "SGD", PaymentType.Recurring),
    ];

    public static CheckoutRequest Checkout { get; } = new(CustomerId: 1, ProductId: 3);

    public static CheckoutResponse CheckoutStarted { get; } =
        new(Invoice, TransactionStatus.Pending, "https://sandbox-pgw-ui.2c2p.com/payment/4.3/#/token/kSAops9Zwhos8hSTSeLTUcCsA%2b...");

    public static ProblemDetails CheckoutMissingKey { get; } =
        CheckoutProblem(StatusCodes.Status400BadRequest, "Bad Request", "The Idempotency-Key header is required and must be a UUID.");

    public static ProblemDetails CheckoutInProgress { get; } =
        CheckoutProblem(StatusCodes.Status409Conflict, "Conflict", "A checkout with this Idempotency-Key is still in progress. Retry shortly with the same key.");

    public static HttpValidationProblemDetails CheckoutInvalidCard { get; } =
        new(new Dictionary<string, string[]> { ["customerCardId"] = ["Card 7 is not a saved card of customer 1."] })
        {
            Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status422UnprocessableEntity,
            Instance = "/api/checkout",
        };

    public static NotificationRequest Notification { get; } = new(
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJtZXJjaGFudElEIjoiSlQwMSIsImludm9pY2VObyI6IklOVjIwMjYxMDA5MDQ4MjkxMzM3NCIsImFtb3VudCI6MTk5LjAsImN1cnJlbmN5Q29kZSI6IlNHRCIsInJlc3BDb2RlIjoiMDAwMCJ9.c2lnbmF0dXJl");

    public static ProblemDetails NotificationRejected { get; } = new()
    {
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        Title = "Bad Request",
        Status = StatusCodes.Status400BadRequest,
        Detail = "The notification could not be verified.",
        Instance = "/api/payments/2c2p/notification",
    };

    public static TransactionResponse Transaction { get; } = new(
        Invoice,
        TransactionStatus.Succeeded,
        199.00m,
        "SGD",
        1,
        "Wei Ling Tan",
        3,
        "Monthly Unlimited Membership",
        PaymentType.Recurring,
        PaymentFlow.HostedPage,
        "CC",
        "411111XXXXXX1111",
        "0000",
        "Success",
        new DateTime(2026, 10, 9, 14, 29, 30, DateTimeKind.Utc),
        new DateTime(2026, 10, 9, 14, 30, 12, DateTimeKind.Utc));

    public static ProblemDetails TransactionNotFound { get; } =
        ProblemExamples.NotFound($"Transaction {Invoice} does not exist.", $"/api/transactions/{Invoice}");

    public static ProblemDetails InquiryNotFound { get; } =
        ProblemExamples.NotFound($"Transaction {Invoice} does not exist.", $"/api/transactions/{Invoice}/inquiry");

    private static ProblemDetails CheckoutProblem(int status, string title, string detail) => new()
    {
        Type = $"https://tools.ietf.org/html/rfc9110#section-15.5.{(status == StatusCodes.Status400BadRequest ? 1 : 10)}",
        Title = title,
        Status = status,
        Detail = detail,
        Instance = "/api/checkout",
    };
}
