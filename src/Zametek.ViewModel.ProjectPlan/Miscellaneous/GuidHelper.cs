using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class GuidHelper
    {
        private const int c_ShortSizeLimit = 8;

        public static string ToShortString(this Guid guid)
        {
            return guid.ToFlatString()[..c_ShortSizeLimit];
        }
    }
}
