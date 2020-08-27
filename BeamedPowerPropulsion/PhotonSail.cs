using System;
using System.Collections.Generic;
using UnityEngine;
using BeamedPowerStandalone;

namespace BeamedPowerPropulsion
{
    // optional part module, use for photon sails powered by beamed power
    public class PhotonSail : PartModule
    {
        [KSPField(guiName = "Received power", guiActive = true, guiActiveEditor = false, guiUnits = "kW")]
        public float receivedPower;

        [KSPField(guiName = "Current Thrust", guiActive = true, guiActiveEditor = false, guiUnits = "N")]
        public float thrustN;

        [KSPField(guiName = "Current Acceleration", guiActive = true, guiActiveEditor = false, guiUnits = "mm/s²")]
        public float acceleration;

        [KSPField(guiName = "Power Status", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string state;

        [KSPField(guiName = "Skin Temperature", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K")]
        public float skinTemp;

        // parameters set in part.cfg
        [KSPField(isPersistant = false)]
        public float SurfaceArea;

        [KSPField(isPersistant = false)]
        public float Reflectivity;

        [KSPField(isPersistant = false)]
        public string Wavelength;

        ModuleEngines engines; ReceivedPower receiver;
        const double h = 6.62607004E-34;  // planck's constant

        // a lot of the usual part.cfg parameters for engines are now set within the code itself
        public void Start()
        {
            receiver = new ReceivedPower();
            try
            {
                engines = this.part.Modules.GetModule<ModuleEnginesFX>();
            }
            catch
            {
                Debug.LogError(("BeamedPowerPropulsion.PhotonSail : ModuleEnginesFX not found on part-" + this.part.partName));
            }
        }

        private void AnimationState()
        {
            if (this.part.Modules.Contains<ModuleDeployablePart>())
            {
                if (this.part.Modules.GetModule<ModuleDeployablePart>().deployState == ModuleDeployablePart.DeployState.EXTENDED)
                {
                    this.part.Modules.GetModule<ModuleEnginesFX>().Activate();
                }
                else
                {
                    this.part.Modules.GetModule<ModuleEnginesFX>().Shutdown();
                }
            }
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
            if (HighLogic.LoadedSceneIsFlight)
            {
                acceleration = (float)Math.Round(this.vessel.acceleration.magnitude * 1000, 2);
                AnimationState();

                receiver.Spherical(this.part, true, 100f, SurfaceArea, 1d, true, true, state, out state, out double recvPower);
                receivedPower = (float)Math.Round(recvPower, 1);

                double lambda;

                if (Wavelength == "Long")
                {
                    lambda = Math.Pow(10, -3);
                }
                else
                {
                    lambda = 5 * Math.Pow(10, -8);
                }
                
                double momentum = h / lambda;
                double photonCount = receivedPower * 1000 / (h * (3 * Math.Pow(10, 8) / lambda));

                // adding heat to part's skin
                double heatModifier = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat;
                this.part.AddSkinThermalFlux((float)((1 - Reflectivity) * receivedPower * ((heatModifier / 100) * 0.7)));
                skinTemp = (float)Math.Round(this.part.skinTemperature, 1);

                // code related to the engine module
                double thrustMult = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().photonthrust;
                float Thrust = (float)(momentum * photonCount * Reflectivity * 10 * (engines.realIsp / 30592000) * thrustMult); // in N
                thrustN = engines.GetCurrentThrust() * 1000;
                float percentThrust = Thrust / (engines.maxThrust * 1000);
                engines.thrustPercentage = Mathf.Clamp((float)Math.Round(percentThrust * 100, 2), 0f, 100f);

                // replenishes photons resource
                double fuel_rate = engines.maxThrust * 10000 / (9.81 * engines.realIsp);
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
                float spotArea = (float)(Math.Pow((1.44 * wavelength_num * dist_ui * 1000000d / dish_dia_ui), 2) * 3.14);
                double powerReceived = (spotArea > SurfaceArea) ?
                    SurfaceArea / spotArea * beamedPower * (efficiency / 100) : beamedPower * (efficiency / 100);

                double momentum2 = h / wavelength_num;
                double photonCount2 = powerReceived * 1000 / (h * (30592000 / wavelength_num));
                Thrust = (float)Math.Round((momentum2 * photonCount2 * Reflectivity 
                    * 10d * HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().photonthrust), 3);
            }
        }
    }
}
