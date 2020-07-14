using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;

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
        public List<string> LcdList { get; set; } = new List<string>() { "Solar_Array_1_LCD", "Command_LCD_1" };
        public float DesiredSolarYield { get; set; } = 120f;        

        /// <summary>
        /// Global properties that should not be altered manually
        /// </summary>        
        public float LastReading { get; set; } = 0.0f;
        public SolarPanelStates SolarPanelState { get; set; } = SolarPanelStates.SeekingSun;
        public SolarPanelTrackingCases SolarPanelTrackingCase { get; set; } = Program.SolarPanelTrackingCases.None;
        public Utils Utilities { get; set; }
        public List<IMyTextSurface> SurfaceList { get; set; } = new List<IMyTextSurface>();
        public float CombinedSolarOutput { get; set; } = 0.0f;
        public float AverageSolarOutput { get; set; } = 0.0f;

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="argument">supplied by game</param>
        /// <param name="updateSource">supplied by game</param>
        public void Main(string argument, UpdateType updateSource)
        {
            CombinedSolarOutput = 0.0f;
            Utilities = new Utils(GridTerminalSystem);

            //Set up all the lcds in the list as panels and add to panel list
            foreach (var lcd in LcdList)
            {
                var panel = Utilities.SetupPanel(lcd);

                if (panel != null)
                {
                    SurfaceList.Add(panel);
                }
                else
                {
                    Echo($"Panel with name {lcd} not found");
                }
            }

            //Handle error if panel not found
            if (SurfaceList.Count == 0)
            {
                Echo("No lcd panel found");
            }

            //Find the horizontal rotor for the solar array
            var hrotor = GridTerminalSystem.GetBlockWithName(SolarArrayNameBase + SolarArrayHorizontalRotorSuffix);

            //Find the vertical rotor for the solar array
            var vrotor1 = GridTerminalSystem.GetBlockWithName(SolarArrayNameBase + SolarArrayVerticalRotorSuffix1);
            var vrotor2 = GridTerminalSystem.GetBlockWithName(SolarArrayNameBase + SolarArrayVerticalRotorSuffix2);

            //handle error if rotor not found
            if (hrotor == null || vrotor1 == null || vrotor2 == null)
            {
                foreach (var surface in SurfaceList)
                {
                    Utilities.WriteToPanel("VRotor/HRotor not found", surface);
                }

                return;
            }

            //Get rotor angles
            var horizontalRotorAngle = Utilities.OutputRotorAngle(hrotor);
            var verticalRotorAngle1 = Utilities.OutputRotorAngle(vrotor1);
            var verticalRotorAngle2 = Utilities.OutputRotorAngle(vrotor2);

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
                }
            }

            //Calculate average output
            AverageSolarOutput = CombinedSolarOutput / (float)SolarArrayPanelSuffixList.Count;
            Echo($"Average (taken by dividing {CombinedSolarOutput} by {SolarArrayPanelSuffixList.Count}: {AverageSolarOutput}");

            //Calculate Solar Panel State
            if (AverageSolarOutput <= 0f)
            {
                SolarPanelState = SolarPanelStates.NighttimeMode;
            }
            else if (AverageSolarOutput > 0 && AverageSolarOutput < DesiredSolarYield - 30)
            {
                SolarPanelState = SolarPanelStates.SeekingSun;
            }
            else
            {
                SolarPanelState = SolarPanelStates.TrackingSun;
            }

            float velocity = 0f;

            //Based on Solar Panel Stat, calculate desired velocity of horizontal rotor
            switch (SolarPanelState)
            {
                case SolarPanelStates.SeekingSun:
                    
                        velocity = 2.0f;
                    
                    break;

                case SolarPanelStates.TrackingSun:

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
                        velocity = 1.0f;                        

                        SolarPanelTrackingCase = SolarPanelTrackingCases.ImprovedWithinFifty;
                    }
                    //If current reading is better but not close to desired
                    else if (AverageSolarOutput > LastReading)
                    {
                        velocity = 2.0f;                        

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
                        velocity = -0.2f;                        

                        SolarPanelTrackingCase = SolarPanelTrackingCases.WorsenedWithinTen;
                    }
                    else
                    {
                        velocity = 2.0f;                        

                        SolarPanelTrackingCase = SolarPanelTrackingCases.LostSun;
                        SolarPanelState = SolarPanelStates.SeekingSun;
                    }
                    break;

                case SolarPanelStates.NighttimeMode:
                    velocity = 0.0f;
                    break;

                default:
                    break;
            }

            if (RotateAntiClockwise)
            {
                velocity = velocity * -1;
            }

            //Set the calculated velocity or horizontal rotor
            hrotor.SetValueFloat("Velocity", velocity);
            vrotor1.SetValueFloat("Velocity", velocity);
            vrotor2.SetValueFloat("Velocity", velocity * -1);

            //Set the last solar panel output reading
            LastReading = AverageSolarOutput;
            
            var time = Utilities.GetTime();

            //Output to LCD Panel
            foreach (var surface in SurfaceList)
            {
                Utilities.WriteToPanel($"Solar Array 1 Online " +
                    $"\n{time}" +
                    $"\nRotor velocity: {velocity}" +                    
                    $"\nPanel Output: {AverageSolarOutput}kw" +
                    $"\nHRotor Angle: {horizontalRotorAngle}{(Char)176}" +
                    $"\nVRotor Angles: {verticalRotorAngle1}{(Char)176}" +
                    $"\nTracking mode: {SolarPanelState}" +
                    $"\nTracking case: {SolarPanelTrackingCase}", surface);
            }
        }
    }    
}
