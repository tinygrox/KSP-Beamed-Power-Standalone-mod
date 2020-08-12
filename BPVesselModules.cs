using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    // as vessel modules are processed in the background, this adds background vessel resource management
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BPBackgroundProcessing : VesselModule
    {
        int EChash; BPSettings settings;
        public void Start()
        {
            EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;
            settings = new BPSettings();
        }
        public void FixedUpdate()
        {
            if (settings.BackgroundProcessing == true)
            {
                ConfigNode Node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/persistent.sfs");
                ConfigNode FlightNode = Node.GetNode("GAME").GetNode("FLIGHTSTATE");

                foreach (ConfigNode partnode in FlightNode.GetNode("VESSEL", "name", this.vessel.GetDisplayName()).GetNodes("PART"))
                {
                    if (partnode.HasNode("MODULE"))
                    {
                        foreach (ConfigNode module in partnode.GetNodes("MODULE"))
                        {
                            if (module.GetValue("name") == "WirelessSource")
                            {
                                double powerBeamed = Convert.ToDouble(module.GetValue("powerBeamed"));
                                this.vessel.RequestResource(this.vessel.Parts[0], EChash, powerBeamed, true, true);
                                break;
                            }
                            if (module.GetValue("name") == "WirelessReceiver")
                            {
                                double receivedPower = Convert.ToDouble(module.GetValue("receivedPower"));
                                this.vessel.RequestResource(this.vessel.Parts[0], EChash, -receivedPower, true, true);
                                break;
                            }
                            if (module.GetValue("name") == "WirelessReceiverDirectional")
                            {
                                double receivedPower = Convert.ToDouble(module.GetValue("receivedPower"));
                                this.vessel.RequestResource(this.vessel.Parts[0], EChash, -receivedPower, true, true);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    // a class that calculates waste heat produce by all part modules in the mod
    //[KSPAddon(KSPAddon.Startup.Flight, false)]
    //public class BPWasteHeat : VesselModule
    //{
    //    readonly BPSettings settings = new BPSettings(); 
    //    private int frames; double HeatExcess;
    //    public void Start()
    //    {
    //        frames = 0;
    //    }

    //    public void FixedUpdate()
    //    {
    //        frames += 1;
    //        if (frames == 120)
    //        {
    //            double heatModifier = settings.PercentHeat;
    //            for (int p = 0; p < this.vessel.Parts.Count; p++)
    //            {
    //                if (this.vessel.Parts[p].Modules.Contains<WirelessSource>() == true)
    //                {
    //                    double efficiency = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessSource>().Fields.GetValue("Efficiency"));
    //                    double beamed = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessSource>().Fields.GetValue("powerBeamed"));
    //                    HeatExcess = (1 - efficiency) * beamed * (heatModifier / 100);
    //                }
    //                if (this.vessel.Parts[p].Modules.Contains<WirelessReceiverDirectional>() == true)
    //                {
    //                    double efficiency = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiverDirectional>().Fields.GetValue("recvEfficiency"));
    //                    double received = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiverDirectional>().Fields.GetValue("received_power_ui"));
    //                    HeatExcess = (1 - efficiency) * received * (heatModifier / 100);
    //                }
    //                if (this.vessel.Parts[p].Modules.Contains<WirelessReceiver>() == true)
    //                {
    //                    double efficiency = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiver>().Fields.GetValue("recvEfficiency"));
    //                    double received = Convert.ToDouble(this.vessel.Parts[p].Modules.GetModule<WirelessReceiver>().Fields.GetValue("received_power_ui"));
    //                    HeatExcess = (1 - efficiency) * received * (heatModifier / 100);
    //                }
    //            }
    //            frames = 0;
    //        }
    //    }
    //}
}
