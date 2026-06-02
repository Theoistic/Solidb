# Solidb

Solidb is a lightweight typed .NET micro-ORM with change tracking, an identity map, schema helpers, and providers for SQLite, SQL Server, and JSON-backed storage.

## Installation

```bash
dotnet add package Solidb
```

Solidb currently targets `net8.0`.

## What changed in v2

The legacy `Solidbase` v1 API has been replaced by the v2 stack:

- `SolidContext` for configuration and unit-of-work behavior
- `SolidSet<T>` for entity access
- `SolidQuery<T>` for filtering, ordering, paging, includes, and caching

The old `Solidbase` entry point remains only as an empty compatibility stub.

## Quick start

```csharp
using Solidb;
using Solidb.Mapping;

[Table("users")]
public sealed class User
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}

using var db = SolidContext.SQLite(
    "Data Source=solidb.db",
    typeof(User));

await db.Schema.CreateAllAsync();

db.Set<User>().Add(new User
{
    Id = Guid.NewGuid(),
    Name = "Theo",
    IsActive = true
});

await db.SaveChangesAsync();

var activeUsers = await db.Set<User>()
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .ToListAsync();
```

## Providers

Create a context with one of the built-in providers:

```csharp
var sqlite = SolidContext.SQLite("Data Source=solidb.db", typeof(User), typeof(Post));
var sqlServer = SolidContext.SqlServer("Server=.;Database=Solidb;Trusted_Connection=True;TrustServerCertificate=True;", typeof(User), typeof(Post));
var json = SolidContext.Json("App_Data", typeof(User), typeof(Post));
```

You can also supply a custom provider:

```csharp
var db = SolidContext.FromProvider(provider, typeof(User), typeof(Post));
```

## Mapping

Solidb builds its model from CLR types and supports these attributes:

- `[Table("name")]` to override the table name
- `[Key]` to mark the primary key
- `[Column("name")]` to override the column name
- `[ForeignKey(nameof(UserId))]` to bind a navigation property to a foreign key
- `[NotMapped]` to exclude a property
- `[Generated]` to mark store-generated values

If no `[Table]` attribute is present, Solidb uses the lowercase type name plus `s`.
If no `[Key]` attribute is present, Solidb falls back to `Id` or `{TypeName}Id`.

## Querying

`SolidSet<T>` creates `SolidQuery<T>` instances for typed queries:

```csharp
var firstActive = await db.Set<User>()
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .Skip(10)
    .Take(5)
    .FirstOrDefaultAsync();
```

Available query features include:

- `Where(...)`
- `OrderBy(...)`, `OrderByDescending(...)`, `ThenBy(...)`, `ThenByDescending(...)`
- `Skip(...)`, `Take(...)`
- `CountAsync()`, `AnyAsync()`, `FirstOrDefaultAsync()`, `ToListAsync()`

## Relationships and includes

Solidb supports one-to-many and many-to-one navigation properties.

```csharp
[Table("users")]
public sealed class User
{
    [Key]
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<Post> Posts { get; set; } = new();
}

[Table("posts")]
public sealed class Post
{
    [Key]
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}
```

Use `Include(...)` for collection navigation loading and `IncludeJoin(...)` for joined reference loading:

```csharp
var usersWithPosts = await db.Set<User>()
    .Include(u => u.Posts)
    .ToListAsync();

var postsWithUsers = await db.Set<Post>()
    .IncludeJoin(p => p.User)
    .ToListAsync();
```

## Change tracking

Entities loaded through Solidb are tracked automatically. New entities can be added, updated, and removed through `SolidSet<T>`:

```csharp
var users = db.Set<User>();
var user = await users.FindAsync(id);

if (user is not null)
{
    user.Name = "Updated";
    await db.SaveChangesAsync();
}
```

You can also track explicit changes:

```csharp
users.Add(new User { Id = Guid.NewGuid(), Name = "Alice" });
users.Update(existingUser);
users.Remove(existingUser);

await db.SaveChangesAsync();
```

## Schema management

Schema operations are available through `db.Schema`:

```csharp
await db.Schema.CreateAllAsync();
await db.Schema.CreateTableAsync<User>();
await db.Schema.MigrateAsync();
```

- `CreateAllAsync()` creates all mapped tables if they do not exist
- `CreateTableAsync<T>()` creates one mapped table
- `MigrateAsync()` creates only missing tables without modifying existing ones

## Caching and logging

SQL-backed contexts can be configured with logging, and any context can be wrapped with the built-in in-memory cache:

```csharp
using Solidb.Caching;
using Solidb.Diagnostics;

var db = SolidContext.SQLite("Data Source=solidb.db", typeof(User))
    .WithLogger(new ConsoleLogger())
    .WithCache(new MemorySolidCache());

var cachedUsers = await db.Set<User>()
    .Where(u => u.IsActive)
    .CacheFor(TimeSpan.FromMinutes(5))
    .ToListAsync();
```

## Development

Build:

```bash
dotnet build Solidb/Solidb.csproj
```

Test:

```bash
dotnet test Solidb.Tests/Solidb.Tests.csproj
```
