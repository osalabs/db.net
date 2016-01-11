﻿' Framework DB class
'
' Part of ASP.NET osa framework  www.osalabs.com/osafw/asp.net
' (c) 2009-2016 Oleg Savchuk www.osalabs.com

Imports System.Data.OleDb
Imports System.Data.SqlClient
Imports System.Data
Imports System.Data.Common
Imports System.IO

Public Class DB
    Private Shared schema_cache As Hashtable 'cache for the schema, lifetime = app lifetime

    'Private Shared TimeFieldLabel As String = " <small>Format 24hr.time. Eg, 1330 for 1:30pm"
    Private Shared globalUseDateforTime2SQL As String
    Private Shared useTimeFormat As Integer = 0   '0- use 13:00, 1- use 1300   FOR 1:00pm
    Private Shared useTimeFormatUS As Integer = 1 '0 - use military time, 1 - use AM/PM

    Private fw As FW
    Private dbconn_cache As New Hashtable 'that's ok because we using connections just for the time or request (i.e. it's not Shared/Static cache)

    Private current_db As String
    Private conf As Hashtable
    Private dbtype As String
    Private schema As Hashtable 'schema for currently connected db

    Public Sub New(fw As FW)
        Me.fw = fw
    End Sub

    Public Function connect(Optional conf_db_name As String = "current_db") As DbConnection
        Dim conn As DbConnection = Nothing

        If current_db IsNot Nothing Then
            conn = dbconn_cache(current_db)
        End If
        If conn Is Nothing OrElse conn.State <> ConnectionState.Open Then
                'connect/reconnect
                current_db = fw.config(conf_db_name)
                conf = fw.config("db")(current_db)
                dbtype = conf("type")
                'schema = conf("schema")
                schema = New Hashtable

                Dim oConnStr As String = conf("connection_string")
                If dbtype = "SQL" Then
                    conn = New SqlConnection(oConnStr)
                ElseIf dbtype = "OLE" Then
                    conn = New OleDbConnection(oConnStr)
                Else
                    Dim msg As String = "Unknown type [" & dbtype & "] for db " & current_db
                    fw.logger(msg)
                    Throw New ApplicationException(msg)
                End If
                conn.Open()
            dbconn_cache(current_db) = conn
        End If

        Return conn
    End Function

    Public Sub check_create_mdb(filepath As String)
        If File.Exists(filepath) Then Exit Sub

        Dim connstr As String = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" & filepath

        Dim cat As Object = CreateObject("ADOX.Catalog")
        cat.Create(connstr)
    End Sub

    'close all connections in cache
    Public Sub disconnect()
        For Each conn_name As String In dbconn_cache.Keys
            dbconn_cache(conn_name).Close()
        Next
    End Sub

    <Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")> _
    Public Function query(ByVal sql As String) As DbDataReader
        connect()
        fw.logger("DB:" & current_db & ", SQL QUERY: " & sql)

        Dim dbcomm As DbCommand = Nothing
        If dbtype = "SQL" Then
            dbcomm = New SqlCommand(sql, dbconn_cache(current_db))
        ElseIf dbtype = "OLE" Then
            dbcomm = New OleDbCommand(sql, dbconn_cache(current_db))
        End If

        Dim dbread As DbDataReader = dbcomm.ExecuteReader()
        Return dbread
    End Function

    'exectute without results (so db reader will be closed)
    <Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")> _
    Public Sub exec(ByVal sql As String)
        connect()
        fw.logger("DB=" & current_db & " SQL QUERY = " & sql)

        Dim dbcomm As DbCommand = Nothing
        If dbtype = "SQL" Then
            dbcomm = New SqlCommand(sql, dbconn_cache(current_db))
        ElseIf dbtype = "OLE" Then
            dbcomm = New OleDbCommand(sql, dbconn_cache(current_db))
        End If

        dbcomm.ExecuteNonQuery()
    End Sub


    Public Overloads Function row(ByVal sql As String) As Hashtable
        Dim dbread As DbDataReader = query(sql)
        dbread.Read()

        Dim h As New Hashtable

        If dbread.HasRows Then
            For i As Integer = 0 To dbread.FieldCount - 1
                Try
                    Dim value As String = dbread(i).ToString()
                    Dim name As String = dbread.GetName(i).ToString()
                    h.Add(name, value)
                Catch Ex As Exception
                    Exit For
                End Try
            Next i
        End If

        dbread.Close()
        Return h
    End Function

    Public Overloads Function row(ByVal table As String, ByVal where As Hashtable, Optional order_by As String = "") As Hashtable
        Return row(hash2sql_select(table, where, order_by))
    End Function

    Public Overloads Function obj(ByVal table As String, ByVal id As Integer) As Hashtable
        Dim where As New Hashtable
        where("id") = id
        Return row(hash2sql_select(table, where))
    End Function

    Public Overloads Function array(ByVal sql As String) As ArrayList
        Dim dbread As DbDataReader = query(sql)
        Dim a As New ArrayList
        Dim last_col_num As Integer = dbread.FieldCount - 1
        While dbread.Read()
            Dim h As New Hashtable
            Dim i As Integer
            For i = 0 To last_col_num
                Try
                    Dim value As String = dbread(i).ToString()
                    Dim name As String = dbread.GetName(i).ToString()
                    h.Add(name, value)
                Catch Ex As Exception
                    last_col_num = i - 1
                    Exit For
                End Try
            Next i
            a.Add(h)
        End While

        dbread.Close()
        Return a
    End Function

    Public Overloads Function array(ByVal table As String, ByVal where As Hashtable, Optional ByRef order_by As String = "") As ArrayList
        Return array(hash2sql_select(table, where, order_by))
    End Function

    'return just first column values as arraylist
    Public Overloads Function col(ByVal sql As String) As ArrayList
        Dim dbread As DbDataReader = query(sql)
        Dim a As New ArrayList
        Dim last_col_num As Integer = dbread.FieldCount
        While dbread.Read()
            a.Add(dbread(0).ToString())
        End While

        dbread.Close()
        Return a
    End Function

    'return just first value from column
    Public Overloads Function value(ByVal sql As String) As Object
        Dim dbread As DbDataReader = query(sql)
        Dim result As Object = Nothing

        While dbread.Read()
            result = dbread(0)
        End While

        dbread.Close()
        Return result
    End Function

    Public Function q(ByVal str As String) As String
        If IsNothing(str) Then str = ""
        Return "'" & Replace(str, "'", "''") & "'"
    End Function

    'simple just replace quotes, don't add start/end single quote - for example, for use with LIKE
    Public Function qq(ByVal str As String) As String
        If IsNothing(str) Then str = ""
        Return Replace(str, "'", "''")
    End Function

    'simple quote as Integer Value
    Public Function qi(ByVal str As String) As Integer
        Dim result As Integer = 0
        Int32.TryParse(str, result)
        Return result
    End Function

    Public Function quote(ByVal table As String, ByVal fields As Hashtable) As Hashtable
        connect()
        load_table_schema(table)
        If Not schema.ContainsKey(table) Then Throw New ApplicationException("table [" & table & "] does not defined in FW.config(""schema"")")

        Dim fieldsq As New Hashtable
        Dim k, q As String

        For Each k In fields.Keys
            q = qone(table, k, fields(k))
            'quote field name too
            k = Replace(k, "[", "")
            k = Replace(k, "]", "")
            If q IsNot Nothing Then fieldsq("[" & k & "]") = q
        Next k

        Return fieldsq
    End Function

    Public Function qone(ByVal table As String, ByVal field_name As String, ByVal field_value As Object) As String
        load_table_schema(table)
        field_name = field_name.ToLower()
        If Not schema(table).containskey(field_name) Then Throw New ApplicationException("field " & table & "." & field_name & " does not defined in FW.config(""schema"") ")

        Dim quoted As String = ""

        'if value set to Nothing or DBNull - assume it's NULL in db
        If field_value Is Nothing OrElse IsDBNull(field_value) Then
            quoted = "NULL"
        Else
            Dim field_type As String = schema(table)(field_name)
            'fw.logger(table & "." & field_name & " => " & field_type & ", value=[" & field_value & "]")
            If Regex.IsMatch(field_type, "int") Then
                If field_value IsNot Nothing AndAlso Regex.IsMatch(field_value, "true", RegexOptions.IgnoreCase) Then
                    quoted = "1"
                ElseIf field_value IsNot Nothing AndAlso Regex.IsMatch(field_value, "false", RegexOptions.IgnoreCase) Then
                    quoted = "0"
                ElseIf field_value IsNot Nothing AndAlso TypeOf field_value Is String AndAlso field_value = "" Then
                    'if empty string for numerical field - assume NULL
                    quoted = "NULL"
                Else
                    quoted = Utils.f2int(field_value)
                    'Dim f As String = Regex.Replace(field_value, "\D", "")
                    'If (f.Length > 0) Then quoted = f
                End If

            ElseIf field_type = "datetime" Then
                If dbtype = "SQL" Then
                    Dim tmpdate As DateTime
                    If DateTime.TryParse(field_value, tmpdate) Then
                        quoted = "convert(DATETIME, '" & tmpdate.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.DateTimeFormatInfo.InvariantInfo) & "', 120)"
                    Else
                        quoted = "NULL"
                    End If
                Else
                    quoted = Regex.Replace(field_value.ToString, "['""\]\[]", "")
                    If Regex.IsMatch(quoted, "\D") Then
                        quoted = "'" & field_value & "'"
                    Else
                        quoted = "NULL"
                    End If
                End If

            ElseIf field_type = "float" Then
                quoted = Utils.f2float(Regex.Replace(field_value, ",", "."))
                'Dim f As String = Regex.Replace(field_value, "[^\d,.]", "")
                'f = Regex.Replace(f, ",", ".")
                'If (f.Length > 0) Then quoted = f

            Else
                'fieldsq(k) = "'" & Regex.Replace(fields(k), "(['""])", "\\$1") & "'"
                If IsNothing(field_value) Then
                    quoted = "''"
                Else
                    'escape backslash following by carriage return char(13) with doubling backslash and carriage return
                    'because of https://msdn.microsoft.com/en-us/library/dd207007.aspx
                    quoted = Regex.Replace(field_value, "\\(\r\n?)", "\\$1$1")
                    quoted = Regex.Replace(quoted, "'", "''") 'escape single quotes
                    quoted = "'" & quoted & "'"
                End If
            End If
        End If

        Return quoted
    End Function

    'return last inserted id
    Public Function insert(ByVal table As String, ByVal fields As Hashtable) As Integer
        If fields.Count < 1 Then Return False
        exec(hash2sql_i(table, fields))

        Dim insert_id As Object

        If dbtype = "SQL" Then
            insert_id = value("select SCOPE_IDENTITY() AS [SCOPE_IDENTITY] ")
        ElseIf dbtype = "OLE" Then
            insert_id = value("select @@identity")
        Else
            Throw New ApplicationException("Get last insert ID for DB type [" & dbtype & "] not implemented")
        End If

        'if table doesn't have identity insert_id would be DBNull
        If IsDBNull(insert_id) Then insert_id = 0

        Return insert_id
    End Function

    Public Overloads Sub update(ByVal sql As String)
        exec(sql)
    End Sub

    Public Overloads Sub update(ByVal table As String, ByVal fields As Hashtable, ByVal where As Hashtable)
        exec(hash2sql_u(table, fields, where))
    End Sub

    Public Sub update_or_insert(ByVal table As String, ByVal fields As Hashtable, ByVal where As Hashtable)
        ' merge fields and where
        Dim allfields As New Hashtable
        Dim k As String
        For Each k In fields.Keys
            allfields(k) = fields(k)
        Next k

        For Each k In where.Keys
            allfields(k) = where(k)
        Next k

        Dim update_sql As String = hash2sql_u(table, fields, where)
        Dim insert_sql As String = hash2sql_i(table, allfields)
        Dim full_sql As String = update_sql & "  IF @@ROWCOUNT = 0 " & insert_sql

        exec(full_sql)
    End Sub

    Public Sub del(ByVal table As String, ByVal where As Hashtable)
        exec(hash2sql_d(table, where))
    End Sub

    'join key/values with quoting values according to table
    ' h - already quoted! values
    Private Function _join_hash(h As Hashtable, ByVal kv_delim As String, ByVal pairs_delim As String) As String
        Dim res As String = ""
        If h.Count < 1 Then Return res

        Dim ar(h.Count - 1) As String

        Dim i As Integer = 0
        Dim k As String
        For Each k In h.Keys
            ar(i) = k & kv_delim & h(k)
            i += 1
        Next k
        res = String.Join(pairs_delim, ar)
        Return res
    End Function

    Private Function hash2sql_select(ByVal table As String, ByVal where As Hashtable, Optional ByRef order_by As String = "") As String
        where = quote(table, where)
        'FW.logger(where)
        Dim where_string As String = _join_hash(where, "=", " and ")
        If where_string.Length > 0 Then where_string = " where " & where_string

        Dim sql As String = "select * from   [" & table & "] " & where_string
        If order_by.Length > 0 Then sql = sql & " order by " & order_by
        Return sql
    End Function

    Private Function hash2sql_u(ByVal table As String, ByVal fields As Hashtable, ByVal where As Hashtable) As String
        fields = quote(table, fields)
        where = quote(table, where)

        Dim update_string As String = _join_hash(fields, "=", ", ")
        Dim where_string As String = _join_hash(where, "=", " and ")

        If where_string.Length > 0 Then where_string = " where " & where_string

        Dim sql As String = "update [" & table & "] " & " set " & update_string & where_string

        Return sql
    End Function

    Private Function hash2sql_i(ByVal table As String, ByVal fields As Hashtable) As String
        fields = quote(table, fields)

        Dim ar(fields.Count - 1) As String

        fields.Keys.CopyTo(ar, 0)
        Dim names_string As String = String.Join(", ", ar)

        fields.Values.CopyTo(ar, 0)
        Dim values_string As String = String.Join(", ", ar)
        Dim sql As String = "insert into [" & table & "] " & "(" & names_string & " ) values(" & values_string & ")"
        Return sql
    End Function

    Private Function hash2sql_d(ByVal table As String, ByVal where As Hashtable) As String
        where = quote(table, where)
        Dim where_string As String = _join_hash(where, "=", " and ")
        If where_string.Length > 0 Then where_string = " where " & where_string

        Dim sql As String = "delete from [" & table & "] " & where_string
        Return sql
    End Function

    'load table schema from db
    Private Sub load_table_schema(table As String)
        'for non-MSSQL schemas - just use config schema for now - TODO
        If dbtype <> "SQL" AndAlso dbtype <> "OLE" Then
            If schema.Count = 0 Then
                schema = conf("schema")
            End If
            Return
        End If

        'check if schema already there
        If schema.ContainsKey(table) Then Return

        If IsNothing(schema_cache) Then schema_cache = New Hashtable
        If Not schema_cache.ContainsKey(current_db) Then schema_cache(current_db) = New Hashtable
        If Not schema_cache(current_db).ContainsKey(table) Then
            Dim h As New Hashtable

            If dbtype = "SQL" Then
                'fw.logger("cache MISS " & current_db & "." & table)
                'get information about all columns in the table
                Dim sql As String = "SELECT c.column_name as 'name'," & _
                        " c.data_type as 'type'," & _
                        " CASE c.is_nullable WHEN 'YES' THEN 1 ELSE 0 END AS 'is_nullable'," & _
                        " c.column_default as 'default'," & _
                        " c.character_maximum_length as 'maxlen'," & _
                        " c.numeric_precision," & _
                        " c.numeric_scale," & _
                        " c.character_set_name as 'charset'," & _
                        " c.collation_name as 'collation'" & _
                        " FROM INFORMATION_SCHEMA.TABLES t," & _
                        "   INFORMATION_SCHEMA.COLUMNS c" & _
                        " WHERE t.table_name = c.table_name" & _
                        "   AND t.table_name = " & q(table)
                Dim rows As ArrayList = array(sql)
                For Each row As Hashtable In rows
                    h(row("name").ToString().ToLower()) = map_mssqltype2internal(row("type"))
                Next
            Else
                'OLE DB (Access)
                Dim schemaTable As DataTable = _
                    dbconn_cache(current_db).GetOleDbSchemaTable(OleDb.OleDbSchemaGuid.Columns, _
                    New Object() {Nothing, Nothing, table, Nothing})

                For Each row As DataRow In schemaTable.Rows
                    'COLUMN_NAME
                    'COLUMN_HASDEFAULT
                    'COLUMN_DEFAULT
                    'IS_NULLABLE
                    'DATA_TYPE
                    'CHARACTER_MAXIMUM_LENGTH
                    'fw.logger(row("COLUMN_NAME"))
                    'fw.logger(row("DATA_TYPE"))
                    'fw.logger(row("CHARACTER_MAXIMUM_LENGTH"))
                    h(row("COLUMN_NAME").ToString().ToLower()) = map_oletype2internal(row("DATA_TYPE"))
                Next
            End If

            schema(table) = h
            schema_cache(current_db)(table) = h
        Else
            'fw.logger("schema_cache HIT " & current_db & "." & table)
            schema(table) = schema_cache(current_db)(table)
        End If

    End Sub

    Private Function map_mssqltype2internal(mstype As String) As String
        Dim result As String = ""
        Select Case LCase(mstype)
            'TODO - unsupported: bit, image, varbinary, timestamp
            Case "tinyint", "smallint", "int", "bigint"
                result = "int"
            Case "real", "numeric", "decimal", "money", "smallmoney"
                result = "float"
            Case "datetime", "datetime2", "date", "smalldatetime"
                result = "datetime"
            Case Else '"text", "ntext", "varchar", "nvarchar", "char", "nchar"
                result = "varchar"
        End Select

        Return result
    End Function

    Private Function map_oletype2internal(mstype As Integer) As String
        Dim result As String = ""
        Select Case mstype
            'TODO - unsupported: bit, image, varbinary, timestamp
            'NOTE: Boolean here is: True=-1 (vbTrue), False=0 (vbFalse)
            Case OleDbType.Boolean, OleDbType.TinyInt, OleDbType.UnsignedTinyInt, OleDbType.SmallInt, OleDbType.UnsignedSmallInt, OleDbType.Integer, OleDbType.UnsignedInt, OleDbType.BigInt, OleDbType.UnsignedBigInt
                result = "int"
            Case OleDbType.Double, OleDbType.Numeric, OleDbType.VarNumeric, OleDbType.Single, OleDbType.Decimal
                result = "float"
            Case OleDbType.Date, OleDbType.DBDate, OleDbType.DBTimeStamp
                result = "datetime"
            Case Else '"text", "ntext", "varchar", "nvarchar", "char", "nchar"
                result = "varchar"
        End Select

        Return result
    End Function

End Class
