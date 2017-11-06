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
so before we create the lists we need to create a strategy as seen below, where it resolves to a SqlConnection. Solidbase uses this to connect to the various databases, however only SQL-Server is supported as of the first commit.

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

# Requirements

all objects have to have an "Id" property of the type "System.Int32",   
one way that could easily be handled is nested types with a base type. I did not want to force a base type upon ya'll
especially if you wanted to use anonymous types.

create a base class like

```cs
public class MyBaseClass {
   public int Id { get; set; }
}
```

But then again, not required if you want to go with

```cs
list.Add(new { Id = list.NextId, content = "rainbow and unicorns" });
```
