using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class ThermalEngine : PartModule
    {
        [KSPField(guiName = "Received Power", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "kW")]
        public float receivedHeat;

        [KSPField(guiName = "Propellant", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string propellantName;

        [KSPField(isPersistant = false)]
        public float recvDiameter;

        [KSPField(isPersistant = false)]
        public float recvEfficiency;

        [KSPField(isPersistant = false)]
        public float thermalEfficiency;

        Vector3d source; Vector3d dest; double received_power;
        readonly int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        ModuleEnginesFX engine; VesselFinder vesselFinder = new VesselFinder(); int frames;
        OcclusionData occlusion = new OcclusionData();

        List<Vessel> VesselList;
        List<double> excessList;
        List<double> constantList;
        List<string> targetList;
        List<string> wavelengthList;

        public void Start()
        {
            frames = 145;
            engine = this.part.Modules.GetModule<ModuleEnginesFX>();
            VesselList = new List<Vessel>();
            excessList = new List<double>();
            targetList = new List<string>();
            wavelengthList = new List<string>();
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
                + "Thermal Efficiency: " + Convert.ToString(thermalEfficiency * 100) + "%");
        }

        public void FixedUpdate()
        {
            frames += 1;
            if (frames == 150)
            {
                vesselFinder.SourceData(out VesselList, out excessList, out constantList, out targetList, out wavelengthList);
                frames = 0;
            }
            
            if (VesselList.Count > 0)
            {
                dest = this.vessel.GetWorldPos3D();
                received_power = 0;

                // adds up all the received power values from all vessels in CorrectVesselList 
                for (int n = 0; n < VesselList.Count; n++)
                {
                    if (targetList[n] == this.vessel.GetDisplayName())
                    { 
                        double excess2 = excessList[n]; double constant2 = constantList[n];
                        source = VesselList[n].GetWorldPos3D();
                        double distance = Vector3d.Distance(source, dest);
                        double spotsize = constant2 * distance;
                        occlusion.IsOccluded(source, dest, wavelengthList[n], out _, out bool occluded);

                        // adding EC that has been received
                        if (recvDiameter < spotsize)
                        {
                            if (occluded == false)
                            {
                                received_power += Math.Round(((recvDiameter / spotsize) * thermalEfficiency * excess2), 1);
                            }
                        }
                        else
                        {
                            if (occluded == false)
                            {
                                received_power += Math.Round((thermalEfficiency * excess2), 1);
                            }
                        }
                    }
                }

                if (HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().BackgroundProcessing == false)
                {
                    this.part.RequestResource(EChash, -received_power * Time.fixedDeltaTime);
                }
                receivedHeat = Convert.ToSingle(Math.Round(received_power, 1));
            }
            else
            {
                receivedHeat = 0;
                received_power = 0;
            }
            //engine.throttleResponseRate *= -0.2f;
            //float currentisp = engine.atmosphereCurve.Evaluate((float)this.vessel.staticPressurekPa / 101);
            //propellantName = engine.propellants[0].displayName;
            //float shc = engine.propellants[0].resourceDef.specificHeatCapacity / 1000;
            //engine.maxThrust = (float)(received_power * 9.8 * currentisp * thermalEfficiency / (shc * 2400));
        }
    }
}
