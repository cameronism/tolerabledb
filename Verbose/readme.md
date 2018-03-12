# TolerableDB.Verbose

Abstract base class

Methods:

- Insert
- Update
- Delete
- Select (for symmetry, json and binary support)

Note this is starting out very SQL server specific but ultimately could support more


## Database.Update

Update specified table with diff and confirmation
will rollback immediately if more than one row was updated

sql server will implicitly convert string to almost anything so params can be passed as string
except: binary, varbinary, timestamp, image -- not supporting timestamp or image for now

https://docs.microsoft.com/en-us/sql/t-sql/functions/cast-and-convert-transact-sql#implicit-conversions


### Parameters



- `table`

  name of table  
  required

- `input`

  Name of file (or `-`) to read as JSON describing everything to update.
  Requires a top level object, property names will be used as column names.
  Any non primitives (object or array) must correspond to a string column containing JSON.
  (By default) an additional round trip will be performed before confirmation
  so JSON can be merged

- `jsonmerge`

  default `true`  
  specify `false` to completely disable JSON object merge behavior. JSON will be "diffed" but new values will be set directly.
  TBD merge needing extra round trip

- `dryrun`

  default `false`

  if `true`

  - display diff only
  - don't make any changes at all
  - useful for generated sql (and fully baked json)
  - but diff is not technically valid outside transaction
  - does call the server but immediately aborts transaction if any

- `sqlonly`

  Just generate the inital sql, does not hit DB server at all
  Nice for all commands not requiring (or supporting) json merge


#### Column parameters

All can be specified multiple times

Column parameter formats:

- `name`  
  Name of column  
  For parameter `null` end of story  
  For other parameters value is read from property of same name in the `input` JSON  
  Not supported for parameters: `bytes`, `function`, `set`

- `name=value`  
  Name of column and value to use. Works for all column parameters that accept values  
  Not supported for parameters: `bytes`, `null`  
  For parameter `bytes` use `base64`, `utf8`, `utf16` or `hex` instead

- `name:path/to/file`  
  Name of column and path to file containing value. Works for all column parameters that accept values  
  Not supported for parameters: `function`, `null`  

- `name:-`  
  Same as `name:path/to/file` except read from standard input stream.
  Not supported for parameters: `bytes`, `function`, `null`

| name     |`column`|`column=value`|`column:file`|`column:-`|notes|
| ---      |:---:   |:---:         |:---:        |:---:     |---  |
|`null`    | ✓      |              |             |          |set named column to null|
|`set`     |        | ✓            | ✓           | ✓        |value to set|
|`where`   | ✓      | ✓            | ✓           | ✓        |value for `WHERE` clause, combined with `AND` if multiple|
|`json`    | ✓      | ✓            | ✓           | ✓        |JSON string, if object merge unless `jsonmerge=false`|
|`jsonset` | ✓      | ✓            | ✓           | ✓        |JSON string, do not merge anything|
|`base64`  | ✓      | ✓            | ✓           | ✓        |binary data encoded as base64 string|
|`hex`     | ✓      | ✓            | ✓           | ✓        |binary data encoded as hexadecimal string, Prefix `0x` allowed|
|`utf8`    | ✓      | ✓            | ✓           | ✓        |binary data encoded as utf8 string|
|`utf16`   | ✓      | ✓            | ✓           | ✓        |binary data encoded as utf16 string|
|`bytes`   |        |              | ✓           |          |verbatim bytes from specified file|
|`function`|        | ✓            |             |          |name of SQL function, only supports parameterless functions|


additional notes

- `where`  
  required unless `input` is used and only one prop ends with "id"
  will be used for where clause (using `AND` if specified multiple times)


- `function`  
  Only supports `column=value` format. Value is name of SQL function.
  so close to arbitrary SQL but feels safe enough if only support parameterless functions
  strictly checked that format is `column name` = `function name`  ONLY SAFE IDENTIFIER CHARACTERS (no parens or etc.)


- `json`  
  If `input` JSON is used and value is an object or an array then this parameter is redundant (still allowed).

  Necessary if `input` JSON is used and value is null, boolean, number or string.

  Triggers JSON diff handling of column
  Object values will be merged (TBD if we use another round trip for this)
  unless `jsonmerge=false`


- `jsonset`  
  Same as `json` but without object merge behavior, new value is used directly.  
  Redundant if `jsonmerge=false` and value is object or array (still allowed)
				

