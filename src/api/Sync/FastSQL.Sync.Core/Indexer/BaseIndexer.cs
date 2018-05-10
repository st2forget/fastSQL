﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using Dapper;
using FastSQL.Core;
using FastSQL.Sync.Core.Enums;
using FastSQL.Sync.Core.ExtensionMethods;
using FastSQL.Sync.Core.Models;
using FastSQL.Sync.Core.Repositories;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using st2forget.commons.datetime;

namespace FastSQL.Sync.Core
{
    public abstract class BaseIndexer : IIndexer
    {
        protected readonly IOptionManager OptionManager;
        public DbConnection Connection { get; set; }
        protected Action<string> Reporter;
        protected DbTransaction Transaction;

        public BaseIndexer(IOptionManager optionManager)
        {
            OptionManager = optionManager;
        }

        public virtual IEnumerable<OptionItem> Options => OptionManager.Options;

        protected abstract IIndexModel GetIndexer();
        protected abstract BaseRepository GetRepository();
        
        public virtual IEnumerable<OptionItem> GetOptionsTemplate()
        {
            return OptionManager.GetOptionsTemplate();
        }

        public void OnReport(Action<string> reporter)
        {
            Reporter = reporter;
        }

        public virtual void StartIndexing(bool cleanAll)
        {
            var indexer = GetIndexer();
            if (cleanAll)
            {
                Report($@"Initializing index ""{indexer.Name}""....");
            } else
            {
                Report($@"Updating index ""{indexer.Name}""....");
            }
            Report($"Cleaning up table {indexer.NewValueTableName}");
            Connection.Execute($@"TRUNCATE TABLE [{indexer.NewValueTableName}];", transaction: Transaction);
            if (cleanAll)
            {
                Report($"Cleaning up table {indexer.ValueTableName}");
                Connection.Execute($@"TRUNCATE TABLE [{indexer.ValueTableName}];", transaction: Transaction);
                Report($"Cleaning up table {indexer.OldValueTableName}");
                Connection.Execute($@"TRUNCATE TABLE [{indexer.OldValueTableName}];", transaction: Transaction);
            }
        }

        private IEnumerable<string> FilterColumns(string columnStr)
        {
            var resultStr = columnStr ?? string.Empty;
            resultStr = Regex.Replace(resultStr, @"[|;,]", ",", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            resultStr = Regex.Replace(resultStr, @":[a-zA-Z0-9\(\)]+", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);//
            return resultStr.Split(',')
                .Select(s => s?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s));
        }

        public virtual void Persist(IEnumerable<object> data = null)
        {
            if (data == null || data.Count() <= 0)
            {
                return;
            }
            var indexer = GetIndexer();
            var repo = GetRepository();
            var options = repo.LoadOptions(indexer.Id.ToString());
            var idColumn = FilterColumns(options.GetValue("indexer_key_column")).First();
            var primaryColumns = FilterColumns(options.GetValue("indexer_primary_key_columns"));
            var valueColumns = FilterColumns(options.GetValue("indexer_value_columns"));

            var allColumns = primaryColumns.Concat(valueColumns).ToList();
            // The Id column must be hard coded
            var insertData = data?.Select(d =>
            {
                var jData = JObject.FromObject(d);
                var r = new DynamicParameters();
                r.Add("Id", jData.GetValue(idColumn).ToString());
                foreach (var c in allColumns)
                {
                    r.Add(c, jData.GetValue(c).ToString());
                }
                return r;
            }).ToList();
            
            // Persist the data
            Insert(idColumn, primaryColumns, valueColumns, allColumns, insertData);

            // Check items that are created
            CheckNewItems(idColumn, primaryColumns, valueColumns, allColumns, insertData);

            // Check items that are updated
            CheckChangedItems(idColumn, primaryColumns, valueColumns, allColumns, insertData);
        }


        /**
         * 
         * 
         */
        protected virtual void Insert(string idColumn,
            IEnumerable<string> primaryColumns,
            IEnumerable<string> valueColumns,
            IEnumerable<string> allColumns,
            IEnumerable<DynamicParameters> data)
        {
            var schemaSQL = string.Join(",\n\t", allColumns.Select(c => $"[{c}]"));
            var paramsSQL = string.Join(",\n\t", allColumns.Select(c => $"@{c}"));
            var indexer = GetIndexer();
            var sql = $@"
INSERT INTO [{indexer.NewValueTableName}](
    [Id],
    {schemaSQL})
VALUES (
    @Id,
    {paramsSQL})
";
            foreach(var d in data) // don't worry about performance
            {
                Connection.Execute(sql, param: d, transaction: Transaction);
            }
            Report($"Inserted {data?.Count() ?? 0} to {indexer.NewValueTableName}");
        }

