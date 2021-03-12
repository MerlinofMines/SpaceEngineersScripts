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
        public class DoorBlockHandler : FunctionalBlockHandler<IMyDoor> {
            public DoorBlockHandler() : base() {
                AddBooleanHandler(PropertyType.OPEN, (b) => b.Status != DoorStatus.Closed, (b, v) => { if (v) b.OpenDoor(); else b.CloseDoor(); });
                AddPropertyHandler(PropertyType.RATIO, new DoorRatioHandler());
                defaultBooleanProperty = PropertyType.OPEN;
                defaultDirection = DirectionType.UP;
                defaultNumericProperties.Add(DirectionType.UP, PropertyType.RATIO);
            }

            public class DoorRatioHandler : PropertyHandler<IMyDoor> {
                public DoorRatioHandler() {
                    GetNumeric = (b) => 1 - b.OpenRatio;
                    GetBoolean = (b) => b.OpenRatio > 0;
                    GetString = (b) => b.OpenRatio.ToString();
                    Set = (b, v) => Exception();
                    SetDirection = (b, d, v) => Exception();
                    Increment = (b, v) => Exception();
                    IncrementDirection = (b, d, v) => Exception();
                    Reverse = (b) => b.ToggleDoor();
                    Move = (b, d) => {
                        if (d == DirectionType.UP) b.OpenDoor();
                        if (d == DirectionType.DOWN) b.CloseDoor();
                    };
                }
            }

            public static void Exception() { throw new Exception("Cannot manually set door open amount"); }

        }
    }
}
