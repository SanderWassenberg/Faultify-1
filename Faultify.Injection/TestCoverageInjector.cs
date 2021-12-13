using System;
using System.IO;
using System.Linq;
using Faultify.TestRunner.Shared;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Faultify.Injection
{
    /// <summary>
    ///     Injects coverage code into an assembly.
    /// </summary>
    public class TestCoverageInjector
    {
        private static readonly Lazy<TestCoverageInjector> Lazy =
            new(() => new TestCoverageInjector());

        private readonly string _currentAssemblyPath = typeof(TestCoverageInjector).Assembly.Location;
        private readonly MethodDefinition _initializeMethodDefinition;
        private readonly MethodDefinition _registerTargetCoverage;
        private readonly MethodDefinition _beginRegisterTestCoverage;
        private readonly MethodDefinition _endRegisterTestCoverage;

        private TestCoverageInjector()
        {
            // Get the ModuleDefintion for the Assembly in which the injection methods are located.
            using var injectionAssembly = ModuleDefinition.ReadModule(_currentAssemblyPath);

            // Local function for getting the MethodDefinitions, it needs to be repeated for each method that we inject.
            MethodDefinition GetMethodDefinition(string name) =>
                injectionAssembly.Types.SelectMany(x => x.Methods.Where(y => y.Name == name)).First();

            // Retrieve the method definitions for the register functions. 
            _registerTargetCoverage = GetMethodDefinition(nameof(CoverageRegistry.RegisterTargetCoverage));
            _beginRegisterTestCoverage = GetMethodDefinition(nameof(CoverageRegistry.BeginRegisterTestCoverage));
            _endRegisterTestCoverage = GetMethodDefinition(nameof(CoverageRegistry.EndRegisterTestCoverage));
            _initializeMethodDefinition = GetMethodDefinition(nameof(CoverageRegistry.Initialize));

            if (_initializeMethodDefinition is null ||
                _registerTargetCoverage is null ||
                _beginRegisterTestCoverage is null ||
                _endRegisterTestCoverage is null)
            {
                throw new Exception("Testcoverage Injector could not initialize injection methods");
            }
        }

        public static TestCoverageInjector Instance => Lazy.Value;

        /// <summary>
        ///     Injects a call to <see cref="CoverageRegistry" /> Initialize method into the
        ///     <Module>
        ///         initialize function.
        ///         For more info see: https://einaregilsson.com/module-initializers-in-csharp/
        /// </summary>
        /// <param name="toInjectModule"></param>
        public void InjectModuleInit(ModuleDefinition toInjectModule)
        {
            File.Copy(_currentAssemblyPath,
                Path.Combine(Path.GetDirectoryName(toInjectModule.FileName), Path.GetFileName(_currentAssemblyPath)),
                true);

            const MethodAttributes moduleInitAttributes = MethodAttributes.Static
                                                          | MethodAttributes.Assembly
                                                          | MethodAttributes.SpecialName
                                                          | MethodAttributes.RTSpecialName;

            var assembly = toInjectModule.Assembly;
            var moduleType = assembly.MainModule.GetType("<Module>");
            var method = toInjectModule.ImportReference(_initializeMethodDefinition);

            // Get or create ModuleInit method
            var cctor = moduleType.Methods.FirstOrDefault(moduleTypeMethod => moduleTypeMethod.Name == ".cctor");
            if (cctor == null)
            {
                cctor = new MethodDefinition(".cctor", moduleInitAttributes, method.ReturnType);
                moduleType.Methods.Add(cctor);
            }

            var isCallAlreadyDone = cctor.Body.Instructions.Any(instruction =>
                instruction.OpCode == OpCodes.Call && instruction.Operand == method);

            // If the method is not called, we can add it
            if (!isCallAlreadyDone)
            {
                var ilProcessor = cctor.Body.GetILProcessor();
                var retInstruction =
                    cctor.Body.Instructions.FirstOrDefault(instruction => instruction.OpCode == OpCodes.Ret);
                var callMethod = ilProcessor.Create(OpCodes.Call, method);

                if (retInstruction == null)
                {
                    // If a ret instruction is not present, add the method call and ret
                    // Insert instruction that loads the meta data token as parameter for the register method.
                    ilProcessor.Append(callMethod);
                    ilProcessor.Emit(OpCodes.Ret);
                }
                else
                {
                    // If a ret instruction is already present, just add the method to call before
                    ilProcessor.InsertBefore(retInstruction, callMethod);
                }
            }
        }

        /// <summary>
        ///     Injects the required references for the `Faultify.Injection` <see cref="CoverageRegistry" /> code into the given
        ///     module.
        /// </summary>
        /// <param name="module"></param>
        public void InjectAssemblyReferences(ModuleDefinition module)
        {
            // Find the references for `Faultify.TestRunner.Shared` and copy it over to the module directory and add it as reference.
            var assembly = typeof(MutationCoverage).Assembly;

            var dest = Path.Combine(Path.GetDirectoryName(module.FileName), Path.GetFileName(assembly.Location));
            File.Copy(assembly.Location, dest, true);

            var shared =
                _registerTargetCoverage.Module.AssemblyReferences.First(x => x.Name == assembly.GetName().Name);

            module.AssemblyReferences.Add(shared);
            module.AssemblyReferences.Add(_registerTargetCoverage.Module.Assembly.Name);
        }

        /// <summary>
        ///     Injects the coverage register function for each method in the given module.
        /// </summary>
        public void InjectTargetCoverage(ModuleDefinition module)
        {
            foreach (var typeDefinition in module.Types.Where(x => !x.Name.StartsWith("<")))
            {
                foreach (var method in typeDefinition.Methods)
                {
                    if (method.Body == null) continue;

                    var processor = method.Body.GetILProcessor();

                    // Insert instructions that load the MetaDataToken as parameter for the register method.
                    var assemblyName = processor.Create(OpCodes.Ldstr, method.Module.Assembly.Name.Name);
                    var entityHandle = processor.Create(OpCodes.Ldc_I4, method.MetadataToken.ToInt32());

                    // Insert instruction that calls the register function.
                    var callInstruction = processor.Create(OpCodes.Call, method.Module.ImportReference(_registerTargetCoverage));

                    method.Body.Instructions.Insert(0, callInstruction);
                    method.Body.Instructions.Insert(0, entityHandle);
                    method.Body.Instructions.Insert(0, assemblyName);
                }
            }
        }

        /// <summary>
        ///     Injects the test register function for each test method in the given module.
        /// </summary>
        public void InjectTestCoverage(ModuleDefinition module)
        {
            module.AssemblyReferences.Add(_beginRegisterTestCoverage.Module.Assembly.Name);
            module.AssemblyReferences.Add(
                _registerTargetCoverage.Module.AssemblyReferences.First(x => x.Name == "Faultify.TestRunner.Shared"));

            foreach (var typeDefinition in module.Types.Where(x => !x.Name.StartsWith("<")))
            {
                var testMethods = typeDefinition.Methods.Where(m =>
                    m.HasCustomAttributes && m.CustomAttributes.Any(x =>
                        x.AttributeType.Name == "TestCaseAttribute" ||
                        x.AttributeType.Name == "TestAttribute" ||
                        x.AttributeType.Name == "TestMethodAttribute" ||
                        x.AttributeType.Name == "FactAttribute"));

                foreach (var method in testMethods)
                {
                    if (method.Body == null) continue;

                    var processor = method.Body.GetILProcessor();
                    
                    // The string with which the register-method identifies the current test.
                    Instruction entityHandleInstruction = processor.Create(OpCodes.Ldstr, method.DeclaringType.FullName + "." + method.Name);

                    // The register methods.
                    Instruction beginRegisterInstruction = processor.Create(OpCodes.Call, method.Module.ImportReference(_beginRegisterTestCoverage));
                    Instruction endRegisterInstruction =   processor.Create(OpCodes.Call, method.Module.ImportReference(_endRegisterTestCoverage));

                    // Insert the method signaling the start of a test, insert at index 0.
                    method.Body.Instructions.Insert(0, beginRegisterInstruction); // method call
                    method.Body.Instructions.Insert(0, entityHandleInstruction);  // its argument

                    // Insert the method signaling the end of the test. This needs to be insterted in place of the last instruction, which is 'ret'.
                    // The Count-1 is therefore very important, if you don't the instruction is placed after 'ret', which makes it unreachable code.
                    method.Body.Instructions.Insert(method.Body.Instructions.Count - 1, endRegisterInstruction);
                }
            }
        }
    }
}