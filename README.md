# Solidb
Solidb is a Dynamic .NET micro ORM with change tracking observable objects.

# Installation

From Nuget.Org

```cs
PM> Install-Package Solidb
```

# How

Solidbase is a List just like how List<object> is used. you can take and create a new Solidbase list
  
```cs
Solidbase list = new Solidbase<User>();
```

Since in the background it's all build upon the IDbConnection interface we need to tell Solidb where to look and store stuff.
so before we create the lists we need to create a strategy as seen below, where it resolves to a SqlConnection. Solidbase uses this to connect to the various databases, If a Solidbase Strategy is not set, it will default to a SQLite database.

```cs
Solidbase.Strategy = () => new SqlConnection("Server=.\\SQLEXPRESS;Database=NewSolidb;Trusted_Connection=True;");
```

setting "Random" as the table and then injecting a anonymous type, afterwards fetching the inserted object and changing it
by adding a new property to the object as "newStuff".

```cs
Solidbase list = new Solidbase("Random");
list.Add(new { Id = list.NextId, content = "rainbow" });
var latest = list.Last();
latest.newStuff = "something else";
```

Or using a generic type argument

```cs
Solidbase list = new Solidbase<Product>();
list.Add(new { Id = list.NextId, Price = 1.0 });
```


