using System;
using System.Collections.Generic;
using UnityEngine;

namespace BeamedPowerStandalone
{
    public class AblativeEngine : ModuleEnginesFX
    {
        [KSPField(guiName = "Thrust", guiActive = true, guiActiveEditor = false, guiUnits = "N")]
        public float thrust_ui;
		// not yet complete, and thus not compiled into released .dll
    }
}
