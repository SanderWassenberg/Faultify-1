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
        private int _lineNumber;
        private Instruction _instruction;

        public EmptyArrayStrategy(MethodDefinition methodDefinition, Instruction instruction)
        {
            _arrayBuilder = new RandomizedArrayBuilder();
            _methodDefinition = methodDefinition;
            _type = methodDefinition.ReturnType.GetElementType();
            _instruction = instruction;
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

            var length = 0;
            var beforeArray = new List<Instruction>();
            var afterArray = new List<Instruction>();

            // Get first instruction
            var currentInstruction = _methodDefinition.Body.Instructions[0];

            bool isnewarr = false;
            // Find the instruction for creating a new array, after that the actual values start
            // Get the length of the array from the instructions
            while (currentInstruction != null)
            {
                if ((currentInstruction.OpCode == OpCodes.Dup || currentInstruction.OpCode == OpCodes.Stloc) && isnewarr)
                {
                    break;
                }

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
                int dataCounter = 0;
                while (currentInstruction != null)
                {
                    // All variables have been found
                    if (dataCounter == length || (currentInstruction.OpCode == OpCodes.Stloc && _instruction.Next.OpCode == OpCodes.Dup ))
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
                            currentInstruction = currentInstruction.Next.Next.Next;
                        }
                        // For variables outside of the method
                        else if (currentInstruction.Next.OpCode == OpCodes.Ldarg)
                        {
                            currentInstruction = currentInstruction.Next.Next.Next.Next;
                        } 
                        // for values inside of the method
                        else
                        {
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
            var newArray = _arrayBuilder.CreateEmptyArray(processor, _type);

            // append new array instructions to processor
            foreach (var newInstruction in newArray) processor.Append(newInstruction);

            // append after array instructions to processor
            foreach (var after in afterArray) processor.Append(after);

            _methodDefinition.Body.OptimizeMacros();
        }

        public string GetStrategyStringForReport()
        {
            return $"Emptied the array at line {_lineNumber}";
        }
    }
}
