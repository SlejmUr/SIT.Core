﻿#pragma warning disable CS0618 // Type or member is obsolete
using EFT;
using SIT.Coop.Core.Web;
using SIT.Core.Coop;
using SIT.Core.Coop.Components;
using SIT.Core.Coop.Player;
using SIT.Core.Misc;
using SIT.Core.SP.Raid;
using SIT.Tarkov.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SIT.Coop.Core.Player
{
    /// <summary>
    /// Player Replicated Component is the Player/AI direct communication to the Server
    /// </summary>
    internal class PlayerReplicatedComponent : MonoBehaviour, IPlayerPacketHandlerComponent
    {
        internal const int PacketTimeoutInSeconds = 1;
        //internal ConcurrentQueue<Dictionary<string, object>> QueuedPackets { get; } = new();
        internal Dictionary<string, object> LastMovementPacket { get; set; }
        internal EFT.LocalPlayer player { get; set; }
        public bool IsMyPlayer { get { return player != null && player.IsYourPlayer; } }
        public bool IsClientDrone { get; internal set; }

        private float PoseLevelDesired { get; set; } = 1;
        public float ReplicatedMovementSpeed { get; set; }
        private float PoseLevelSmoothed { get; set; } = 1;

        void Awake()
        {
            //PatchConstants.Logger.LogDebug("PlayerReplicatedComponent:Awake");
        }

        void Start()
        {
            //PatchConstants.Logger.LogDebug($"PlayerReplicatedComponent:Start");

            if (player == null)
            {
                player = this.GetComponentInParent<EFT.LocalPlayer>();
                PatchConstants.Logger.LogDebug($"PlayerReplicatedComponent:Start:Set Player to {player}");
            }

            // ---------------------------------------------------------
            // TODO: Add Dogtags to PMC Clients in match
            if (player.ProfileId.StartsWith("pmc"))
            {
                if (UpdateDogtagPatch.GetDogtagItem(player) == null)
                {
                    var dogtagSlot = player.Inventory.Equipment.GetSlot(EFT.InventoryLogic.EquipmentSlot.Dogtag);
                    //var dogtagItemComponent = dogtagSlot.Add(new DogtagComponent(new Item("")));
                }
            }

            GCHelpers.EnableGC();
        }

        public void ProcessPacket(Dictionary<string, object> packet)
        {
            if (!packet.ContainsKey("m"))
                return;

            var method = packet["m"].ToString();

            var patch = ModuleReplicationPatch.Patches.FirstOrDefault(x => x.MethodName == method);
            if (patch != null)
            {
                // Early bird stop to processing the same item twice!
                //if (!ModuleReplicationPatch.HasProcessed(patch.GetType(), player, packet))
                patch.Replicated(player, packet);

                return;
            }

            ProcessPlayerState(packet);

            //var packetHandlerComponents = this.GetComponents<IPlayerPacketHandlerComponent>();
            //if (packetHandlerComponents != null)
            //{
            //    packetHandlerComponents = packetHandlerComponents.Where(x => x.GetType() != typeof(PlayerReplicatedComponent)).ToArray();
            //    foreach (var packetHandlerComponent in packetHandlerComponents)
            //    {
            //        packetHandlerComponent.ProcessPacket(packet);
            //    }
            //}
        }

        void ProcessPlayerState(Dictionary<string, object> packet)
        {
            if (!packet.ContainsKey("m"))
                return;

            var method = packet["m"].ToString();
            if (method != "PlayerState")
                return;


            if (IsClientDrone)
            {
                // Pose
                float poseLevel = float.Parse(packet["pose"].ToString());
                PoseLevelDesired = poseLevel;

                // Speed
                if (packet.ContainsKey("spd"))
                {
                    ReplicatedMovementSpeed = float.Parse(packet["spd"].ToString());
                    player.CurrentManagedState.ChangeSpeed(ReplicatedMovementSpeed);
                }
                // ------------------------------------------------------
                // Prone -- With fixes. Thanks @TehFl0w
                ProcessPlayerStateProne(packet);



                // Rotation
                if (packet.ContainsKey("rX") && packet.ContainsKey("rY"))
                {
                    Vector2 packetRotation = new(
                float.Parse(packet["rX"].ToString())
                , float.Parse(packet["rY"].ToString())
                );
                    //player.Rotation = packetRotation;
                    ReplicatedRotation = packetRotation;
                }

                if (packet.ContainsKey("spr"))
                {
                    // Sprint
                    ShouldSprint = bool.Parse(packet["spr"].ToString());
                    //ProcessPlayerStateSprint(packet);
                }

                // Position
                Vector3 packetPosition = new(
                    float.Parse(packet["pX"].ToString())
                    , float.Parse(packet["pY"].ToString())
                    , float.Parse(packet["pZ"].ToString())
                    );

                ReplicatedPosition = packetPosition;

                // Move / Direction
                if (packet.ContainsKey("dX") && packet.ContainsKey("dY"))
                {
                    Vector2 packetDirection = new(
                    float.Parse(packet["dX"].ToString())
                    , float.Parse(packet["dY"].ToString())
                    );
                    ReplicatedDirection = packetDirection;
                }
                else
                {
                    ReplicatedDirection = null;
                }

                if (packet.ContainsKey("tilt"))
                {
                    var tilt = float.Parse(packet["tilt"].ToString());
                    player.MovementContext.SetTilt(tilt);
                }


                if (packet.ContainsKey("dX") && packet.ContainsKey("dY") && packet.ContainsKey("spr") && packet.ContainsKey("spd"))
                {
                    // Force Rotation
                    player.Rotation = ReplicatedRotation.Value;
                    var playerMovePatch = (Player_Move_Patch)ModuleReplicationPatch.Patches.FirstOrDefault(x => x.MethodName == "Move");
                    playerMovePatch?.Replicated(player, packet);
                }

                if (packet.ContainsKey("alive"))
                {
                    bool isCharAlive = bool.Parse(packet.ContainsKey("alive").ToString());
                    if (!isCharAlive && (player.PlayerHealthController.IsAlive || player.ActiveHealthController.IsAlive))
                    {
                        var damageType = EFT.EDamageType.Undefined;
                        if (Player_ApplyDamageInfo_Patch.LastDamageTypes.ContainsKey(packet["profileId"].ToString()))
                        {
                            damageType = Player_ApplyDamageInfo_Patch.LastDamageTypes[packet["profileId"].ToString()];
                        }
                        player.ActiveHealthController.Kill(damageType);
                        player.PlayerHealthController.Kill(damageType);

                    }
                }

                return;
            }

        }

        public bool ShouldSprint { get; set; }
        private bool isSprinting;

        public bool IsSprinting
        {
            get { return isSprinting || player.IsSprintEnabled; }
            set { isSprinting = value; }
        }


        private void ProcessPlayerStateSprint(Dictionary<string, object> packet)
        {
            ShouldSprint = bool.Parse(packet["spr"].ToString());

            //    // If we are requesting to sprint but we are alreadying sprinting, don't do anything
            //    //if (ShouldSprint && IsSprinting)
            //    //    return;

            //    if (ShouldSprint)
            //    {
            //        // normalize the movement direction. sprint requires 0 on the Y.
            //        player.MovementContext.MovementDirection = new Vector2(1, 0);
            //        player.MovementContext.PlayerAnimatorEnableSprint(true);
            //        //player.Physical.Sprint(true);
            //        //player.Physical.StaminaCapacity = 100;
            //        //player.Physical.StaminaRestoreRate = 100;
            //        IsSprinting = true;
            //    }
            //    else
            //    {
            //        //player.Physical.Sprint(false);
            //        IsSprinting = false;
            //        player.MovementContext.PlayerAnimatorEnableSprint(false);

            //}
        }

        private void ProcessPlayerStateProne(Dictionary<string, object> packet)
        {
            bool prone = bool.Parse(packet["prn"].ToString());
            if (!player.IsInPronePose)
            {
                if (prone)
                {
                    player.CurrentManagedState.Prone();
                }
            }
            else
            {
                if (!prone)
                {
                    player.ToggleProne();
                    player.MovementContext.UpdatePoseAfterProne();
                }
            }
        }

        private void ShouldTeleport(Vector3 desiredPosition)
        {
            var direction = (player.Position - desiredPosition).normalized;
            Ray ray = new(player.Position, direction);
            LayerMask layerMask = LayerMaskClass.HighPolyWithTerrainNoGrassMask;
        }

        void Update()
        {
            Update_ClientDrone();

            if (IsClientDrone && ShouldSprint)
            {
                player.Physical.Sprint(ShouldSprint);
            }

            if (IsClientDrone)
                return;

            if (player.ActiveHealthController.IsAlive)
            {
                var bodyPartHealth = player.ActiveHealthController.GetBodyPartHealth(EBodyPart.Common);
                if (bodyPartHealth.AtMinimum)
                {
                    var packet = new Dictionary<string, object>();
                    packet.Add("dmt", EDamageType.Undefined.ToString());
                    packet.Add("m", "Kill");
                    AkiBackendCommunicationCoopHelpers.PostLocalPlayerData(player, packet, true);
                }
            }
        }

        private void Update_ClientDrone()
        {
            if (!IsClientDrone)
                return;

            if (!CoopGameComponent.TryGetCoopGameComponent(out _))
                return;

            // Replicate Position.
            // If a short distance -> Smooth Lerp to the Desired Position
            // If the other side of a wall -> Teleport to the correct side (TODO)
            // If far away -> Teleport
            //if (ReplicatedPosition.HasValue)
            //{
            //    var replicationDistance = Vector3.Distance(ReplicatedPosition.Value, player.Position);
            //    var replicatedPositionDirection = ReplicatedPosition.Value - player.Position;
            //    if (replicationDistance >= 3)
            //    {
            //        player.Teleport(ReplicatedPosition.Value, true);
            //    }
            //    else
            //    {
            //        player.Position = Vector3.Lerp(player.Position, ReplicatedPosition.Value, Time.deltaTime * 7);
            //    }
            //}

            // Replicate Rotation.
            // Smooth Lerp to the Desired Rotation
            if (ReplicatedRotation.HasValue)
            {
                player.Rotation = ShouldSprint ? ReplicatedRotation.Value : Vector3.Lerp(player.Rotation, ReplicatedRotation.Value, Time.deltaTime * 4);
            }

            // This will continue movements set be Player_Move_Patch
            //if (ReplicatedDirection.HasValue)
            //{
            //    player.CurrentManagedState.Move(ReplicatedDirection.Value);
            //    player.InputDirection = ReplicatedDirection.Value;
            //}
            //else
            //{
            //    player.InputDirection = Vector2.zero;
            //}

            if (!ShouldSprint)
            {
                PoseLevelSmoothed = Mathf.Lerp(PoseLevelSmoothed, PoseLevelDesired, Time.deltaTime);
                player.MovementContext.SetPoseLevel(PoseLevelSmoothed, true);
            }

            if (ReplicatedDirection.HasValue)
            {
                if (_playerMovePatch == null)
                    _playerMovePatch = (Player_Move_Patch)ModuleReplicationPatch.Patches.FirstOrDefault(x => x.MethodName == "Move");

                _playerMovePatch?.ReplicatedMove(player
                    , new Player_Move_Patch.PlayerMovePacket(player.ProfileId)
                    {
                        dX = ReplicatedDirection.Value.x,
                        dY = ReplicatedDirection.Value.y,
                        spd = ReplicatedMovementSpeed,
                        //spr = ShouldSprint,
                    }
                  );
            }




        }

        Player_Move_Patch _playerMovePatch = (Player_Move_Patch)ModuleReplicationPatch.Patches.FirstOrDefault(x => x.MethodName == "Move");

        private Vector2 LastDirection { get; set; } = Vector2.zero;
        private DateTime LastDirectionSent { get; set; } = DateTime.Now;
        private Vector2 LastRotation { get; set; } = Vector2.zero;
        private DateTime LastRotationSent { get; set; } = DateTime.Now;
        private Vector3 LastPosition { get; set; } = Vector3.zero;
        private DateTime LastPositionSent { get; set; } = DateTime.Now;
        public Vector2? ReplicatedDirection { get; internal set; }
        public Vector2? ReplicatedRotation { get; internal set; }
        public bool? ReplicatedRotationClamp { get; internal set; }
        public Vector3? ReplicatedPosition { get; internal set; }
        public DateTime LastPoseSent { get; private set; }
        public float LastPose { get; private set; }
        public DateTime LastSpeedSent { get; private set; }
        public float LastSpeed { get; private set; }
        public DateTime LastPlayerStateSent { get; private set; } = DateTime.Now;
        public bool TriggerPressed { get; internal set; }

        public Dictionary<string, object> PreMadeMoveDataPacket = new()
        {
            { "dX", "0" },
            { "dY", "0" },
            { "rX", "0" },
            { "rY", "0" },
            { "m", "Move" }
        };
        public Dictionary<string, object> PreMadeTiltDataPacket = new()
        {
            { "tilt", "0" },
            { "m", "Tilt" }
        };

        public bool IsAI()
        {
            return player.IsAI && !player.Profile.Id.StartsWith("pmc");
        }

        public bool IsOwnedPlayer()
        {
            return player.Profile.Id.StartsWith("pmc") && !IsClientDrone;
        }
    }
}
