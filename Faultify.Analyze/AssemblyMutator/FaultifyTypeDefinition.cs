﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Faultify.Analyze.Analyzers;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil.Cil;
using FieldDefinition = Mono.Cecil.FieldDefinition;
using MethodDefinition = Mono.Cecil.MethodDefinition;
using TypeDefinition = Mono.Cecil.TypeDefinition;

namespace Faultify.Analyze.AssemblyMutator
{
    /// <summary>
    ///     Represents a raw type definition and provides access to its fields and methods..
    /// </summary>
    public class FaultifyTypeDefinition : IFaultifyMemberDefinition, IMutationProvider
    {
        private readonly HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>> _constantAnalyzers;

        public FaultifyTypeDefinition(TypeDefinition typeDefinition,
            HashSet<IMutationAnalyzer<OpCodeMutation, MethodDefinition>> opcodeAnalyzers,
            HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>> fieldAnalyzers,
            HashSet<IMutationAnalyzer<VariableMutation, MethodDefinition>> variableMutationAnalyzers,
            HashSet<IMutationAnalyzer<ArrayMutation, MethodDefinition>> arrayMutationAnalyzers,
            HashSet<IMutationAnalyzer<ListMutation, MethodDefinition>> listMutationAnalyzers,
            HashSet<IMutationAnalyzer<LinqMutation, MethodDefinition>> linqMutationAnalyzers
        )
        {
            _constantAnalyzers = fieldAnalyzers;
            TypeDefinition = typeDefinition;

            Fields = TypeDefinition.Fields.Select(x => new FaultifyFieldDefinition(x, fieldAnalyzers)).ToList();
            Methods = TypeDefinition.Methods.Select(x =>
                    new FaultifyMethodDefinition(x, fieldAnalyzers, opcodeAnalyzers, variableMutationAnalyzers,
                        arrayMutationAnalyzers, listMutationAnalyzers, linqMutationAnalyzers))
                .ToList();
        }

        /// <summary>
        ///     The fields in this type.
        ///     For example: const, static, non-static fields.
        /// </summary>
        public List<FaultifyFieldDefinition> Fields { get; }

        /// <summary>
        ///     The methods in this type.
        /// </summary>
        public List<FaultifyMethodDefinition> Methods { get; }

        public TypeDefinition TypeDefinition { get; }

        public string Name => TypeDefinition.Name;
        public EntityHandle Handle => MetadataTokens.EntityHandle(TypeDefinition.MetadataToken.ToInt32());
        public string AssemblyQualifiedName => TypeDefinition.FullName;

        public IEnumerable<IMutationGrouping<IMutation>> AllMutations(MutationLevel mutationLevel)
        {
            foreach (var analyzer in _constantAnalyzers)
            {
                foreach (var field in TypeDefinition.Fields)
                {
                    IMutationGrouping<IMutation> mutations = analyzer.AnalyzeMutations(field, mutationLevel);
                    if (mutations.Any())
                    {
                        yield return mutations;
                    }
                }
            }
        }
    }
}