﻿using FastSQL.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FastSQL.API.ViewModels
{
    public class CreateEntityViewModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string SourceProcessorId { get; set; }
        public string DestinationProcessorId { get; set; }
        public bool Enabled { get; set; }
        public Guid SourceConnectionId { get; set; }
        public Guid DestinationConnectionId { get; set; }
        public IEnumerable<OptionItem> Options { get; set; }
    }
}
