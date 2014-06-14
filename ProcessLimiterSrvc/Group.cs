using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization;

namespace ProcessLimiterSrvc
{
    [Serializable]
    public class Group
    {
        public readonly List<string> ProcessNames = new List<string>();
        public TimeSpan? TimeLimit = null;
        public TimeSpan? StartTime = null, EndTime = null;
    }
}
