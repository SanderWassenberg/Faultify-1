using System.Collections.Generic;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.OpcodeAnalyzer
{
    /// <summary>
    ///     Analyzer that searches for possible bitwise mutations inside a method definition.
    ///     Mutations such as such as 'and' and 'xor'.
    /// </summary>
    public class BitwiseMutationAnalyzer : OpCodeMutationAnalyzer
    {
        private static readonly Dictionary<OpCode, IEnumerable<(MutationLevel, OpCode)>> Bitwise =
            new Dictionary<OpCode, IEnumerable<(MutationLevel, OpCode)>>
            {
                // Opcodes for mutation bitwise operator: '|' to '&' , and '^'. 
                { OpCodes.Or, new[] { (MutationLevel.Simple, OpCodes.And), (MutationLevel.Medium, OpCodes.Xor) } },

                // Opcodes for mutation bitwise operator: '&' to '|' , and '^'. 
                { OpCodes.And, new[] { (MutationLevel.Simple, OpCodes.Or), (MutationLevel.Medium, OpCodes.Xor) } },

                // Opcodes for mutation bitwise operator: '^' to '|' , and '&'. 
                { OpCodes.Xor, new[] { (MutationLevel.Simple, OpCodes.Or), (MutationLevel.Medium, OpCodes.And) } },
                
                // Opcodes for mutation bitwise operator: '<<' to '>>'. 
                { OpCodes.Shl, new[] { (MutationLevel.Simple, OpCodes.Shr)} },

                // Opcodes for mutation bitwise operator: '>>' (signed) to '<<'. 
                { OpCodes.Shr, new[] { (MutationLevel.Simple, OpCodes.Shl)} },

                // Opcodes for mutation bitwise operator: '>>' (unsigned) to '<<'. 
                { OpCodes.Shr_Un, new[] { (MutationLevel.Simple, OpCodes.Shl)} }
            };

        public BitwiseMutationAnalyzer() : base(Bitwise)
        {
        }

        public override string Description =>
            "Analyzer that searches for possible bitwise mutations such as 'or' to 'and' and 'xor'.";

        public override string Name => "Bitwise Analyzer";
    }
}