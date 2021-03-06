using Dotmim.Sync.Builders;
using Dotmim.Sync.Data;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dotmim.Sync.SqlServer
{
    public static class SqlManagementUtils
    {

        /// <summary>
        /// Get columns for table
        /// </summary>
        public static DmTable ColumnsForTable(SqlConnection connection, SqlTransaction transaction, string tableName)
        {

            var commandColumn = $"Select col.name as name, col.column_id, typ.name as [type], col.max_length, col.precision, col.scale, col.is_nullable, col.is_computed, col.is_identity, ind.is_unique " +
                                $"from sys.columns as col " +
                                $"Inner join sys.tables as tbl on tbl.object_id = col.object_id " +
                                $"Inner Join sys.systypes typ on typ.xusertype = col.system_type_id " +
                                $"Left outer join sys.indexes ind on ind.object_id = col.object_id and ind.index_id = col.column_id " +
                                $"Where tbl.name = @tableName";

            var tableNameNormalized = ParserName.Parse(tableName).Unquoted().Normalized().ToString();
            var tableNameString = ParserName.Parse(tableName).ToString();

            var dmTable = new DmTable(tableNameNormalized);
            using (var sqlCommand = new SqlCommand(commandColumn, connection, transaction))
            {
                sqlCommand.Parameters.AddWithValue("@tableName", tableNameString);

                using (var reader = sqlCommand.ExecuteReader())
                {
                    dmTable.Fill(reader);
                }
            }
            return dmTable;
        }

        /// <summary>
        /// Get columns for table
        /// </summary>
        internal static DmTable PrimaryKeysForTable(SqlConnection connection, SqlTransaction transaction, string tableName)
        {

            var commandColumn = @"select ind.name, col.name as columnName, ind_col.column_id, ind_col.key_ordinal 
                                  from sys.indexes ind
                                  left outer join sys.index_columns ind_col on ind_col.object_id = ind.object_id and ind_col.index_id = ind.index_id
                                  inner join sys.columns col on col.object_id = ind_col.object_id and col.column_id = ind_col.column_id
                                  inner join sys.tables tbl on tbl.object_id = ind.object_id
                                  where tbl.name = @tableName and ind.index_id >= 0 and ind.type <> 3 and ind.type <> 4 and ind.is_hypothetical = 0 and ind.is_primary_key = 1
                                  order by ind.index_id, ind_col.key_ordinal";

            var tableNameNormalized = ParserName.Parse(tableName).Unquoted().Normalized().ToString();
            var tableNameString = ParserName.Parse(tableName).ToString();

            var dmTable = new DmTable(tableNameNormalized);
            using (var sqlCommand = new SqlCommand(commandColumn, connection, transaction))
            {
                sqlCommand.Parameters.AddWithValue("@tableName", tableNameString);

                using (var reader = sqlCommand.ExecuteReader())
                {
                    dmTable.Fill(reader);
                }
            }
            return dmTable;
        }

        internal static DmTable RelationsForTable(SqlConnection connection, SqlTransaction transaction, string tableName)
        {
            var commandRelations = @"SELECT f.name AS ForeignKey,
                                        constraint_column_id as ForeignKeyOrder,
                                        OBJECT_NAME (f.referenced_object_id)  AS TableName,
                                        COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS ColumnName,
                                        OBJECT_NAME(f.parent_object_id) AS ReferenceTableName,
                                        COL_NAME(fc.parent_object_id, fc.parent_column_id) AS ReferenceColumnName
                                    FROM sys.foreign_keys AS f
                                    INNER JOIN sys.foreign_key_columns AS fc ON f.OBJECT_ID = fc.constraint_object_id
                                    WHERE OBJECT_NAME(f.referenced_object_id) = @tableName";

            var tableNameNormalized = ParserName.Parse(tableName).Unquoted().Normalized().ToString();
            var tableNameString = ParserName.Parse(tableName).ToString();

            var dmTable = new DmTable(tableNameNormalized);
            using (var sqlCommand = new SqlCommand(commandRelations, connection, transaction))
            {
                sqlCommand.Parameters.AddWithValue("@tableName", tableNameString);

                using (var reader = sqlCommand.ExecuteReader())
                {
                    dmTable.Fill(reader);
                }
            }


            return dmTable;
        }

        public static void DropProcedureIfExists(SqlConnection connection, SqlTransaction transaction, int commandTimout, string quotedProcedureName)
        {
            var procName = ParserName.Parse(quotedProcedureName).ToString();
            using (var sqlCommand = new SqlCommand(string.Format(CultureInfo.InvariantCulture, "IF EXISTS (SELECT * FROM sys.procedures p JOIN sys.schemas s ON s.schema_id = p.schema_id WHERE p.name = @procName AND s.name = @schemaName) DROP PROCEDURE {0}", quotedProcedureName), connection, transaction))
            {
                sqlCommand.CommandTimeout = commandTimout;
                sqlCommand.Parameters.AddWithValue("@procName", procName);
                sqlCommand.Parameters.AddWithValue("@schemaName", SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedProcedureName)));
                sqlCommand.ExecuteNonQuery();
            }
        }

        public static string DropProcedureScriptText(string quotedProcedureName)
        {
            return string.Format(CultureInfo.InvariantCulture, "DROP PROCEDURE {0};\n", quotedProcedureName);
        }

        public static void DropTableIfExists(SqlConnection connection, SqlTransaction transaction, int commandTimeout, string quotedTableName)
        {
            var tableName = ParserName.Parse(quotedTableName).ToString();
            using (var sqlCommand = new SqlCommand(string.Format(CultureInfo.InvariantCulture, "IF EXISTS (SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.name = @tableName AND s.name = @schemaName) DROP TABLE {0}", quotedTableName), connection, transaction))
            {
                sqlCommand.CommandTimeout = commandTimeout;
                sqlCommand.Parameters.AddWithValue("@tableName", tableName);
                sqlCommand.Parameters.AddWithValue("@schemaName", SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedTableName)));
                sqlCommand.ExecuteNonQuery();
            }
        }

        public static string DropTableIfExistsScriptText(string quotedTableName)
        {
            var tableName = ParserName.Parse(quotedTableName).ToString();

            object[] escapedString = new object[] { tableName, SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedTableName)), quotedTableName };
            return string.Format(CultureInfo.InvariantCulture, "IF EXISTS (SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.name = N'{0}' AND s.name = N'{1}') DROP TABLE {2}\n", escapedString);
        }

        public static string DropTableScriptText(string quotedTableName)
        {
            var invariantCulture = CultureInfo.InvariantCulture;
            object[] objArray = new object[] { quotedTableName };
            return string.Format(invariantCulture, "DROP TABLE {0};\n", objArray);
        }

        public static void DropTriggerIfExists(SqlConnection connection, SqlTransaction transaction, int commandTimeout, string quotedTriggerName)
        {
            var triggerName = ParserName.Parse(quotedTriggerName).ToString();

            using (var sqlCommand = new SqlCommand(string.Format(CultureInfo.InvariantCulture, "IF EXISTS (SELECT tr.name FROM sys.triggers tr JOIN sys.tables t ON tr.parent_id = t.object_id JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE tr.name = @triggerName and s.name = @schemaName) DROP TRIGGER {0}", quotedTriggerName), connection, transaction))
            {
                sqlCommand.CommandTimeout = commandTimeout;
                sqlCommand.Parameters.AddWithValue("@triggerName", triggerName);
                sqlCommand.Parameters.AddWithValue("@schemaName", SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedTriggerName)));
                sqlCommand.ExecuteNonQuery();
            }
        }

        public static string DropTriggerScriptText(string quotedTriggerName)
        {
            return string.Format(CultureInfo.InvariantCulture, "DROP TRIGGER {0};\n", quotedTriggerName);
        }


        public static void DropTypeIfExists(SqlConnection connection, SqlTransaction transaction, int commandTimeout, string quotedTypeName)
        {
            var typeName = ParserName.Parse(quotedTypeName).ToString();

            using (var sqlCommand = new SqlCommand(string.Format(CultureInfo.InvariantCulture, "IF EXISTS (SELECT * FROM sys.types t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.name = @typeName AND s.name = @schemaName) DROP TYPE {0}", quotedTypeName), connection, transaction))
            {
                sqlCommand.CommandTimeout = commandTimeout;
                sqlCommand.Parameters.AddWithValue("@typeName", typeName);
                sqlCommand.Parameters.AddWithValue("@schemaName", SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedTypeName)));
                sqlCommand.ExecuteNonQuery();
            }
        }

        public static string DropTypeScriptText(string quotedTypeName)
        {
            return string.Format(CultureInfo.InvariantCulture, "DROP TYPE {0};\n", quotedTypeName);
        }



        internal static string GetObjectSchemaValue(string value)
        {
            string empty = value ?? string.Empty;
            if (empty.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                empty = string.Empty;

            return empty;
        }

        public static string GetUnquotedSqlSchemaName(ParserName parser)
        {
            if (string.IsNullOrEmpty(parser.SchemaName))
                return "dbo";

            return parser.SchemaName;
        }

        internal static bool IsStringNullOrWhitespace(string value)
        {
            return Regex.Match(value ?? string.Empty, "^\\s*$").Success;
        }

        public static bool ProcedureExists(SqlConnection connection, SqlTransaction transaction, string quotedProcedureName)
        {
            bool flag;
            var procedureName = ParserName.Parse(quotedProcedureName).ToString();

            using (var sqlCommand = new SqlCommand("IF EXISTS (SELECT * FROM sys.procedures p JOIN sys.schemas s ON s.schema_id = p.schema_id WHERE p.name = @procName AND s.name = @schemaName) SELECT 1 ELSE SELECT 0", connection))
            {
                sqlCommand.Parameters.AddWithValue("@procName", procedureName);
                sqlCommand.Parameters.AddWithValue("@schemaName", SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedProcedureName)));
                if (transaction != null)
                {
                    sqlCommand.Transaction = transaction;
                }
                flag = (int)sqlCommand.ExecuteScalar() != 0;
            }
            return flag;
        }

        public static bool TableExists(SqlConnection connection, SqlTransaction transaction, string quotedTableName)
        {
            bool tableExist;
            var tableName = ParserName.Parse(quotedTableName).ToString();

            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "IF EXISTS (SELECT t.name FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.name = @tableName AND s.name = @schemaName) SELECT 1 ELSE SELECT 0";

                SqlParameter sqlParameter = new SqlParameter()
                {
                    ParameterName = "@tableName",
                    Value = tableName
                };
                dbCommand.Parameters.Add(sqlParameter);

                sqlParameter = new SqlParameter()
                {
                    ParameterName = "@schemaName",
                    Value = SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedTableName))
                };
                dbCommand.Parameters.Add(sqlParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                tableExist = (int)dbCommand.ExecuteScalar() != 0;
            }
            return tableExist;
        }

        public static bool SchemaExists(SqlConnection connection, SqlTransaction transaction, string schemaName)
        {
            bool schemaExist;
            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "IF EXISTS (SELECT sch.name FROM sys.schemas sch WHERE sch.name = @schemaName) SELECT 1 ELSE SELECT 0";

                var sqlParameter = new SqlParameter()
                {
                    ParameterName = "@schemaName",
                    Value = schemaName
                };
                dbCommand.Parameters.Add(sqlParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                schemaExist = (int)dbCommand.ExecuteScalar() != 0;
            }
            return schemaExist;
        }

        public static bool TriggerExists(SqlConnection connection, SqlTransaction transaction, string quotedTriggerName)
        {
            bool triggerExist;
            var triggerName = ParserName.Parse(quotedTriggerName).ToString();

            using (var sqlCommand = new SqlCommand("IF EXISTS (SELECT tr.name FROM sys.triggers tr JOIN sys.tables t ON tr.parent_id = t.object_id JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE tr.name = @triggerName and s.name = @schemaName) SELECT 1 ELSE SELECT 0", connection))
            {
                sqlCommand.Parameters.AddWithValue("@triggerName", triggerName);
                sqlCommand.Parameters.AddWithValue("@schemaName", SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedTriggerName)));
                if (transaction != null)
                    sqlCommand.Transaction = transaction;

                triggerExist = (int)sqlCommand.ExecuteScalar() != 0;
            }
            return triggerExist;
        }

        public static bool TypeExists(SqlConnection connection, SqlTransaction transaction, string quotedTypeName)
        {
            bool typeExist;

            var columnName = ParserName.Parse(quotedTypeName).ToString();

            using (SqlCommand sqlCommand = new SqlCommand("IF EXISTS (SELECT * FROM sys.types t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.name = @typeName AND s.name = @schemaName) SELECT 1 ELSE SELECT 0", connection))
            {
                sqlCommand.Parameters.AddWithValue("@typeName", columnName);
                sqlCommand.Parameters.AddWithValue("@schemaName", SqlManagementUtils.GetUnquotedSqlSchemaName(ParserName.Parse(quotedTypeName)));
                if (transaction != null)
                    sqlCommand.Transaction = transaction;

                typeExist = (int)sqlCommand.ExecuteScalar() != 0;
            }
            return typeExist;
        }

        internal static string JoinTwoTablesOnClause(IEnumerable<DmColumn> columns, string leftName, string rightName)
        {
            var stringBuilder = new StringBuilder();
            string strRightName = (string.IsNullOrEmpty(rightName) ? string.Empty : string.Concat(rightName, "."));
            string strLeftName = (string.IsNullOrEmpty(leftName) ? string.Empty : string.Concat(leftName, "."));

            string str = "";
            foreach (var column in columns)
            {
                var quotedColumn = ParserName.Parse(column).Quoted().ToString() ;

                stringBuilder.Append(str);
                stringBuilder.Append(strLeftName);
                stringBuilder.Append(quotedColumn);
                stringBuilder.Append(" = ");
                stringBuilder.Append(strRightName);
                stringBuilder.Append(quotedColumn);

                str = " AND ";
            }
            return stringBuilder.ToString();
        }

        internal static string ColumnsAndParameters(IEnumerable<DmColumn> columns, string fromPrefix)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string str1 = "";
            foreach (DmColumn column in columns)
            {
                var quotedColumn = ParserName.Parse(column).Quoted().ToString();
                var unquotedColumn = ParserName.Parse(column).Unquoted().Normalized().ToString();

                stringBuilder.Append(str1);
                stringBuilder.Append(strFromPrefix);
                stringBuilder.Append(quotedColumn);
                stringBuilder.Append(" = ");
                stringBuilder.Append($"@{unquotedColumn}");
                str1 = " AND ";
            }
            return stringBuilder.ToString();
        }

        internal static string CommaSeparatedUpdateFromParameters(DmTable table, string fromPrefix = "")
        {
            StringBuilder stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string strSeparator = "";
            foreach (DmColumn mutableColumn in table.MutableColumnsAndNotAutoInc)
            {
                var quotedColumn = ParserName.Parse(mutableColumn).Quoted().ToString();
                var unquotedColumn = ParserName.Parse(mutableColumn).Unquoted().Normalized().ToString();

                stringBuilder.AppendLine($"{strSeparator} {strFromPrefix}{quotedColumn} = @{unquotedColumn}");
                strSeparator = ", ";
            }
            return stringBuilder.ToString();

        }
    }
}
