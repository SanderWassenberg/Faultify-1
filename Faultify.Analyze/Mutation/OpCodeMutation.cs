using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Linq;

namespace Faultify.Analyze.Mutation
{
    /// <summary>
    ///     Opcode mutation that can be performed or reverted.
    /// </summary>
    public class OpCodeMutation : IMutation
    {
        public OpCodeMutation(OpCode original, OpCode replacement, Instruction scope, MethodDefinition method)
        {
            Original = original;
            Replacement = replacement;
            Instruction = scope;
            MethodScope = method;
            LineNumber = AnalyzeUtils.FindLineNumber(Instruction, MethodScope);
        }

        /// <summary>
        ///     The original opcode.
        /// </summary>
        private OpCode Original { get; set; }

        /// <summary>
        ///     The replacement for the original opcode.
        /// </summary>
        public OpCode Replacement { get; set; }

        /// <summary>
        ///     Reference to the instruction line in witch the opcode can be mutated.
        /// </summary>
        private Instruction Instruction { get; set; }

        private MethodDefinition MethodScope { get; set; }

        private int LineNumber { get; set; }

        public void Mutate()
        {
            Instruction.OpCode = Replacement;
        }

        public void Reset()
        {
            Instruction.OpCode = Original;
        }

        private string GetOpCodeName(OpCode opcode)
        {
            string name = opcode.Name switch
            {
                "add" => "+",
                "sub" => "-",
                "mul" => "*",
                "div" => "/",
                "rem" => "%",
                "or" => "|",
                "and" => "&",
                "xor" => "^",
                "ceq" => "==",
                "clt" => "<",
                "cgt" => ">",
                "shl" => "<<",
                "shr" => ">>",
                _ => throw new System.NotImplementedException()
            };
            return name;
        }

        public string Report
        {
            get
            {
                if (LineNumber == -1)
                    return $"{MethodScope.FullName.Split(' ').Last()}: Change operator from: '{GetOpCodeName(Original)}' to '{GetOpCodeName(Replacement)}'";

                return $"{MethodScope.FullName.Split(' ').Last()}: Change operator from: '{GetOpCodeName(Original)}' to '{GetOpCodeName(Replacement)}' at line {LineNumber}";
            }
        }
    }
}