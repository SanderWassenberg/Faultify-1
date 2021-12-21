using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Faultify.Analyze.Strategies
{
    public class EmptyListStrategy : IMutationStrategy
    {
        public string GetStrategyStringForReport() => "Emptied the list";

        public void Mutate()
        {
            throw new NotImplementedException();
        }
    }
}
