using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Faultify.Analyze.Mutation;
using Faultify.Core.Extensions;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Utils;
using GenericParameter = Mono.Cecil.GenericParameter;
using MethodDefinition = Mono.Cecil.MethodDefinition;
using ModuleDefinition = Mono.Cecil.ModuleDefinition;
using OpCodes = Mono.Cecil.Cil.OpCodes;
using SequencePoint = Mono.Cecil.Cil.SequencePoint;
using TypeDefinition = Mono.Cecil.TypeDefinition;
using TypeReference = Mono.Cecil.TypeReference;

namespace Faultify.Analyze
{
    /// <summary>
    ///     Analyzer that searches for possible Linq mutations such as 'Last()' to 'First()'."
    /// </summary>
    public class LinqMutationAnalyzer : IMutationAnalyzer<LinqMutation, MethodDefinition>
    {
        private static readonly Dictionary<string, IEnumerable<(MutationLevel, string)>> MethodsToMutate =
            new Dictionary<string, IEnumerable<(MutationLevel, string)>>
            {
                {"SingleOrDefault", new[] {(MutationLevel.Simple, "Single")}},
                {"Single", new[] {(MutationLevel.Simple, "SingleOrDefault")}},
                {"FirstOrDefault", new[] {(MutationLevel.Simple, "First")}},
                {"First", new[] {(MutationLevel.Simple, "FirstOrDefault")}},
                {"Last", new[] {(MutationLevel.Simple, "First")}},
                {"All", new[] {(MutationLevel.Simple, "Any")}},
                {"Any", new[] {(MutationLevel.Simple, "All")}},
                {"Skip", new[] {(MutationLevel.Simple, "Take")}},
                {"Take", new[] {(MutationLevel.Simple, "Skip")}},
                {"SkipWhile", new[] {(MutationLevel.Simple, "TakeWhile")}},
                {"TakeWhile", new[] {(MutationLevel.Simple, "SkipWhile")}},
                {"Min", new[] {(MutationLevel.Simple, "Max")}},
                {"Max", new[] {(MutationLevel.Simple, "Min")}},
                {"Sum", new[] {(MutationLevel.Simple, "Max")}},
                {"Count", new[] {(MutationLevel.Simple, "Sum")}},
                {"Average", new[] {(MutationLevel.Simple, "Min")}},
                {"OrderBy", new[] {(MutationLevel.Simple, "OrderByDescending") }},
                {"OrderByDescending", new[] {(MutationLevel.Simple, "OrderBy") }},
                {"ThenBy", new[] {(MutationLevel.Simple, "ThenByDescending") }},
                {"ThenByDescending", new[] {(MutationLevel.Simple, "ThenBy") }},
                {"Reverse", new[] {(MutationLevel.Simple, "AsEnumerable") }},
                {"AsEnumerable", new[] {(MutationLevel.Simple, "Reverse") }},
                {"Union", new[] {(MutationLevel.Simple, "Intersect") }},
                {"Intersect", new[] {(MutationLevel.Simple, "Union") }},
                {"Concat", new[] {(MutationLevel.Simple, "Except") }},
                {"Except", new[] {(MutationLevel.Simple, "Concat") }}
            };

        public string Description => "Analyzer that searches for possible Linq mutations such as 'Last()' to 'First()'.";

        public string Name => "Linq Mutation Analyzer";


        public IEnumerable<LinqMutation> AnalyzeMutations(MethodDefinition method, MutationLevel currentMutationLevel, IDictionary<Instruction, SequencePoint> debug = null)
        {
            if (method?.Body == null) return Enumerable.Empty<LinqMutation>();

            var lineNumber = -1;

            var mutations = new List<LinqMutation>();

            foreach (var instruction in method.Body.Instructions)
            {
                // if the instruction is not a call to a GenericInstanceMethod we dont need to mutate it here
                if (instruction.OpCode != OpCodes.Call || instruction.Operand?.GetType() != typeof(GenericInstanceMethod)) continue;

                var originalMethod = (GenericInstanceMethod)instruction.Operand;
                var originalMethodDefinition = originalMethod.Resolve();

                // the library housing the linq methods
                var linqModule = ModuleDefinition.ReadModule(typeof(Enumerable).Assembly.Location);

                // If the name of the orignalMethod is not in the dictionary or if the originalMethod did not come from the linqModule, we dont need to mutate it here
                if (!MethodsToMutate.ContainsKey(originalMethod.Name) || originalMethodDefinition.Module.Name != linqModule.Name) continue;

                var originalParameters = originalMethodDefinition.Parameters.Select(item => item.ParameterType).ToArray();

                // Loop through each mutation as defined in MethodsToMutate
                foreach (var (mutationLevel, newMethodName) in MethodsToMutate[originalMethodDefinition.Name])
                {
                    // If the mutationLevel of the mutation is higher then the currentMutatationLevel (console argument), skip the mutation
                    if (currentMutationLevel < mutationLevel) continue;

                    // Get the linq method we want to use as replacement
                    var replacementMethod = typeof(Enumerable).GetMethods().FirstOrDefault(m => m.Name == newMethodName &&
                        m.IsGenericMethodDefinition && ParametersAreEqual(originalParameters, m.GetParameters()));
                    
                    // If the specified linq method could not be found, we probably want to log a error and skip the instruction
                    // for now, assert the linq method can always be found
                    if (replacementMethod == null)
                    {
                        // TODO handle what to do if the linq method could not be found (also change comment above)
                        continue;
                    }

                    mutations.Add(new LinqMutation(instruction, originalMethod, replacementMethod));
                }
            }

            return mutations;
        }

        private static bool ParametersAreEqual(IReadOnlyList<TypeReference> originalParameters, IReadOnlyList<ParameterInfo> newParameters)
        {
            if (originalParameters.Count != newParameters.Count) return false;

            for (var i = 0; i < newParameters.Count; i++)
            {
                if (newParameters[i].ParameterType.Name != originalParameters[i].Name) return false;
            }

            return true;
        }
    }
}