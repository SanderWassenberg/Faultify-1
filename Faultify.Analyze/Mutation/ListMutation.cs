using Faultify.Analyze.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using MonoMod.Utils;

namespace Faultify.Analyze.Mutation
{
    public class ListMutation : IMutation
    {
        private readonly IMutationStrategy _mutationStrategy;
        private readonly MethodDefinition _methodDefinitionToMutate;
        private readonly MethodDefinition _originalMethod;

        public ListMutation(IMutationStrategy mutationStrategy, MethodDefinition methodDef)
        {
            _mutationStrategy = mutationStrategy;
            _methodDefinitionToMutate = methodDef;
            _originalMethod = methodDef.Clone();
        }

        public string Report => $"Change list contents. {_mutationStrategy.GetStrategyStringForReport()}";

        public void Mutate()
        {
            _mutationStrategy.Mutate();
        }

        public void Reset()
        {
            // Replace modified with original
            AnalyzeUtils.ReplaceMethodBody(_methodDefinitionToMutate, _originalMethod);
        }
    }
}
