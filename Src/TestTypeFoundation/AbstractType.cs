﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ploeh.TestTypeFoundation
{
    public abstract class AbstractType
    {
        protected AbstractType()
        {
        }

        public object Property1 { get; set; }

        public object Property2 { get; set; }

        public object Property3 { get; set; }

        public object Property4 { get; set; }
    }
}
