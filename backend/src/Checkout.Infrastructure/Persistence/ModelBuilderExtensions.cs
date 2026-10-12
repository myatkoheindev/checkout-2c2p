using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Checkout.Infrastructure.Persistence;

/// <summary>Model-wide naming rules.</summary>
internal static class ModelBuilderExtensions
{
    /// <summary>
    /// Names every column in snake_case (<c>InvoiceNo</c> → <c>invoice_no</c>) unless a
    /// configuration already named it. Table names are set explicitly in each configuration.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    public static void UseSnakeCaseColumnNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }
    }

    /// <summary>Converts <c>PascalCase</c> to <c>snake_case</c>.</summary>
    /// <param name="name">The CLR name.</param>
    /// <returns>The column name.</returns>
    internal static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
