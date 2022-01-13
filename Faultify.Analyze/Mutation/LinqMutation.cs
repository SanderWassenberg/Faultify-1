using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Reflection;
using MonoMod.Utils;
using System.Linq;

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
        private MethodDefinition MethodScope { get; }
        private int LineNumber { get; set; }

        public LinqMutation(Instruction instruction, GenericInstanceMethod originalMethod, MethodInfo replacementMethodInfo, MethodDefinition method)
        {
            Instruction = instruction;
            OriginalMethod = originalMethod;
            ReplacementMethodInfo = replacementMethodInfo;
            MethodScope = method;
            LineNumber = AnalyzeUtils.FindLineNumber(instruction, method);
        }

        public void Mutate()
        {
            // import the replacementMethod in the target module
            var replacementMethodInModule = OriginalMethod.Module.ImportReference(ReplacementMethodInfo);

            // create an instance of the replacement method and add the type arguments
            var replacementMethod = new GenericInstanceMethod(replacementMethodInModule);
            replacementMethod.GenericArguments.AddRange(OriginalMethod.GenericArguments);

            Instruction.Operand = replacementMethod;
        }

        public void Reset()
        {
            Instruction.Operand = OriginalMethod;
        }

        public string Report => $"{MethodScope.FullName.Split(' ').Last()}: Change LINQ method from '{OriginalMethod.Name}' to '{ReplacementMethodInfo.Name}' at line {LineNumber}";
    }
}