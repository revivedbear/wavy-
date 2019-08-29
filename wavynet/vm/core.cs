﻿/*
 * James Clarke
 * 22/08/19
 */

using System;
using System.Collections.Generic;
using System.Threading;
using wavynet.vm.data.items;

namespace wavynet.vm
{
    /* 
     * Represents a vm core
     * Performs fde cycle
    */
    public class Core
    {
        private VM vm;
        public Thread thread;
        // Holds information about the current state of this core
        public CoreState state;
        // Program counter
        public int pc;
        // Store the operations the core has carried out
        public TraceBack traceback;
        // The bytecode that the core is executing
        public BytecodeInstance[] bytecode;
        // The function stack that this core is using
        public FuncStack func_stack;
        // The execution stack that this core is using
        public ExecStack exec_stack;

        // The max Program Counter, this determines program length
        public const int MAX_PC = 65536; // 2^16

        public Core(VM vm, int id)
        {
            this.vm = vm;
            this.state = CoreState.setup(id);
            this.pc = 0;
            this.traceback = new TraceBack();
            this.func_stack = new FuncStack(this);
            this.exec_stack = new ExecStack(this);
        }

        // Setup the core thread
        public void setup(BytecodeInstance[] sequence)
        {
            this.bytecode = sequence;
            this.state.opcode_count = sequence.Length;
            this.state.multi_core_state = MultiCoreState.READY;
            // Create a new EventWaitHandle to allow us to check when this core is finished
            // Create a new Thread and accociate the handle with it
            this.thread = new Thread(() => {
                this.evaluate_sequence();
                });
        }

        // Run the core & start executing code
        public void run()
        {
            this.thread.Start();
        }

        // When we want the core to suspend execution
        public void suspend()
        {
            this.ASSERT_ERR(this.state.multi_core_state == MultiCoreState.SUSPENDED, CoreErrorType.INVALID_MULTICORE_STATE, "Cannot suspend core that has already been suspended!");
            this.state.multi_core_state = MultiCoreState.SUSPENDED;

        }

        // When we want to resume the core operation
        public void resume()
        {
            this.ASSERT_ERR(this.state.multi_core_state != MultiCoreState.SUSPENDED, CoreErrorType.INVALID_MULTICORE_STATE, "Cannot resume core that has not been suspended!");
            this.state.multi_core_state = MultiCoreState.RUNNING;
        }

        // Called when we want to close this core thread
        public CoreState close()
        {
            // Return an END state
            this.state.currently_interpreting = false;
            // Change the state of the multi_core_state
            this.state.multi_core_state = MultiCoreState.DONE;
            this.vm.core_manager.close_core(this.state.id);
            return this.state;
        }

