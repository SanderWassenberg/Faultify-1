using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using FieldDefinition = Mono.Cecil.FieldDefinition;
using MethodDefinition = Mono.Cecil.MethodDefinition;

namespace Faultify.Analyze.AssemblyMutator
{
    /// <summary>
    ///     Represents a raw method definition.
    /// </summary>
    public class FaultifyMethodDefinition : IMutationProvider, IFaultifyMemberDefinition
    {
        private readonly HashSet<IMutationAnalyzer<ArrayMutation, MethodDefinition>> _arrayMutationAnalyzers;

        private readonly HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>>
            _constantReferenceMutationAnalyers;

        private readonly HashSet<IMutationAnalyzer<OpCodeMutation, Instruction>> _opcodeMethodAnalyzers;

        private readonly HashSet<IMutationAnalyzer<VariableMutation, MethodDefinition>> _variableMutationAnalyzers;

        private readonly HashSet<IMutationAnalyzer<LinqMutation, MethodDefinition>> _linqMutationAnalyzers;

        /// <summary>
        ///     Underlying Mono.Cecil TypeDefinition.
        /// </summary>
        public readonly MethodDefinition MethodDefinition;

        public FaultifyMethodDefinition(MethodDefinition methodDefinition,
            HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>> constantReferenceMutationAnalyers,
            HashSet<IMutationAnalyzer<OpCodeMutation, Instruction>> opcodeMethodAnalyzers,
            HashSet<IMutationAnalyzer<VariableMutation, MethodDefinition>> variableMutationAnalyzers,
            HashSet<IMutationAnalyzer<ArrayMutation, MethodDefinition>> arrayMutationAnalyzers,
            HashSet<IMutationAnalyzer<LinqMutation, MethodDefinition>> linqMutationAnalyzers)
        {
            MethodDefinition = methodDefinition;
            _constantReferenceMutationAnalyers = constantReferenceMutationAnalyers;
            _opcodeMethodAnalyzers = opcodeMethodAnalyzers;
            _variableMutationAnalyzers = variableMutationAnalyzers;
            _arrayMutationAnalyzers = arrayMutationAnalyzers;
            _linqMutationAnalyzers = linqMutationAnalyzers;
        }

        public int IntHandle => MethodDefinition.MetadataToken.ToInt32();

        /// <summary>
        ///     Full assembly name of this method.
        /// </summary>
        public string AssemblyQualifiedName => MethodDefinition.FullName;

        public string Name => MethodDefinition.Name;

        public EntityHandle Handle => MetadataTokens.EntityHandle(IntHandle);

        public IEnumerable<IMutationGrouping<IMutation>> AllMutations(MutationLevel mutationLevel)
        {
            if (MethodDefinition.Body == null)
                return Enumerable.Empty<IMutationGrouping<IMutation>>();

            MethodDefinition.Body.SimplifyMacros();

            var allMutationsList = new List<IMutationGrouping<IMutation>>();

            allMutationsList.AddRange(OpCodeMutations(mutationLevel));
            allMutationsList.AddRange(VariableMutations(mutationLevel));
            allMutationsList.AddRange(ArrayMutations(mutationLevel));
            allMutationsList.AddRange(LinqMutations(mutationLevel));

            return allMutationsList;
        }

        /// <summary>
        ///     Returns all possible mutations from the method its instructions.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<OpCodeMutationGrouping> OpCodeMutations(MutationLevel mutationLevel)
        {
            foreach (var analyzer in _opcodeMethodAnalyzers)
                if (MethodDefinition.Body?.Instructions != null)
                    foreach (var instruction in MethodDefinition.Body?.Instructions)
                    {
                        var mutations = analyzer.AnalyzeMutations(instruction, mutationLevel,
                            MethodDefinition.DebugInformation.GetSequencePointMapping()).ToList();

                        if (mutations.Any())
                            yield return new OpCodeMutationGrouping
                            {
                                Key = instruction.OpCode.ToString(),
                                Mutations = mutations,
                                AnalyzerName = analyzer.Name,
                                AnalyzerDescription = analyzer.Description
                            };
                    }
        }

        public IEnumerable<ConstMutationGrouping> ConstantReferenceMutations(MutationLevel mutationLevel)
        {
            var fieldReferences = MethodDefinition.Body.Instructions
                .OfType<FieldReference>();

            foreach (var field in fieldReferences)
                foreach (var analyzer in _constantReferenceMutationAnalyers)
                {
                    var mutations = analyzer.AnalyzeMutations(field.Resolve(), mutationLevel);

                    yield return new ConstMutationGrouping
                    {
                        AnalyzerName = analyzer.Name,
                        AnalyzerDescription = analyzer.Description,
                        Key = field.Name,
                        Mutations = mutations
                    };
                }
        }

        public IEnumerable<VariableMutationGrouping> VariableMutations(MutationLevel mutationLevel)
        {
            return _variableMutationAnalyzers.Select(analyzer => new VariableMutationGrouping
            {
                Mutations = analyzer.AnalyzeMutations(MethodDefinition, mutationLevel,
                    MethodDefinition.DebugInformation.GetSequencePointMapping()),
                Key = MethodDefinition.Name,
                AnalyzerName = analyzer.Name,
                AnalyzerDescription = analyzer.Description
            });
        }

        public IEnumerable<ArrayMutationGrouping> ArrayMutations(MutationLevel mutationLevel)
        {
            return _arrayMutationAnalyzers.Select(analyzer => new ArrayMutationGrouping
            {
                Mutations = analyzer.AnalyzeMutations(MethodDefinition, mutationLevel),
                Key = MethodDefinition.Name,
                AnalyzerName = analyzer.Name,
                AnalyzerDescription = analyzer.Description
            });
        }

        public IEnumerable<LinqMutationGrouping> LinqMutations(MutationLevel mutationLevel)
        {
            return _linqMutationAnalyzers.Select(analyzer => new LinqMutationGrouping
            {
                Mutations = analyzer.AnalyzeMutations(MethodDefinition, mutationLevel,
                    MethodDefinition.DebugInformation.GetSequencePointMapping()),
                Key = MethodDefinition.Name,
                AnalyzerName = analyzer.Name,
                AnalyzerDescription = analyzer.Description
            });
        }

        public class VariableMutationGrouping : BaseGrouping<VariableMutation>
        {
        }

        public class ArrayMutationGrouping : BaseGrouping<ArrayMutation>
        {
        }
    }
}