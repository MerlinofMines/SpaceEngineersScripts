﻿using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript {
    partial class Program {

        public class InterruptException : Exception {
            public ProgramState ProgramState;

            public InterruptException(ProgramState programState) {
                ProgramState = programState;
            }
        }

        public abstract class Command {
            public virtual void Reset() { }
            public virtual Command Clone() { return this; }

            //Returns true if the program has finished execution.
            public abstract bool Execute();
        }

        public class QueueCommand : Command {
            public Command command;
            bool async;

            public QueueCommand(Command command, bool async) {
                this.command = command;
                this.async = async;
            }

            public override bool Execute() {
                Thread thread;
                if (async) {
                    thread = new Thread(command.Clone(), "Async", "Unknown");
                    PROGRAM.QueueAsyncThread(thread);
                } else {
                    thread = new Thread(command.Clone(), "Queued", "Unknown");
                    PROGRAM.QueueThread(thread);
                }
                if (command is FunctionCommand) {
                    thread.SetName(((FunctionCommand)command).functionDefinition.functionName);
                }
                thread.threadVariables = new Dictionary<string, Variable>(PROGRAM.GetCurrentThread().threadVariables);
                return true;
            }
        }

        public class PrintCommand : Command {
            public Variable variable;

            public PrintCommand(Variable variable) {
                this.variable = variable;
            }

            public override bool Execute() {
                Print(CastString(variable.GetValue()).GetTypedValue());
                return true;
            }
        }

        public class FunctionCommand : Command {
            public Function type;
            public FunctionDefinition functionDefinition;
            public Dictionary<String, Variable> inputParameters;

            MultiActionCommand function;

            public FunctionCommand(Function type, FunctionDefinition functionDefinition, Dictionary<string, Variable> inputParameters) {
                this.type = type;
                this.functionDefinition = functionDefinition;
                this.inputParameters = inputParameters;
                function = null;
            }

            public override bool Execute() {
                Thread currentThread = PROGRAM.GetCurrentThread();
                if (function == null) {
                    function = (MultiActionCommand)PROGRAM.functions[functionDefinition.functionName].function.Clone();
                    foreach(string key in inputParameters.Keys) {
                        currentThread.threadVariables[key] = new StaticVariable(inputParameters[key].GetValue().DeepCopy());
                    }
                }
                switch (type) {
                    case Function.GOSUB:
                        return function.Execute();
                    case Function.GOTO:
                        currentThread.Command = function;
                        currentThread.SetName(functionDefinition.functionName);
                        return false;
                    default:
                        throw new Exception("Unsupported Function Type: " + type);
                }
            }
            public override Command Clone() { return new FunctionCommand(type, functionDefinition, inputParameters); }
            public override void Reset() => function = null;
        }

        public class VariableAssignmentCommand : Command {
            public String variableName;
            public Variable variable;
            public bool isGlobal;
            public bool useReference;

            public VariableAssignmentCommand(string variableName, Variable variable, bool useReference, bool isGlobal) {
                this.variableName = variableName;
                this.variable = variable;
                this.useReference = useReference;
                this.isGlobal = isGlobal;
            }

            public override bool Execute() {
                Variable value = useReference ? variable : new StaticVariable(variable.GetValue().DeepCopy());
                if (isGlobal) {
                    PROGRAM.SetGlobalVariable(variableName, value);
                } else {
                    PROGRAM.GetCurrentThread().threadVariables[variableName] = value;
                }
                return true;
            }
        }

        public class ListVariableAssignmentCommand : Command {
            public ListIndexVariable list;
            public Variable value;
            public bool useReference;

            public ListVariableAssignmentCommand(ListIndexVariable list, Variable value, bool useReference) {
                this.list = list;
                this.value = value;
                this.useReference = useReference;
            }

            public override bool Execute() {
                list.SetValue(useReference ? value : new StaticVariable(value.GetValue().DeepCopy()));
                return true;
            }
        }

        public class ControlCommand : Command {
            public Control controlType;
            bool executed = false;

            public ControlCommand(Control type) {
                controlType = type;
            }

            public override bool Execute() {
                switch (controlType) {
                    case Control.STOP:
                        PROGRAM.ClearAllThreads();
                        throw new InterruptException(ProgramState.STOPPED);
                    case Control.RESTART:
                        PROGRAM.ClearAllThreads();
                        throw new InterruptException(ProgramState.RUNNING);
                    case Control.PAUSE:
                        executed = !executed;
                        if (executed) throw new InterruptException(ProgramState.PAUSED);
                        else return true;
                    case Control.REPEAT:
                        Thread currentThread = PROGRAM.GetCurrentThread();
                        currentThread.Command = currentThread.Command.Clone();
                        return false;
                    default: throw new Exception("Unsupported Control Type: " + controlType);
                }
            }

            public override Command Clone() => new ControlCommand(controlType);
        }

        public class WaitCommand : Command {
            public Variable waitInterval;
            public Unit units;
            int ticksLeft = -1;

            public WaitCommand(Variable waitInterval, Unit units) {
                this.waitInterval = waitInterval;
                this.units = units;
            }

            public override Command Clone() { return new WaitCommand(waitInterval,units); }
            public override void Reset() { ticksLeft = -1; }
            public override bool Execute() {
                if (ticksLeft < 0) {
                    ticksLeft = getTicks(CastNumber(waitInterval.GetValue()).GetTypedValue(), units);
                }
                Debug("Waiting for " + ticksLeft + " ticks");
                return ticksLeft-- <= 0;
            }

            int getTicks(float numeric, Unit unitType) {
                switch (unitType) {
                    case Unit.SECONDS:
                        return (int)(numeric * 60);//Assume 60 ticks / second
                    case Unit.TICKS:
                        return (int)numeric;
                    default:
                        throw new Exception("Unsupported Unit Type: " + unitType);
                }
            }
        }

        public class ListenCommand : Command {
            public Variable tag;

            public ListenCommand(Variable tag) {
                this.tag = tag;
            }

            public override bool Execute() {
                PROGRAM.IGC.RegisterBroadcastListener(CastString(tag.GetValue()).GetTypedValue());
                return true;
            }
        }

        public class SendCommand : Command {
            public Variable message, tag;

            public SendCommand(Variable message, Variable tag) {
                this.message = message;
                this.tag = tag;
            }

            public override bool Execute() {
                PROGRAM.IGC.SendBroadcastMessage(CastString(tag.GetValue()).GetTypedValue(), CastString(message.GetValue()).GetTypedValue());
                return true; 
            }
        }

        public class NullCommand : Command { public override bool Execute() { return true; } }

        public class BlockCommand : Command {
            public EntityProvider entityProvider;
            public Action<BlockHandler, Object> blockAction;

            public BlockCommand(EntityProvider provider, Action<BlockHandler, Object> action) {
                entityProvider = provider;
                blockAction = action;
            }

            public override bool Execute() {
                BlockHandler handler = BlockHandlerRegistry.GetBlockHandler(entityProvider.GetBlockType());
                entityProvider.GetEntities().ForEach(e => blockAction(handler, e));
                return true;
            }
        }

        public class TransferItemCommand : Command {
            public EntityProvider from;//Must be Inventory
            public EntityProvider to;//Must be Inventory
            public Variable first, second;//One of these is an amount (nullable), other must be ItemFilter (non nullable)

            public TransferItemCommand(EntityProvider from, EntityProvider to, Variable first, Variable second) {
                this.from = from;
                this.to = to;
                this.first = first;
                this.second = second;
            }

            public override bool Execute() {
                if (from.GetBlockType() != Block.CARGO || to.GetBlockType() != Block.CARGO) throw new Exception("Transfers can only be executed on cargo block types");

                BlockHandler blockHandler = BlockHandlerRegistry.GetBlockHandler(Block.CARGO);

                var filter = PROGRAM.AnyItem(PROGRAM.GetItemFilters(CastString((second ?? first).GetValue()).GetTypedValue()));
                var items = new List<MyInventoryItem>();

                var toInventories = to.GetEntities().Select(i => (IMyInventory)i).Where(i => !i.IsFull).ToList();
                var fromInventories = from.GetEntities().Select(i => (IMyInventory)i)
                    .Where(i => toInventories.TrueForAll(to => i.Owner.EntityId != to.Owner.EntityId)) //Don't transfer to yourself
                    .ToList();

                MyFixedPoint amountLeft = MyFixedPoint.MaxValue;
                if (second != null) amountLeft = (MyFixedPoint)CastNumber(first.GetValue()).GetTypedValue();

                int transfers = 0;

                foreach(IMyInventory fromInventory in fromInventories) {
                    fromInventory.GetItems(items, filter);
                    for(int i = 0; i < toInventories.Count; i++) {
                        foreach (MyInventoryItem item in items) {
                            var destinationInventory = toInventories[i];
                            var startMass = fromInventory.CurrentMass;
                            fromInventory.TransferItemTo(destinationInventory, item, amountLeft);
                            amountLeft -= (startMass - fromInventory.CurrentMass);
                            if (amountLeft <= MyFixedPoint.Zero || ++transfers >= PROGRAM.maxItemTransfers) return true;
                            if (destinationInventory.IsFull) {
                                toInventories.RemoveAt(i--);
                                break;
                            }
                        }
                    }
                }
                return true;
            }
        }

        public class ConditionalCommand : Command {
            public Variable Condition;
            public bool alwaysEvaluate = false;
            public bool evaluated = false;
            public bool evaluatedValue = false;
            public bool isExecuting = false;
            public Command conditionMetCommand;
            public Command conditionNotMetCommand;

            public ConditionalCommand(Variable condition, Command conditionMetCommand, Command conditionNotMetCommand, bool alwaysEvaluate) {
                this.Condition = condition;
                this.conditionMetCommand = conditionMetCommand;
                this.conditionNotMetCommand = conditionNotMetCommand;
                this.alwaysEvaluate = alwaysEvaluate;
                if (alwaysEvaluate) UpdateAlwaysEvaluate();
            }

            public override bool Execute() {
                Debug("Executing Conditional Command");
                Debug("Condition: " + Condition.ToString());
                Trace("Action Command: " + conditionMetCommand.ToString());
                Trace("Other Command: " + conditionNotMetCommand.ToString());
                Trace("Always Evaluate: " + alwaysEvaluate);
                bool conditionMet = EvaluateCondition();
                bool commandResult = false;

                if (conditionMet) {
                    commandResult = conditionMetCommand.Execute();
                } else {
                    commandResult = conditionNotMetCommand.Execute();
                }

                isExecuting = !commandResult;

                if (isExecuting) return false; //Keep executing subcommand

                //Finished Executing.  Reset Commands
                conditionMetCommand.Reset();
                conditionNotMetCommand.Reset();

                //throw new Exception("Stop!");
                if (alwaysEvaluate) { return !conditionMet; } else { return commandResult; }
            }

            public override void Reset() {
                conditionMetCommand.Reset();
                conditionNotMetCommand.Reset();
                evaluated = false;
                isExecuting = false;
            }
            public override Command Clone() { return new ConditionalCommand(Condition, conditionMetCommand.Clone(), conditionNotMetCommand.Clone(), alwaysEvaluate); ; }

            private void UpdateAlwaysEvaluate() {
                alwaysEvaluate = true;
                if (conditionMetCommand is ConditionalCommand) ((ConditionalCommand)conditionMetCommand).UpdateAlwaysEvaluate();
                if (conditionNotMetCommand is ConditionalCommand) ((ConditionalCommand)conditionNotMetCommand).UpdateAlwaysEvaluate();
            }

            private bool EvaluateCondition() {
                if ((!isExecuting && alwaysEvaluate) || !evaluated) {
                    Trace("Evaluating Value");
                    evaluatedValue = CastBoolean(Condition.GetValue()).GetTypedValue(); evaluated = true;
                }
                Debug("Evaluated Value: " + evaluatedValue);
                return evaluatedValue;
            }
        }

        public class MultiActionCommand : Command {
            public List<Command> commandsToExecute;
            public Variable loopCount;
            List<Command> currentCommands = null;
            int loopsLeft;

            public MultiActionCommand(List<Command> commandsToExecute, int loops = 1) : this(commandsToExecute, new StaticVariable(new NumberPrimitive(loops))) {

            }

            public MultiActionCommand(List<Command> commandsToExecute, Variable loops) {
                this.commandsToExecute = commandsToExecute;
                loopCount = loops;
            }

            public override bool Execute() {
                if (currentCommands == null || currentCommands.Count == 0) {
                    currentCommands = commandsToExecute.Select(c => c.Clone()).ToList();//Deep Copy
                    if (loopsLeft == 0) loopsLeft = (int)Math.Round(CastNumber(loopCount.GetValue()).GetTypedValue());
                    loopsLeft -= 1;
                }

                Debug("Commands left: " + currentCommands.Count);
                Debug("Loops Left: " + loopsLeft);

                while (currentCommands.Count > 0) {
                    if (currentCommands[0].Execute()) {
                        currentCommands.RemoveAt(0);
                    } else {
                        break;
                    }
                    Debug("Command is handled, continuing to next command");
                }

                if (currentCommands.Count > 0) return false;
                if (loopsLeft == 0) return true;

                Reset();
                return false;
            }
            public override void Reset() { currentCommands = null; }
            public override Command Clone() { return new MultiActionCommand(commandsToExecute, loopCount); }
            public void Loop(int times) { loopsLeft += times; }
        }
    }
}
