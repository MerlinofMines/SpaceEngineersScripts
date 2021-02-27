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
        public interface Condition {
            bool Evaluate();
        }

        public enum AggregationMode {
            ANY,
            ALL,
            NONE
        }

        public static String getAggregationModeName(AggregationMode mode) {
            switch (mode) {
                case AggregationMode.ALL: return "All";
                case AggregationMode.ANY: return "Any";
                case AggregationMode.NONE: return "None";
                default: throw new Exception("Unsupported Aggregation Mode");
            }
        }

        public class NotCondition : Condition {
            Condition condition;

            public NotCondition(Condition condition) {
                this.condition = condition;
            }

            public bool Evaluate() {
                return !condition.Evaluate();
            }
            public override String ToString() {
                return "Not ( " + condition + " ) ";
            }
        }

        public class AndCondition : Condition {
            Condition conditionA, conditionB;

            public AndCondition(Condition conditionA, Condition conditionB) {
                this.conditionA = conditionA;
                this.conditionB = conditionB;
            }

            public bool Evaluate() {
                return conditionA.Evaluate() && conditionB.Evaluate();
            }

            public override String ToString() {
                return "And ( " + conditionA + " , " + conditionB + " ) ";
            }
        }

        public class OrCondition : Condition {
            Condition conditionA, conditionB;

            public OrCondition(Condition conditionA, Condition conditionB) {
                this.conditionA = conditionA;
                this.conditionB = conditionB;
            }

            public bool Evaluate() {
                return conditionA.Evaluate() || conditionB.Evaluate();
            }
            public override String ToString() {
                return "Or ( " + conditionA + " , " + conditionB + " ) ";
            }
        }

        public class AggregateCondition : Condition {
            AggregationMode aggregationMode;
            BlockCondition blockCondition;
            EntityProvider entityProvider;

            public AggregateCondition(AggregationMode aggregationMode, BlockCondition blockCondition, EntityProvider entityProvider) {
                this.aggregationMode = aggregationMode;
                this.blockCondition = blockCondition;
                this.entityProvider = entityProvider;
            }

            public bool Evaluate() {
                List<Object> blocks = entityProvider.GetEntities();

                if (blocks.Count == 0) return false; //If there are no blocks, consider this not matching

                int matches = blocks.Count(block => blockCondition.evaluate(block));

                switch (aggregationMode) {
                    case AggregationMode.ALL: return matches == blocks.Count;
                    case AggregationMode.ANY: return matches > 0;
                    case AggregationMode.NONE: return matches == 0;
                    default: throw new Exception("Unsupported Aggregation Mode");
                }
            }
            public override String ToString() {
                return getAggregationModeName(aggregationMode) + " of " + entityProvider + " are " + blockCondition;
            }
        }

        public interface BlockCondition {
            bool evaluate(Object block);
        }

        public class NotBlockCondition : BlockCondition {
            BlockCondition blockCondition;

            public NotBlockCondition(BlockCondition blockCondition) {
                this.blockCondition = blockCondition;
            }

            public bool evaluate(Object block) {
                return !blockCondition.evaluate(block);
            }
            public override String ToString() {
                return "not " + blockCondition;
            }
        }

        public class AndBlockCondition : BlockCondition {
            BlockCondition conditionA;
            BlockCondition conditionB;

            public AndBlockCondition(BlockCondition conditionA, BlockCondition conditionB) {
                this.conditionA = conditionA;
                this.conditionB = conditionB;
            }

            public bool evaluate(object block) {
                return conditionA.evaluate(block) && conditionB.evaluate(block);
            }
        }

        public class OrBlockCondition : BlockCondition {
            BlockCondition conditionA;
            BlockCondition conditionB;

            public OrBlockCondition(BlockCondition conditionA, BlockCondition conditionB) {
                this.conditionA = conditionA;
                this.conditionB = conditionB;
            }

            public bool evaluate(object block) {
                return conditionA.evaluate(block) || conditionB.evaluate(block);
            }
        }

        public abstract class BlockCondition<T, U> : BlockCondition {
            protected BlockHandler blockHandler;
            protected T property;
            protected Comparator<U> comparator;
            protected U comparisonValue;

            protected BlockCondition(BlockHandler blockHandler, T property, Comparator<U> comparator, U comparisonValue) {
                this.blockHandler = blockHandler;
                this.property = property;
                this.comparator = comparator;
                this.comparisonValue = comparisonValue;
            }
            public abstract bool evaluate(Object block);
            public override String ToString() {
                return property + " " + comparator + " " + comparisonValue;
            }
        }

        public class BooleanBlockCondition : BlockCondition<BooleanPropertyType, bool> {
            public BooleanBlockCondition(BlockHandler blockHandler, BooleanPropertyType property, Comparator<bool> comparator, bool comparisonValue) : base(blockHandler, property, comparator, comparisonValue) { }
            public override bool evaluate(Object block) { return comparator.compare(blockHandler.GetBooleanPropertyValue(block, property), comparisonValue); }
        }

        public class StringBlockCondition : BlockCondition<StringPropertyType, String> {
            public StringBlockCondition(BlockHandler blockHandler, StringPropertyType property, Comparator<String> comparator, String comparisonValue) : base(blockHandler, property, comparator, comparisonValue) { }
            public override bool evaluate(Object block) { return comparator.compare(blockHandler.GetStringPropertyValue(block, property), comparisonValue); }
        }

        public class NumericBlockCondition : BlockCondition<NumericPropertyType, float> {
            public NumericBlockCondition(BlockHandler blockHandler, NumericPropertyType property, Comparator<float> comparator, float comparisonValue) : base(blockHandler, property, comparator, comparisonValue) { }
            public override bool evaluate(Object block) { return comparator.compare(blockHandler.GetNumericPropertyValue(block, property), comparisonValue); }
        }

        public class NumericDirectionBlockCondition : BlockCondition<NumericPropertyType, float> {
            DirectionType direction;
            public NumericDirectionBlockCondition(BlockHandler blockHandler, NumericPropertyType property, DirectionType direction, Comparator<float> comparator, float comparisonValue) : base(blockHandler, property, comparator, comparisonValue) {
                this.direction = direction;
            }
            public override bool evaluate(Object block) { return comparator.compare(blockHandler.GetNumericPropertyValue(block, property, direction), comparisonValue); }
        }

        public abstract class Comparator<T> {
            protected ComparisonType comparisonType;

            protected Comparator(ComparisonType comparisonType) {
                this.comparisonType = comparisonType;
            }
            public abstract bool compare(T a, T b);
            public override String ToString() {
                return comparisonType.ToString();
            }
        }

        public class BooleanComparator : Comparator<bool> {
            public BooleanComparator(ComparisonType comparisonType) : base(comparisonType) { }

            public override bool compare(bool a, bool b) {
                if (ComparisonType.EQUAL == comparisonType) return a == b;
                else throw new Exception("Boolean Comparisons Only Support Equality");
            }
        }

        public class StringComparator : Comparator<String> {
            public StringComparator(ComparisonType comparisonType) : base(comparisonType) { }

            public override bool compare(string a, string b) {
                if (ComparisonType.EQUAL == comparisonType) return a == b;
                else throw new Exception("Boolean Comparisons Only Support Equality");
                //TODO: More Comparison Types?? 
            }
        }

        public class NumericComparator : Comparator<float> {
            public NumericComparator(ComparisonType comparisonType) : base(comparisonType) { }

            public override bool compare(float a, float b) {
                switch (comparisonType) {
                    case ComparisonType.GREATER: return a > b;
                    case ComparisonType.GREATER_OR_EQUAL: return a >= b;
                    case ComparisonType.EQUAL: return a == b;
                    case ComparisonType.LESS_OR_EQUAL: return a <= b;
                    case ComparisonType.LESS: return a < b;
                    default: throw new Exception("Unsupported Comparison Type");
                }
            }
        }
    }
}
