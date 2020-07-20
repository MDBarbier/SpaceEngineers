using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        /// <summary>
        /// Global properties to be set by user
        /// </summary>        
        public bool RotateAntiClockwise { get; set; } = true;
        public string SolarArrayNameBase { get; set; } = "Solar_Array_1";
        public string SolarArrayHorizontalRotorSuffix { get; set; } = "_Rotor_1";
        public string SolarArrayVerticalRotorSuffix1 { get; set; } = "_Rotor_2";
        public string SolarArrayVerticalRotorSuffix2 { get; set; } = "_Rotor_3";
        public List<string> SolarArrayPanelSuffixList { get; set; } = new List<string>() { "_Panel_1", "_Panel_2", "_Panel_3", "_Panel_4",
            "_Panel_5", "_Panel_6", "_Panel_7", "_Panel_8"};
        public string Lcd { get; set; } = "Command_LCD_1";
        public float DesiredSolarYield { get; set; } = 120f;
        public float NighttimeThreshold { get; set; } = 10f;

        /// <summary>
        /// Global properties that should not be altered manually
        /// </summary>        
        public float LastReading { get; set; } = 0.0f;
        public SolarPanelStates SolarPanelState { get; set; } = SolarPanelStates.SeekingSun;
        public SolarPanelTrackingCases SolarPanelTrackingCase { get; set; } = Program.SolarPanelTrackingCases.None;
        public Utils Utilities { get; set; }
        public float CombinedSolarOutput { get; set; } = 0.0f;
        public float AverageSolarOutput { get; set; } = 0.0f;
        public IMyTextSurface Surface { get; set; }
        public IMyTerminalBlock HRotor { get; set; }
        public IMyTerminalBlock VRotor1 { get; set; }
        public IMyTerminalBlock VRotor2 { get; set; }
        public List<IMyTerminalBlock> SolarPanels { get; set; } = new List<IMyTerminalBlock>();
        public DateTime StartTime { get; set; }
        public TimeSpan[] ExecutionTimes { get; set; } = new TimeSpan[100];
        public int ExecutionCounter { get; set; } = 0;
        public TimeSpan AverageExecution { get; set; } = new TimeSpan();

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="argument">supplied by game</param>
        /// <param name="updateSource">supplied by game</param>
        public void Main(string argument, UpdateType updateSource)
        {
            StartTime = DateTime.Now;
            CombinedSolarOutput = 0.0f;
            ExecutionCounter += 1;

            if (ExecutionCounter == 100)
            {
                ExecutionCounter = 0;
            }
            
            if (Utilities == null)
            {                
                Utilities = new Utils(GridTerminalSystem);
            }

            //Set up all the lcds in the list as panels and add to panel list
            if (Surface == null)
            {                
                Surface = Utilities.SetupPanel(Lcd);
            }

            //Find the blocks
            if (HRotor == null)
            {                
                HRotor = GridTerminalSystem.GetBlockWithName(SolarArrayNameBase + SolarArrayHorizontalRotorSuffix);
            }

            if (VRotor1 == null)
            {                
                VRotor1 = GridTerminalSystem.GetBlockWithName(SolarArrayNameBase + SolarArrayVerticalRotorSuffix1);
            }

            if (VRotor2 == null)
            {                
                VRotor2 = GridTerminalSystem.GetBlockWithName(SolarArrayNameBase + SolarArrayVerticalRotorSuffix2);
            }

            //handle error if rotor not found
            if (HRotor == null || VRotor1 == null || VRotor2 == null)
            {
                Echo("One or more rotors not found, exiting");
                return;
            }

            //Get rotor angles
            var horizontalRotorAngle = Utilities.OutputRotorAngle(HRotor);
            var verticalRotorAngle1 = Utilities.OutputRotorAngle(VRotor1);
            var verticalRotorAngle2 = Utilities.OutputRotorAngle(VRotor2);

            if (SolarPanels == null || SolarPanels.Count == 0)
            {
                foreach (var solarPanel in SolarArrayPanelSuffixList)
                {
                    string fullName = SolarArrayNameBase + solarPanel;
                    var thePanel = GridTerminalSystem.GetBlockWithName(fullName);

                    if (thePanel == null)
                    {
                        Echo("Did not find " + SolarArrayNameBase + solarPanel);
                    }
                    else
                    {
                        float panelOutput = Utilities.GetSolarPanelOutput(thePanel);
                        CombinedSolarOutput += panelOutput;
                        SolarPanels.Add(thePanel);
                    }
                }

            }
            else
            {                
                foreach (var thePanel in SolarPanels)
                {
                    float panelOutput = Utilities.GetSolarPanelOutput(thePanel);
                    CombinedSolarOutput += panelOutput;                    
                }
            }

            //Calculate average output
            AverageSolarOutput = CombinedSolarOutput / (float)SolarArrayPanelSuffixList.Count;            

            //Calculate Solar Panel State
            float velocity = 0f;
            if (AverageSolarOutput <= NighttimeThreshold)
            {
                SolarPanelState = SolarPanelStates.NighttimeMode;
                velocity = 0.0f;
            }
            else if (AverageSolarOutput > NighttimeThreshold && AverageSolarOutput < DesiredSolarYield - 30)
            {
                SolarPanelState = SolarPanelStates.SeekingSun;
                velocity = 6.0f;
            }
            else
            {
                SolarPanelState = SolarPanelStates.TrackingSun;
                SolarPanelTrackingCase = SolarPanelTrackingCases.None;

                //If the current reading is better and within 10 of desired
                if (AverageSolarOutput > LastReading && AverageSolarOutput > DesiredSolarYield - 10.0f)
                {
                    velocity = 0.1f;

                    SolarPanelTrackingCase = SolarPanelTrackingCases.ImprovedWithinTen;
                }
                //If the current reading is better and within 50 of desired
                else if (AverageSolarOutput > LastReading && AverageSolarOutput > DesiredSolarYield - 50.0f)
                {
                    velocity = 2.0f;

                    SolarPanelTrackingCase = SolarPanelTrackingCases.ImprovedWithinFifty;
                }
                //If current reading is better but not close to desired
                else if (AverageSolarOutput > LastReading)
                {
                    velocity = 4.0f;

                    SolarPanelTrackingCase = SolarPanelTrackingCases.ImprovedNotClose;
                }
                else if (AverageSolarOutput == LastReading)
                {
                    velocity = 0.0f;

                    SolarPanelTrackingCase = SolarPanelTrackingCases.Equal;
                }
                //If the current reading is less than last reading but still within 10f then reverse direction                    
                else if (AverageSolarOutput < LastReading && (AverageSolarOutput > LastReading - 10.0f))
                {
                    velocity = -0.5f;

                    SolarPanelTrackingCase = SolarPanelTrackingCases.WorsenedWithinTen;
                }
                else
                {
                    velocity = 6.0f;

                    SolarPanelTrackingCase = SolarPanelTrackingCases.LostSun;
                    SolarPanelState = SolarPanelStates.SeekingSun;
                }
            }

            if (RotateAntiClockwise)
            {
                velocity = velocity * -1;
            }

            //Set the calculated velocity or horizontal rotor
            HRotor.SetValueFloat("Velocity", velocity);
            VRotor1.SetValueFloat("Velocity", velocity);
            VRotor2.SetValueFloat("Velocity", velocity * -1);

            //Set the last solar panel output reading
            LastReading = AverageSolarOutput;            

            //Work out script execution time
            AverageExecution = Utilities.CalculateExecutionTime(ExecutionTimes, AverageExecution, StartTime, ExecutionCounter);

            //Output to LCD Panel
            Utilities.WriteToPanel($"Solar Array 1 Online " +
                    $"\n{StartTime}" +
                    $"\nRotor velocity: {velocity}" +
                    $"\nPanel Output: {AverageSolarOutput}kw" +
                    $"\nHRotor Angle: {horizontalRotorAngle}{(Char)176}" +
                    $"\nVRotor Angles: {verticalRotorAngle1}{(Char)176}" +
                    $"\nTracking mode: {SolarPanelState}" +
                    $"\nTracking case: {SolarPanelTrackingCase}" +
                    $"\nExecution time avg ({ExecutionCounter} runs):\n {AverageExecution}", Surface);
        }
    }
}
