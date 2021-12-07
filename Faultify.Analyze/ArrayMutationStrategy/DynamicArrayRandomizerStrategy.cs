﻿using System.Collections.Generic;
using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Faultify.Analyze.ArrayMutationStrategy
{
    /// <summary>
    ///     Contains Mutating Strategy for Dynamic Arrays.
    /// </summary>
    public class DynamicArrayRandomizerStrategy : IArrayMutationStrategy
    {
        private readonly RandomizedArrayBuilder _randomizedArrayBuilder;
        private readonly MethodDefinition _methodDefinition;
        private TypeReference _type;

        public DynamicArrayRandomizerStrategy(MethodDefinition methodDefinition)
        {
            _randomizedArrayBuilder = new RandomizedArrayBuilder();
            _methodDefinition = methodDefinition;
        }

        public void Reset(MethodDefinition mutatedMethodDef, MethodDefinition methodClone)
        {
            mutatedMethodDef.Body.Instructions.Clear();
            foreach (var instruction in methodClone.Body.Instructions)
                mutatedMethodDef.Body.Instructions.Add(instruction);
        }

        /// <summary>
        ///     Mutates a dynamic array by creating a new array with random values with the arraybuilder.
        /// </summary>
        public void Mutate()
        {
            var processor = _methodDefinition.Body.GetILProcessor();
            _methodDefinition.Body.SimplifyMacros();

            var length = 0;
            var afterArray = new List<Instruction>();

            var currentInstruction = _methodDefinition.Body.Instructions[0];

            // Get the length and type of the array from the instructions
            // After the 'Dup' instruction the setup ends and the actual values start
            while (currentInstruction != null)
            {
                if (currentInstruction.OpCode == OpCodes.Ldc_I4) length = (int)currentInstruction.Operand;
                if(currentInstruction.OpCode == OpCodes.Newarr) _type = (TypeReference)currentInstruction.Operand;
                if (currentInstruction.OpCode == OpCodes.Dup) break;

                currentInstruction = currentInstruction.Next;
            }
            currentInstruction = currentInstruction?.Next;

            // create a new object array with the length of the original array
            object[] data = new object[length];

            // Process the values of the array
            // if the array is created using Ldtoken, get the values from there
            if (currentInstruction?.OpCode == OpCodes.Ldtoken)
            {
                var initialValues = ((FieldDefinition)currentInstruction.Operand).InitialValue;

                for (var index = 0; index < length; index++)
                {
                    data[index] = initialValues[index];
                }

                // skip the array initialization
                currentInstruction = currentInstruction.Next.Next;
            }
            // if the array is created by adding each item by index, go over all these instructions
            else
            {
                while (currentInstruction != null)
                {
                    // when you reach an Stloc, all values have been set
                    if (currentInstruction.OpCode == OpCodes.Stloc) break;

                    // the first Ldc_i4 instruction sets the index, the following commands sets the value
                    if (currentInstruction.OpCode == OpCodes.Ldc_I4)
                    {
                        data[(int) currentInstruction.Operand] = currentInstruction.Next.Operand;
                        currentInstruction = currentInstruction.Next;
                    }

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
            var newArray = _randomizedArrayBuilder.CreateRandomizedArray(processor, length, _type, data);

            // append new array instructions to processor
            foreach (var newInstruction in newArray) processor.Append(newInstruction);

            // append after array instructions to processor
            foreach (var after in afterArray) processor.Append(after);

            _methodDefinition.Body.OptimizeMacros();
        }
    }
}