        /**
         * Items that are in NEW table 
         * but not in OLD table are 
         * marked as CREATED
         */
        protected virtual void CheckNewItems(string idColumn,
            IEnumerable<string> primaryColumns,
            IEnumerable<string> valueColumns,
            IEnumerable<string> allColumns,
            IEnumerable<DynamicParameters> data)
        {
            var indexer = GetIndexer();
            var comparePrimarySQL = $@"o.[Id] = n.[Id]";
            if (primaryColumns?.Count() > 0)
            {
                comparePrimarySQL = string.Join(" AND ", primaryColumns.Select(c => $@"o.[{c}] = n.[{c}]"));
            }

            var newItemsSQL = $@"
SELECT n.*
FROM (VALUES (@Id, {string.Join(", ", allColumns.Select(a => $"@{a}"))})) AS n([Id], {string.Join(", ", allColumns.Select(a => $"[{a}]"))})
LEFT JOIN [{indexer.OldValueTableName}] AS o ON {comparePrimarySQL}
WHERE o.[Id] IS NULL
ORDER BY n.[Id]
";

            var mergeCondition = $@"s.[Id] = t.[Id]";
            if (primaryColumns?.Count() > 0)
            {
                mergeCondition = string.Join(" AND ", primaryColumns.Select(c => $@"s.[{c}] = t.[{c}]"));
            }
            var mergeSQL = $@"
MERGE INTO [{indexer.ValueTableName}] as t
USING (
    VALUES (@Id, {string.Join(", ", allColumns.Select(a => $"@{a}"))})
)
AS s([Id], {string.Join(",\n", allColumns.Select(c => $"[{c}]"))})
ON {mergeCondition}
WHEN NOT MATCHED THEN
	INSERT (
        [SourceId],
        [DestinationId],
        [State],
        [LastUpdated],
        {string.Join(",\n", allColumns.Select(c=> $"[{c}]"))}
    )
    VALUES(
        s.[Id],
        NULL,
        @State,
        @LastUpdated,
        {string.Join(",\n", allColumns.Select(c => $"s.[{c}]"))}
    );
";
            var affectedRows = 0;
            foreach (var d in data)
            {
                var newItems = Connection
                    .Query(newItemsSQL, param: d, transaction: Transaction)
                    .ToList();
                if (newItems.Count <= 0)
                {
                    break;
                }

                var extParam = new DynamicParameters();
                foreach(var key in d.ParameterNames)
                {
                    extParam.Add(key, d.Get<object>(key));
                }
                extParam.Add("State", ItemState.Changed);
                extParam.Add("LastUpdated", DateTime.Now.ToUnixTimestamp());
                affectedRows += Connection.Execute(mergeSQL, extParam, transaction: Transaction);
            }
            Report($@"Checked {data?.Count() ?? 0} item(s), found {affectedRows} new item(s)");
        }

        /**
         * Items that are both in NEW & OLD tables 
         * but with different lookup values (not primary keys) 
         * are marked as UPDATED
         */
        protected virtual void CheckChangedItems(string idColumn,
            IEnumerable<string> primaryColumns,
            IEnumerable<string> valueColumns,
            IEnumerable<string> allColumns,
            IEnumerable<DynamicParameters> data)
        {
            if (valueColumns == null || valueColumns.Count() <= 0)
            {
                // No value columns means no different check
                return;
            }

            var indexer = GetIndexer();
            string comparePrimarySQL = $@"o.[Id] = n.[Id]";
            if (primaryColumns?.Count() > 0)
            {
                comparePrimarySQL = string.Join(" AND ", primaryColumns.Select(c => $@"o.[{c}] = n.[{c}]"));
            }
            var compareValueSQL = string.Join(" AND ", valueColumns.Select(c => $@"o.[{c}] <> n.[{c}]"));
            var changedItemsSQL = $@"
SELECT n.*
FROM (VALUES (@Id, {string.Join(", ", allColumns.Select(a => $"@{a}"))})) AS n([Id], {string.Join(", ", allColumns.Select(a => $"[{a}]"))})
INNER JOIN [{indexer.OldValueTableName}] AS o ON {comparePrimarySQL}
WHERE o.[Id] IS NOT NULL AND {compareValueSQL}
ORDER BY n.[Id];
";

            var mergeCondition = $@"s.[Id] = t.[Id]";
            if (primaryColumns?.Count() > 0)
            {
                mergeCondition = string.Join(" AND ", primaryColumns.Select(c => $@"s.[{c}] = t.[{c}]"));
            }
            var mergeSQL = $@"
MERGE INTO [{indexer.ValueTableName}] as t
USING (
    VALUES (@Id, {string.Join(", ", allColumns.Select(a => $"@{a}"))})
)
AS s([Id], {string.Join(",\n", allColumns.Select(c => $"[{c}]"))})
ON {mergeCondition}
WHEN MATCHED THEN
    UPDATE SET 
        [State] = CASE  
                WHEN [State] = 0 THEN @State
                WHEN [State] IS NULL THEN @State 
                ELSE ([State] | @State | @StatesToExclude) ^ @StatesToExclude
            END,
        [LastUpdated] = @LastUpdated,
        {string.Join(",\n", valueColumns.Select(c => $"[{c}] = s.[{c}]"))}
";
            var affectedRows = 0;
            foreach (var d in data)
            {
                var changedItems = Connection
                    .Query(changedItemsSQL, param: d, transaction: Transaction)
                    .ToList();
                if (changedItems.Count <= 0)
                {
                    break;
                }

                var extParam = new DynamicParameters();
                foreach (var key in d.ParameterNames)
                {
                    extParam.Add(key, d.Get<object>(key));
                }
                extParam.Add("State", ItemState.Changed);
                extParam.Add("StatesToExclude", ItemState.Removed);
                extParam.Add("LastUpdated", DateTime.Now.ToUnixTimestamp());
                affectedRows += Connection.Execute(mergeSQL, extParam, transaction: Transaction);
            }
            Report($@"Checked {data?.Count() ?? 0} item(s), found {affectedRows} updated item(s)");
        }
        
