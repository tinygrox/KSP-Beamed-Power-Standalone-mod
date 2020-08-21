using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class AblativeEngine : ModuleEnginesFX
    {
        [KSPField(guiName = "Thrust", guiActive = true, guiActiveEditor = false, guiUnits = "N", isPersistant = false)]
        public float thrust_ui;

        [KSPField(isPersistant = false)]
        public float SurfaceArea;

        Vector3d source; Vector3d dest; double prevThrust;
        List<Vessel> VesselsList; List<string> targetList;
        List<double> excessList; List<double> constantList;
        List<string> wavelengthList; int frames; double received_power;
        VesselFinder vesselFinder = new VesselFinder();
        RelativeOrientation rotation = new RelativeOrientation();
        //BPOcclusion occlusion = new BPOcclusion();

        public void Start()
        {
            throttleInstant = false;
            useEngineResponseTime = true;
            engineID = "BPAE";
            minThrust = 0;
            engineType = EngineType.Generic;
            frames = 145; prevThrust = 0;
            VesselsList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            wavelengthList = new List<string>();
        }

        public void FixedUpdate()
        {
            frames += 1;
            if (frames == 150)
            {
                vesselFinder.SourceData(out VesselsList, out excessList, out constantList, out targetList, out wavelengthList);
                frames = 0;
            }

            if (VesselsList.Count > 0)
            {
                dest = this.vessel.GetWorldPos3D();
                received_power = 0;

                // adds up all the received power values from all vessels in CorrectVesselList 
                for (int n = 0; n < VesselsList.Count; n++)
                {
                    if (targetList[n] == this.vessel.GetDisplayName())
                    {
                        double excess2 = excessList[n]; double constant2 = constantList[n];
                        source = VesselsList[n].GetWorldPos3D();
                        double distance = Vector3d.Distance(source, dest);
                        double spot_area = Math.Pow((constant2 * distance) / 2, 2) * 3.14;
                        double flux = rotation.FractionalFlux(source, dest, this.vessel, this.part);
                        //occlusion.CheckIfOccluded(VesselsList[n], this.vessel, out _, out bool occluded);

                        // adding EC that has been received
                        if (SurfaceArea < spot_area)
                        {
                            //if (occluded == false)
                            //{
                                received_power += flux * Math.Round(SurfaceArea / spot_area * excess2);
                            //}
                        }
                        else
                        {
                            //if (occluded == false)
                            //{
                                received_power += flux * Math.Round(excess2, 1);
                            //}
                        }
                    }
                }
            }
            else
            {
                received_power = 0;
            }
            
            atmCurveIsp.FindMinMaxValue(out _, out float VacIsp);
            maxThrust = (float)(2 * received_power * 1000 / (VacIsp * 9.8) * 0.4);
            double fuelrate = maxThrust / (9.81 * 3 * VacIsp);
            double TempChangePerSec = received_power * 1000 / (fuelrate * 1200) * 0.7;
            throttleResponseRate = (float)(Math.Abs(GetCurrentThrust() - prevThrust));   // yet to complete
            prevThrust = GetCurrentThrust();
        }
    }
}
