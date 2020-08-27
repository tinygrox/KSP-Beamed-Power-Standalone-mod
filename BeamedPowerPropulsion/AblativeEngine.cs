using System;
using System.Collections.Generic;
using UnityEngine;
using BeamedPowerStandalone;

namespace BeamedPowerPropulsion
{
    public class AblativeEngine : PartModule
    {
        [KSPField(guiName = "Received Power", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "kW")]
        public float receivedPower;

        [KSPField(guiName = "Received Power Limiter", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, requireFullControl = true, scene = UI_Scene.Flight)]
        public float percentagePower;

        [KSPField(guiName = "Receiving from", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string CorrectVesselName;

        [KSPField(guiName = "Power Status", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string state;

        [KSPField(guiName = "Propellant", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string propellantNameUI;

        [KSPField(guiName = "Propellant Loss", isPersistant = false, guiActive = true, guiActiveEditor = false, guiUnits = "U/s")]
        public float loss;

        // parameters set in part.cfg file
        [KSPField(isPersistant = false)]
        public float SurfaceArea;

        [KSPField(isPersistant = false)]
        public float Efficiency;

        [KSPField(isPersistant = false)]
        public float thrustMult = 1f;

        // increment through each transmitter vessel
        [KSPField(isPersistant = true)]
        public int counter;

        [KSPEvent(guiName = "Cycle through vessels", guiActive = true, isPersistent = false, requireFullControl = true)]
        public void VesselCounter()
        {
            counter += 1;
        }

        ModuleEnginesFX engine; ReceivedPower receiver;

        public void Start()
        {
            try
            {
                engine = this.part.Modules.GetModule<ModuleEnginesFX>();
            }
            catch
            {
                Debug.LogError(("BeamedPowerPropulsion.AblativeEngine : ModuleEnginesFX not found on part-" + this.part.partName));
            }
            engine.throttleInstant = false;
            engine.throttleResponseRate /= 5;
            engine.engineSpoolTime = 5;
            receiver = new ReceivedPower();
        }

        public string GetModuleTitle()
        {
            return "AblativeEngine";
        }
        public override string GetModuleDisplayName()
        {
            return "Ablative Engine";
        }
        public override string GetInfo()
        {
            return ("Engine Exposed Area: " + Convert.ToString(SurfaceArea) + "m²" + "\n"
                + "Engine Efficiency: " + Convert.ToString(Efficiency * 100) + "%" + "\n");
        }

        // main block of code runs every physics frame only in flight scene
        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                state = "Operational";
                
                // received power code
                receiver.Directional(this.part, counter, true, percentagePower, SurfaceArea, Efficiency, true, true,
                    state, out state, out CorrectVesselName, out double received_power, out counter);

                this.part.GetConnectedResourceTotals(engine.propellants[0].id, out double amount, out _);
                if (amount <= 0.5d)
                {
                    received_power = 0f;
                    state = "Engine has been turned off";
                }
                receivedPower = (float)Math.Round(received_power, 1);

                // code related to engine module
                float currentisp = engine.realIsp;

                // getting propellant resource definition
                propellantNameUI = engine.propellants[0].displayName; string propellantname = engine.propellants[0].name;
                float shc = PartResourceLibrary.Instance.GetDefinition(propellantname).specificHeatCapacity * 1.5f / 1000f;    // in kJ kg^-1 K^-1
                float density = PartResourceLibrary.Instance.GetDefinition(propellantname).density * 1000;  // in kg/l

                // calculate thrust based on power received
                float Thrust = (float)(received_power * 0.2f / (shc * 8600f)) * 9.8f * currentisp;    // in kN
                Thrust = (Thrust < 15f) ? 0f : Thrust; // minimum thrust (min received power) below this there isnt enough heat to exceed metal's boiling point

                // propellant loss
                loss = (float)((received_power / Efficiency) * (1 - Efficiency) * 0.2f / (shc * 2600f * density));  // units/s (l/s)
                loss = (engine.GetCurrentThrust() > 1f) ? loss : 0f;
                this.part.RequestResource(propellantname, (double)loss * Time.fixedDeltaTime);

                // adding heat to part's skin
                double heatModifier = HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat;    // in kW
                this.part.AddSkinThermalFlux(received_power * (1 - Efficiency) * 0.8 * (heatModifier / 100));

                // adjust thrust limiter, based on what max thrust should be as calculated
                float percentThrust = Thrust * thrustMult / engine.maxThrust;
                engine.thrustPercentage = Mathf.Clamp((float)Math.Round(percentThrust * 100, 2), 0f, 100f);
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

        [KSPField(guiName = "Engine Isp", groupName = "calculator4", guiUnits = "s", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 5000, stepIncrement = 5, scene = UI_Scene.Editor)]
        public float Isp;

        [KSPField(guiName = "Angle to source", groupName = "calculator4", guiUnits = "°", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 90, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float angle;

        [KSPField(guiName = "Thrust", groupName = "calculator4", guiUnits = "kN", guiActive = false, guiActiveEditor = true)]
        public float Thrust;

        [KSPField(guiName = "Beamed Wavelength", groupName = "calculator4", guiActiveEditor = true, guiActive = false)]
        public string wavelength_ui = "Long";

        [KSPEvent(guiName = "Toggle Wavelength", guiActive = false, guiActiveEditor = true, groupName = "calculator4")]
        public void ToggleWavelength()
        {
            wavelength_ui = (wavelength_ui == "Long") ? "Short" : "Long";
        }

        // only runs in editor (code for thrust calculator)
        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                engine.thrustPercentage = 100f;
                float wavelength_num = (float)((wavelength_ui == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
                float spotArea = (float)Math.Pow((1.44 * wavelength_num * dist_ui * 1000000 / dish_dia_ui)/2, 2) * 3.14f;
                double powerReceived = (spotArea > SurfaceArea) ?
                    (SurfaceArea / spotArea) * beamedPower * (efficiency / 100) * Efficiency : beamedPower * (efficiency / 100) * Efficiency;
                powerReceived *= Math.Cos(angle * Math.PI / 180);

                string propellantname = engine.propellants[0].name;
                float shc = PartResourceLibrary.Instance.GetDefinition(propellantname).specificHeatCapacity * 1.5f / 1000f;    // in kJ kg^-1 K^-1
                Thrust = Mathf.Clamp((float)Math.Round((powerReceived * 0.2f / (shc * 8600f)) * 9.8d * Isp, 1), 
                    0f, engine.GetMaxThrust());
                Thrust = (Thrust < 15f) ? 0f : Thrust;
            }
        }
    }
}
