using Mono.Cecil;
using Mono.Cecil.Cil;
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

            var startList = new List<Instruction>();
            var endList = new List<Instruction>();

            var currentInstruction = _methodDefinition.Body.Instructions[0];

            // add the instructions to be run after the values have been set
            while (currentInstruction != null)
            {
                endList.Add(currentInstruction);
                currentInstruction = currentInstruction.Next;
            }

            // remove all the instructions
            processor.Clear();

            // append new list instructions to processor
            foreach (var start in startList) processor.Append(start);

            // append after array instructions to processor
            foreach (var end in endList) processor.Append(end);

            Console.WriteLine("ListMutation!");
            Environment.Exit(1);
        }
    }
}
