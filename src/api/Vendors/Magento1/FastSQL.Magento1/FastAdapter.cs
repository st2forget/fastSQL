﻿using FastSQL.Core;
using System;
using System.Data;

namespace FastSQL.Magento1
{
    public class FastAdapter : BaseAdapter
    {
        //protected FastAdapter(IRichProvider provider) : base(provider)
        //{
        //}

        private readonly Magento1Soap api;

        public FastAdapter(FastProvider provider, Magento1Soap api) : base(provider)
        {
            this.api = api;
        }

        public override bool TryConnect(out string message)
        {
            IDbConnection conn = null;
            try
            {
                message = "Connected.";
                api.SetOptions(Options);
                var task = api.Connect();
                task.Wait();
                return true;
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
    }
}