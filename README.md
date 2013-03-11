TolerableDB 
===========

TolerableDB adds somewhat convenient, high performance extension methods to the .NET `IDbConnection` interface.  TolerableDB is a single, small, readable source file; under 200 lines with comments.

Using these methods requires familiarity with the `IDataReader` interface.

See [Dapper](https://code.google.com/p/dapper-dot-net/) for a much more convenient and featureful `IDbConnection` extension library.

ForEach
--------

```csharp
connection.ForEach("SELECT id, stuff FROM mytable", reader =>
    Console.WriteLine("{0}: {1}", reader[0], reader[1])
);
```

Execute
--------

```csharp
connection.Execute("DELETE FROM mytable WHERE stuff = ?)", new [] { "things" }));
```

Read
-----

```csharp
var things = connection.Read(
    "SELECT id, stuff FROM mytable"
	reader => new { 
        id = reader.GetInt64(0), 
        stuff = reader.GetString(1), 
    });
```

Select
-------

```csharp
var things = connection.Select(
    reader => new { 
        id = reader.GetInt64(0), 
        stuff = reader.GetString(1) }, 
    "FROM mytable");
```

