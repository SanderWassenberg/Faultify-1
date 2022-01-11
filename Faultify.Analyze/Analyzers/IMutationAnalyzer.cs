using System.Collections.Generic;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.Analyzers
{
    /// <summary>
    ///     Interface for analyzers that search for possible source code mutations on byte code level.
    /// </summary>
    /// <typeparam name="TMutation">The type of the returned metadata.</typeparam>
    /// <typeparam name="TScope"></typeparam>
    public interface IMutationAnalyzer<TMutation, in TScope> where TMutation : IMutation
    {
        /// <summary>
        ///     Description of the mutator.
        /// </summary>
        string Description { get; }

        /// <summary>
        ///     Name of the mutator.
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Analyzes possible mutations in the given scope.
        ///     Returns the mutation that can be either executed or reverted.
        /// </summary>
        IMutationGrouping<TMutation> AnalyzeMutations(TScope scope, MutationLevel mutationLevel,
            IDictionary<Instruction, SequencePoint> debug = null);
    }
}