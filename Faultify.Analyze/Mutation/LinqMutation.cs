using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MonoMod.Utils;

namespace Faultify.Analyze.Mutation
{
    /// <summary>
    ///     LINQ mutation that can be performed or reverted.
    /// </summary>
    public class LinqMutation : IMutation
    {
        private GenericInstanceMethod OriginalMethod { get; }
        private MethodInfo ReplacementMethodInfo { get; }
        private Instruction Instruction { get; }

        public LinqMutation(Instruction instruction, GenericInstanceMethod originalMethod, MethodInfo replacementMethodInfo)
        {
            Instruction = instruction;
            OriginalMethod = originalMethod;
            ReplacementMethodInfo = replacementMethodInfo;
        }

        public void Mutate()
        {
            // import the replacementMethod in the target module
            var replacementMethodInModule = OriginalMethod.Module.ImportReference(ReplacementMethodInfo);

            // create a instance of the replacement method and add the type arguments
            var replacementMethod = new GenericInstanceMethod(replacementMethodInModule);
            replacementMethod.GenericArguments.AddRange(OriginalMethod.GenericArguments);

            Instruction.Operand = replacementMethod;
        }

        public void Reset()
        {
            Instruction.Operand = OriginalMethod;
        }

        public string Report => $"Change LINQ method from '{OriginalMethod.Name}' to '{ReplacementMethodInfo.Name}'";
    }
}