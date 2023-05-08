﻿using SIT.Core.Misc;
using SIT.Tarkov.Core;
using System.Reflection;

namespace SIT.Core.SP.Menus
{
    /// <summary>
    /// This Patch Disables the Ready button after you select the location so you will not be jumping to online match by default
    /// Game needs to initialize the offline match variables first unfortunatly
    /// </summary>
    internal class DisableReadyButtonOnLocationScreen_Patch : ModulePatch
    {
        public DisableReadyButtonOnLocationScreen_Patch()
        {
        }

        [PatchPostfix]
        public static void PatchPostfix(ref EFT.UI.DefaultUIButton ____readyButton)
        {
            ____readyButton.gameObject.SetActive(false);
        }

        protected override MethodBase GetTargetMethod()
        {
            return ReflectionHelpers.GetMethodForType(typeof(EFT.UI.Matchmaker.MatchMakerSelectionLocationScreen), "Show", false, true);
            //foreach (var method in typeof(EFT.UI.Matchmaker.MatchMakerSelectionLocationScreen).GetProperties(BindingFlags.NonPublic | BindingFlags.Instance))
            //{
            //    return method?.SetMethod; 
            //}
            //return null;
        }
    }
}