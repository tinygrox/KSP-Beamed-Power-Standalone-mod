using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
    public class WirelessSource : PartModule
    {
        // creating things on part right click menu (flight)
        [KSPField(guiName = "Power Transmitter", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Transmitting;

        [KSPField(guiName = "Beamed Power", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s", guiActiveUnfocused = true, unfocusedRange = 10000000000000)]
        public float excess;

        [KSPField(guiName = "Power to Beam", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s"), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Flight)]
        public float powerBeamed;

        [KSPField(isPersistant = true)]
        public float constant;

        [KSPField(guiName = "Transmitting To", isPersistant = true, guiActive = true, guiActiveEditor = false)]
        public string TransmittingTo;

        // 'dish_diameter', 'efficiency', and 'wavelength' are set in part.cfg file:
        [KSPField(isPersistant = false)]
        public float DishDiameter;

        [KSPField(isPersistant = true)]
        public string Wavelength;

        [KSPField(isPersistant = false)]
        public float Efficiency;

        List<ConfigNode> receiversList; int frames;

        public void Start()
        {
            counter = (counter == null) ? counter = 0 : counter;
            frames = 145;
            receiversList = new List<ConfigNode>();
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
            return "Wireless Source";
        }
        public override string GetModuleDisplayName()
        {
            return "Beamed Power Transmitter";
        }
        public override string GetInfo()
        {
            return ("Dish Diameter: " + Convert.ToString(DishDiameter) + "\n" 
                + "EM Wavelength: " + Convert.ToString(Wavelength) + "\n" 
                + "Efficiency: " + Convert.ToString(Efficiency));
        } 

        // gets all receiver spacecraft's confignodes from savefile
        private void LoadReceiverData()
        {
            ConfigNode Node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/persistent.sfs");
            ConfigNode FlightNode = Node.GetNode("GAME").GetNode("FLIGHTSTATE");
            receiversList = new List<ConfigNode>();

            foreach (ConfigNode vesselnode in FlightNode.GetNodes("VESSEL"))
            {
                foreach (ConfigNode partnode in vesselnode.GetNodes("PART"))
                {
                    if (partnode.HasNode("MODULE"))
                    {
                        foreach (ConfigNode module in partnode.GetNodes("MODULE"))
                        {
                            if (module.GetValue("name") == "WirelessReceiver" | module.GetValue("name") == "WirelessReceiverDirectional")
                            {
                                receiversList.Add(vesselnode);
                                break;
                            }
                        }
                    }
                }
            }
        }

        // main block of code - runs every physics frame
        public void FixedUpdate()
        {
            if (Transmitting == true)
            {
                frames += 1;
                if (frames == 150)
                {
                    try
                    {
                        LoadReceiverData();
                    }
                    catch
                    {
                        Debug.Log("BeamedPowerStandalone.WirelessSource : Unable to load receiver vessel list.");
                    }
                    frames = 0;
                }
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
                    Debug.Log("BeamedPowerStandalone.WirelessSource : Incorrect paramater for wavelength in part.cfg");
                    constant = 0;
                }

                BPSettings settings = new BPSettings();
                if (settings.BackgroundProcessing == false)
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
    }
}
