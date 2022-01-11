using System;
using System.Collections.Generic;
using System.Linq;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.Analyzers
{
    /// <summary>
    ///     Analyzer that searches for possible variable mutations.
    ///     Mutations such as 'true' to 'false'
    /// </summary>
    public class VariableMutationAnalyzer : IMutationAnalyzer<VariableMutation, MethodDefinition>
    {
        private readonly RandomValueGenerator _valueGenerator;

        public VariableMutationAnalyzer()
        {
            _valueGenerator = new RandomValueGenerator();
            Mapped = new TypeCollection();
            Mapped.AddBooleanTypes();
        }

        public TypeCollection Mapped { get; }

        public string Description =>
            "Analyzer that searches for possible variable mutations such as 'true' to 'false'.";

        public string Name => "Variable Mutation Analyzer";

        public IMutationGrouping<VariableMutation> AnalyzeMutations(MethodDefinition method, MutationLevel mutationLevel,
            IDictionary<Instruction, SequencePoint> debug = null)
        {
            var mutations = new List<VariableMutation>();
            foreach (var instruction in method.Body.Instructions)
            {
                // Booleans (0,1) or number literals are loaded on the evaluation stack with 'ldc_...' and popped of with 'stloc'.
                // Therefore if there is an 'ldc' instruction followed by 'stdloc' we can assert there is a literal of some type. 
                // 'ldc' does not specify the variable type. 
                // In order to know the type cast the 'Operand' to 'VariableDefinition'.

                if (instruction.OpCode != OpCodes.Stloc) continue;

                if (instruction.Operand as VariableDefinition is null) continue;

                try
                {
                    // Get variable type. Might throw InvalidCastException
                    var type = ((VariableReference)instruction.Operand).Resolve().VariableType.ToSystemType();

                    // Get previous instruction.
                    var variableInstruction = instruction.Previous;

                    // If the previous instruction is 'ldc' its loading a boolean or integer on the stack. 
                    if (!variableInstruction.IsLdc()) continue;

                    // If the value is mapped then mutate it.
                    if (TypeChecker.IsVariableType(type))
                        mutations.Add(
                            new VariableMutation(variableInstruction, method, _valueGenerator.GenerateValueForField(type, instruction.Previous.Operand)));
                }
                catch (InvalidCastException e)
                {
                    // ignore (sometimes `Type.GetType` fails)
                }
            }

            return new MutationGrouping<VariableMutation>
            {
                AnalyzerName = Name,
                AnalyzerDescription = Description,
                Mutations = mutations,
            };
        }
    }
}