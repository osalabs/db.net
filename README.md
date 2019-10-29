# db.net
simplified work with SQL Server or MS Access databases for your website, convenient wrapper

Contains `DB` class. Pure VB.NET, let me know if you want C# version.

### Why I created this library?
Because of much easier and simplier work with queries and results. Compare:

**DB.vb usage:**
```vb.net

Dim db as DB = New DB()
Dim sql as String = "SELECT * FROM table ORDER by id"
Dim rows as ArrayList = db.array(sql); 'db opened automatically based on web.config, errors handled automatically
For Each row As Hashtable In rows
    'work with row("Field1"), row("Field2")
Next
db.disconnect() 'recommended, but not necessary as disconnect happens on db object disposal
```

**"native" SqlConnection/SqlCommand/SqlDataReader usage:**
```vb.net
Dim connectionString as String = "Data Source=(local)\SQLEXPRESS;Initial Catalog=demo;Integrated Security=True" //sample
Dim sql As String = "SELECT * FROM table ORDER by id"
Using connection As New SqlConnection(connectionString)
    Dim command As New SqlCommand(sql, connection)
    connection.Open()
    Dim reader As SqlDataReader = command.ExecuteReader()
    Try
        While reader.Read()
            'work with fields: Field1 now in reader(0), Field2 in reader(1)
        End While
    Finally
        ' Always need to call Close when done reading.
        reader.Close()
    End Try
End Using
```

### API Summary

The following methods available

- `connect(config_db_name)` connect to database by config name (config may contain multiple connection strings)
- `check_create_mdb(filepath)` create new MS Access database (TBD remove? is it necessary)
- `disconnect()` disconnect from currently connected db
- `query(sql)` run arbitrary sql query and return DbDataReader
- `exec(sql)` run arbitrary non-select sql query (for inserts, updates...)

- `value(sql)` get single value via arbitrary sql
- `value(table_name, where[, field_name])` get single value from table/where conditions and optional field_name(if not passed - first field value returned) TODO
- `value(table_name, where, 'count(*)')` get count(*) from table/where TODO

- `row(sql)` get single row As Hashtable via arbitrary sql
- `row(table_name, where[, order_by])` get single row (first row) by table/where and optional order by
- `obj(table_name, id)` get single row As Hashtable by table and id (your table must have `id` primary key column)

- `array(sql)` get all rows As ArrayList of Hashtables via arbitrary sql
- `array(table_name, where[, order_by])` get all rows by table/where and optional order by

- `col(sql)` get all values As ArrayList from first column

- `insert(table_name, data)` insert new row into db, return last inserted id
- `update(sql)` alias for `exec(sql)`
- `update(table_name, data, where)` update record by where conditions (AND)
- `update_or_insert(table_name, data, where)` tries to update, it no records affected - insert new record
- `del(table_name, where)` delete record by where conditions (AND)

- `q(string[, length=0])` quote string - double single quotes and wrap result into single quotes, optionally trim to left `length` chars
- `qq(string)` quote string witout wrapping result into single quotes
- `qi(string)` quote string as integer - convert string into Integer
- `qd(string)` quote string as date or NULL (if string cannot be parsed as Date)
- `quote(table_name, data)` quote all field names and values in `data` for a table according to field types
- `qone(table_name, field_name, field_value)` quote one field value according to table/field type
- `left(string, length)` trim string and return only left `length` chars

### Samples

TODO add many samples, add sample live demo code

## TODO

- there is a dependency on `osafw` framework (logger and config), need to be refactored
  - redo logging via Diagnostics.Debug.WriteLine or other way
  - config - read connection from web.config or constructor params
- better error handling without dependency on framework
