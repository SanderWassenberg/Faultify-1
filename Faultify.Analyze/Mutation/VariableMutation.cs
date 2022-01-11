using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace Faultify.Analyze.Mutation
{
    public class VariableMutation : IMutation
    {
        public VariableMutation(Instruction instruction, MethodDefinition method, object replacement)
        {
            Original = instruction.Operand;
            Replacement = replacement;
            Variable = instruction;
            MethodScope = method;
            LineNumber = AnalyzeUtils.FindLineNumber(Variable, MethodScope);
        }

        /// <summary>
        ///     The original variable value.
        /// </summary>
        private object Original { get; set; }

        /// <summary>
        ///     The replacement for the variable value.
        /// </summary>
        private object Replacement { get; set; }

        /// <summary>
        ///     Reference to the variable instruction that can be mutated.
        /// </summary>
        private Instruction Variable { get; set; }

        private MethodDefinition MethodScope { get; set; }

        private int LineNumber { get; set; }

        public void Mutate()
        {
            Variable.Operand = Replacement;
        }

        public void Reset()
        {
            Variable.Operand = Original;
        }

        public string Report
        {
            get
            {
                if (LineNumber == -1)
                    return $"{MethodScope.FullName.Split(' ').Last()}: Change variable from: '{Original}' to '{Replacement}'";

                return $"{MethodScope.FullName.Split(' ').Last()}: Change variable from: '{Original}' to '{Replacement}' at line {LineNumber}";
            }
        }
    }
}