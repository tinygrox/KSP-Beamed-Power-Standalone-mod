using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // as vessel modules are processed in the background, this adds background vessel resource management
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BPBackgroundProcessing : VesselModule
    {
        int EChash;
        public void Start()
        {
            EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
        }
        public void FixedUpdate()
        {
            for (int p = 0; p < this.vessel.Parts.Count; p++)
            {
                if (this.vessel.Parts[p].Modules.Contains<WirelessSource>() == true)
                {
                    double demand = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessSource>().Fields.GetValue("powerBeamed"));
                    this.vessel.Parts[p].RequestResource(EChash, demand * Time.fixedDeltaTime);
                }
                if (this.vessel.Parts[p].Modules.Contains<WirelessReceiverDirectional>() == true)
                {

                }
                if (this.vessel.Parts[p].Modules.Contains<WirelessReceiver>() == true)
                {


                }
            }
        }
    }

    // a class that calculates waste heat produce by all part modules in the mod
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BPWasteHeat : VesselModule
    {
        readonly BPSettings settings = new BPSettings(); 
        private int frames; double HeatExcess;
        public void Start()
        {
            frames = 0;
        }

        public void FixedUpdate()
        {
            frames += 1;
            if (frames == 120)
            {
                double heatModifier = settings.PercentHeat;
                for (int p = 0; p < this.vessel.Parts.Count; p++)
                {
                    if (this.vessel.Parts[p].Modules.Contains<WirelessSource>() == true)
                    {
                        double efficiency = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessSource>().Fields.GetValue("Efficiency"));
                        double beamed = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessSource>().Fields.GetValue("powerBeamed"));
                        HeatExcess = (1 - efficiency) * beamed * (heatModifier / 100);
                    }
                    if (this.vessel.Parts[p].Modules.Contains<WirelessReceiverDirectional>() == true)
                    {
                        double efficiency = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiverDirectional>().Fields.GetValue("recvEfficiency"));
                        double received = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiverDirectional>().Fields.GetValue("received_power_ui"));
                        HeatExcess = (1 - efficiency) * received * (heatModifier / 100);
                    }
                    if (this.vessel.Parts[p].Modules.Contains<WirelessReceiver>() == true)
                    {
                        double efficiency = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiver>().Fields.GetValue("recvEfficiency"));
                        double received = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiver>().Fields.GetValue("received_power_ui"));
                        HeatExcess = (1 - efficiency) * received * (heatModifier / 100);
                    }
                }
                frames = 0;
            }
        }
    }
}
