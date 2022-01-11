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
        private int _arrayCounter;

        public DynamicArrayRandomizerStrategy(MethodDefinition methodDefinition, int arrayCounter)
        {
            _randomizedArrayBuilder = new RandomizedArrayBuilder();
            _methodDefinition = methodDefinition;
            _type = methodDefinition.ReturnType.GetElementType();
            _arrayCounter = arrayCounter;
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

            var currentInstruction = _methodDefinition.Body.Instructions[0];

            // Get the length of the array from the instructions
            // After the 'Dup' instruction the setup ends and the actual values start
            bool isnewarr = false;
            int arrayCounter = 1;
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
                else if (arrayCounter == _arrayCounter)
                {
                    length = (int)currentInstruction.Previous.Operand;
                    beforeArray.Remove(currentInstruction.Previous);
                    isnewarr = true;
                    _lineNumber = AnalyzeUtils.FindLineNumber(currentInstruction, _methodDefinition);
                }
                else
                {
                    beforeArray.Add(currentInstruction);
                    arrayCounter++;
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
                    // when you reach an Stloc, all values have been set
                    if (currentInstruction.OpCode == OpCodes.Stloc) break;

                    // the first Ldc_i4 instruction sets the index, the following commands sets the value
                    if (currentInstruction.OpCode == OpCodes.Ldc_I4 && dataCounter != length)
                    {
                        if (currentInstruction.Next.OpCode == OpCodes.Ldloc && _type.ToSystemType() == typeof(bool))
                        {
                            string ldloc = currentInstruction.Next.Operand.ToString();
                            data[(int)currentInstruction.Operand] = localVariables[ldloc];
                        }
                        else
                        {
                            data[(int)currentInstruction.Operand] = currentInstruction.Next.Operand;
                            dataCounter++;
                        }
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

            // Append everything before array.
            foreach (var before in beforeArray) processor.Append(before);

            // get the instructions to create the array with all its values
            var newArray = _randomizedArrayBuilder.CreateRandomizedArray(processor, length, _type, data);

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