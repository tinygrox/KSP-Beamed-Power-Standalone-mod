using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class BeamedPowerReflector : PartModule
    {
        // parameters set in part.cfg file
        [KSPField(isPersistant = false)]
        public float Reflectivity;

        [KSPField(isPersistant = false)]
        public float ReflectorDiameter;

        [KSPField(isPersistant = false)]
        public float Efficiency;

        [KSPField(isPersistant = false)]
        public bool CanAmplify;

        // counter variables used to cycle through transmitter and receiver lists respectively
        [KSPField(isPersistant = true)]
        public int? transmitterCounter;

        [KSPField(isPersistant = true)]
        public int? receiverCounter;

        // variables on transmitter
        [KSPField(isPersistant = true)]
        public float excess;

        [KSPField(isPersistant = true)]
        public float constant;

        [KSPField(isPersistant = true)]
        public string Wavelength;

        // reflector specific variables
        [KSPField(guiName = "Power Reflected", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "kW")]
        public float powerReflected;

        [KSPField(guiName = "Amplify power", guiActive = true, guiActiveEditor = false, isPersistant = false, guiUnits = "x"), UI_FloatRange(minValue = 1, maxValue = 5, stepIncrement = 0.01f, scene = UI_Scene.Flight)]
        public float amplifyMult;

        // adding vessel names for 'from' and 'to' to part right-click menu in flight
        [KSPField(guiName = "From", guiActive = true, guiActiveEditor = false, isPersistant = false)]
        public string transmitterName;

        [KSPField(guiName = "To", guiActive = true, guiActiveEditor = false, isPersistant = true)]
        public string TransmittingTo;

        // declaring frequently used variables
        List<Vessel> TransmitterList; int frames; List<ConfigNode> receiverConfigList;
        List<double> excessList; List<double> constantList; List<string> targetList;  List<string> wavelengthList;
        VesselFinder vesselFinder = new VesselFinder();
        OcclusionData occlusion = new OcclusionData();

        // KSPEvent buttons to cycle through vessels lists
        [KSPEvent(guiName = "Cycle through transmitter vessels", guiActive = true, guiActiveEditor = false, requireFullControl = true)]
        public void TransmitterCounter()
        {
            transmitterCounter = (transmitterCounter < TransmitterList.Count - 1) ? transmitterCounter += 1 : transmitterCounter = 0;
        }
        
        [KSPEvent(guiName = "Cycle through receiver vessels", guiActive = true, guiActiveEditor = false, requireFullControl = true)]
        public void ReceiverCounter()
        {
            receiverCounter = (receiverCounter < receiverConfigList.Count - 1) ? receiverCounter += 1 : receiverCounter = 0;
        }

        // initialise variables
        public void Start()
        {
            frames = 145;
            transmitterCounter = (transmitterCounter == null) ? 0 : transmitterCounter;
            receiverCounter = (transmitterCounter == null) ? 0 : receiverCounter;
            TransmitterList = new List<Vessel>();
            excessList = new List<double>();
            constantList = new List<double>();
            targetList = new List<string>();
            wavelengthList = new List<string>();
            receiverConfigList = new List<ConfigNode>();
        }

        // adding part info to part description tab in editor
        public string GetModuleTitle()
        {
            return "BeamedPowerReflector";
        }
        public override string GetModuleDisplayName()
        {
            return "Beamed Power Reflector";
        }
        public override string GetInfo()
        {
            return ("Diameter: " + Convert.ToString(ReflectorDiameter) + "m" + "\n"
                + "Reflectivity: " + Convert.ToString(Reflectivity * 100) + "%");
        }

        // main block of code- runs every physics frame
        public void FixedUpdate()
        {
            if (frames == 150)
            {
                vesselFinder.SourceData(out TransmitterList, out excessList, out constantList, out targetList, out wavelengthList);
                vesselFinder.ReceiverData(out receiverConfigList);
                frames = 0;
            }

            int transmitterCount = Convert.ToInt32(transmitterCounter);
            if (TransmitterList.Count > 0 && receiverConfigList.Count > 0)
            {
                if (targetList[transmitterCount] == this.vessel.GetDisplayName())
                {
                    transmitterName = TransmitterList[transmitterCount].GetDisplayName();
                    double excess1 = (float)excessList[transmitterCount];
                    double constant1 = (float)constantList[transmitterCount];
                    TransmittingTo = receiverConfigList[Convert.ToInt32(receiverCounter)].GetValue("name");
                    Vector3d source = TransmitterList[transmitterCount].GetWorldPos3D();
                    Vector3d dest = TransmitterList[transmitterCount].GetWorldPos3D();
                    double distance = Vector3d.Distance(source, dest);
                    double spot_size = constant1 * distance;
                    occlusion.IsOccluded(source, dest, wavelengthList[transmitterCount], out CelestialBody celestial, out bool occluded);

                    if (spot_size > ReflectorDiameter)
                    {
                        excess = (float)Math.Round(((ReflectorDiameter / spot_size) * Reflectivity * excess1), 1);
                    }
                    else
                    {
                        excess = (float)Math.Round((Reflectivity * excess1), 1);
                    }
                    if (occluded)
                    {
                        excess = 0;
                    }
                    if (CanAmplify)
                    {
                        this.part.RequestResource("ElectricCharge", (double)((excess * amplifyMult) / Efficiency - excess));
                        excess *= amplifyMult;
                    }

                    powerReflected = Convert.ToSingle(Math.Round(excess, 1));

                    if (wavelengthList[transmitterCount] == "Short")
                    {
                        constant = Convert.ToSingle((1.44 * 5 * Math.Pow(10, -8)) / ReflectorDiameter);
                        Wavelength = "Short";
                    }
                    else
                    {
                        constant = Convert.ToSingle((1.44 * Math.Pow(10, -3)) / ReflectorDiameter);
                        Wavelength = "Long";
                    }
                }
            }
        }
    }
}
