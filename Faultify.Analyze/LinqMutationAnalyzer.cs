using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Faultify.Analyze.Analyzers;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodDefinition = Mono.Cecil.MethodDefinition;
using ModuleDefinition = Mono.Cecil.ModuleDefinition;
using OpCodes = Mono.Cecil.Cil.OpCodes;
using SequencePoint = Mono.Cecil.Cil.SequencePoint;
using TypeReference = Mono.Cecil.TypeReference;

namespace Faultify.Analyze
{
    /// <summary>
    ///     Analyzer that searches for possible LINQ mutations such as 'Last()' to 'First()'."
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


        public IMutationGrouping<LinqMutation> AnalyzeMutations(MethodDefinition method, MutationLevel currentMutationLevel, IDictionary<Instruction, SequencePoint> debug = null)
        {
            var grouping = new MutationGrouping<LinqMutation>()
            {
                AnalyzerName = Name,
                AnalyzerDescription = Description
            };

            if (method?.Body == null) return grouping;

            var mutations = new List<LinqMutation>();

            foreach (var instruction in method.Body.Instructions)
            {
                // if the instruction is not a call to a GenericInstanceMethod we dont need to mutate it here
                // TODO: if e.g. Sum() doesnt have a predicate, it is not a a GenericInstanceMethod, but a method Reference
                // this code and the code in LinqMutation will have to be reviewed at some point, but due to lack of documentation
                // in Mono.Cecil this is not feasible for the current project.
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
                    
                    // If the specified linq method could not be found, log an error and skip the instruction
                    if (replacementMethod == null)
                    {
                        // TODO once logging has been made global, log the following message:
                        var errorMessage = $"The specified LINQ method ('{newMethodName}') could not be found with the same parameters of the original method." +
                                           $" This could be due to a change in the LINQ library";
                        continue;
                    }

                    mutations.Add(new LinqMutation(instruction, originalMethod, replacementMethod));
                }
            }

            grouping.Mutations = mutations;
            return grouping;
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