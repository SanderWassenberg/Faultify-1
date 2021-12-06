using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Faultify.Core.Extensions;

namespace Faultify.Analyze.ArrayMutationStrategy
{
    /// <summary>
    /// Contains Mutating Strategy to make all arrays empty at initialization
    /// </summary>
    public class EmptyArrayStrategy : IArrayMutationStrategy
    {
        private readonly RandomizedArrayBuilder _arrayBuilder;
        private readonly MethodDefinition _methodDefinition;
        private TypeReference _type;

        public EmptyArrayStrategy(MethodDefinition methodDefinition)
        {
            _arrayBuilder = new RandomizedArrayBuilder();
            _methodDefinition = methodDefinition;
        }

        public void Reset(MethodDefinition methodBody, MethodDefinition methodClone)
        {
            methodBody.Body.Instructions.Clear();
            foreach (var instruction in methodClone.Body.Instructions)
                methodBody.Body.Instructions.Add(instruction);
        }

        public void Mutate()
        {
            var processor = _methodDefinition.Body.GetILProcessor();
            _methodDefinition.Body.SimplifyMacros();

            var beforeArray = new List<Instruction>();
            var afterArray = new List<Instruction>();

            var currentInstruction = _methodDefinition.Body.Instructions[0];

            // Get the type of the array from the instructions
            // After the 'Dup' instruction the setup ends and the actual values start
            while (currentInstruction != null)
            {
                if (currentInstruction.OpCode == OpCodes.Newarr) _type = (TypeReference)currentInstruction.Operand;
                if (currentInstruction.OpCode == OpCodes.Dup) break;

                currentInstruction = currentInstruction.Next;
            }
            currentInstruction = currentInstruction?.Next;

            // Process the values of the array. we want to skip all of them since the new array will be empty
            // if the array is created using Ldtoken, get the values from there
            if (currentInstruction?.OpCode == OpCodes.Ldtoken)
            {
                // skip the array initialization
                currentInstruction = currentInstruction.Next.Next;
            }
            // if the array is created by adding each item by index, go over all these instructions
            else
            {
                // when you reach an Stloc, all values have been visited
                while (currentInstruction != null && currentInstruction.OpCode != OpCodes.Stloc)
                {
                    currentInstruction = currentInstruction.Next;
                }
            }

            // add the instructions to be run after the values have been set
            while (currentInstruction != null)
            {
                afterArray.Add(currentInstruction);
                currentInstruction = currentInstruction.Next;
            }

            // remove all the instructions
            processor.Clear();

            // get the instructions to create the array with all its values
            var newArray = _arrayBuilder.CreateEmptyArray(processor, _type);

            // append new array instructions to processor
            foreach (var newInstruction in newArray) processor.Append(newInstruction);

            // append after array instructions to processor
            foreach (var after in afterArray) processor.Append(after);

            _methodDefinition.Body.OptimizeMacros();
        }
    }
}
