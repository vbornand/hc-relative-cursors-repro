using System.Text.Json;
using GreenDonut.Data;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using HotChocolate.Types.Pagination;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RelativeCursorsRepro;

// Five products, pages of two: pages 1 [1, 2], 2 [3, 4], 3 [5].
using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

var services = new ServiceCollection()
    .AddDbContext<CatalogContext>(o => o.UseSqlite(connection))
    .AddGraphQL()
    .AddRelativeCursorsReproTypes()
    .AddPagingArguments()
    .ModifyPagingOptions(o =>
    {
        o.EnableRelativeCursors = true;
        o.IncludeTotalCount = true;
    })
    .Services
    .BuildServiceProvider();

using (var scope = services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    db.Database.EnsureCreated();
    db.Products.AddRange(Enumerable.Range(1, 5).Select(id => new Product { Id = id, Name = $"Product {id}" }));
    db.SaveChanges();
}

var executor = await services.GetRequiredService<IRequestExecutorProvider>().GetExecutorAsync();

async Task<JsonElement> ProductsAsync(string? after, bool totalCount)
{
    var result = await executor.ExecuteAsync(OperationRequestBuilder.New()
        .SetDocument($$"""
            query($after: String) {
              products(first: 2, after: $after) {
                {{(totalCount ? "totalCount" : "")}}
                nodes { id }
                pageInfo { endCursor forwardCursors { page cursor } backwardCursors { page cursor } }
              }
            }
            """)
        .SetVariableValues(new Dictionary<string, object?> { ["after"] = after })
        .Build());
    var json = JsonDocument.Parse(result.ToJson()).RootElement;
    if (json.TryGetProperty("errors", out var errors))
    {
        throw new InvalidOperationException(errors.ToString());
    }
    return json.GetProperty("data").GetProperty("products");
}

static string Ids(JsonElement products) =>
    string.Join(", ", products.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("id").GetInt32()));

static string Pages(JsonElement products, string cursors) =>
    string.Join(", ", products.GetProperty("pageInfo").GetProperty(cursors).EnumerateArray().Select(c => c.GetProperty("page").GetInt32()));

static string Cursor(JsonElement products, int page) =>
    products.GetProperty("pageInfo").GetProperty("forwardCursors").EnumerateArray()
        .Single(c => c.GetProperty("page").GetInt32() == page).GetProperty("cursor").GetString()!;

var failed = false;

void Check(string scenario, JsonElement products, string forward, string backward)
{
    var actualForward = Pages(products, "forwardCursors");
    var actualBackward = Pages(products, "backwardCursors");
    var ok = actualForward == forward && actualBackward == backward;
    failed |= !ok;
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {scenario}");
    Console.WriteLine($"     nodes: [{Ids(products)}]");
    Console.WriteLine($"     forwardCursors pages:  expected [{forward}], actual [{actualForward}]");
    Console.WriteLine($"     backwardCursors pages: expected [{backward}], actual [{actualBackward}]");
}

var page1 = await ProductsAsync(after: null, totalCount: false);
Check("Page 1", page1, "2, 3", "");

var page2ByRelativeCursor = await ProductsAsync(after: Cursor(page1, 2), totalCount: false);
Check("Page 2, reached with the relative cursor of page 2", page2ByRelativeCursor, "3", "1");

var endCursor = page1.GetProperty("pageInfo").GetProperty("endCursor").GetString();

var page2ByEndCursor = await ProductsAsync(after: endCursor, totalCount: false);
Check("Page 2, reached with page 1's endCursor", page2ByEndCursor, "3", "1");

var page2ByEndCursorWithTotal = await ProductsAsync(after: endCursor, totalCount: true);
Check("Page 2, reached with page 1's endCursor, totalCount selected", page2ByEndCursorWithTotal, "3", "1");

return failed ? 1 : 0;
