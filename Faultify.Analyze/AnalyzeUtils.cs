using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;

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
    }
}
