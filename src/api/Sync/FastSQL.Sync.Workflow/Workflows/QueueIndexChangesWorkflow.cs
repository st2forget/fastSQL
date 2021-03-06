﻿using FastSQL.Core;
using FastSQL.Sync.Core.Enums;
using FastSQL.Sync.Core.Indexer;
using FastSQL.Sync.Core.Models;
using FastSQL.Sync.Core.Repositories;
using FastSQL.Sync.Core.Workflows;
using FastSQL.Sync.Workflow.Models;
using FastSQL.Sync.Workflow.Steps;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using WorkflowCore.Interface;
using WorkflowCore.Primitives;

namespace FastSQL.Sync.Workflow.Workflows
{
    [Description("Queue Index Changes")]
    public class QueueIndexChangesWorkflow : BaseWorkflow<GeneralMessage>
    {
        private ILogger _logger;
        protected ILogger Logger => _logger ?? (_logger = ResolverFactory.Resolve<ILogger>("Workflow"));
        private ILogger _errorLogger;
        protected ILogger ErrorLogger => _logger ?? (_errorLogger = ResolverFactory.Resolve<ILogger>("Error"));
        public override string Id => nameof(QueueIndexChangesWorkflow);

        public override int Version => 1;
        public override bool IsGeneric => true;

        public QueueIndexChangesWorkflow()
        {
        }

        public override void Build(IWorkflowBuilder<GeneralMessage> builder)
        {
            builder.StartWith(x => { })
               .While(d => true)
               .Do(x =>
               {
                   x.StartWith<FilterRunningEntitiesStep>()
                    .Input(s => s.WorkflowId, s => Id)
                    .Output(d => d.Indexes, d => d.Indexes)
                    .Output(d => d.Counter, d => 0)
                    .If(s => s.Indexes == null || s.Indexes.Count() <= 0)
                    .Do(i => i.StartWith<Delay>(d => TimeSpan.FromMinutes(10)))
                    .If(s => s.Indexes != null && s.Indexes.Count() > 0)
                    .Do(i =>
                    {
                        i
                            .StartWith(ii => { })
                            .While(w => w.Counter < w.Indexes.Count())
                            .Do(dd => dd.StartWith<QueueIndexChangesStep>()
                                .Input(u => u.IndexModel, g => g.Indexes.ElementAt(g.Counter))
                                .Output(s => s.Counter, u => u.Counter))
                            .Then<Delay>(d => TimeSpan.FromSeconds(2));
                    });
               });
        }
    }
}
