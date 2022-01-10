using System.Collections.Generic;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.ConstantAnalyzer
{
    /// <summary>
    ///     Analyzer that searches for possible boolean constant mutations inside a type definition.
    ///     Mutations such as 'true' to 'false'.
    /// </summary>
    public class BooleanConstantMutationAnalyzer : ConstantMutationAnalyzer
    {
        public override string Description =>
            "Analyzer that searches for possible boolean constant mutations such as 'true' to 'false'.";

        public override string Name => "Boolean ConstantMutation Analyzer";

        public override IMutationGrouping<ConstantMutation> AnalyzeMutations(FieldDefinition field,
            MutationLevel mutationLevel, IDictionary<Instruction, SequencePoint> debug = null)
        {
            // Make a new mutation list
            List<ConstantMutation> mutations = new List<ConstantMutation>();

            if (field.Constant is bool original)
                mutations.Add(new ConstantMutation
                {
                    Original = original,
                    ConstantName = field.Name,
                    Replacement = !original,
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