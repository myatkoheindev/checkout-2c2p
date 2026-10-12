namespace Checkout.Infrastructure.Persistence.Configurations;

/// <summary>Builds a MySQL <c>ENUM(...)</c> column type from a CLR enum.</summary>
internal static class MySqlEnum
{
    /// <summary>The column type listing every member of <typeparamref name="TEnum"/>.</summary>
    /// <typeparam name="TEnum">The enum stored as its name.</typeparam>
    /// <returns>For example <c>enum('Pending','Succeeded','Failed')</c>.</returns>
    public static string Of<TEnum>()
        where TEnum : struct, Enum =>
        $"enum({string.Join(',', Enum.GetNames<TEnum>().Select(name => $"'{name}'"))})";
}
