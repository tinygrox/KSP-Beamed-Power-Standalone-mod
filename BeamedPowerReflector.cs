using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class WirelessReflector : PartModule
    {
        // parameters set in part.cfg file
        [KSPField(isPersistant = false)]
        public float Reflectivity;

        [KSPField(isPersistant = false)]
        public float ReflectorDiameter;

        // optional parameters also set in part.cfg, only needed if the reflector can amplify
        [KSPField(isPersistant = false)]
        public string CanAmplify;

        [KSPField(isPersistant = false)]
        public float Efficiency;

        [KSPField(isPersistant = false)]
        public string wavelength;

        // counter variables used to cycle through transmitter and receiver lists respectively
        [KSPField(isPersistant = true)]
        public int transmitterCounter;

        [KSPField(isPersistant = true)]
        public int receiverCounter;

        // variables on transmitter
        [KSPField(isPersistant = true)]
        public float excess;

        [KSPField(isPersistant = true)]
        public float constant;

        [KSPField(isPersistant = true)]
        public string Wavelength;

        [KSPField(isPersistant = true)]
        public float resourceConsumption;

        // reflector specific variables
        [KSPField(guiName = "Beam Reflector", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool IsEnabled;

        [KSPField(guiName = "Power Reflected", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float powerReflected;

        [KSPField(guiName = "Amplify power", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "x"), UI_FloatRange(minValue = 1, maxValue = 5, stepIncrement = 0.05f, scene = UI_Scene.Flight)]
        public float amplifyMult = 1f;

        [KSPField(guiName = "Status", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string state;

        // adding vessel names for 'from' and 'to' to part right-click menu in flight
        [KSPField(guiName = "From", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string transmitterName = "None";

        [KSPField(guiName = "To", guiActive = true, guiActiveEditor = false, isPersistant = true)]
        public string TransmittingTo = "None";

        // declaring frequently used variables
        VesselFinder vesselFinder = new VesselFinder(); RelativisticEffects relativistic = new RelativisticEffects();
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        PlanetOcclusion occlusion = new PlanetOcclusion(); int frames;

        List<Vessel> TransmitterList; List<ConfigNode> receiverConfigList;
        List<double> excessList; List<double> constantList;
        List<string> targetList; List<string> wavelengthList;

        // KSPEvent buttons to cycle through vessels lists
        [KSPEvent(guiName = "Cycle through transmitter vessels", guiActive = true, guiActiveEditor = false, requireFullControl = true)]
        public void TransmitterCounter()
        {
            transmitterCounter = (transmitterCounter < TransmitterList.Count - 1) ? transmitterCounter += 1 : transmitterCounter = 0;
        }

        [KSPEvent(guiName = "Cycle through receiver vessels", guiActive = true, guiActiveEditor = false, requireFullControl = true)]
        public void ReceiverCounter()
        {
            receiverCounter = (receiverCounter < receiverConfigList.Count - 1) ? receiverCounter += 1 : receiverCounter = 0;
        }

        // initialise variables
        public void Start()
        {
            frames = 140;
            TransmitterList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            targetList = new List<string>();
            wavelengthList = new List<string>();
            receiverConfigList = new List<ConfigNode>();
        }

        private void SyncAnimationState()
        {
            if (this.part.Modules.Contains<ModuleDeployableAntenna>() &&
                this.part.Modules.GetModule<ModuleDeployableAntenna>().deployState != ModuleDeployableAntenna.DeployState.EXTENDED)
            {
                excess = 0;
            }
            else if (this.part.Modules.Contains<ModuleDeployablePart>() &&
                this.part.Modules.GetModule<ModuleDeployablePart>().deployState != ModuleDeployablePart.DeployState.EXTENDED)
            {
                excess = 0;
            }
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "BeamedPowerReflector";
        }
        public override string GetModuleDisplayName()
        {
            return "Beamed Power Reflector";
        }
        public override string GetInfo()
        {
            return ("Diameter: " + Convert.ToString(ReflectorDiameter) + "m" + "\n"
                + "Reflectivity: " + Convert.ToString(Reflectivity * 100) + "%" + "\n" + "" + "\n"
                + "Can Amplify: " + Convert.ToString(CanAmplify) + "\n"
                + "Efficiency: " + Convert.ToString(Efficiency * 100) + "%" + "\n"
                + "Wavelength: " + wavelength + "\n" + "" + "\n"
                + "To amplify power, incoming beam wavelength must match this part's beam wavelength");
        }

        // main block of code- runs every physics frame
        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                frames += 1;
                if (frames == 150)
                {
                    vesselFinder.SourceData(out TransmitterList, out excessList, out constantList, out targetList, out wavelengthList);
                    vesselFinder.ReceiverData(out receiverConfigList);
                    frames = 0;
                }
                this.vessel.GetConnectedResourceTotals(EChash, out double amount, out double maxAmount);
                if (amount/maxAmount < 0.2d)
                {
                    amplifyMult = 1f;
                }

                if (TransmitterList.Count > 0 && IsEnabled)
                {
                    if (targetList[transmitterCounter] == this.vessel.GetDisplayName())
                    {
                        transmitterName = TransmitterList[transmitterCounter].GetDisplayName();
                        double excess1 = (float)excessList[transmitterCounter];
                        double constant1 = (float)constantList[transmitterCounter];
                        Vector3d source = TransmitterList[transmitterCounter].GetWorldPos3D();
                        Vector3d dest = this.vessel.GetWorldPos3D();
                        double distance = Vector3d.Distance(source, dest);
                        double spot_size = constant1 * distance;
                        occlusion.IsOccluded(source, dest, wavelengthList[transmitterCounter], out CelestialBody body, out bool occluded);

                        if (spot_size > ReflectorDiameter)
                        {
                            excess = (float)Math.Round((ReflectorDiameter / spot_size) * Reflectivity * excess1, 1);
                        }
                        else
                        {
                            excess = (float)Math.Round(Reflectivity * excess1, 1);
                        }
                        excess *= (float)relativistic.RedOrBlueShift(TransmitterList[transmitterCounter], this.vessel, state, out state);

                        if (occluded)
                        {
                            excess = 0;
                            state = "Occluded by " + body.GetDisplayName().TrimEnd(Convert.ToChar("N")).TrimEnd(Convert.ToChar("^"));
                        }
                        else
                        {
                            state = "Operational";
                        }

                        if (CanAmplify == "True" && wavelength == wavelengthList[transmitterCounter])
                        {
                            bool background = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().BackgroundProcessing;
                            resourceConsumption = (float)((ReflectorDiameter / spot_size) * excess1 * (amplifyMult - 1));
                            if (background == false)
                            {
                                this.part.RequestResource("ElectricCharge", (double)resourceConsumption * Time.fixedDeltaTime);
                            }
                            excess += Mathf.Clamp((resourceConsumption * Efficiency), 0f, 50000f);
                        }
                        else
                        {
                            amplifyMult = 1f;
                        }

                        SyncAnimationState();

                        powerReflected = Convert.ToSingle(Math.Round(excess, 1));
                        double heatModifier = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat;
                        this.part.AddSkinThermalFlux((excess / Reflectivity) * (1 - Reflectivity) * (heatModifier / 100));

                        if (wavelengthList[transmitterCounter] == "Short")
                        {
                            constant = Convert.ToSingle((1.44 * 5 * Math.Pow(10, -8)) / ReflectorDiameter);
                            Wavelength = "Short";
                        }
                        else
                        {
                            constant = Convert.ToSingle((1.44 * Math.Pow(10, -3)) / ReflectorDiameter);
                            Wavelength = "Long";
                        }
                    }
                    else
                    {
                        excess = 0;
                        powerReflected = 0;
                        transmitterName = "None";
                    }
                }
                else
                {
                    excess = 0;
                    powerReflected = 0;
                    transmitterName = "None";
                }

                if (receiverConfigList.Count > 0 && IsEnabled)
                {
                    TransmittingTo = receiverConfigList[receiverCounter].GetValue("name");
                }
                else
                {
                    excess = 0;
                    powerReflected = 0;
                    TransmittingTo = "None";
                }
            }
        }

        // reflected power calculator in part right-click menu
        [KSPField(guiName = "Received Power", guiActive = false, guiActiveEditor = true, groupName = "reflectedpowerCalc", groupDisplayName = "Reflected Power Calculator", groupStartCollapsed = true, isPersistant = false, guiUnits = "kW"), UI_FloatRange(scene = UI_Scene.Editor, minValue = 0, maxValue = 100000, stepIncrement = 1)]
        public float recvpower;

        [KSPField(guiName = "Amplify power", guiActive = false, guiActiveEditor = true, groupName = "reflectedpowerCalc", isPersistant = false, guiUnits = "x"), UI_FloatRange(scene = UI_Scene.Editor, minValue = 1, maxValue = 5, stepIncrement = 0.05f)]
        public float amplify = 1f;

        [KSPField(guiName = "Result", guiActive = false, guiActiveEditor = true, groupName = "reflectedpowerCalc", isPersistant = false, guiUnits = "kW")]
        public float result;

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                result = recvpower * Reflectivity;
                if (CanAmplify == "True")
                {
                    result += Mathf.Clamp((recvpower * (amplifyMult - 1) * Efficiency), 0f, 50000f);
                }
                else
                {
                    amplify = 1f;
                }
            }
        }
    }
}