- binary types

  - `base64`
  - `utf8`
  - `utf16`
  - `hex`  
    Looks closest to varbinary literal, preceding `0x` is tolerated
  - `bytes`  
    Only file path is supports. Bytes from file will be used exactly as-is.  
    `column:-` for stdin is not supported because of potential ambiguity (looking at you PowerShell).
	If you _definitely_ know what your shell is doing then redirect stdin to a temporary file first then provide the name of that file


### Base Class

Inheritors implement 

```csharp
// may change to a single method with a much richer parameter with `Commit` and `Rollback` methods

// not much to confirm for the select, feels cleaner to call same method for DataSet than to feed it to something else
protected abstract DoItOrNot Confirm(StatementKind statementKind, DataSet changes, SomethingToHelpDescribeWhereTableTwoAndFollwingCameFrom stuff);

// this is not for confirmation but you could throw an exception to abort if you really had to
protected virtual void OnExecuteSql(StatementKind statementKind, string sql, IReadOnlyDictionary<string, object> parameters) { }
```

### `Update` Table Descriptions

Even indexed tables show values that have not changed (0, 2, 4, ...), always 1 row (unless everything changed)
odd indexed tables show values that have changed (1, 3, 5, ...), always 2 rows (unless nothing changed)


Table[0]
	unchanged values of original named table
	rows: 1
	name {table}.$Same
	columns:
		all unchanged columns
		except json or binary columns (which are in Table[2+])

Table[1]
	differences from original named table
	rows: 2 (or 0 if there are no differences)
	name {table}.$Diff
	columns:
		column 0: new column, value: Current | Change
		all columns with changes
		except json or binary columns (which are in Table[2+])
json tables

TBD how to nicely handle JSON types other than object

Table[2n + 0] even table
	unchanged values of json
	rows: 1 (or 0 if everything different)
	name {table}.{column}.$Same
	columns:
		all unchanged properties
		of original json column

Table[2n + 1] odd table
	differences of json
	rows: 2 (or 0 if there are no differences)
	name {table}.{column}.$Diff
	columns:
		column 0: new column, value: Current | Change
		properties that changed
		of original json column

Binary tables
refer to SomethingToHelpDescribeWhereTableTwoAndFollwingCameFrom
to determine source and source format which may be helpful in displaying, interpreting, etc these tables

Table[2n + 0] even table
	unchanged values of binary
	rows: 1 (or 0 if everything different)
	name {table}.{column}.$Same
	columns:
		original column name

Table[2n + 1] odd table
	differences of binary
	rows: 2 (or 0 if there are no differences)
	name {table}.{column}.$Diff
	columns:
		column 0: new column, value: Current | Change
		column 1: original column name


### Table Descriptions for `Insert`, `Delete`, `Select`

similar to the even (0, 2, 4, etc) half of the `Update` tables (no "virtual" Current | Change) column



## JSON output

All four methods provide fully baked json suitable for use as `input` for Insert or Update (technically `Delete` also, it just needs more guidance).
`Update` produce 2 versions: current and changed.


## Delete and Select

`Delete` and `Select` are harder to use with fully baked `input` json, might be nice to have a parameter indicating only use `where` parameters.
Need to think out what the automatic "ends with id" behavior would mean for these 2.  Tempted to get rid of "automatic id" behavior for all commands.
For these 2 `input` but no `where` seems like all properties should go into the where clause.

`set`, `null` and `function` not supported
JSON and binary related parameters are "display" indicators only (virtual tables and potentially formatting of baked JSON)



## Less formed thoughts

- Consider no JSON merge round trip. Probably have enough for nested JSON_MODIFY calls

  JSON_MODIFY is case sensitive unlike practically everything else in sql, round trip merge can be case insensitive

- Tolerable db verbose execute command

  Run specified sql file(s). Treat input and suitable parameters as variables to send with command(s)

  Run each file in transaction  that must be confirmed. Useful with good output (or following select statements).   Could run single confirmation step after all files for optional faster one big transaction behavior

  TBD transaction per file OR one big transaction OR nested transaction.

  Fundamentally not that different than running same sql in SSMS or VS (etc.).  e.g. 1. send with begin transaction but no commit or rollback. 2. examine results 3. send commit or rollback  
  Difference: not having to wrap in transaction manually. Send selections or edit every time  
  Difference: automatic timeout  
  Difference: wrapping program’s confirmation step. e.g. type database name, type server name, re-enter password  
  Difference: multiple files with same parameters  
  Difference: multiple files one big transaction  



- public JSON to DataTable utility function somewhere

  seems potentially handy
