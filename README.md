# Relative cursors after a regular cursor

Minimal reproduction for Hot Chocolate / GreenDonut **16.7.6**: on a `[UseConnection]` field returning `PageConnection<T>` with `EnableRelativeCursors`, a page reached with a regular cursor (`endCursor`, `startCursor` or an edge's `cursor`) gets no relative cursors, or wrongly numbered ones when `totalCount` is selected.

## Run

```
dotnet run
```

Five products, pages of two (pages 1 `[1, 2]`, 2 `[3, 4]`, 3 `[5]`), SQLite in memory. The program exits with 1 while the issue is present.

```
OK   Page 1
     nodes: [1, 2]
     forwardCursors pages:  expected [2, 3], actual [2, 3]
     backwardCursors pages: expected [], actual []
OK   Page 2, reached with the relative cursor of page 2
     nodes: [3, 4]
     forwardCursors pages:  expected [3], actual [3]
     backwardCursors pages: expected [1], actual [1]
FAIL Page 2, reached with page 1's endCursor
     nodes: [3, 4]
     forwardCursors pages:  expected [3], actual []
     backwardCursors pages: expected [1], actual []
FAIL Page 2, reached with page 1's endCursor, totalCount selected
     nodes: [3, 4]
     forwardCursors pages:  expected [3], actual [2, 3]
     backwardCursors pages: expected [1], actual []
```

The field is in `Catalog.cs`, the queries in `Program.cs`.
