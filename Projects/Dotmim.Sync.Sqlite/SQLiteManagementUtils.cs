﻿using Dotmim.Sync.Data;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using System.Linq;
using Microsoft.Data.Sqlite;
using Dotmim.Sync.Builders;

namespace Dotmim.Sync.Sqlite
{
    internal static class SqliteManagementUtils
    {

        public static void DropTableIfExists(SqliteConnection connection, SqliteTransaction transaction, string quotedTableName)
        {
            var tableName = ParserName.Parse(quotedTableName).ToString();

            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = $"drop table if exist {tableName}";
                if (transaction != null)
                    dbCommand.Transaction = transaction;

                dbCommand.ExecuteNonQuery();
            }
        }

        public static string DropTableIfExistsScriptText(string quotedTableName)
        {
            var tableName = ParserName.Parse(quotedTableName).ToString();
            return $"drop table if exist {tableName}";
        }

        public static string DropTableScriptText(string quotedTableName)
        {
            var tableName = ParserName.Parse(quotedTableName).ToString();
            return $"drop table {tableName}";
        }

        public static void DropTriggerIfExists(SqliteConnection connection, SqliteTransaction transaction, string quotedTriggerName)
        {
            var triggerName = ParserName.Parse(quotedTriggerName).ToString();

            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = $"drop trigger if exist {triggerName}";
                if (transaction != null)
                    dbCommand.Transaction = transaction;
             
                dbCommand.ExecuteNonQuery();
            }
        }

        public static string DropTriggerScriptText(string quotedTriggerName)
        {
            var triggerName = ParserName.Parse(quotedTriggerName).ToString();
            return $"drop trigger {triggerName}";
        }


        public static bool TableExists(SqliteConnection connection, SqliteTransaction transaction, ParserName tableName)
        {
            bool tableExist;
            var quotedTableName = tableName.Unquoted().ToString();

            using (DbCommand dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "select count(*) from sqlite_master where name = @tableName AND type='table'";

                var SqliteParameter = new SqliteParameter()
                {
                    ParameterName = "@tableName",
                    Value = quotedTableName
                };
                dbCommand.Parameters.Add(SqliteParameter);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                tableExist = (long)dbCommand.ExecuteScalar() != 0L;
            }
            return tableExist;
        }

        public static bool TriggerExists(SqliteConnection connection, SqliteTransaction transaction, string quotedTriggerName)
        {
            bool triggerExist;
            var triggerName = ParserName.Parse(quotedTriggerName).ToString();

            using (var dbCommand = connection.CreateCommand())
            {
                dbCommand.CommandText = "select count(*) from sqlite_master where name = @triggerName AND type='trigger'";

                dbCommand.Parameters.AddWithValue("@triggerName", triggerName);

                if (transaction != null)
                    dbCommand.Transaction = transaction;

                triggerExist = (long)dbCommand.ExecuteScalar() != 0L;
            }
            return triggerExist;
        }

        internal static string JoinTwoTablesOnClause(IEnumerable<DmColumn> columns, string leftName, string rightName)
        {
            var stringBuilder = new StringBuilder();
            string strRightName = (string.IsNullOrEmpty(rightName) ? string.Empty : string.Concat(rightName, "."));
            string strLeftName = (string.IsNullOrEmpty(leftName) ? string.Empty : string.Concat(leftName, "."));

            string str = "";
            foreach (var column in columns)
            {
                var quotedColumn = ParserName.Parse(column).Quoted().ToString();

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

        internal static string WhereColumnAndParameters(IEnumerable<DmColumn> columns, string fromPrefix)
        {
            var stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string str1 = "";
            foreach (var column in columns)
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
            var stringBuilder = new StringBuilder();
            string strFromPrefix = (string.IsNullOrEmpty(fromPrefix) ? string.Empty : string.Concat(fromPrefix, "."));
            string strSeparator = "";
            foreach (var mutableColumn in table.MutableColumns)
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
