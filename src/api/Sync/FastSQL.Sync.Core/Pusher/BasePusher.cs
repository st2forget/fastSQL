﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FastSQL.Core;
using FastSQL.Sync.Core.Enums;
using FastSQL.Sync.Core.Models;
using FastSQL.Sync.Core.Repositories;

namespace FastSQL.Sync.Core.Pusher
{
    public abstract class BasePusher : IPusher
    {
        protected readonly IOptionManager OptionManager;
        protected readonly IRichAdapter Adapter;
        protected readonly IRichProvider Provider;
        public ConnectionRepository ConnectionRepository { get; set; }
        protected Action<string> _reporter;
        protected IndexItemModel IndexedItem;
        protected ConnectionModel ConnectionModel;
        public virtual EntityRepository EntityRepository { get; set; }
        public virtual AttributeRepository AttributeRepository { get; set; }

        public BasePusher(IOptionManager optionManager, IRichAdapter adapter, IRichProvider provider)
        {
            OptionManager = optionManager;
            this.Adapter = adapter;
            this.Provider = provider;
        }

        public virtual IEnumerable<OptionItem> Options => OptionManager.Options;

        public virtual IEnumerable<OptionItem> GetOptionsTemplate()
        {
            return OptionManager.GetOptionsTemplate();
        }

        public IPusher OnReport(Action<string> reporter)
        {
            _reporter = reporter;
            return this;
        }

        public IPusher SetItem(IndexItemModel item)
        {
            IndexedItem = item;
            return this;
        }

        public abstract IIndexModel GetIndexModel();

        public IOptionManager SetOptions(IEnumerable<OptionItem> options)
        {
            return OptionManager.SetOptions(options);
        }

        public abstract PushState Create(out string destinationId);
        public abstract string GetDestinationId();
        public abstract PushState Remove(string destinationId = null);
        public abstract PushState Update(string destinationId = null);
        public abstract IPusher SetIndex(IIndexModel model);

        protected virtual IPusher SpreadOptions()
        {
            var indexModel = GetIndexModel();
            ConnectionModel = ConnectionRepository.GetById(indexModel.DestinationConnectionId.ToString());
            var connectionOptions = ConnectionRepository.LoadOptions(ConnectionModel.Id.ToString());
            var connectionOptionItems = connectionOptions.Select(c => new OptionItem { Name = c.Key, Value = c.Value });
            Adapter.SetOptions(connectionOptionItems);
            Provider.SetOptions(connectionOptionItems);
            return this;
        }

        protected virtual Dictionary<string, string> GetNormalizedValuesByDependencies(IndexItemModel indexedItem = null)
        {
            var indexedModel = GetIndexModel();
            indexedItem = indexedItem ?? IndexedItem;
            var dependencies = EntityRepository.GetDependencies(indexedModel.Id, indexedModel.EntityType);
            var normalizedValues = new Dictionary<string, string>();
            foreach (var d in dependencies)
            {
                var foreignKeys = d.ForeignKeysArr;
                var referenceKeys = d.ReferenceKeysArr;
                IIndexModel dependsOnModel = null;
                if (d.TargetEntityType == EntityType.Attribute)
                {
                    dependsOnModel = AttributeRepository.GetById(d.TargetEntityId.ToString());
                }
                else
                {
                    dependsOnModel = EntityRepository.GetById(d.TargetEntityId.ToString());
                }
                var hasDependencies = EntityRepository.GetDependsOnItem(dependsOnModel.ValueTableName, d, indexedItem, out IndexItemModel referencedItem);
                if (!hasDependencies)
                {
                    return normalizedValues;
                }
                for (var i = 0; i < foreignKeys.Length; i++)
                {
                    var foreignKey = foreignKeys[i];
                    var referenceKey = referenceKeys[i];

                    if (!IndexedItem.Properties().Select(p => p.Name).Contains(foreignKey))
                    {
                        continue;
                    }
                    if (normalizedValues.ContainsKey(foreignKey))
                    {
                        continue;
                    }

                    normalizedValues.Add(foreignKey, referencedItem?.Value<string>(referenceKey));
                }
            }
            return normalizedValues;
        }
    }

    public abstract class BaseEntityPusher : BasePusher, IEntityPusher
    {
        protected readonly IProcessor Processor;
        protected EntityModel EntityModel;

        public BaseEntityPusher(
            IOptionManager optionManager,
            IProcessor processor,
            IRichProvider provider,
            IRichAdapter adapter) : base(optionManager, adapter, provider)
        {
            Processor = processor;
        }

        public bool IsImplemented(string processorId, string providerId)
        {
            return Processor.Id == processorId && Provider.Id == providerId;
        }
        
        public override IPusher SetIndex(IIndexModel model)
        {
            EntityModel = model as EntityModel;
            SpreadOptions();
            return this;
        }

        public override IIndexModel GetIndexModel()
        {
            return EntityModel;
        }
    }

    public abstract class BaseAttributePusher : BasePusher, IAttributePusher
    {
        protected readonly IProcessor EntityProcessor;
        protected readonly IProcessor AttributeProcessor;
        protected EntityModel EntityModel;
        protected AttributeModel AttributeModel;

        public BaseAttributePusher(
            IOptionManager optionManager,
            IProcessor entityProcessor,
            IProcessor attributeProcessor,
            IRichProvider provider,
            IRichAdapter adapter) : base(optionManager, adapter, provider)
        {
            this.EntityProcessor = entityProcessor;
            this.AttributeProcessor = attributeProcessor;
        }

        public bool IsImplemented(string attributeProcessorId, string entityProcessorId, string providerId)
        {
            return AttributeProcessor.Id == attributeProcessorId && EntityProcessor.Id == entityProcessorId && Provider.Id == providerId;
        }

        public override IPusher SetIndex(IIndexModel model)
        {
            AttributeModel = model as AttributeModel;
            EntityModel = EntityRepository.GetById(AttributeModel.EntityId.ToString());
            SpreadOptions();
            return this;
        }

        public override IIndexModel GetIndexModel()
        {
            return AttributeModel;
        }

        protected IIndexModel GetEntityModel()
        {
            return EntityModel;
        }
    }
}
