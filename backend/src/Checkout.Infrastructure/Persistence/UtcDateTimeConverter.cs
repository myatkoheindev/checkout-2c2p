using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Checkout.Infrastructure.Persistence;

/// <summary>Writes <see cref="DateTime"/> as UTC and reads it back with <see cref="DateTimeKind.Utc"/>.</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
