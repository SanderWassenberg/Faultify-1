using System;
using System.Collections.Generic;
using System.Linq;
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
        private int _lineNumber;
        private Instruction _instruction;

        public DynamicArrayRandomizerStrategy(MethodDefinition methodDefinition, Instruction instruction)
        {
            _randomizedArrayBuilder = new RandomizedArrayBuilder();
            _methodDefinition = methodDefinition;
            _type = methodDefinition.ReturnType.GetElementType();
            _instruction = instruction;
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
            var beforeArray = new List<Instruction>();
            var afterArray = new List<Instruction>();
            Dictionary<string, int> localVariables = new Dictionary<string, int>();

            // Get first instruction
            var currentInstruction = _methodDefinition.Body.Instructions[0];

            bool isnewarr = false;
            // Get the length of the array from the instructions
            // Find the instruction for creating a new array, after that the actual values start
            while (currentInstruction != null)
            {
                if (_type.ToSystemType() == typeof(bool) && currentInstruction.OpCode == OpCodes.Stloc && currentInstruction.Previous.OpCode == OpCodes.Ldc_I4)
                {
                    localVariables.Add(currentInstruction.Operand.ToString(), (int)currentInstruction.Previous.Operand);
                }

                if ((currentInstruction.OpCode == OpCodes.Dup || currentInstruction.OpCode == OpCodes.Stloc) && isnewarr)
                    break;

                if (!currentInstruction.IsDynamicArray())
                {
                    beforeArray.Add(currentInstruction);
                }
                else if (currentInstruction.Equals(_instruction))
                {
                    length = (int)currentInstruction.Previous.Operand;
                    beforeArray.Remove(currentInstruction.Previous);
                    isnewarr = true;
                    _lineNumber = AnalyzeUtils.FindLineNumber(currentInstruction, _methodDefinition);
                }
                else
                {
                    beforeArray.Add(currentInstruction);
                }

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
                int dataCounter = 0;
                while (currentInstruction != null)
                {
                    // All variables have been found
                    if (dataCounter == length || (currentInstruction.OpCode == OpCodes.Stloc && _instruction.Next.OpCode == OpCodes.Dup))
                    {
                        break;
                    }

                    // check if the ldc is not for a new array and check if the ldc is for the array that needs to be mutated
                    if (currentInstruction.OpCode == OpCodes.Ldc_I4 && currentInstruction.Previous.Operand == _instruction.Next.Operand && currentInstruction.Next.OpCode != OpCodes.Newarr)
                    {
                        beforeArray.Remove(currentInstruction.Previous);

                        // For booleans
                        if (currentInstruction.Next.OpCode == OpCodes.Ldloc && _type.ToSystemType() == typeof(bool))
                        {
                            string ldloc = currentInstruction.Next.Operand.ToString();
                            data[(int)currentInstruction.Operand] = localVariables[ldloc];
                            currentInstruction = currentInstruction.Next.Next.Next;
                        }
                        // For variables outside of the method
                        else if (currentInstruction.Next.OpCode == OpCodes.Ldarg)
                        {
                            data[(int)currentInstruction.Operand] = currentInstruction.Next.Operand;
                            currentInstruction = currentInstruction.Next.Next.Next.Next;
                        }
                        // for values inside of the method
                        else
                        {
                            data[(int)currentInstruction.Operand] = currentInstruction.Next.Operand;
                            currentInstruction = currentInstruction.Next.Next.Next;
                        }
                        dataCounter++;
                    }
                    // Not an instruction for the array that needs to be mutated
                    else
                    {
                        beforeArray.Add(currentInstruction);
                        currentInstruction = currentInstruction.Next;
                    }
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

            // Append everything before array.
            foreach (var before in beforeArray) processor.Append(before);

            // get the instructions to create the array with all its values
            var newArray = _randomizedArrayBuilder.CreateRandomizedArray(processor, length, _type, data, _instruction.Next);

            // append new array instructions to processor
            foreach (var newInstruction in newArray) processor.Append(newInstruction);

            // append after array instructions to processor
            foreach (var after in afterArray) processor.Append(after);

            _methodDefinition.Body.OptimizeMacros();
        }

        public string GetStrategyStringForReport()
        {
            return $"Randomized the array at line {_lineNumber}";
        }
    }
}