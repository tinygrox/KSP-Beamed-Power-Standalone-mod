using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // part module for beamed power thermal engines (microwave thermal engine analog)
    // it has to be applied to a part with ModuleEngineFX
    public class ThermalEngine : PartModule
    {
        [KSPField(guiName = "Received Power", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "kW")]
        public float receivedHeat;

        [KSPField(guiName = "Received Power Limiter", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Flight)]
        public float powerLimiter;

        [KSPField(guiName = "Propellant", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string propellantNameUI;

        [KSPField(guiName = "Core Temperature", groupName = "HeatInfo", groupDisplayName = "Thermal Receiver Heat Info", groupStartCollapsed = false, guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/900K")]
        public float coreTemp;

        [KSPField(guiName = "Skin Temperature", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/1200K")]
        public float skinTemp;

        [KSPField(guiName = "Waste Heat", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float wasteHeat;

        // parameters set in part.cfg file
        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;

        [KSPField(isPersistant = false)]
        public float thermalEfficiency;

        [KSPField(isPersistant = false)]
        public string propellantName;

        //declaring frequently used variables
        Vector3d source; Vector3d dest; double received_power; int initFrames; double distance;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        ModuleEnginesFX engine; VesselFinder vesselFinder = new VesselFinder(); int frames;
        OcclusionData occlusion = new OcclusionData(); ModuleCoreHeat coreHeat;

        List<Vessel> VesselList;
        List<double> excessList;
        List<double> constantList;
        List<string> targetList;
        List<string> wavelengthList;

        public void Start()
        {
            frames = 145; initFrames = 0;
            engine = this.part.Modules.GetModule<ModuleEnginesFX>();
            VesselList = new List<Vessel>();
            excessList = new List<double>();
            targetList = new List<string>();
            wavelengthList = new List<string>();
            SetHeatParams();
        }

        private void SetHeatParams()
        {
            this.part.AddModule("ModuleCoreHeat");
            coreHeat = this.part.Modules.GetModule<ModuleCoreHeat>();
            coreHeat.CoreTempGoal = 1400d;  // placeholder value, there is no optimum temperature
            coreHeat.CoolantTransferMultiplier *= 2d;
            coreHeat.radiatorCoolingFactor *= 2d;
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "ThermalEngine";
        }
        public override string GetModuleDisplayName()
        {
            return "Thermal Engine";
        }
        public override string GetInfo()
        {
            return ("Receiver Diameter: " + Convert.ToString(recvDiameter) + "m" + "\n"
                + "Receiver Efficiency: " + Convert.ToString(recvEfficiency * 100) + "%" + "\n"
                + "Thermal Efficiency: " + Convert.ToString(thermalEfficiency * 100) + "%" + "\n" + ""+ "\n"
                + "Max Core Temp: 1300K" + "\n"
                + "Max Skin Temp: 1500K" + "\n" + "" + "\n"
                + "This engine will shutdown past these temperatures.");
        }

        // adds heat to receiver core, not engine core
        // which is why the temp limits are so low
        private void AddHeatToReceiverCore()
        {
            coreTemp = (float)(Math.Round(coreHeat.CoreTemperature, 1));
            skinTemp = (float)(Math.Round(this.part.skinTemperature, 1));
            
            if (coreTemp > 1000f | skinTemp > 1200f)
            {
                engine.status = "Exceeded Temperature Limit";
                engine.Shutdown(); powerLimiter = 0;
            }
            if (engine.status == "Exceeded Temperature Limit" & (coreTemp > 800f | skinTemp > 1000f))
            {
                engine.Shutdown(); powerLimiter = 0;
            }
            else if (coreTemp < 800f & skinTemp < 1000f)
            {
                engine.status = "Nominal";
            }
            double heatModifier = (double)HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat / 100;
            double heatExcess = (1 - recvEfficiency) * (1 - thermalEfficiency) * (received_power / recvEfficiency) * heatModifier;
            wasteHeat = (float)Math.Round(heatExcess, 1);
            coreHeat.AddEnergyToCore(heatExcess * 0.4 * 3 * Time.fixedDeltaTime);  // first converted to kJ
            this.part.AddSkinThermalFlux(heatExcess * 0.6);     // some heat added to skin
        }

        public void FixedUpdate()
        {
            // code taken directly from WirelessReceiver module
            frames += 1;
            if (frames == 150)
            {
                vesselFinder.SourceData(out VesselList, out excessList, out constantList, out targetList, out wavelengthList);
                frames = 0;
            }
            if (initFrames < 60)
            {
                initFrames += 1;
            }
            else
            {
                AddHeatToReceiverCore();
            }

            if (VesselList.Count > 0)
            {
                dest = this.vessel.GetWorldPos3D(); double prevDistance = distance;
                received_power = 0;

                // adds up all the received power values from all vessels in CorrectVesselList 
                for (int n = 0; n < VesselList.Count; n++)
                {
                    if (targetList[n] == this.vessel.GetDisplayName())
                    { 
                        double excess2 = excessList[n]; double constant2 = constantList[n];
                        source = VesselList[n].GetWorldPos3D();
                        distance = Vector3d.Distance(source, dest);
                        double spotsize = constant2 * distance;
                        occlusion.IsOccluded(source, dest, wavelengthList[n], out _, out bool occluded);

                        // adding EC that has been received
                        if (recvDiameter < spotsize)
                        {
                            if (occluded == false)
                            {
                                received_power += Math.Round(((recvDiameter / spotsize) * recvEfficiency * excess2), 1);
                            }
                        }
                        else
                        {
                            if (occluded == false)
                            {
                                received_power += Math.Round((recvEfficiency * excess2), 1);
                            }
                        }
                    }
                }
                
                received_power *= powerLimiter / 100;
                receivedHeat = Convert.ToSingle(Math.Round(received_power, 1));
            }
            else
            {
                receivedHeat = 0;
                received_power = 0;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                float currentisp = engine.realIsp;
                engine.throttleResponseRate *= 5;
                propellantNameUI = PartResourceLibrary.Instance.GetDefinition(propellantName).displayName;
                float shc = PartResourceLibrary.Instance.GetDefinition(propellantName).specificHeatCapacity * 1.5f / 1000f;    // in kJ kg^-1 K^-1
                float density = PartResourceLibrary.Instance.GetDefinition(propellantName).density * 1000;
                float Thrust = (float)(((received_power * thermalEfficiency) / (shc * 2200d * density)) * 9.8d * currentisp);
                float percentThrust = Thrust / engine.maxThrust;
                engine.thrustPercentage = (float)Math.Round(((percentThrust < 1) ? percentThrust * 100 : 100f), 2);
            }
        }

        // thrust calculator
        [KSPField(guiName = "Distance", groupName = "calculator4", groupDisplayName = "Thrust Calculator", groupStartCollapsed = true, guiUnits = "Mm", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 10000000, stepIncrement = 0.001f, scene = UI_Scene.Editor)]
        public float dist_ui;

        [KSPField(guiName = "Source Dish Diameter", groupName = "calculator4", guiUnits = "m", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float dish_dia_ui;

        [KSPField(guiName = "Source Efficiency", groupName = "calculator4", guiActive = false, guiActiveEditor = true, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float efficiency;

        [KSPField(guiName = "Power Beamed", groupName = "calculator4", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float beamedPower;

        [KSPField(guiName = "Engine Isp", groupName = "calculator4", guiUnits = "s", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 1500, stepIncrement = 5, scene = UI_Scene.Editor)]
        public float Isp;

        [KSPField(guiName = "Thrust", groupName = "calculator4", guiUnits = "kN", guiActive = false, guiActiveEditor = true)]
        public float Thrust;

        [KSPField(guiName = "Beamed Wavelength", groupName = "calculator4", guiActiveEditor = true, guiActive = false)]
        public string wavelength_ui;

        [KSPEvent(guiName = "Toggle Wavelength", guiActive = false, guiActiveEditor = true, groupName = "calculator4")]
        public void ToggleWavelength()
        {
            wavelength_ui = (wavelength_ui == "Long") ? "Short" : "Long";
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                engine.thrustPercentage = 100f;
                float wavelength_num = (float)((wavelength_ui == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
                float spotSize = (float)(1.44 * wavelength_num * dist_ui * 1000000 / dish_dia_ui);
                double powerReceived = (spotSize > recvDiameter) ?
                    recvDiameter / spotSize * beamedPower * (efficiency / 100) * recvEfficiency : beamedPower * (efficiency / 100) * recvEfficiency;

                float shc = PartResourceLibrary.Instance.GetDefinition(propellantName).specificHeatCapacity * 1.5f / 1000f;    // in kJ kg^-1 K^-1
                float density = PartResourceLibrary.Instance.GetDefinition(propellantName).density * 1000;
                Thrust = (float)Math.Round((((powerReceived * thermalEfficiency) / (shc * 2200d * density)) * 9.8d * Isp), 1);
                if (Thrust > engine.GetMaxThrust())
                {
                    Thrust = (float)Math.Round(engine.GetMaxThrust(), 1);
                }
            }
        }
    }
}
