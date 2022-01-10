using Faultify.Core.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;

namespace Faultify.Analyze.Strategies
{
    public class EmptyListStrategy : IMutationStrategy
    {
        private readonly MethodDefinition _methodDefinition;
        private TypeReference _type;

        public EmptyListStrategy(MethodDefinition methodDefinition)
        {
            _methodDefinition = methodDefinition;
            _type = methodDefinition.ReturnType.GetElementType();
        }

        public string GetStrategyStringForReport() => "Emptied the list";

        public void Mutate()
        {
            _methodDefinition.Body.SimplifyMacros();

            var instructions = _methodDefinition.Body.Instructions;

            // Loop over all instructions, until the one making the list is found.
            // Check each instruction, then immediately increase the index.
            // This way, when exiting the loop, the index lands right after
            // the instruction which creates the list.
            int index = 0;
            while (index < instructions.Count
                && !instructions[index++].IsListInitialiser()) ;

            // Assume we're on the instruction following the newobj that makes the list.
            // There is one list reference on the stack (stackSize = 1). It will stay there for as long as the list
            // initializer does its thing. When the list reference disappears (stackSize == 0),
            // the list initializer is done, and we can stop removing instructions.
            int stackSize = 1;
            do
            {
                var currentInstruction = instructions[index];

                /*
                 This code is buggy and cannot handle initializers with branching such as:
                    new List<int>{ (cond ? a : b), c, d, e }
                 A possible solution is show below.
                 */

                //if (/* currentInstruction is a jump forward (backwards causes loop and can be skipped) */)
                //{
                //    /*do jump*/
                //    continue;
                //}

                UpdateStackSize(ref stackSize, currentInstruction);
                if (stackSize <= 0) break;
                instructions.RemoveAt(index);
                // Everytime an instruction is removed at an index, everything after shifts
                // back one, so index can stay where it is. It's like pressing 'Delete'.
            } while (true);

            _methodDefinition.Body.OptimizeMacros();
        }

        /// <summary>
        /// Updates the given stackSize according to the behaviour of this particular instruction.
        /// </summary>
        private static void UpdateStackSize(ref int stackSize, Instruction instr)
        {
            ApplyStackBehaviour(ref stackSize, instr.OpCode.StackBehaviourPop, instr);
            ApplyStackBehaviour(ref stackSize, instr.OpCode.StackBehaviourPush, instr);
        }

        /// <summary>
        /// Updates the given stackSize according to the given StackBehaviour. The StackbeHaviour may have a different result depending on the instruction it is a part of, which is why that needs to be passed too.
        /// </summary>
        private static void ApplyStackBehaviour(ref int stackSize, StackBehaviour sb, Instruction instr)
        {
            switch (sb)
            {
                case StackBehaviour.Popi_popi_popi: // Best name ever
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    stackSize -= 3;
                    return;

                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    stackSize -= 2;
                    return;

                case StackBehaviour.Popref:
                case StackBehaviour.Popi:
                case StackBehaviour.Pop1:
                    stackSize--;
                    return ;

                case StackBehaviour.Pop0:
                case StackBehaviour.Push0:
                    // push/pop nothing, do nothing
                    return;

                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stackSize++;
                    return;

                case StackBehaviour.Push1_push1:
                    stackSize += 2;
                    return;

                case StackBehaviour.PopAll:
                    stackSize = 0; // <- this right here is the reason why this method can't just return an integer
                    return;

                case StackBehaviour.Varpop: // This behaviour may pop a varying number of items from the stack
                    switch (instr.OpCode.Code)
                    {
                        case Code.Call:
                        case Code.Newobj:
                            // All arguments to the function placed on the stack get consumed
                            stackSize -= ((MethodReference) instr.Operand).Parameters.Count;
                            return;

                        case Code.Calli:
                        case Code.Callvirt:
                            // All arguments to the function placed on the stack get consumed,
                            // as well as the object on which the method is called (callvirt),
                            // or a reference to the function itself (calli)
                            stackSize -= ((MethodReference) instr.Operand).Parameters.Count + 1;
                            return;

                        default:
                            // Unknown opcode. You can look up what this opcode does on:
                            // https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.call?view=net-6.0
                            // Please update the switch above once you know how many items this opcode pops off the stack.
                            throw new NotImplementedException($"Don't know how many items get consumed from the stack by this opcode: {instr.OpCode.Code}\nPlease modify {nameof(EmptyListStrategy)} to account for it.");
                    }

                case StackBehaviour.Varpush: // This behaviour may push a varying number of items onto the stack
                    switch (instr.OpCode.Code)
                    {
                        case Code.Call:
                        case Code.Calli:
                        case Code.Callvirt:
                        case Code.Newobj:
                            // A method always pushes one item onto the stack, unless it returns void.
                            if (((MethodReference) instr.Operand).ReturnType.ToString() != "System.Void")
                            {
                                stackSize++;
                            }

                            return;

                        default:
                            // Unknown opcode. You can look up what this opcode does on:
                            // https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.call?view=net-6.0
                            // Please update the switch above once you know how many items this opcode pushed onto the stack.
                            throw new NotImplementedException($"Don't know how many items get pushed onto the stack by this opcode: {instr.OpCode.Code}\nPlease modify {nameof(EmptyListStrategy)} to account for it.");
                    }
            }
        }
    }
}
