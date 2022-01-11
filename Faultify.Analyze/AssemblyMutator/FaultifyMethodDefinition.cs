using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Faultify.Analyze.Analyzers;
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
    ///     Contains all of the instructions and mutations within the scope of a method definition.
    /// </summary>
    public class FaultifyMethodDefinition : IMutationProvider, IFaultifyMemberDefinition
    {
        private readonly HashSet<IMutationAnalyzer<ArrayMutation, MethodDefinition>> _arrayMutationAnalyzers;
        private readonly HashSet<IMutationAnalyzer<ListMutation, MethodDefinition>> _listMutationAnalyzers;

        private readonly HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>>
            _constantReferenceMutationAnalyers;

        private readonly HashSet<IMutationAnalyzer<OpCodeMutation, MethodDefinition>> _opcodeMethodAnalyzers;

        private readonly HashSet<IMutationAnalyzer<VariableMutation, MethodDefinition>> _variableMutationAnalyzers;


        /// <summary>
        ///     Underlying Mono.Cecil TypeDefinition.
        /// </summary>
        public readonly MethodDefinition MethodDefinition;

        public FaultifyMethodDefinition(MethodDefinition methodDefinition,
            HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>> constantReferenceMutationAnalyers,
            HashSet<IMutationAnalyzer<OpCodeMutation, MethodDefinition>> opcodeMethodAnalyzers,
            HashSet<IMutationAnalyzer<VariableMutation, MethodDefinition>> variableMutationAnalyzers,
            HashSet<IMutationAnalyzer<ArrayMutation, MethodDefinition>> arrayMutationAnalyzers,
            HashSet<IMutationAnalyzer<ListMutation, MethodDefinition>> listMutationAnalyzers)
        {
            MethodDefinition = methodDefinition;
            _constantReferenceMutationAnalyers = constantReferenceMutationAnalyers;
            _opcodeMethodAnalyzers = opcodeMethodAnalyzers;
            _variableMutationAnalyzers = variableMutationAnalyzers;
            _arrayMutationAnalyzers = arrayMutationAnalyzers;
            _listMutationAnalyzers = listMutationAnalyzers;
        }

        public int IntHandle => MethodDefinition.MetadataToken.ToInt32();

        /// <summary>
        ///     Full assembly name of this method.
        /// </summary>
        public string AssemblyQualifiedName => MethodDefinition.FullName;

        public string Name => MethodDefinition.Name;

        public EntityHandle Handle => MetadataTokens.EntityHandle(IntHandle);

        /// <summary>
        ///     Returns all available mutations within the scope of this method.
        /// </summary>
        public IEnumerable<IMutationGrouping<IMutation>> AllMutations(MutationLevel mutationLevel)
        {
            if (MethodDefinition.Body == null)
                return Enumerable.Empty<IMutationGrouping<IMutation>>();

            MethodDefinition.Body.SimplifyMacros();

            return ((IEnumerable<IMutationGrouping<IMutation>>) OpCodeMutations(mutationLevel))
                .Concat(VariableMutations(mutationLevel))
                .Concat(ArrayMutations(mutationLevel))
                .Concat(ArrayMutations(mutationLevel))
                .Concat(ListMutations(mutationLevel));
        }

        /// <summary>
        ///     Returns all possible opcode mutations from the method its instructions.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IMutationGrouping<OpCodeMutation>> OpCodeMutations(MutationLevel mutationLevel)
        {
            foreach (var analyzer in _opcodeMethodAnalyzers)
            {
                if (MethodDefinition.Body?.Instructions != null)
                {
                    IMutationGrouping<OpCodeMutation> mutations = analyzer.AnalyzeMutations(
                        MethodDefinition,
                        mutationLevel,
                        MethodDefinition.DebugInformation.GetSequencePointMapping());

                    yield return mutations;
                }
            }
        }

        /// <summary>
        ///     Returns all possible constant mutations from the method its instructions.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IMutationGrouping<ConstantMutation>> ConstantReferenceMutations(MutationLevel mutationLevel)
        {
            var fieldReferences = MethodDefinition.Body.Instructions
                .OfType<FieldReference>();

            foreach (var field in fieldReferences)
            foreach (var analyzer in _constantReferenceMutationAnalyers)
            {
                IMutationGrouping<ConstantMutation> mutations = analyzer.AnalyzeMutations(field.Resolve(), mutationLevel);
                if (mutations.Any())
                {
                    yield return mutations;
                }
            }
        }

        /// <summary>
        ///     Returns all possible list mutations from the method its instructions.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IMutationGrouping<ListMutation>> ListMutations(MutationLevel mutationLevel)
        {
            return _listMutationAnalyzers.Select(analyzer => analyzer.AnalyzeMutations(MethodDefinition, mutationLevel));
        }

        /// <summary>
        ///     Returns all possible variable mutations from the method its instructions.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IMutationGrouping<VariableMutation>> VariableMutations(MutationLevel mutationLevel)
        {
            return _variableMutationAnalyzers.Select(analyzer => analyzer.AnalyzeMutations(MethodDefinition, mutationLevel));
        }

        /// <summary>
        ///     Returns all possible array mutations from the method its instructions.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IMutationGrouping<ArrayMutation>> ArrayMutations(MutationLevel mutationLevel)
        {
            return _arrayMutationAnalyzers.Select(analyzer => analyzer.AnalyzeMutations(MethodDefinition, mutationLevel));
        }
    }
}