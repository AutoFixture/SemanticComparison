﻿#if SYSTEM_COMPONENTSETTINGS_DATAERRORINFO
using System.ComponentModel;

namespace SemanticComparisonUnitTest.TestTypes
{
    public class DataErrorInfo : IDataErrorInfo
    {
        public string Error
        {
            get { return string.Empty; }
        }

        public string this[string columnName]
        {
            get { return string.Empty; }
        }
    }
}
#endif