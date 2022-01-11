using System.Collections.Generic;
using System.Linq;
using Faultify.Analyze.ArrayMutationStrategy;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.Analyzers
{
    /// <summary>
    ///     Analyzer that searches for possible array mutations inside a method definition.
    ///     Limitations:
    ///     - Only one-dimensional arrays.
    ///     - Only arrays that are created dynamically with more than 2 values which are not default values.
    ///     - Only array of the following types: double, float, long, ulong, int, uint, byte, sbyte, short, ushort, char,
    ///     boolean.
    /// </summary>
    public class ArrayMutationAnalyzer : IMutationAnalyzer<ArrayMutation, MethodDefinition>
    {
        public ArrayMutationAnalyzer()
        {
            Mapped = new TypeCollection();
            Mapped.Types.Add(typeof(char));
            Mapped.AddBooleanTypes();
            Mapped.AddIntegerTypes();
            Mapped.WithStringTypes();
        }

        public TypeCollection Mapped { get; }

        public string Description => "Analyzer that searches for possible array mutations.";

        public string Name => "Array Analyzer";

        /// <summary>
        ///     Analyzes the method body and searches for dynamic array.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public IMutationGrouping<ArrayMutation> AnalyzeMutations(MethodDefinition method, MutationLevel mutationLevel,
            IDictionary<Instruction, SequencePoint> debug = null)
        {
            List<ArrayMutation> mutations = new List<ArrayMutation>();
            foreach (var instruction in method.Body.Instructions)
                // Call the corresponding strategy based on the result
                if (instruction.IsDynamicArray() && SupportedTypeCheck(instruction))
                {
                    //Add all possible or desired strategies to the mutation list
                    mutations.Add(new ArrayMutation(new EmptyArrayStrategy(method), method));
                    mutations.Add(new ArrayMutation(new DynamicArrayRandomizerStrategy(method), method));
                }

            // Build Mutation Group
            return new MutationGrouping<ArrayMutation>
            {
                AnalyzerName = Name,
                AnalyzerDescription = Description,
                Mutations = mutations,
            };
        }

        /// <summary>
        ///     Checks if the to be mutated array is of a supported type
        /// </summary>
        /// <param name="newarr"></param>
        /// <returns></returns>
        private bool SupportedTypeCheck(Instruction newarr)
        {
            var type = ((TypeReference)newarr.Operand).ToSystemType();
            return Mapped.Types.TryGetValue(type, out _);
        }
    }
}