using Checkout.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Checkout.Api.Extensions;

/// <summary>Turns a failed <see cref="Result{T}"/> into RFC 7807 problem details from a controller.</summary>
internal static class ResultExtensions
{
    /// <summary>
    /// Maps the error code to its HTTP status: validation 400, not found 404, a reused key 422,
    /// a checkout still in progress 409, an unverifiable notification 400.
    /// </summary>
    /// <param name="controller">The controller answering.</param>
    /// <param name="result">The failed result.</param>
    /// <returns>The problem details response.</returns>
    public static ActionResult ToProblem<T>(this ControllerBase controller, Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);

        if (result.ErrorCode == ErrorCodes.ValidationError && result.ValidationErrors is { } errors)
        {
            return Validation(controller, StatusCodes.Status400BadRequest, errors);
        }

        var status = result.ErrorCode switch
        {
            ErrorCodes.CustomerNotFound or ErrorCodes.ProductNotFound or ErrorCodes.CardNotFound or ErrorCodes.TransactionNotFound
                => StatusCodes.Status404NotFound,
            ErrorCodes.IdempotencyKeyReused => StatusCodes.Status422UnprocessableEntity,
            ErrorCodes.CheckoutInProgress => StatusCodes.Status409Conflict,
            ErrorCodes.InvalidSignature => StatusCodes.Status400BadRequest,
            _ => throw new InvalidOperationException($"Unmapped error code {result.ErrorCode}."),
        };

        return controller.Problem(detail: result.Error, statusCode: status);
    }

    /// <summary>A field error, for an id in the request body that does not resolve (422).</summary>
    /// <param name="controller">The controller answering.</param>
    /// <param name="field">The request field, in JSON (camelCase) form.</param>
    /// <param name="message">What is wrong with it.</param>
    /// <returns>The validation problem response.</returns>
    public static ActionResult FieldProblem(this ControllerBase controller, string field, string message) =>
        Validation(controller, StatusCodes.Status422UnprocessableEntity, new Dictionary<string, string[]> { [field] = [message] });

    private static ActionResult Validation(ControllerBase controller, int status, IReadOnlyDictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(CamelCase(field), message);
            }
        }

        return controller.ValidationProblem(statusCode: status, modelStateDictionary: modelState);
    }

    private static string CamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
