using System;
using System.Collections.Generic;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.ConstantAnalyzer
{
    /// <summary>
    ///     Analyzer that searches for possible string constant mutations inside a type definition.
    ///     Mutations such as 'hello' to a GUID like '0f8fad5b-d9cb-469f-a165-70867728950e'.
    /// </summary>
    public class StringConstantMutationAnalyzer : ConstantMutationAnalyzer
    {
        public override string Description =>
            "Analyzer that searches for possible string constant mutations such as 'hello' to a GUID like '0f8fad5b-d9cb-469f-a165-70867728950e'.";

        public override string Name => "String ConstantMutation Analyzer";

        public override IMutationGrouping<ConstantMutation> AnalyzeMutations(FieldDefinition field,
            MutationLevel mutationLevel, IDictionary<Instruction, SequencePoint> debug = null)
        {
            // Make a new mutation list
            List<ConstantMutation> mutations = new List<ConstantMutation>();

            if (field.Constant is string original)
                mutations.Add(new ConstantMutation
                {
                    Original = original,
                    ConstantName = field.Name,
                    Replacement = Guid.NewGuid().ToString(),
                    ConstantField = field
                });

            // Build mutation group
            return new MutationGrouping<ConstantMutation>
            {
                AnalyzerName = Name,
                AnalyzerDescription = Description,
                Mutations = mutations,
            };
        }
    }
}