﻿using System.Collections.Generic;
using Faultify.Analyze.Analyzers;
using Faultify.Analyze.Groupings;
using Faultify.Analyze.Mutation;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Analyze.ConstantAnalyzer
{
    /// <summary>
    ///     Analyzer that searches for possible constant mutations inside a type definition, this class is the parent class to
    ///     all constant analyzers.
    /// </summary>
    public abstract class ConstantMutationAnalyzer : IMutationAnalyzer<ConstantMutation, FieldDefinition>
    {
        public abstract string Description { get; }

        public abstract string Name { get; }

        public abstract IMutationGrouping<ConstantMutation> AnalyzeMutations(FieldDefinition field,
            MutationLevel mutationLevel, IDictionary<Instruction, SequencePoint> debug = null);
    }
}