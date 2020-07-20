using Sandbox.Game.EntityComponents;
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

namespace IngameScript
{
    partial class Program
    {
        public class Utils
        {
            IMyGridTerminalSystem GridTerminalSystem;

            public Utils(IMyGridTerminalSystem myGridTerminalSystem)
            {
                GridTerminalSystem = myGridTerminalSystem;
            }

            public void OutputMessageToConnectedPanels(string message, List<IMyTextSurface> SurfaceList)
            {
                foreach (var surface in SurfaceList)
                {
                    WriteToPanel(message, surface);
                }
            }

            public float GetSolarPanelOutput(IMyTerminalBlock solarPanel)
            {
                var SolarReadout = solarPanel.DetailedInfo.Split('\n');
                SolarReadout = SolarReadout[1].Split(' ');
                float panelOutput = Convert.ToSingle(SolarReadout[2]);
                return panelOutput;
            }

            public IMyTextSurface SetupPanel(string panelName)
            {                
                IMyTextSurface surface = GridTerminalSystem.GetBlockWithName(panelName) as IMyTextSurface;

                if (surface == null)
                {             
                    return null;
                }

                return surface;
            }

            public void WriteToPanel(string message, IMyTextSurface surface)
            {
                // Calculate the viewport by centering the surface size onto the texture size
                var _viewport = new RectangleF(
                    (surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);

                PrepareTextSurfaceForSprites(surface);

                using (var frame = surface.DrawFrame())
                {
                    // Set up the initial position - and add viewport offset
                    var position = new Vector2(256, 125) + _viewport.Position;

                    // Create line of text
                    var sprite = new MySprite()
                    {
                        Type = SpriteType.TEXT,
                        Data = message,
                        Position = position,
                        RotationOrScale = 1.2f,
                        Color = Color.White,
                        Alignment = TextAlignment.CENTER,
                        FontId = "White"
                    };
                    
                    frame.Add(sprite);
                }
            }

            public void PrepareTextSurfaceForSprites(IMyTextSurface textSurface)
            {
                // Set the sprite display mode
                textSurface.ContentType = ContentType.SCRIPT;

                // Make sure no built-in script has been selected
                textSurface.Script = "";
            }

            public string GetTime()
            {
                var now = DateTime.Now;

                return $"{now.Hour}:{now.Minute}:{now.Second}";
            }

            public string ListProperties(string name)
            {
                string returnMessage = "";
                var block = GridTerminalSystem.GetBlockWithName(name);
                List<ITerminalProperty> properties = new List<ITerminalProperty>();
                block.GetProperties(properties);
                returnMessage = $"--Properties of block named {name}:";
                
                foreach (var property in properties)
                {
                    returnMessage += "\n" + property.Id;
                }

                return returnMessage;
            }

            public string ListActions(string name)
            {
                string returnMessage = "";
                var block = GridTerminalSystem.GetBlockWithName(name);
                List<ITerminalAction> actions = new List<ITerminalAction>();
                block.GetActions(actions);
                returnMessage = "$--Actions for block names {name}:";
                foreach (var action in actions)
                {
                    returnMessage += $"{action.Id}: {action.Name}";
                }

                return returnMessage;
            }

            public string OutputDetailedInfo(string name)
            {
                var block = GridTerminalSystem.GetBlockWithName(name);

                if (block != null)
                {
                    return $"Outputting detailed info for block { name}:\n" + block.DetailedInfo;
                }
                else
                {
                    return "Could not find block with name " + name;
                }
            }

            public string OutputRotorAngle(string name)
            {
                var rotor = GridTerminalSystem.GetBlockWithName(name) as IMyMotorStator;

                if (rotor != null)
                {
                    return rotor.Angle.ToString();
                }
                else
                {
                    return "Could not find rotor with name " + name;
                }
            }

            public float OutputRotorAngle(IMyTerminalBlock name)
            {
                var rotor = name as IMyMotorStator;

                if (rotor != null)
                {
                    return rotor.Angle;
                }
                else
                {
                    return float.MinValue;
                }
            }

            public TimeSpan CalculateExecutionTime(TimeSpan[] executionTimes, TimeSpan averageExecution, DateTime startTime, int executionCounter)
            {
                var endOfScript = DateTime.Now;
                var executionTime = endOfScript - startTime;
                executionTimes[executionCounter] = executionTime;

                foreach (var exTime in executionTimes)
                {
                    averageExecution += exTime;
                }

                double doubleAverageTicks = executionTimes.Average(timeSpan => timeSpan.Ticks);
                long longAverageTicks = Convert.ToInt64(doubleAverageTicks);

                averageExecution = TimeSpan.FromTicks(longAverageTicks);

                return averageExecution;
            }
        }

        public enum SolarPanelStates
        {
            SeekingSun, TrackingSun, NighttimeMode
        }

        public enum SolarPanelTrackingCases
        {
            ImprovedWithinTen, ImprovedWithinFifty, ImprovedNotClose, WorsenedWithinTen, Equal, None, LostSun
        }
    }
}
