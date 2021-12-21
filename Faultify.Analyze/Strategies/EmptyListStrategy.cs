using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Faultify.Analyze.Strategies
{
    public class EmptyListStrategy : IMutationStrategy
    {
        private readonly MethodDefinition _methodDefinition;
        private TypeReference _type;

        public EmptyListStrategy(MethodDefinition methodDefinition)
        {
            _methodDefinition = methodDefinition;
            _type = methodDefinition.ReturnType.GetElementType();
        }

        public string GetStrategyStringForReport() => "Emptied the list";

        public void Mutate()
        {
            var processor = _methodDefinition.Body.GetILProcessor();
            _methodDefinition.Body.SimplifyMacros();

            var currentInstruction = _methodDefinition.Body.Instructions[0];

            // add the instructions to be run after the values have been set
            while (currentInstruction != null)
            {
                if (currentInstruction.IsList())
                    break;
                currentInstruction = currentInstruction.Next;
            }

            currentInstruction = currentInstruction.Next;

            while (currentInstruction != null)
            {
                if (currentInstruction.OpCode == OpCodes.Stloc)
                    break;
                var deleteInstruction = currentInstruction;
                currentInstruction = currentInstruction.Next;
                processor.Body.Instructions.Remove(deleteInstruction);
            }

            var test = processor.Body.Instructions;

            _methodDefinition.Body.OptimizeMacros();
        }
    }
}
