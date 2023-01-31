﻿using BepInEx.Logging;
using SIT.Coop.Core.Matchmaker;
using SIT.Coop.Core.Player;
using SIT.Coop.Core.Player.Weapon;
using SIT.Tarkov.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIT.Core.Coop
{
    internal class CoopPatches
    {
        public static ManualLogSource Logger { get; private set; }

        public static void Run(BepInEx.Configuration.ConfigFile config)
        {
            if (Logger == null)
                Logger = BepInEx.Logging.Logger.CreateLogSource("Coop");

            var enabled = config.Bind<bool>("Coop", "Enable", true);
            if(!enabled.Value) // if it is disabled. stop all Coop stuff.
            {
                Logger.LogInfo("Coop has been disabled! Ignoring Patches.");
                return;
            }

            // ------ MATCHMAKER -------------------------
            MatchmakerAcceptPatches.Run();

            // ------ PLAYER -------------------------
            new PlayerOnInitPatch(config).Enable();
            //new PlayerOnApplyCorpseImpulsePatch().Enable();
            //new PlayerOnDamagePatch().Enable();
            //new PlayerOnDeadPatch(config).Enable();
            //new PlayerOnDropBackpackPatch().Enable();
            //new PlayerOnEnableSprintPatch().Enable();
            //new PlayerOnGesturePatch().Enable();
            //new PlayerOnHealPatch().Enable();
            //new PlayerOnInteractWithDoorPatch().Enable();
            //new PlayerOnInventoryOpenedPatch().Enable();
            new PlayerOnJumpPatch().Enable();
            new PlayerOnMovePatch().Enable();
            //new PlayerOnSayPatch().Enable();
            //new PlayerOnSetItemInHandsPatch().Enable();
            //new PlayerOnTryProceedPatch().Enable();

            // ------ WEAPON -------------------------
            //new WeaponOnDropPatch().Enable();
            //new WeaponOnTriggerPressedPatch().Enable();
            //new WeaponOnReloadMagPatch().Enable();



        }
    }
}