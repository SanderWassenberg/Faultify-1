using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Faultify.Analyze.Mutation;
using Faultify.Analyze.Strategies;
using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze
{
    class ListMutationAnalyzer : IMutationAnalyzer<ListMutation, MethodDefinition>
    {
        public string Description => "Analyzer that searches for possible list mutations.";

        public string Name => "List Analyzer";

        public IEnumerable<ListMutation> AnalyzeMutations(MethodDefinition method, MutationLevel mutationLevel, IDictionary<Instruction, SequencePoint> debug = null)
        {
            List<ListMutation> mutations = new List<ListMutation>();
            foreach (var instruction in method.Body.Instructions)
            {
                // Call the corresponding strategy based on the result
                if (instruction.IsList() /* how to detect a list */)
                {
                    //Add all possible or desired strategies to the mutation list
                    mutations.Add(new ListMutation(new EmptyListStrategy(method), method));
                }
            }


            return mutations;
        }
    }
}
