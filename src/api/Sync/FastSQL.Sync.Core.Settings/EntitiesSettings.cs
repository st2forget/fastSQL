﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using FastSQL.Core;
using FastSQL.MsSql;
using FastSQL.Sync.Core.Indexer;
using FastSQL.Sync.Core.Puller;
using FastSQL.Sync.Core.Repositories;
using FastSQL.Sync.Core.Settings.Events;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Prism.Events;
using Serilog;
using st2forget.migrations;

namespace FastSQL.Sync.Core.Settings
{
    public class EntitiesSettingsProvider : BaseSettingProvider
    {
        private readonly IEnumerable<IEntityPuller> pullers;
        private readonly IEnumerable<IEntityIndexer> indexers;

        private ILogger _logger;
        protected ILogger Logger => _logger ?? (_logger = ResolverFactory.Resolve<ILogger>("SyncService"));
        private ILogger _errorLogger;
        protected ILogger ErrorLogger => _logger ?? (_errorLogger = ResolverFactory.Resolve<ILogger>("Error"));

        public override string Id => "wif@3402947offie#$jkfjie+_3i22425";

        public override string Name => "Entities Settings";

        public override string Description => "Optional actions related to entities";

        public override bool Optional => true;

        public override IEnumerable<string> Commands
        {
            get
            {
                var result = new List<string> { "Init All Entities", "Update All Entities" };
                return result;
            }
        }

        public EntitiesSettingsProvider(
            EntitiesSettingsOptionManager optionManager,
            IEnumerable<IEntityPuller> pullers,
            IEnumerable<IEntityIndexer> indexers) : base(optionManager)
        {
            this.pullers = pullers;
            this.indexers = indexers;
        }

        public override ISettingProvider Save()
        {
            return this;
        }

        public override async Task<bool> Validate()
        {
            try
            {
                var ok = true;
                //IsLoading = true;
                await Task.Run(() =>
                {
                    using (var connectionRepository = ResolverFactory.Resolve<ConnectionRepository>())
                    using (var entityRepository = ResolverFactory.Resolve<EntityRepository>())
                    using (var attributeRepository = ResolverFactory.Resolve<AttributeRepository>())
                    {
                        var allEntities = entityRepository.GetAll();
                        foreach (var entity in allEntities)
                        {
                            var initialized = true;

                            var options = entityRepository.LoadOptions(entity.Id.ToString());
                            var connection = connectionRepository.GetById(entity.SourceConnectionId.ToString());
                            var puller = pullers.FirstOrDefault(p => p.IsImplemented(entity.SourceProcessorId, connection.ProviderId));
                            var indexer = indexers.FirstOrDefault(p => p.IsImplemented(entity.SourceProcessorId, connection.ProviderId));
                            puller.SetIndex(entity);
                            puller.SetOptions(options.Select(o => new OptionItem { Name = o.Key, Value = o.Value }));
                            initialized = initialized && puller.Initialized();
                            indexer.SetIndex(entity);
                            indexer.SetOptions(options.Select(o => new OptionItem { Name = o.Key, Value = o.Value }));
                            initialized = initialized && entityRepository.Initialized(entity);
                            ok = ok && initialized;
                            if (!ok)
                            {
                                Logger.Information($@"Index ""{entity.Name}"" is not initialized.");
                                break;
                            }
                        }
                    }
                });
                Message = "All entities has been initialized.";
                Logger.Information(Message);

                return ok;
            }
            catch (Exception ex)
            {
                ErrorLogger.Error(ex, ex.Message);
                throw;
            }
            finally
            {
                //IsLoading = false;
            }
        }

        public override async Task<bool> InvokeChildCommand(string commandName)
        {
            switch (commandName.ToLower())
            {
                case "init all entities":
                    return await InitAllEntities();
                case "update all entities":
                    return await UpdateAllEntities();
            }
            Message = "Command is not available.";
            return false;
        }

        private async Task<bool> UpdateAllEntities()
        {
            try
            {
                //IsLoading = true;
                await Task.Run(async () =>
                {
                    using (var entityRepository = ResolverFactory.Resolve<EntityRepository>())
                    using (var indexerManager = ResolverFactory.Resolve<IndexerManager>())
                    {
                        indexerManager.OnReport(s => Logger.Information(s));
                        var allEntities = entityRepository.GetAll();
                        foreach (var entity in allEntities)
                        {
                            indexerManager.SetIndex(entity);
                            await indexerManager.PullAll(true);
                        }
                    }
                });
                Message = "All entities has been updated.";
                Logger.Information(Message);

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.Error(ex, ex.Message);
                throw;
            }
            finally
            {
                //IsLoading = false;
            }
        }

        private async Task<bool> InitAllEntities()
        {
            try
            {
                //IsLoading = true;
                await Task.Run(async () =>
                {
                    using (var entityRepository = ResolverFactory.Resolve<EntityRepository>())
                    using (var indexerManager = ResolverFactory.Resolve<IndexerManager>())
                    {
                        indexerManager.OnReport(s => Logger.Information(s));
                        var allEntities = entityRepository.GetAll();
                        foreach (var entity in allEntities)
                        {
                            await indexerManager.Init();
                            await indexerManager.PullAll(true);
                        }
                    }
                });
                Message = "All entities has been initialized.";
                Logger.Information(Message);

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogger.Error(ex, ex.Message);
                throw;
            }
            finally
            {
                //IsLoading = false;
            }
        }

        public override ISettingProvider LoadOptions()
        {
            return this;
        }
    }
}