        /**
         * Items that appeared only in OLD Table are marked as REMOVED
         */
        protected virtual void CheckRemovedItems(string idColumn,
            IEnumerable<string> primaryColumns,
            IEnumerable<string> valueColumns)
        {
            var limit = 100;
            var offset = 0;
            var indexer = GetIndexer();
            string comparePrimarySQL = $@"o.[Id] = n.[Id]";
            if (primaryColumns?.Count() > 0) {
                comparePrimarySQL = string.Join(" AND ", primaryColumns.Select(c => $@"o.[{c}] = n.[{c}]"));
            }
            
            var oldItemsSQL = $@"
SELECT o.*
FROM [{indexer.OldValueTableName}] AS o
LEFT JOIN [{indexer.NewValueTableName}] AS n ON {comparePrimarySQL}
WHERE n.[Id] IS NULL
ORDER BY o.[Id]
OFFSET @Offset ROWS
FETCH NEXT @Limit ROWS ONLY;
";
            var updateRemovedSQL = $@"
UPDATE [{indexer.OldValueTableName}]
SET [State] = CASE  
                WHEN [State] = 0 THEN @State
                WHEN [State] IS NULL THEN @State 
                ELSE [State] | @State
            END,
    [LastUpdated] = @LastUpdated
WHERE [SourceId] IN @SourceIds";
            var affectedRows = 0;
            while (true)
            {
                var oldItems = Connection
                    .Query(oldItemsSQL, new { Limit = limit, Offset = offset }, transaction: Transaction)
                    .Select(i => i.Id).ToList();
                if (oldItems.Count <= 0)
                {
                    break;
                }

                affectedRows += Connection.Execute(updateRemovedSQL, new {
                    State = ItemState.Removed | ItemState.Changed,
                    SourceIds = oldItems,
                    LastUpdated = DateTime.Now.ToUnixTimestamp(),
                }, transaction: Transaction);
                offset += limit;
            }
            Report($@"Found {affectedRows} item(s) that are removed.");
        }
        
        public virtual void EndIndexing()
        {
            var indexer = GetIndexer();
            var repo = GetRepository();
            var options = repo.LoadOptions(indexer.Id.ToString());
            var idColumn = FilterColumns(options.GetValue("indexer_key_column")).First();
            var primaryColumns = FilterColumns(options.GetValue("indexer_primary_key_columns"));
            var valueColumns = FilterColumns(options.GetValue("indexer_value_columns"));

            // Check items that are removed
            CheckRemovedItems(idColumn, primaryColumns, valueColumns);

            // Copy New Values to Old Values
            Connection.Execute($@"TRUNCATE TABLE [{indexer.OldValueTableName}];", transaction: Transaction); // truncate the old table first
            Connection.Execute($@"INSERT INTO [{indexer.OldValueTableName}]
SELECT * FROM [{indexer.NewValueTableName}]", transaction: Transaction); // old & new value table has exactly the same structure
        }

        public void Report(string message)
        {
            Reporter?.Invoke(message);
        }

        public IOptionManager SetOptions(IEnumerable<OptionItem> options)
        {
            return OptionManager.SetOptions(options);
        }

        public void BeginTransaction()
        {
            Transaction = Connection.BeginTransaction();
        }

        public void Commit()
        {
            Transaction?.Commit();
            Transaction?.Dispose();
            Transaction = null;
        }

        public void RollBack()
        {
            Transaction?.Rollback();
            Transaction?.Dispose();
            Transaction = null;
        }
    }
}
