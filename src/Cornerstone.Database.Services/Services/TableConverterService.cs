using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Text;
using Cornerstone.Database.Providers;

namespace Cornerstone.Database.Services;

public class TableConverterService
{

    private readonly IDatabaseFactory _databaseFactory;

    public TableConverterService(IDatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public void ConvertTables(TableConverterSettings settings,
        IProgress<TableProgress> progress,
        int maxDegreeOfParallelism)
    {

        var tables = settings.SourceTables.OrderBy(i => i.TableName);

        var options = new ParallelOptions();
        options.MaxDegreeOfParallelism = maxDegreeOfParallelism;

        Parallel.ForEach(tables, options, (sourceTable) =>
        {
            this.ConvertTable(settings, sourceTable, progress);
        });
    }

    public void ConvertTable(TableConverterSettings settings, Models.TableModel sourceTable, IProgress<TableProgress> progress)
    {

        var targetTable = (
                   from i in settings.TargetTables
                   where
                   i.SchemaName.Equals(sourceTable.SchemaName, StringComparison.InvariantCultureIgnoreCase) &&
                   i.TableName.Equals(sourceTable.TableName, StringComparison.InvariantCultureIgnoreCase)
                   select i).FirstOrDefault();

        if (targetTable == null && sourceTable.SchemaName == "dbo")
        {
            targetTable = (
                 from i in settings.TargetTables
                 where
                 i.TableName.Equals(sourceTable.TableName, StringComparison.InvariantCultureIgnoreCase)
                 select i).FirstOrDefault();
        }

        ConvertTable(settings, sourceTable, targetTable, progress);
    }

    public void ConvertTable(TableConverterSettings settings, Models.TableModel sourceTable, Models.TableModel targetTable, IProgress<TableProgress> progress, bool throwOnFailure = false)
    {
        if (targetTable != null)
        {
            try
            {

                if (settings.UseBulkCopy)
                {
                    this.ConvertBulk(progress, sourceTable, settings.SourceConnectionString, targetTable, settings.TargetConnectionString, settings.TrimStrings, settings.BatchSize, settings.UseTransaction);
                }
                else
                {
                    this.Convert(progress, sourceTable, settings.SourceConnectionString, targetTable, settings.TargetConnectionString, settings.TrimStrings);
                }
            }
            catch (Exception ex)
            {
                string strException = string.Empty;
                if (!(ex.Message.Equals("ERROR [00000] [QODBC] Error: 3250 - This feature is not enabled or not available in this version of QuickBooks.")))
                {
                    strException = ex.ToString();
                }

                if (progress == null || throwOnFailure)
                {
                    if (ex as TableConverterException == null)
                    {
                        throw;
                    }
                    else
                    {
                        throw new TableConverterException(sourceTable, strException, ex);
                    }
                }
                else
                {
                    progress.Report(new TableProgress() { ProgressPercentage = 100, Table = sourceTable, ErrorMessage = strException, Exception = ex });
                }

            }
        }
    }

    public void ConvertBulk(IProgress<TableProgress> progress,
      Models.TableModel sourceTable,
      IDataReader sourceReader,
      int sourceRowCount,
      Models.TableModel targetTable,
      ConnectionStringSettings targetConnectionString,
      bool trimStrings,
      int batchSize,
      bool useTransaction = true,
      bool validateTargetTable = true)
    {
        var targetDatabase = new DatabaseService(_databaseFactory, _databaseFactory.GetDatabaseType(targetConnectionString));
        using (System.Data.Common.DbConnection targetConnection = targetDatabase.CreateDbConnection(targetConnectionString))
        {
            targetDatabase.DatabaseProvider.ConvertBulk(this, progress, sourceTable, sourceReader, sourceRowCount, targetTable, targetConnection, trimStrings, batchSize, useTransaction, validateTargetTable);
        }
    }

    private DbCommand CreateSourceCommand(Models.TableModel sourceTable, Models.TableModel targetTable, System.Data.Common.DbConnection sourceConnection)
    {
        var sourceDatabaseType = _databaseFactory.GetDatabaseType(sourceConnection);

        var sourceDatabase = new DatabaseService(_databaseFactory, sourceDatabaseType);

        StringBuilder sbColumns = new System.Text.StringBuilder();

        var sourceMatchedColumns = this.GetMatchedColumns(sourceTable.Columns, targetTable.Columns);

        foreach (var sourceColumn in sourceMatchedColumns)
        {
            if (sbColumns.Length > 0)
            {
                sbColumns.Append(", ");
            }
            sbColumns.AppendFormat("[{0}]", sourceColumn.ColumnName);
        }

        using (var command = sourceDatabase.CreateDbCommand(sourceConnection))
        {
            this.SetReadTimeout(sourceDatabaseType, command);
            command.CommandText = this.FormatCommandText($"SELECT {sbColumns.ToString()} FROM [{sourceTable.SchemaName}].[{sourceTable.TableName}]", sourceDatabaseType);
            return command;
        }

    }

    public void ConvertBulk(IProgress<TableProgress> progress,
        Models.TableModel sourceTable,
        System.Configuration.ConnectionStringSettings sourceConnectionString,
        Models.TableModel targetTable,
        System.Configuration.ConnectionStringSettings targetConnectionString,
        bool trimStrings,
        int batchSize,
        bool useTransaction = true)
    {
        progress?.Report(new TableProgress() { ProgressPercentage = 0, Table = sourceTable });

        var sourceDatabaseType = _databaseFactory.GetDatabaseType(sourceConnectionString);
        var targetDatabaseType = _databaseFactory.GetDatabaseType(targetConnectionString);

        var sourceDatabase = new DatabaseService(_databaseFactory, sourceDatabaseType);
        var targetDatabase = new DatabaseService(_databaseFactory, targetDatabaseType);

        using (var targetConnection = targetDatabase.CreateDbConnection(targetConnectionString))
        {
            var targetRowCount = GetRowCount(targetTable, targetConnection);
            if (targetRowCount != 0)
            {
                progress?.Report(new TableProgress() { ProgressPercentage = 100, Table = sourceTable });
                return;
            }
        }

        int? intSourceRowCount = null;

        if (progress != null)
        {
            using (System.Data.Common.DbConnection sourceConnection = sourceDatabase.CreateDbConnection(sourceConnectionString))
            {
                intSourceRowCount = GetRowCount(sourceTable, sourceConnection);
            }
        }

        if (!intSourceRowCount.HasValue || intSourceRowCount.Value > 0)
        {
            using (System.Data.Common.DbConnection sourceConnection = sourceDatabase.CreateDbConnection(sourceConnectionString))
            {
                using (var command = CreateSourceCommand(sourceTable, targetTable, sourceConnection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        var sourceMatchedColumns = this.GetMatchedColumns(sourceTable.Columns, targetTable.Columns);
                        var targetMatchedColumns = this.GetMatchedColumns(targetTable.Columns, sourceTable.Columns);
                        var reader2 = new TableConverterReader(_databaseFactory, reader, sourceMatchedColumns, targetMatchedColumns, trimStrings, sourceDatabaseType, targetDatabaseType);
                        ConvertBulk(progress, sourceTable, reader2, intSourceRowCount.GetValueOrDefault(), targetTable, targetConnectionString, trimStrings, batchSize, useTransaction, false);
                    }
                }
            }
        }
    }

    public void Convert(IProgress<TableProgress> progress,
        Models.TableModel sourceTable,
        System.Configuration.ConnectionStringSettings sourceConnectionString,
        Models.TableModel targetTable,
        System.Configuration.ConnectionStringSettings targetConnectionString,
        bool trimStrings,
        Action<IDbConnection> connectionCreatedCallback = null)
    {
        progress?.Report(new TableProgress() { ProgressPercentage = 0, Table = sourceTable });

        var sourceDatabaseType = _databaseFactory.GetDatabaseType(sourceConnectionString);
        var targetDatabaseType = _databaseFactory.GetDatabaseType(targetConnectionString);

        var sourceDatabase = new DatabaseService(_databaseFactory, sourceDatabaseType);
        var targetDatabase = new DatabaseService(_databaseFactory, targetDatabaseType);

        int intProgress = 0;

        using (var targetConnection = targetDatabase.CreateDbConnection(targetConnectionString))
        {
            var targetRowCount = GetRowCount(targetTable, targetConnection);
            if (targetRowCount != 0)
            {
                progress?.Report(new TableProgress() { ProgressPercentage = 100, Table = sourceTable });
                return;
            }
        }

        bool blnContainsIdentity = false;

        var sourceMatchedColumns = this.GetMatchedColumns(sourceTable.Columns, targetTable.Columns);
        var targetMatchedColumns = this.GetMatchedColumns(targetTable.Columns, sourceTable.Columns);

        using (System.Data.Common.DbConnection sourceConnection = sourceDatabase.CreateDbConnection(sourceConnectionString))
        {

            connectionCreatedCallback?.Invoke(sourceConnection);

            using (System.Data.Common.DbCommand sourceCommand = sourceDatabase.CreateDbCommand(sourceConnection))
            {
                this.SetReadTimeout(sourceDatabaseType, sourceCommand);

                int? intSourceRowCount = null;

                if (progress != null)
                {
                    intSourceRowCount = this.GetRowCount(sourceConnection, sourceTable.SchemaName, sourceTable.TableName, sourceDatabaseType);
                }

                if (!intSourceRowCount.HasValue || intSourceRowCount.Value > 0)
                {

                    System.Text.StringBuilder sbSelectColumns = new System.Text.StringBuilder();

                    foreach (var sourceColumn in sourceMatchedColumns)
                    {
                        if (sbSelectColumns.Length > 0)
                        {
                            sbSelectColumns.Append(", ");
                        }
                        sbSelectColumns.AppendFormat("[{0}]", sourceColumn.ColumnName);
                    }

                    sourceCommand.CommandText = this.FormatCommandText($"SELECT {sbSelectColumns.ToString()} FROM [{sourceTable.SchemaName}].[{sourceTable.TableName}]", sourceDatabaseType);

                    using (System.Data.Common.DbDataReader sourceReader = sourceCommand.ExecuteReader())
                    {

                        var converterReader = new TableConverterReader(_databaseFactory, sourceReader, sourceMatchedColumns, targetMatchedColumns, trimStrings, sourceDatabaseType, targetDatabaseType);

                        var intFieldCount = converterReader.FieldCount;

                        using (System.Data.Common.DbConnection targetConnection = targetDatabase.CreateDbConnection(targetConnectionString))
                        {
                            using (var targetCommand = targetDatabase.CreateDbCommand(targetConnection))
                            {

                                System.Text.StringBuilder sbColumns = new System.Text.StringBuilder();
                                System.Text.StringBuilder sbParamaters = new System.Text.StringBuilder();

                                foreach (Cornerstone.Database.Models.ColumnModel targetColumn in targetMatchedColumns)
                                {
                                    if (targetColumn.IsIdentity)
                                    {
                                        blnContainsIdentity = true;
                                    }

                                    if (sbColumns.Length > 0)
                                    {
                                        sbColumns.Append(", ");
                                    }
                                    sbColumns.AppendFormat("[{0}]", targetColumn.ColumnName);
                                    if (sbParamaters.Length > 0)
                                    {
                                        sbParamaters.Append(", ");
                                    }

                                    System.Data.Common.DbParameter paramater = targetDatabase.DatabaseProvider.CreateProvider().CreateParameter();
                                    paramater.ParameterName = string.Concat("@", this.GetParameterNameFromColumn(targetColumn.ColumnName));

                                    paramater.DbType = targetColumn.DbType;

                                    targetDatabase.DatabaseProvider.UpdateParameter(paramater, targetColumn);

                                    sbParamaters.Append(paramater.ParameterName);

                                    targetCommand.Parameters.Add(paramater);

                                }

                                targetCommand.CommandText = this.FormatCommandText(string.Format("INSERT INTO [{0}].[{1}] ({2}) VALUES ({3})", targetTable.SchemaName, targetTable.TableName, sbColumns.ToString(), sbParamaters.ToString()), targetDatabaseType);

                                if (blnContainsIdentity)
                                {
                                    targetCommand.CommandText = this.FormatCommandText(string.Format("SET IDENTITY_INSERT [{0}].[{1}] ON;" + Environment.NewLine + targetCommand.CommandText + Environment.NewLine + "SET IDENTITY_INSERT [{0}].[{1}] OFF;", targetTable.SchemaName, targetTable.TableName), targetDatabaseType);
                                }

                                int rowIndex = 0;

                                while (converterReader.Read())
                                {

                                    for (int intIndex = 0; intIndex < targetCommand.Parameters.Count; intIndex++)
                                    {
                                        var parameter = targetCommand.Parameters[intIndex];
                                        parameter.Value = converterReader.GetValue(intIndex);
                                    }

                                    rowIndex += 1;

                                    int intNewProgress = intProgress;

                                    if (intSourceRowCount.HasValue)
                                    {
                                        intNewProgress = System.Convert.ToInt32(rowIndex / (double)intSourceRowCount.Value * 100);
                                    }

                                    try
                                    {
                                        targetCommand.ExecuteNonQuery();

                                        if (progress != null)
                                        {
                                            if (intProgress != intNewProgress &&
                                                intNewProgress != 100)
                                            {
                                                intProgress = intNewProgress;
                                                progress.Report(new TableProgress() { ProgressPercentage = intNewProgress, Table = sourceTable });
                                            }
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        string strRowErrorMessage = this.GetRowErrorMessage(targetCommand, targetMatchedColumns, targetCommand.Parameters.Count - 1, ex);

                                        string strErrorMessage = string.Format("could not insert row on table: {0} at row: {1}", sourceTable.TableName, strRowErrorMessage);

                                        throw new TableConverterException(sourceTable, strErrorMessage, ex);
                                    }

                                    intProgress = intNewProgress;

                                }
                            }
                        }
                    }

                }
            }
        }

        if (progress != null &&
            intProgress != 100)
        {
            progress.Report(new TableProgress() { ProgressPercentage = 100, Table = sourceTable });
        }

    }

    public IList<Models.ColumnModel> GetMatchedColumns(IList<Models.ColumnModel> sourceColumns, IList<Models.ColumnModel> targetColumns)
    {
        var list = new List<Models.ColumnModel>();

        foreach (Cornerstone.Database.Models.ColumnModel sourceColumn in sourceColumns)
        {
            string strColumnName = sourceColumn.ColumnName;
            Cornerstone.Database.Models.ColumnModel targetColumn = (
                from c in targetColumns
                where c.ColumnName.Equals(strColumnName, StringComparison.InvariantCultureIgnoreCase)
                select c).FirstOrDefault();

            if (targetColumn != null && !targetColumn.IsComputed)
            {

                list.Add(sourceColumn);
            }
        }

        return list;
    }

    private void SetReadTimeout(Models.DatabaseType databaseType, System.Data.Common.DbCommand sourceCommand)
    {
        var databaseProvider = _databaseFactory.GetDatabaseProvider(databaseType);
        databaseProvider?.SetReadTimeout(sourceCommand);
    }

    private string FormatCommandText(string commandText, Cornerstone.Database.Models.DatabaseType databaseType)
    {
        switch (databaseType)
        {
            case Models.DatabaseType.Odbc:
                commandText = commandText.Replace("[", "\"").Replace("]", "\"");
                break;
            case Models.DatabaseType.OLE:
                commandText = commandText.Replace("[", "\"").Replace("]", "\"");
                break;
            case Models.DatabaseType.AccessOLE:
                //Do nothing, access likes brackets
                break;
            case Models.DatabaseType.MySql:
                commandText = commandText.Replace("[", "`").Replace("]", "`");
                break;
        }
        return commandText;
    }

    public int GetRowCount(Models.TableModel table, System.Data.Common.DbConnection connection)
    {
        var databaseType = _databaseFactory.GetDatabaseType(connection);
        return this.GetRowCount(connection, table.SchemaName, table.TableName, databaseType);
    }

    private int GetRowCount(System.Data.Common.DbConnection connection, string schemaName, string tableName, Cornerstone.Database.Models.DatabaseType databaseType)
    {
        int rowCount = 0;

        var database = new DatabaseService(_databaseFactory, databaseType);

        using (var command = database.CreateDbCommand(connection))
        {
            try
            {
                if (databaseType == Cornerstone.Database.Models.DatabaseType.MicrosoftSQLServer)
                {
                    command.CommandText = string.Format("(SELECT sys.sysindexes.rows FROM sys.tables INNER JOIN sys.sysindexes ON sys.tables.object_id = sys.sysindexes.id AND sys.sysindexes.indid < 2 WHERE sys.tables.name = '{0}')", tableName);
                    rowCount = System.Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch
            {

            }

            try
            {
                if (rowCount == 0)
                {
                    command.CommandText = this.FormatCommandText(string.Format("SELECT COUNT(1) FROM [{0}].[{1}]", schemaName, tableName), databaseType);
                    rowCount = System.Convert.ToInt32(command.ExecuteScalar());
                }
            }
            catch
            {

            }
        }

        return rowCount;
    }

    private string GetParameterNameFromColumn(string columnName)
    {
        return columnName.Replace("-", "").Replace(" ", "");
    }

    private string GetRowErrorMessage(System.Data.Common.DbCommand command, IList<Cornerstone.Database.Models.ColumnModel> columns, int columnIndex, System.Exception ex)
    {
        System.Text.StringBuilder sbRow = new System.Text.StringBuilder();

        for (int intErrorIndex = 0; intErrorIndex < columnIndex; intErrorIndex++)
        {
            Cornerstone.Database.Models.ColumnModel targetColumn = columns[intErrorIndex];
            if (targetColumn != null)
            {
                if (!targetColumn.IsNullable)
                {
                    string strColumnName = targetColumn.ColumnName;
                    object objTargetValue = command.Parameters[intErrorIndex].Value;
                    if (objTargetValue == null || objTargetValue == DBNull.Value)
                    {
                        objTargetValue = "NULL";
                    }
                    if (sbRow.Length > 0)
                    {
                        sbRow.Append(" AND ");
                    }
                    sbRow.AppendFormat(" {0} = '{1}'", strColumnName, System.Convert.ToString(objTargetValue));
                }
            }
        }
        sbRow.AppendLine();

        sbRow.AppendLine("ERROR:");
        sbRow.AppendLine(ex.Message);

        return sbRow.ToString();
    }

}
