﻿using FastSQL.Sync.Core;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FastSQL.API.Controllers
{
    [Route("api/[controller]")]
    public class ProcessorsController: Controller
    {
        private readonly IEnumerable<IProcessor> processors;

        public ProcessorsController(IEnumerable<IProcessor> processors)
        {
            this.processors = processors;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(processors);
        }
    }
}
