using System;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class WirelessSource : PartModule
    {
        // creating things on part right click menu (flight)
        [KSPField(guiName = "Power Transmitter", isPersistant = true, guiActive = true, guiActiveEditor = false), UI_Toggle(scene = UI_Scene.Flight)]
        public bool Transmitting;

        [KSPField(guiName = "Beamed Power", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s")]
        public float excess;

        [KSPField(guiName = "Power to Beam", isPersistant = true, guiActive = true, guiActiveEditor = false, guiUnits = "EC/s"), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Flight)]
        public float powerBeamed;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false)]
        public float constant;

        // 'dish_diameter', 'efficiency', and 'wavelength' are set in part .cfg file:
        // they're also displayed in the editor in part right click menu
        [KSPField(guiName = "Transmitter Dish Diameter", isPersistant = false, guiActive = false, guiActiveEditor = true, guiUnits = "m")]
        public float DishDiameter;

        [KSPField(guiName = "EM Wavelength", isPersistant = false, guiActive = false, guiActiveEditor = true)]
        public string Wavelength;

        [KSPField(guiName = "Transmitter Efficiency", isPersistant = false, guiActive = false, guiActiveEditor = true)]
        public float Efficiency;

        public int EChash = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").id;

        // declaring variables used frequently
        public double ECperSec;

        // setting action group to toggle functionality of part module
        [KSPAction(guiName = "Toggle Power Transmitter")]
        public void ToggleBPTransmitter(KSPActionParam param)
        {
            if (Transmitting == true)
            {
                Transmitting = false;
            }
            else
            {
                Transmitting = true;
            }
        }

        public void FixedUpdate()
        {
            if (Transmitting == true)
            {
                this.vessel.GetConnectedResourceTotals(EChash, out double amount, out _);
                if (amount<1)
                {
                    powerBeamed = 0;
                }
                // a bunch of math
                ECperSec = powerBeamed;
                excess = (float)Math.Round((ECperSec * Efficiency), 1);
                if (Wavelength == "Short")
                {
                    constant = (float)((1.44 * 5 * Math.Pow(10, -8)) / DishDiameter);
                }
                else
                {
                    constant = (float)((1.44 * 5 * Math.Pow(10, -3)) / DishDiameter);
                }

                // reducing amount of EC in craft in each frame (makes it look like smooth EC consumption)
                double resource_drain = ECperSec * Time.fixedDeltaTime;
                this.part.RequestResource(EChash, resource_drain);
            }
            if (Transmitting==false)
            {
                excess = 0;
                ECperSec = 0;
            }
        }
    }
}