        // Main fde cycle
        public CoreState evaluate_sequence()
        {
            // Initialise the state of the multi_core_state
            this.state.multi_core_state = MultiCoreState.RUNNING;

            // Check we have not reached the end
            while (!end())
            {
                // Only execute if the core has not been suspended
                if (this.state.multi_core_state != MultiCoreState.SUSPENDED)
                {
                    BytecodeInstance bytecode = get_next();

                    int op = get_op(bytecode);
                    int arg = -1;

                    if (has_arg(bytecode))
                        arg = get_arg(bytecode);

                    if (VM.INSTR_DEBUG)
                    {
                        if (has_arg(bytecode))
                            Console.WriteLine("op: " + (Bytecode.Opcode)op + "arg: " + arg);
                        else
                            Console.WriteLine("op: " + op);
                    }

                    // Surround the execute phase in a try catch, this is so the vm can return safely to the error handling
                    // without breaking other vm components
                    try
                    {
                        switch (op)
                        {
                            case (int)Bytecode.Opcode.END:
                                {
                                    // Return an END state
                                    this.state.currently_interpreting = false;
                                    return this.state;
                                }
                            case (int)Bytecode.Opcode.NOP:
                                {
                                    goto_next();
                                    break;
                                }
                            case (int)Bytecode.Opcode.POP_EXEC:
                                {
                                    pop_exec();
                                    goto_next();
                                    break;
                                }
                            case (int)Bytecode.Opcode.LD_LIT:
                                {
                                    push_exec(request_bank_item(data.Bank.Type.LBank, arg));
                                    release_bank_item(data.Bank.Type.LBank, arg);
                                    goto_next();
                                    break;
                                }
                            case (int)Bytecode.Opcode.LD_VAR:
                                {
                                    int id = expect_int(pop_exec());
                                    WavyItem item = request_bank_item(data.Bank.Type.MBank, id);
                                    release_bank_item(data.Bank.Type.MBank, id);
                                    goto_next();
                                    break;
                                }
                            case (int)Bytecode.Opcode.GOTO:
                                {
                                    this.pc += arg;
                                    break;
                                }
                            case (int)Bytecode.Opcode.IF_ZERO:
                                {
                                    if (expect_numeric(pop_exec()) == 0)
                                    {
                                        goto_next(); break;
                                    }
                                    this.pc += arg; break;
                                }
                            case (int)Bytecode.Opcode.IF_NZERO:
                                {
                                    if (expect_numeric(pop_exec()) != 0)
                                    {
                                        goto_next(); break;
                                    }
                                    this.pc += arg; break;
                                }
                            case (int)Bytecode.Opcode.IF_GRT:
                                {
                                    if (expect_numeric(pop_exec()) > expect_numeric(pop_exec()))
                                    {
                                        goto_next(); break;
                                    }
                                    this.pc += arg; break;
                                }
                            case (int)Bytecode.Opcode.IF_GRTE:
                                {
                                    if (expect_numeric(pop_exec()) >= expect_numeric(pop_exec()))
                                    {
                                        goto_next(); break;
                                    }
                                    this.pc += arg; break;
                                }
                            case (int)Bytecode.Opcode.IF_LT:
                                {
                                    if (expect_numeric(pop_exec()) < expect_numeric(pop_exec()))
                                    {
                                        goto_next(); break;
                                    }
                                    this.pc += arg; break;
                                }
                            case (int)Bytecode.Opcode.IF_LTE:
                                {
                                    if (expect_numeric(pop_exec()) <= expect_numeric(pop_exec()))
                                    {
                                        goto_next(); break;
                                    }
                                    this.pc += arg; break;
                                }
                            case (int)Bytecode.Opcode.PRINT_TEST:
                                {
                                    Console.WriteLine("PRINT TEST!!");
                                    goto_next();
                                    break;
                                }
                            default:
                                {
                                    // We have an invalid opcode
                                    push_err(CoreErrorType.INVALID_OP, "op: " + op);
                                    break;
                                }
                        }

                        // Program counter is out of range
                        ASSERT_ERR(pc < 0 || pc > MAX_PC, CoreErrorType.INVALID_PC_RANGE);
                    }
                    catch (CoreErrException e)
                    {
                        if (e.err.is_fatal())
                            this.vm.core_manager.isolate(this.state.id);
                        this.state.err_handler.say_latest();
                        break;
                    }
                }
            }
            return close();
        }

        // We expect a WavyItem to be a numeric
        private dynamic expect_numeric(WavyItem item)
        {
            ASSERT_ERR(item.type != typeof(int) && item.type != typeof(double), CoreErrorType.UNEXPECTED_TYPE, "Expected numeric, but got: " + item.type);
            return item.value;
        }

        // We expect a WavyItem to be an int
        private int expect_int(WavyItem item)
        {
            ASSERT_ERR(item.type != typeof(int), CoreErrorType.UNEXPECTED_TYPE, "Expected int, but got: " + item.type);
            return (int)item.value;
        }

        // We expect a WavyItem to be a WavyFunction
        private WavyItem expect_wfunc(WavyItem item)
        {
            ASSERT_ERR(!(item.type != typeof(WavyFunction)), CoreErrorType.UNEXPECTED_TYPE, "Expected WavyFunction, but got: " + item.type);
            return item;
        }

        // We expect a WavyItem to be a WavyObject
        private WavyItem expect_wobject(WavyItem item)
        {
            ASSERT_ERR(!(item.type != typeof(WavyObject)), CoreErrorType.UNEXPECTED_TYPE, "Expected WavyObject, but got: " + item.type);
            return item;
        }

        // Request a WavyItem from the BankManager
        private WavyItem request_bank_item(data.Bank.Type type, int id)
        {
            // First attempt to request the item from the bank manager
            WavyItem item = this.vm.bank_manager.request_item(this, type, id);
            while(this.state.multi_core_state == MultiCoreState.BLOCKED)
            {
                // This is a very bad implementation, but currently, we try again until we are not blocked
                item = this.vm.bank_manager.request_item(this, type, id);
            }
            return item;
        }

