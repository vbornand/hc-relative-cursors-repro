using GreenDonut.Data;
using HotChocolate;
using HotChocolate.Types;
using HotChocolate.Types.Pagination;
using Microsoft.EntityFrameworkCore;

namespace RelativeCursorsRepro;

public sealed class Product
{
    public int Id { get; set; }

    public required string Name { get; set; }
}

public sealed class CatalogContext(DbContextOptions<CatalogContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}

[QueryType]
public static partial class ProductQueries
{
    [UseConnection]
    public static async Task<PageConnection<Product>> GetProductsAsync(
        PagingArguments pagingArgs,
        [Service] CatalogContext db,
        CancellationToken cancellationToken)
        => new(await db.Products.OrderBy(p => p.Id).ToPageAsync(pagingArgs, cancellationToken));
}
