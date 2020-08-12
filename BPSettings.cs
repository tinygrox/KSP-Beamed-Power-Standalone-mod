using System;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // instructions for implementing custom difficulty settings were found on 1.2 modders notes on forums
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class BPSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Beamed Power Difficulty Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "Beamed Power"; } }
        public override string DisplaySection { get { return "Beamed Power"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return true; } } 

        [GameParameters.CustomIntParameterUI("Heat modifier %", unlockedDuringMission = false, minValue = 0, maxValue = 100, stepSize = 1)]
        public int PercentHeat = 20;

        [GameParameters.CustomIntParameterUI("Photon-sail thrust multiplier", unlockedDuringMission = false, minValue = 0, maxValue = 1000, stepSize = 1)]
        public int photonthrust = 10;

        [GameParameters.CustomParameterUI("Background Vessel Processing", unlockedDuringMission = false)]
        public bool BackgroundProcessing = false;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    PercentHeat = 0;
                    break;

                case GameParameters.Preset.Normal:
                    PercentHeat = 20;
                    break;

                case GameParameters.Preset.Moderate:
                    PercentHeat = 40;
                    break;

                case GameParameters.Preset.Hard:
                    PercentHeat = 70;
                    break;
            }
        }
    }
}
