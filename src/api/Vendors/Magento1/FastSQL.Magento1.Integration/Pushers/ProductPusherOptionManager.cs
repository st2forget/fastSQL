﻿using FastSQL.Core;
using FastSQL.Sync.Core.Models;
using FastSQL.Sync.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace FastSQL.Magento1.Integration.Pushers
{
    public class ProductPusherOptionManager : BaseOptionManager
    {
        public ResolverFactory ResolverFactory { get; set; }

        public ProductPusherOptionManager()
        {
        }
        public override IEnumerable<OptionItem> GetOptionsTemplate()
        {
            using (var connectionRepository = ResolverFactory.Resolve<ConnectionRepository>())
            using (var entityRepository = ResolverFactory.Resolve<EntityRepository>())
            using (var attributeRepository = ResolverFactory.Resolve<AttributeRepository>())
            {
                var entities = entityRepository.GetAll();
                return new List<OptionItem> {
                new OptionItem
                {
                    Name = "website_ids",
                    DisplayName = "@Website Ids",
                    Type = OptionType.Text,
                    Value = "1"
                },
                new OptionItem
                {
                    Name = "store_ids",
                    DisplayName = "@Store Ids",
                    Type = OptionType.Text,
                    Value = "0"
                }
            };
            }
        }

        public override void Dispose()
        {
            //
        }
    }
}
