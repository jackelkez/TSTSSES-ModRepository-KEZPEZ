﻿using Modular_Weaponry.Data.Scripts.WeaponScripts.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static Modular_Weaponry.Data.Scripts.WeaponScripts.Client.ClientSyncDefinitions;

namespace Modular_Weaponry.Data.Scripts.WeaponScripts.Client
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class ClientSync : MySessionComponentBase
    {
        const ushort SyncId = 8770;
        private ClientSync Instance;

        public override void LoadData()
        {
            // This should only run on clients.
            if (MyAPIGateway.Multiplayer.IsServer)
                return;

            MyLog.Default.WriteLineAndConsole("Modular Weaponry: ClientSync loading...");

            Instance = this;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(SyncId, MessageHandler);

            // TODO remove
            MyAPIGateway.Entities.OnEntityAdd += OnGridAdd;
        }

        private void OnGridAdd(IMyEntity entity)
        {
            MyLog.Default.WriteLineAndConsole("gridadd");
            if (!(entity is IMyCubeGrid))
                return;

            IMyCubeGrid grid = (IMyCubeGrid)entity;

            // Exclude projected and held grids
            if (grid.Physics == null)
                return;

            grid.OnBlockAdded += OnBlockAdd;
            grid.OnBlockRemoved += OnBlockRemove;
        }

        private void OnBlockAdd(IMySlimBlock block)
        {
            MyLog.Default.WriteLineAndConsole("blockadd");
            if (WeaponPartManager.Instance.wAPI.HasCoreWeapon((MyEntity)block.FatBlock))
                WeaponPartManager.Instance.wAPI.AddProjectileCallback((MyEntity)block.FatBlock, 0, ProCall);
        }

        private void OnBlockRemove(IMySlimBlock block)
        {
            if (WeaponPartManager.Instance.wAPI.HasCoreWeapon((MyEntity)block.FatBlock))
                WeaponPartManager.Instance.wAPI.RemoveProjectileCallback((MyEntity)block.FatBlock, 0, ProCall);
        }

        protected override void UnloadData()
        {
            // This should only run on clients.
            if (MyAPIGateway.Multiplayer.IsServer)
                return;

            MyLog.Default.WriteLineAndConsole("Modular Weaponry: ClientSync closing...");
            Instance = null;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(SyncId, MessageHandler);
        }

        public static void ServerSyncProjectile(ulong projectileId, MyTuple<bool, Vector3D, Vector3D, float> projectileData)
        {
            // This should only run on server.
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;

            ProjectileContainer container = new ProjectileContainer(projectileId, projectileData, DateTime.Now.Ticks);
            byte[] serializedData = MyAPIGateway.Utilities.SerializeToBinary(container);

            MyAPIGateway.Multiplayer.SendMessageToOthers(SyncId, serializedData);
        }

        private void MessageHandler(ushort handlerId, byte[] package, ulong senderId, bool fromServer)
        {
            if (MyAPIGateway.Session.IsServer && fromServer)
                return;
            try
            {
                ProjectileContainer container = MyAPIGateway.Utilities.SerializeFromBinary<ProjectileContainer>(package);

                if (container == null)
                {
                    MyLog.Default.WriteLineAndConsole($"Modular Weaponry: Invalid message from \nHID: {handlerId}\nSID: {senderId}\nFromServer: {fromServer}");
                    return;
                }
                ClientSyncProjectile(container.ProjectileId, container.ProjectileData, container.Time);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole($"Modular Weaponry: Exception in ClientSync.MessageHandler: {ex}\n{ex.StackTrace}");
            }
        }

        private void ClientSyncProjectile(ulong projectileId, MyTuple<bool, Vector3D, Vector3D, float> projectileData, long fireTime)
        {
            double delta = (DateTime.Now.Ticks - fireTime)/(double) TimeSpan.TicksPerSecond;

            Vector3D newPosition = projectileData.Item2 + projectileData.Item3 * delta;
            projectileData.Item2 = newPosition;

            WeaponPartManager.Instance.wAPI.SetProjectileState(projectileId, projectileData);
            MyLog.Default.WriteLineAndConsole($"UpdateProj Id: {projectileId} Vel: {WeaponPartManager.Instance.wAPI.GetProjectileState(projectileId).Item2.Length()} Delta: {delta}");
        }

        private void ProCall(long firerEntityId, int firerPartId, ulong projectileId, long targetEntityId, Vector3D projectilePosition, bool projectileExists)
        {
            MyLog.Default.WriteLineAndConsole("procall");
            if (projectileExists)
            {
                MyLog.Default.WriteLineAndConsole($"FireProj Id: {projectileId} Vel: {WeaponPartManager.Instance.wAPI.GetProjectileState(projectileId).Item2.Length()}");
            }
        }
    }
}
