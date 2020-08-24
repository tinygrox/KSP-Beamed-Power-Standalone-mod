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

        [KSPField(guiName = "Current Thrust", guiActive = true, guiActiveEditor = false, guiUnits = "N")]
        public float thrust_ui;

        [KSPField(guiName = "Current Acceleration", guiActive = true, guiActiveEditor = false, guiUnits = "mm/s²")]
        public float acceleration;

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
        List<string> wavelengthList; int frames; ModuleEngines engines;
        OcclusionData occlusion = new OcclusionData();
        VesselFinder vesselFinder = new VesselFinder(); AnimationSync animation;
        RelativeOrientation rotation = new RelativeOrientation();

        // a lot of the usual part.cfg parameters for engines are now set within the code itself
        public void Start()
        {
            frames = 145;
            VesselsList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            wavelengthList = new List<string>();
            animation = new AnimationSync();
            SetEngineParams();
        }

        private void SetEngineParams()
        {
            this.part.AddModule("ModuleEngines");
            engines = this.part.Modules.GetModule<ModuleEngines>();
            engines.engineID = "BPPS";
            engines.minThrust = 0;
            engines.maxThrust = 5f;
            engines.thrustVectorTransformName = "thrustTransform";
            engines.throttleLocked = true;
            engines.allowShutdown = false;
            engines.allowRestart = false;
            engines.exhaustDamage = false;
            engines.ignitionThreshold = 0.1f;
            engines.engineType = EngineType.Generic;

            engines.atmosphereCurve = new FloatCurve();
            engines.atmosphereCurve.Add(0, 30592000);
            engines.atmosphereCurve.Add(1, 29000000);
            engines.atmosphereCurve.Add(5, 21000000);
            
            
            Propellant fuel = new Propellant();
            fuel.name = "Photons";
            fuel.displayName = "Photons";
            fuel.ratio = 1f;
            fuel.drawStackGauge = false;
            engines.propellants = new List<Propellant>();
            engines.propellants.Add(fuel);
            this.part.Resources.Add("Photons", 10, 10, true, false, true, false, PartResource.FlowMode.Both);
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
            return ("Surface Area: " + Convert.ToString(SurfaceArea) + "m²" + "\n"
                + "Reflectivity: " + Convert.ToString(Reflectivity * 100) + "%" + "\n"
                + "Wavelength: " + Convert.ToString(Wavelength));
        }

        public void FixedUpdate()
        {
            double momentum = 0;
            double photonCount = 0;
            double received_power = 0;

            frames += 1;
            if (frames == 150)
            {
                vesselFinder.SourceData(out VesselsList, out excessList, out constantList, out targetList, out wavelengthList);
                frames = 0;
            }
            acceleration = (float)Math.Round(this.vessel.acceleration.magnitude * 1000, 2);
            animation.SyncAnimationState(this.part);

            if (VesselsList.Count > 0)
            {
                dest = this.vessel.GetWorldPos3D();
                // adds up all the received power values from all vessels in CorrectVesselList 
                for (int n = 0; n < VesselsList.Count; n++)
                {
                    if (targetList[n] == this.vessel.GetDisplayName() && wavelengthList[n] == Wavelength)
                    {
                        double excess2 = excessList[n]; double constant2 = constantList[n];
                        source = VesselsList[n].GetWorldPos3D();
                        double distance = Vector3d.Distance(source, dest);
                        double spot_area = Math.Pow((constant2 * distance) / 2, 2) * 3.14;
                        double flux = rotation.FractionalFlux(source, dest, this.vessel, this.part);
                        occlusion.IsOccluded(source, dest, wavelengthList[n], out _, out bool occluded);

                        // adding EC that has been received
                        if (SurfaceArea < spot_area)
                        {
                            if (occluded == false)
                            {
                                received_power += flux * Math.Round(SurfaceArea / spot_area * excess2);
                            }
                        }
                        else
                        {
                            if (occluded == false)
                            {
                                received_power += flux * Math.Round(excess2, 1);
                            }
                        }
                    }
                }
                animation.SyncAnimationState(this.part);

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
                    lambda = 1;
                    Debug.LogError("BeamedPowerStandalone.PhotonSail : Unknown Wavelength type received.");
                }
                double h = 6.62607004 * Math.Pow(10, -34);  // planck's constant
                momentum = h / lambda;
                photonCount = received_power * 1000 / (h * (3 * Math.Pow(10, 8) / lambda));
            }
            else
            {
                received_power = 0;
                received_power_ui = 0;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                double heatModifier = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat;
                float Thrust = (float)(momentum * photonCount * Reflectivity);
                engines.heatProduction = (float)((1 - Reflectivity) * received_power * ((heatModifier / 100) * 0.7));
                thrust_ui = engines.GetCurrentThrust() * 1000;
                float percentThrust = Thrust / (engines.maxThrust * 1000);
                engines.thrustPercentage = (float)Math.Round(((percentThrust < 1) ? percentThrust * 100 : 100f), 2);
                double fuel_rate = engines.maxThrust * 1000 / (9.81 * 30592000);
                this.part.RequestResource("Photons", -fuel_rate * Time.fixedDeltaTime); // increases quantity of the photons resource
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

        [KSPField(guiName = "Thrust", groupName = "calculator4", guiUnits = "N", guiActive = false, guiActiveEditor = true)]
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
                float wavelength_num = (float)((wavelength_ui == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
                float spotArea = (float)(Math.Pow((1.44 * wavelength_num * dist_ui * 1000000 / dish_dia_ui), 2) * 3.14);
                double powerReceived2 = (spotArea > SurfaceArea) ?
                    SurfaceArea / spotArea * beamedPower * (efficiency / 100) : beamedPower * (efficiency / 100);

                const double h = 6.62607E-34;  // planck's constant
                double momentum2 = h / wavelength_num;
                double photonCount = powerReceived2 * 1000 / (h * (30592000 / wavelength_num));
                Thrust = (float)(momentum2 * photonCount * Reflectivity * HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().photonthrust);
                Thrust = (float)Math.Round(Thrust, 3);
            }
        }
    }
}
