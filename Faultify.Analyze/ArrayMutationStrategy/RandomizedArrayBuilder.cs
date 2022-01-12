using System.Collections.Generic;
using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;

namespace Faultify.Analyze.ArrayMutationStrategy
{
    /// <summary>
    ///     Builder for building arrays in IL-code.
    /// </summary>
    public class RandomizedArrayBuilder
    {
        private RandomValueGenerator _randomValueGenerator;

        /// <summary>
        ///     Creates array with the given length and array type.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="length"></param>
        /// <param name="arrayType"></param>
        /// <returns></returns>
        public List<Instruction> CreateRandomizedArray(ILProcessor processor, int length, TypeReference arrayType, object[] data, object operand)
        {
            _randomValueGenerator = new RandomValueGenerator();
            var opcodeTypeValueAssignment = arrayType.GetLdcOpCodeByTypeReference();
            var stelem = arrayType.GetStelemByTypeReference();
            if (arrayType.ToSystemType() == typeof(long) || arrayType.ToSystemType() == typeof(ulong))
                opcodeTypeValueAssignment = OpCodes.Ldc_I4;

            // create the list
            var list = new List<Instruction>
            {
                processor.Create(OpCodes.Ldc_I4, length),
                processor.Create(OpCodes.Newarr, arrayType)
            };

            // for each item in the original array, create a new, random one (flipped for bools)
            for (var i = 0; i < length; i++)
            {
                var random = _randomValueGenerator.GenerateValueForField(arrayType.ToSystemType(), data[i]);

                list.Add(processor.Create(OpCodes.Dup));

                if (length > 2147483647 && length < -2147483647) list.Add(processor.Create(OpCodes.Ldc_I8, i));
                else list.Add(processor.Create(OpCodes.Ldc_I4, i));
                
                list.Add(processor.Create(opcodeTypeValueAssignment, random));

                if (arrayType.ToSystemType() == typeof(long) || arrayType.ToSystemType() == typeof(ulong)) 
                    list.Add(processor.Create(OpCodes.Conv_I8));

                list.Add(processor.Create(stelem));
            }

            list.Add(processor.Create(OpCodes.Stloc, operand));

            return list;
        }

        /// <summary>
        ///     Creates the instructions for a new, empty array
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="arrayType"></param>
        /// <returns></returns>
        public List<Instruction> CreateEmptyArray(ILProcessor processor, TypeReference arrayType)
        {
            var list = new List<Instruction>
            {
                processor.Create(OpCodes.Ldc_I4, 0),
                processor.Create(OpCodes.Newarr, arrayType)
            };

            return list;
        }
    }
}