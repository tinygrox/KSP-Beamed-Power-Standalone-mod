using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // optional part module, use for photon sails powered by beamed power
    public class PhotonSail : PartModule
    {
        [KSPField(guiName = "Received power", guiActive = true, guiActiveEditor = false, guiUnits = "kW")]
        public float received_power_ui;

        // parameters set in part.cfg
        [KSPField(isPersistant = false)]
        public float SurfaceArea;

        [KSPField(isPersistant = false)]
        public float Reflectivity;

        [KSPField(isPersistant = false)]
        public string Wavelength;

        Vector3d source; Vector3d dest;
        List<Vessel> VesselsList; List<string> targetList;
        List<double> excessList; List<double> constantList;
        List<string> wavelengthList; int frames;
        TransmitterVessel transmitter = new TransmitterVessel();

        public double momentum;
        public double photonCount;
        public double received_power;

        public void Start()
        {
            frames = 295;
            VesselsList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            wavelengthList = new List<string>();
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "PhotonSail";
        }
        public override string GetModuleDisplayName()
        {
            return "Photon Sail";
        }
        public override string GetInfo()
        {
            return ("Surface Area: " + Convert.ToString(SurfaceArea) + "\n"
                + "Reflectivity: " + Convert.ToString(Reflectivity) + "\n"
                + "Wavelength: " + Convert.ToString(Wavelength));
        }

        

        public void FixedUpdate()
        {
            frames += 1;
            if (frames == 300)
            {
                transmitter.LoadVessels(out VesselsList, out excessList, out constantList, out targetList, out wavelengthList);
                frames = 0;
            }

            if (VesselsList.Count > 0)
            {
                dest = this.vessel.GetWorldPos3D();
                received_power = 0;

                // adds up all the received power values from all vessels in CorrectVesselList 
                for (int n = 0; n < VesselsList.Count; n++)
                {
                    if (targetList[n] == this.vessel.GetDisplayName() && wavelengthList[n] == Wavelength)
                    {
                        double excess2 = excessList[n]; double constant2 = constantList[n];
                        source = VesselsList[n].GetWorldPos3D();
                        double distance = Vector3d.Distance(source, dest);
                        double spot_area = Math.Pow(constant2 * distance, 2) * Math.PI;

                        // adding EC that has been received
                        if (SurfaceArea < spot_area)
                        {
                            //if (DirectionalClass.CheckifOccluded(CorrectVesselList[n], this.vessel)==false)
                            //{
                            received_power += Math.Round(SurfaceArea / spot_area * excess2);
                            //}
                        }
                        else
                        {
                            //if (DirectionalClass.CheckifOccluded(CorrectVesselList[n], this.vessel) == false)
                            //{
                            received_power += Math.Round(excess2, 1);
                            //}
                        }
                    }
                }
                RelativeOrientation rotation = new RelativeOrientation();
                received_power *= rotation.FractionalFlux(source, dest, this.vessel);
                received_power_ui = (float)Math.Round(received_power, 1);
                double lambda;
                if (Wavelength == "Long")
                {
                    lambda = Math.Pow(10, -3);
                }
                else if (Wavelength == "Short")
                {
                    lambda = 5 * Math.Pow(10, -8);
                }
                else
                {
                    lambda = 0;
                    Debug.Log("BeamedPowerStandalone.PhotonSail : Unknown Wavelength type received.");
                }
                double h = 6.62607004 * Math.Pow(10, -34);  // planck's constant
                momentum = h / lambda;
                photonCount = received_power / h * (3 * Math.Pow(10, 8) / lambda);
            }
            else
            {
                received_power = 0;
            }
        }
    }

    public class RelativeOrientation
    {
        public double FractionalFlux(Vector3d source_pos, Vector3d dest_pos, Vessel vessel)
        {
            Vector3d resultant = source_pos - dest_pos;
            Vector3d upvector = vessel.upAxis;
            double Angle = Vector3d.Angle(resultant, upvector);
            double flux = (Angle < 90 & Angle > -90) ? flux = Math.Cos(Angle) : flux = 0;
            return flux;
        }
    }

    public class PhotonEngine : ModuleEnginesFX
    {
        [KSPField(guiName = "Thrust", guiActive = true, guiActiveEditor = false, guiUnits = "N")]
        public float thrust_ui;

        public void Start()
        {
            engineID = "BPPS";
            minThrust = 0;
            throttleLocked = true;
            allowShutdown = false;
            useEngineResponseTime = false;
        }
        public void FixedUpdate()
        {
            PhotonSail sail = new PhotonSail(); BPSettings settings = new BPSettings();
            float Thrust = (float)(sail.momentum * sail.photonCount * sail.Reflectivity);
            heatProduction = (float)((1 - sail.Reflectivity) * sail.received_power * (settings.PercentHeat / 100));
            maxThrust = Thrust * settings.photonthrust;
            thrust_ui = GetCurrentThrust();
        }
    }
}
