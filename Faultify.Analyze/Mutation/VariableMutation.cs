using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace Faultify.Analyze.Mutation
{
    public class VariableMutation : IMutation
    {
        public VariableMutation(Instruction instruction, Type type, MethodDefinition method, object replacement)
        {
            Original = instruction.Operand;
            Replacement = replacement;
            Variable = instruction;
            MethodScope = method;
            LineNumber = FindLineNumber();
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

        private int FindLineNumber()
        {
            var debug = MethodScope.DebugInformation.GetSequencePointMapping();
            int lineNumber = -1;

            if (debug != null)
            {
                Instruction prev = Variable;
                SequencePoint seqPoint = null;
                // If prev is not null and line number is not found try previous instruction.
                while (prev != null && !debug.TryGetValue(prev, out seqPoint))
                {
                    prev = prev.Previous;
                }

                if (seqPoint != null)
                {
                    lineNumber = seqPoint.StartLine;
                }
            }
            return lineNumber;
        }

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
                    return $"{MethodScope.FullName.Split(']').Last()}: Change variable from: '{Original}' to '{Replacement}'";

                return $"{MethodScope.FullName.Split(']').Last()} at line {LineNumber}: Change variable from: '{Original}' to '{Replacement}'";
            }
        }
    }
}