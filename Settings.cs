using System;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // instructions for implementing custom difficulty settings were found on '1.2 modders notes' on the forums
    public class BPSettings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "Beamed Power Difficulty Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "Beamed Power"; } }
        public override string DisplaySection { get { return "Beamed Power"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return true; } } 

        [GameParameters.CustomIntParameterUI("Heat modifier %", unlockedDuringMission = false, minValue = 0, maxValue = 100, stepSize = 1)]
        public int PercentHeat = 50;

        [GameParameters.CustomIntParameterUI("Photon-sail thrust multiplier", unlockedDuringMission = true, minValue = 0, maxValue = 1000, stepSize = 1)]
        public int photonthrust = 40;

        [GameParameters.CustomParameterUI("Background Vessel Processing", unlockedDuringMission = false)]
        public bool BackgroundProcessing = false;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    PercentHeat = 0;
                    photonthrust = 500;
                    break;

                case GameParameters.Preset.Normal:
                    PercentHeat = 30;
                    photonthrust = 100;
                    break;

                case GameParameters.Preset.Moderate:
                    PercentHeat = 60;
                    photonthrust = 40;
                    break;

                case GameParameters.Preset.Hard:
                    PercentHeat = 90;
                    photonthrust = 10;
                    break;

                case GameParameters.Preset.Custom:
                    PercentHeat = 50;
                    photonthrust = 100;
                    break;
            }
        }
    }
}
