using System;
using System.Collections.Generic;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Faultify.Analyze.Strategies;
using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.Analyzers
{
    class ListMutationAnalyzer : IMutationAnalyzer<ListMutation, MethodDefinition>
    {
        public string Description => "Analyzer that searches for possible list mutations.";

        public string Name => "List Analyzer";

        public IMutationGrouping<ListMutation> AnalyzeMutations(MethodDefinition method, MutationLevel mutationLevel, IDictionary<Instruction, SequencePoint> debug = null)
        {
            List<ListMutation> mutations = new List<ListMutation>();
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.IsListInitialiser())
                {
                    //Add all possible or desired strategies to the mutation list
                    mutations.Add(new ListMutation(new EmptyListStrategy(method, instruction), method));
                }
            }

            return new MutationGrouping<ListMutation>()
            {
                AnalyzerName = Name,
                AnalyzerDescription = Description,
                Mutations = mutations,
            };
        }
    }
}
