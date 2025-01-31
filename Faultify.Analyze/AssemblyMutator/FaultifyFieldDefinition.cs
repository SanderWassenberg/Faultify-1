﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Faultify.Analyze.Analyzers;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using FieldDefinition = Mono.Cecil.FieldDefinition;

namespace Faultify.Analyze.AssemblyMutator
{
    /// <summary>
    ///     Represents a raw field definition.
    /// </summary>
    public class FaultifyFieldDefinition : IMutationProvider, IFaultifyMemberDefinition
    {
        private readonly HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>> _fieldAnalyzers;

        /// <summary>
        ///     Underlying Mono.Cecil FieldDefinition.
        /// </summary>
        private readonly FieldDefinition _fieldDefinition;

        public FaultifyFieldDefinition(FieldDefinition fieldDefinition,
            HashSet<IMutationAnalyzer<ConstantMutation, FieldDefinition>> fieldAnalyzers)
        {
            _fieldDefinition = fieldDefinition;
            _fieldAnalyzers = fieldAnalyzers;
        }

        public string AssemblyQualifiedName => _fieldDefinition.FullName;
        public string Name => _fieldDefinition.Name;
        public EntityHandle Handle => MetadataTokens.EntityHandle(_fieldDefinition.MetadataToken.ToInt32());

        public IEnumerable<IMutationGrouping<IMutation>> AllMutations(MutationLevel mutationLevel)
        {
            return ConstantFieldMutations(mutationLevel);
        }

        /// <summary>
        ///     Returns possible constant field mutations.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IMutationGrouping<ConstantMutation>> ConstantFieldMutations(MutationLevel mutationLevel)
        {
            foreach (IMutationAnalyzer<ConstantMutation, FieldDefinition> analyzer in _fieldAnalyzers)
            {
                IMutationGrouping<ConstantMutation> mutationGrouping = analyzer.AnalyzeMutations(_fieldDefinition, mutationLevel);

                if (mutationGrouping.Any())
                {
                    yield return mutationGrouping;
                }
            }
        }
    }
}