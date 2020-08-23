using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class WirelessSource : PartModule
    {
        // creating things on part right click menu (flight)
        [KSPField(guiName = "Power Transmitter", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Transmitting;

        [KSPField(guiName = "Power to Beam", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s"), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Flight)]
        public float powerBeamed;

        [KSPField(guiName = "Beamed Power", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s", guiActiveUnfocused = true, unfocusedRange = 10000000000000)]
        public float excess;

        [KSPField(isPersistant = true)]
        public float constant;

        [KSPField(guiName = "Transmitting To", isPersistant = true, guiActive = true, guiActiveEditor = false)]
        public string TransmittingTo;

        [KSPField(guiName = "Status", isPersistant = false, guiActive = true, guiActiveEditor = false)]
        public string state;

        [KSPField(guiName = "Core Temperature", groupName = "HeatInfo", groupDisplayName = "Heat Info", groupStartCollapsed = false, guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/900K")]
        public float coreTemp;

        [KSPField(guiName = "Skin Temperature", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "K/1200K")]
        public float skinTemp;

        [KSPField(guiName = "Waste Heat", groupName = "HeatInfo", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float wasteHeat;

        // 'dish_diameter', 'efficiency', and 'wavelength' are set in part.cfg file:
        [KSPField(isPersistant = false)]
        public float DishDiameter;

        [KSPField(isPersistant = true)]
        public string Wavelength;

        [KSPField(isPersistant = true)]
        public float Efficiency;

        List<ConfigNode> receiversList; int frames; ModuleCoreHeat coreHeat; int initFrames;
        VesselFinder vesselFinder = new VesselFinder(); AnimationSync animation = new AnimationSync();

        public void Start()
        {
            counter = (counter == null) ? counter = 0 : counter;
            frames = 145; initFrames = 0;
            receiversList = new List<ConfigNode>();
            animation = new AnimationSync();
            SetHeatParams();
        }

        private void SetHeatParams()
        {
            this.part.AddModule("ModuleCoreHeat");
            coreHeat = this.part.Modules.GetModule<ModuleCoreHeat>();
            coreHeat.CoreTempGoal = 1300d;  // placeholder value, there is no optimum temperature
            coreHeat.CoolantTransferMultiplier *= 3d;
            coreHeat.radiatorCoolingFactor *= 2d;
            coreHeat.HeatRadiantMultiplier *= 2d;
        }

        [KSPField(isPersistant = true)]
        public int? counter;

        [KSPEvent(guiName = "Cycle through vessels", guiActive = true, isPersistent = false, requireFullControl = true)]
        public void VesselCounter()
        {
            counter = (counter < receiversList.Count - 1) ? counter += 1 : counter = 0;
        }

        // getting resource id of 'Electric Charge'
        public int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;

        // setting action group capability
        [KSPAction(guiName = "Toggle Power Transmitter")]
        public void ToggleBPTransmitter(KSPActionParam param)
        {
            Transmitting = Transmitting ? false : true;
        }

        [KSPAction(guiName = "Activate Power Transmitter")]
        public void ActivateBPTransmitter(KSPActionParam param)
        {
            Transmitting = Transmitting ? true : true;
        }

        [KSPAction(guiName = "Deactivate Power Transmitter")]
        public void DeactivateBPTransmitter(KSPActionParam param)
        {
            Transmitting = Transmitting ? false : false;
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "WirelessSource";
        }
        public override string GetModuleDisplayName()
        {
            return "Beamed Power Transmitter";
        }
        public override string GetInfo()
        {
            return ("Dish Diameter: " + Convert.ToString(DishDiameter) + "m" + "\n" 
                + "EM Wavelength: " + Convert.ToString(Wavelength) + "\n" 
                + "Efficiency: " + Convert.ToString(Efficiency * 100) + "%" + "\n" + "" +"\n"
                + "Max Core Temp: 900K" + "\n"
                + "Max Skin Temp: 1200K" + "\n" + "" + "\n"
                + "This transmitter will shutdown past these temperatures.");
        }

        private void AddHeatToCore()
        {
            coreTemp = (float)(Math.Round(coreHeat.CoreTemperature, 1));
            skinTemp = (float)(Math.Round(this.part.skinTemperature, 1));
            if (coreTemp > 900f | skinTemp > 1200f)
            {
                state = "Exceeded Temperature Limit";
                Transmitting = (Transmitting) ? false : false;
            }
            if (state == "Exceeded Temperature Limit" & (coreTemp > 700f | skinTemp > 1000f))
            {
                Transmitting = (Transmitting) ? false : false;
            }
            if (coreTemp < 700f & skinTemp < 1000f)
            {
                state = "Operational";
            }
            double heatModifier = (double)HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().PercentHeat / 100;
            double heatExcess = (1 - Efficiency) * (excess / Efficiency) * heatModifier;
            wasteHeat = (float)Math.Round(heatExcess, 1);
            coreHeat.AddEnergyToCore(heatExcess * 0.7 * 3 * Time.fixedDeltaTime);  // first converted to kJ
            part.AddSkinThermalFlux(heatExcess * 0.3);      // some heat added to skin
        }

        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            if (initFrames < 60)
            {
                initFrames += 1;
            }
            else
            {
                AddHeatToCore();
            }
            
            if (Transmitting == true)
            {
                frames += 1;
                if (frames == 150)
                {
                    try
                    {
                        vesselFinder.ReceiverData(out receiversList);
                    }
                    catch
                    {
                        Debug.Log("BeamedPowerStandalone.WirelessSource : Unable to load receiver vessel list.");
                    }
                    frames = 0;
                }
                
                counter = (counter < receiversList.Count) ? counter : counter = 0;
                try
                {
                    TransmittingTo = receiversList[Convert.ToInt32(counter)].GetValue("name");
                }
                catch
                {
                    TransmittingTo = "None";
                }
                
                this.vessel.GetConnectedResourceTotals(EChash, out double amount, out _);
                if (amount < 1)
                {
                    powerBeamed = 0;
                }
                // a bunch of math
                excess = Convert.ToSingle(Math.Round((powerBeamed * Efficiency), 1));
                if (Wavelength == "Short")      // short ultraviolet
                {
                    constant = Convert.ToSingle((1.44 * 5 * Math.Pow(10, -8)) / DishDiameter);
                }
                else if (Wavelength == "Long")  // short microwave
                {
                    constant = Convert.ToSingle((1.44 * Math.Pow(10, -3)) / DishDiameter);
                }
                else
                {
                    Debug.LogError("BeamedPowerStandalone.WirelessSource : Incorrect paramater for wavelength in part.cfg");
                    constant = 0;
                }
                animation.SyncAnimationState(this.part);

                if (HighLogic.CurrentGame.Parameters.CustomParams<BPSettings>().BackgroundProcessing == false)
                {
                    // reducing amount of EC in craft in each frame (makes it look like continuous EC consumption)
                    double resource_drain = powerBeamed * Time.fixedDeltaTime;
                    this.part.RequestResource(EChash, resource_drain);
                }
            }
            if (Transmitting==false)
            {
                excess = 0;
            }
        }

        // adds received power calculator in transmitter part's right click menu in editor
        [KSPField(guiName = "Distance", groupName = "calculator3", groupDisplayName = "Received Power Calculator", groupStartCollapsed = true, guiUnits = "Mm", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 10000000, stepIncrement = 0.001f, scene = UI_Scene.Editor)]
        public float dist_ui;

        [KSPField(guiName = "Receiver Diameter", groupName = "calculator3", guiUnits = "m", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 50, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float recv_diameter;

        [KSPField(guiName = "Receiver Efficiency", groupName = "calculator3", guiActive = false, guiActiveEditor = true, guiUnits = "%"), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float recv_efficiency;

        [KSPField(guiName = "Power Beamed", groupName = "calculator3", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float beamedPower;

        [KSPField(guiName = "Result", groupName = "calculator3", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true)]
        public float powerReceived;

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                float wavelength_num = (float)((Wavelength == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
                float spot_size = (float)(1.44 * wavelength_num * dist_ui * 1000000 / DishDiameter);
                powerReceived = (spot_size > recv_diameter) ?
                    recv_diameter / spot_size * beamedPower * Efficiency * (recv_efficiency / 100) : beamedPower * Efficiency * (recv_efficiency / 100);
                powerReceived = (float)Math.Round(powerReceived, 1);
            }
        }
    }
}
