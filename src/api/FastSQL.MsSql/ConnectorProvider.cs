﻿using FastSQL.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace FastSQL.MsSql
{
    public class ConnectorProvider : IConnectorProvider
    {
        private readonly ConnectorOptions _options;
        private readonly ConnectorAdapter _adapter;

        public string Id => "1064aecb081027138f91e1e7e401a99239f89362";
        public string Name => "MsSql";

        public string DisplayName => "Microsoft SQL Server";

        public string Description => "Microsoft SQL Server";

        private IEnumerable<OptionItem> _selfOptions;
        public IEnumerable<OptionItem> Options => _selfOptions ?? _options?.GetOptions() ?? new List<OptionItem>();

        public ConnectorProvider(ConnectorOptions options, ConnectorAdapter adapter)
        {
            _options = options;
            _adapter = adapter;
        }

        public bool TryConnect(out string message)
        {
            IDbConnection conn = null;
            try
            {
                message = "Connected.";
                var connBuilder = new ConnectionStringBuilder(_selfOptions);
                using (conn = new SqlConnection(connBuilder.Build()))
                {
                    conn.Open();
                    return true;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
            finally
            {
                conn?.Dispose();
            }
        }

        public IConnectorProvider SetOptions(IEnumerable<OptionItem> options)
        {
            _selfOptions = options;
            return this;
        }

        public IEnumerable<QueryResult> Query(string rawSQL, object @params = null)
        {
            return _adapter
                .SetOptions(_selfOptions)
                .Query(rawSQL, @params);
        }

        public int Execute(string rawQuery, object @params = null)
        {
            return _adapter
                .SetOptions(_selfOptions)
                .Execute(rawQuery, @params);
        }
    }
}
