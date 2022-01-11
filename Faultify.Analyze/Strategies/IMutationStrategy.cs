using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Faultify.Analyze.Strategies
{
    public interface IMutationStrategy
    {
        void Mutate();

        string GetStrategyStringForReport();
    }
}