        // Release a WavyItem from the BankManager
        private void release_bank_item(data.Bank.Type type, int id)
        {
            this.vm.bank_manager.release_item(this, type, id);
        }

        // Used when we may need to register an error (for convenience like a macro)
        public void ASSERT_ERR(bool condition, CoreErrorType type, string msg = null)
        {
            if (condition)
                push_err(type, msg);
        }

        // Push an error to the cores' error handler
        public void push_err(CoreErrorType type, string msg = null)
        {
            CoreError err = new CoreError(this.state, this.traceback, type, msg);
            // Register the error with the handler
            this.state.err_handler.register_err(err);
            this.state.had_err = true;
            throw new CoreErrException(err);
        }

        // Perform a function call with a trace
        // WARNING: This is a dev version, it should not take a string, it should take a ref to a WavyFunction
        private Trace func_call_trace(string name)
        {
            // Then create a new Trace instance referencing that frame
            Trace trace = new Trace(func_call(name));
            // Push the trace to the traceback
            this.traceback.push_call_trace(trace);
            return trace;
        }

        // Perform a function call
        // WARNING: This is a dev version, it should not take a string, it should take a ref to a WavyFunction
        private FuncFrame func_call(string name)
        {
            // First create a new FuncFrame to push to the function stack
            FuncFrame frame = new FuncFrame(this, name, ExecStack.deep_copy(this, this.exec_stack));
            // Then push the frame to the FuncStack
            this.func_stack.push(frame);
            return frame;
        }

        // Check if we have reached the end
        public bool end()
        {
            return (this.state.multi_core_state == MultiCoreState.ABORTED) || (this.state.multi_core_state == MultiCoreState.DONE) || (pc >= state.opcode_count) || (bytecode[pc].op == (int)Bytecode.Opcode.END);
        }

        // Called once the current bytecode execution has been completed
        public void goto_next()
        {
            pc++;
        }

        // Returns the next bytecode instance
        public BytecodeInstance get_next()
        {
            return bytecode[pc];
        }

        // Get the next opcode
        public int get_op(BytecodeInstance bytecode)
        {
            return bytecode.op;
        }

        // Get the next argument
        public int get_arg(BytecodeInstance bytecode)
        {
            return bytecode.arg;
        }

        public bool has_arg(BytecodeInstance bytecode)
        {
            return bytecode.has_arg;
        }

        // Pop from the current execution stack in use
        public WavyItem pop_exec()
        {
            // We then get the stack and pop the top value
            WavyItem item = exec_stack.pop();
            if (item != null)
                return item;
            return null;
        }

        // Pop from the current execution stack in use
        public WavyItem peek_exec()
        {
            return exec_stack.peek();
        }

        // Pop from the current execution stack in use
        public void push_exec(WavyItem item)
        {
            exec_stack.push(item);
        }

        // Get the top Function Frame 
        public FuncFrame top_f_frame()
        {
            return this.func_stack.peek();
        }
    }

    public enum MultiCoreState
    {
        
        BLOCKED,    // For when the core has requested data, and has been denied and is waiting
        NIL,        // For when the core has not registered the bytecode sequence
        READY,      // For when the core has been created, but is not running code yet
        RUNNING,    // For when the core is running code
        DONE,       // For when the core is done execution
        ABORTED,
        SUSPENDED,
    }

    // Represents a state of the core at a particular time
    public class CoreState
    {
        public int id;
        public MultiCoreState multi_core_state;
        public ErrorHandler err_handler;
        public int opcode_count;
        public bool currently_interpreting;
        public int func_depth;
        public bool had_err;
        
        public CoreState(int id, ErrorHandler err_handler, int opcode_count, bool currently_interpreting, int func_depth)
        {
            this.id = id;
            this.multi_core_state = MultiCoreState.NIL; // Initialise the multi_core_state to NIL
            this.err_handler = err_handler;
            this.opcode_count = opcode_count;
            this.currently_interpreting = currently_interpreting;
            this.func_depth = func_depth;
            this.had_err = false;
        }

        // Create a fresh CoreState instance
        public static CoreState setup(int id)
        {
            return new CoreState(id, new CoreErrorHandler(), 0, false, 0);
        }
    }
}