using System.Collections.Generic;
using System.Linq;
using Faultify.Analyze.Analyzers;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.OpcodeAnalyzer
{
    /// <summary>
    ///     Analyzer that searches for possible opcode mutations inside a method definition.
    ///     A list with opcodes definitions can be found here: https://en.wikipedia.org/wiki/List_of_CIL_instructions
    /// </summary>
    public abstract class
        OpCodeMutationAnalyzer : IMutationAnalyzer<OpCodeMutation, MethodDefinition>
    {
        private readonly Dictionary<OpCode, IEnumerable<(MutationLevel, OpCode)>> _mappedOpCodes;

        protected OpCodeMutationAnalyzer(Dictionary<OpCode, IEnumerable<(MutationLevel, OpCode)>> mappedOpCodes)
        {
            _mappedOpCodes = mappedOpCodes;
        }

        public abstract string Description { get; }

        public abstract string Name { get; }

        public IMutationGrouping<OpCodeMutation> AnalyzeMutations(MethodDefinition scope, MutationLevel mutationLevel,
            IDictionary<Instruction, SequencePoint> debug = null)
        {
            var mutationGroup = new List<IEnumerable<OpCodeMutation>>();
            foreach (var instruction in scope.Body.Instructions)
            {
                // Store original opcode for a reset later on.
                var original = instruction.OpCode;

                if (_mappedOpCodes.ContainsKey(original))
                {
                    var mutations = _mappedOpCodes[original]
                        .Where(mutant => mutationLevel.HasFlag(mutant.Item1))
                        .Select(mutant => new OpCodeMutation(
                            original,
                            mutant.Item2,
                            instruction,
                            scope
                            ));

                    mutationGroup.Add(mutations);
                }
            }

            return new MutationGrouping<OpCodeMutation>
            {
                Mutations = mutationGroup.SelectMany(x => x),
                AnalyzerName = Name,
                AnalyzerDescription = Description
            };
        }
    }
}