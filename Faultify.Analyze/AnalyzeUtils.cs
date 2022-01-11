using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze
{
    public static class AnalyzeUtils
    {
        public static void ReplaceMethodBody(MethodDefinition targetMethod, MethodDefinition sourceMethod)
        {
            targetMethod.Body.Instructions.Clear();
            foreach (var instruction in sourceMethod.Body.Instructions)
                targetMethod.Body.Instructions.Add(instruction);
        }

        public static int FindLineNumber(Instruction variable, MethodDefinition methodDefinition)
        {
            var debug = methodDefinition.DebugInformation.GetSequencePointMapping();
            int lineNumber = -1;

            if (debug != null)
            {
                Instruction prev = variable;
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
    }
}
