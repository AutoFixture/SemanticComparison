﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ploeh.TestTypeFoundation
{
    public interface IInterfaceWithVoidMethod
    {
        void VoidMethod();
        string SetOnlyProperty { set; }
    }
}
