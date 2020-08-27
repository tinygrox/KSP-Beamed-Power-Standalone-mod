using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class ReceivedPower
    {
        PlanetOcclusion occlusion = new PlanetOcclusion();
        VesselFinder vesselFinder = new VesselFinder();
        RelativisticEffects relativistic = new RelativisticEffects();

        List<Vessel> VesselList = new List<Vessel>();
        List<double> excessList = new List<double>();
        List<double> constantList = new List<double>();
        List<string> targetList = new List<string>();
        List<string> wavelengthList = new List<string>();
        List<CelestialBody> planetList = new List<CelestialBody>();
        int frames = 140;

        private double FractionalFlux(Vector3d source_pos, Vector3d dest_pos, Part thisPart)
        {
            Vector3d resultant = (source_pos - dest_pos).normalized;
            double Angle = Vector3d.Angle(resultant, -thisPart.transform.up);
            double flux = (Angle < 90 & Angle > -90) ? Math.Cos(Math.PI * Angle / 180) : 0;
            return flux;
        }

        public void Directional(Part thisPart, int counter, bool Listening, float percentagePower, double recvSize, double recvEfficiency,
            bool useSpotArea, bool useFacingVector, string state, out string status, out string VesselName, out double received_power, out int counter2)
        {
            frames += 1;
            if (frames == 150)
            {
                vesselFinder.SourceData(out VesselList, out excessList, out constantList, out targetList, out wavelengthList);
                frames = 0;
            }
            if (counter >= VesselList.Count)
            {
                counter = 0;
            }
            
            if (VesselList.Count > 0)
            {
                if (Listening == true & targetList[counter] == thisPart.vessel.GetDisplayName())
                {
                    Vector3d dest = thisPart.vessel.GetWorldPos3D(); 
                    double excess2 = excessList[counter]; double constant2 = constantList[counter];
                    VesselName = VesselList[counter].GetDisplayName();
                    Vector3d source = VesselList[counter].GetWorldPos3D();
                    double distance = Vector3d.Distance(source, dest);
                    double spotsize = (useSpotArea) ? Math.Pow((constant2 * distance / 2), 2) * 3.14 : constant2 * distance;

                    occlusion.IsOccluded(source, dest, wavelengthList[counter], out CelestialBody body, out bool occluded);

                    // adding EC that has been received
                    if (recvSize < spotsize)
                    {
                        received_power = Math.Round(((recvSize / spotsize) * recvEfficiency * excess2 * (percentagePower / 100)), 1);
                    }
                    else
                    {
                        received_power = Math.Round(((recvEfficiency * excess2) * (percentagePower / 100)), 1);
                    }
                    if (occluded)
                    {
                        received_power = 0;
                        state = "Occluded by " + body.GetDisplayName().TrimEnd(Convert.ToChar("N")).TrimEnd(Convert.ToChar("^"));
                    }

                    received_power *= relativistic.RedOrBlueShift(VesselList[counter], thisPart.vessel, state, out state);
                    if (useFacingVector)
                    {
                        received_power *= FractionalFlux(source, dest, thisPart);
                    }
                }
                else
                {
                    received_power = 0;
                    VesselName = "None";
                }
            }
            else
            {
                received_power = 0;
                VesselName = "None";
            }

            status = state; counter2 = counter;
        }

        public void Spherical(Part thisPart, bool Listening, float percentagePower, double recvSize,
             double recvEfficiency, bool useSpotArea, bool useFacingVector, string state, out string status, out double received_power)
        {
            frames += 1;
            if (frames == 150)
            {
                vesselFinder.SourceData(out VesselList, out excessList, out constantList, out targetList, out wavelengthList);
                frames = 0;
            }

            received_power = 0;
            if (VesselList.Count > 0)
            {
                if (Listening == true)
                {
                    Vector3d dest = thisPart.vessel.GetWorldPos3D();
                    planetList.Clear();
                    // adds up all the received power values from all vessels in CorrectVesselList 
                    for (int n = 0; n < VesselList.Count; n++)
                    {
                        if (targetList[n] == thisPart.vessel.GetDisplayName())
                        {
                            double excess2 = excessList[n]; double constant2 = constantList[n];
                            Vector3d source = VesselList[n].GetWorldPos3D();
                            double distance = Vector3d.Distance(source, dest);
                            double spotsize = (useSpotArea) ? Math.Pow(constant2 * distance / 2, 2) * 3.14 : constant2 * distance;
                            occlusion.IsOccluded(source, dest, wavelengthList[n], out CelestialBody celestial, out bool occluded);


                            // adding EC that has been received
                            if (recvSize < spotsize)
                            {
                                if (occluded == false)
                                {
                                    received_power += ((recvSize / spotsize) * recvEfficiency * excess2 * (percentagePower / 100))
                                        * relativistic.RedOrBlueShift(VesselList[n], thisPart.vessel, state, out state)
                                        * ((useFacingVector) ? FractionalFlux(source, dest, thisPart) : 1);
                                }
                            }
                            else
                            {
                                if (occluded == false)
                                {
                                    received_power += (recvEfficiency * excess2 * (percentagePower / 100))
                                        * relativistic.RedOrBlueShift(VesselList[n], thisPart.vessel, state, out state)
                                        * ((useFacingVector) ? FractionalFlux(source, dest, thisPart) : 1);
                                }
                            }
                            if (occluded)
                            {
                                planetList.Add(celestial);
                            }
                        }
                    }
                    if (planetList.Count > 0)
                    {
                        state = "Occluded by " + planetList[planetList.Count - 1].GetDisplayName()
                            .TrimEnd(Convert.ToChar("N")).TrimEnd(Convert.ToChar("^"));
                    }
                    else
                    {
                        state = "Operational";
                    }
                }
            }
            status = state;
        }
    }

    public class ReceiverPowerCalc : PartModule
    {
        // adds received power calculator to receivers right-click menu in editor

        [KSPField(guiName = "Distance", groupName = "calculator1", groupDisplayName = "Received Power Calculator", groupStartCollapsed = true, guiUnits = "Mm", guiActive = false, guiActiveEditor = true, isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 10000000, stepIncrement = 0.001f, scene = UI_Scene.Editor)]
        public float dist_ui;

        [KSPField(guiName = "Source Dish Diameter", groupName = "calculator1", guiUnits = "m", guiActive = false, guiActiveEditor = true, isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float dish_dia_ui;

        [KSPField(guiName = "Source Efficiency", groupName = "calculator1", guiActive = false, guiActiveEditor = true, guiUnits = "%", isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 100, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float efficiency;

        [KSPField(guiName = "Power Beamed", groupName = "calculator1", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true, isPersistant = false), UI_FloatRange(minValue = 0, maxValue = 100000, stepIncrement = 1, scene = UI_Scene.Editor)]
        public float beamedPower;

        [KSPField(guiName = "Result", groupName = "calculator1", guiUnits = "EC/s", guiActive = false, guiActiveEditor = true, isPersistant = false)]
        public float powerReceived;

        [KSPField(guiName = "Beamed Wavelength", groupName = "calculator1", guiActiveEditor = true, guiActive = false, isPersistant = false)]
        public string wavelength_ui = "Long";

        [KSPEvent(guiName = "Toggle Wavelength", guiActive = false, guiActiveEditor = true, groupName = "calculator1", isPersistent = false)]
        public void ToggleWavelength()
        {
            wavelength_ui = (wavelength_ui == "Long") ? "Short" : "Long";
        }

        public void CalculatePower(float recvDia, float recvefficiency)
        {
            float wavelength_num = (float)((wavelength_ui == "Long") ? Math.Pow(10, -3) : 5 * Math.Pow(10, -8));
            float spot_size = (float)(1.44 * wavelength_num * dist_ui * 1000000 / dish_dia_ui);
            powerReceived = (spot_size > recvDia) ?
                recvDia / spot_size * beamedPower * (efficiency / 100) * recvefficiency : beamedPower * (efficiency / 100) * recvefficiency;
            powerReceived = (float)Math.Round(powerReceived, 1);
        }
    }
}
