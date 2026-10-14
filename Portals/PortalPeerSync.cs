using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Relays live portal changes through Valheim's routed RPC service.
    internal static partial class PortalPeerSync
    {
        private const string RpcName = "Landoria.PortalSelector.PortalNoticeV2";
        private const int Format = 2;
        private const int Upsert = 1;
        private const int Remove = 2;
        private const int MaxPending = 256;
        private static readonly Queue<PortalNotice> Pending =
            new Queue<PortalNotice>();
        private static ZRoutedRpc _registered;
        private static bool _sending;

        // Registers on each new network session and applies buffered notices.
        internal static void Update()
        {
            EnsureRegistered();
            if (Player.m_localPlayer == null || ZNet.instance == null ||
                WorldGenerator.instance == null)
            {
                return;
            }
            while (Pending.Count > 0)
            {
                Apply(Pending.Dequeue());
            }
        }

        // Clears notices that outlived this plugin instance.
        internal static void Reset()
        {
            Pending.Clear();
        }

        // Announces a newly cataloged or renamed portal to online peers.
        internal static void AnnouncePortal(ZDOID id, PortalRecord record)
        {
            if (record != null && record.Guid != Guid.Empty && id != ZDOID.None)
            {
                Publish(Upsert, id, record.Guid, record.LastKnownName,
                    record.Position);
            }
        }

        // Announces a portal whose destruction Valheim reported locally.
        internal static void AnnounceRemoval(Guid guid)
        {
            if (guid != Guid.Empty)
            {
                Publish(Remove, ZDOID.None, guid, "", Vector3.zero);
            }
        }

        // Installs one handler on the active routed RPC instance.
        private static void EnsureRegistered()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, _registered))
            {
                return;
            }
            rpc.Register<ZPackage>(RpcName, Receive);
            rpc.Register<ZPackage>(SnapshotRequestRpc, ReceiveSnapshotRequest);
            rpc.Register<ZPackage>(SnapshotReplyRpc, ReceiveSnapshotReply);
            _registered = rpc;
            Pending.Clear();
        }

        // Sends a small world-scoped notice to all ready peers.
        private static void Publish(int kind, ZDOID id, Guid guid,
            string name, Vector3 position)
        {
            EnsureRegistered();
            if (ZNet.instance == null || Player.m_localPlayer == null ||
                _registered != ZRoutedRpc.instance)
            {
                return;
            }
            var data = new ZPackage();
            data.Write(Format);
            data.Write(ZNet.instance.GetWorldUID());
            data.Write(kind);
            data.Write(id);
            data.Write(guid.ToString("D"));
            data.Write(name ?? "");
            data.Write(position);
            _sending = true;
            try
            {
                _registered.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName,
                    data);
            }
            finally
            {
                _sending = false;
            }
            Plugin.DebugLog("Peer: announced " + kind + " guid=" + guid + ".");
        }

        // Decodes a notice from another player and waits for local world load.
        private static void Receive(long sender, ZPackage data)
        {
            if (_sending || data == null || data.GetArray().Length > 512)
            {
                return;
            }
            try
            {
                PortalNotice notice = Read(data);
                if (notice == null)
                {
                    return;
                }
                if (Player.m_localPlayer == null || ZNet.instance == null ||
                    WorldGenerator.instance == null)
                {
                    if (Pending.Count < MaxPending)
                    {
                        Pending.Enqueue(notice);
                    }
                    return;
                }
                Apply(notice);
            }
            catch (Exception exception)
            {
                Plugin.WarningLog("Peer: invalid portal notice (" +
                    exception.GetType().Name + ").");
            }
        }

        // Validates the protocol and reconstructs one portal notice.
        private static PortalNotice Read(ZPackage data)
        {
            if (data.ReadInt() != Format)
            {
                return null;
            }
            long world = data.ReadLong();
            int kind = data.ReadInt();
            ZDOID id = data.ReadZDOID();
            string identity = data.ReadString();
            string name = data.ReadString();
            Vector3 position = data.ReadVector3();
            if ((kind != Upsert && kind != Remove) ||
                (kind == Upsert && id == ZDOID.None) ||
                !Guid.TryParse(identity, out Guid guid) || guid == Guid.Empty ||
                name.Length > PlayerPortalNames.MaxLength ||
                !Finite(position.x) || !Finite(position.y) ||
                !Finite(position.z))
            {
                return null;
            }
            return new PortalNotice(world, kind, id, guid, name, position);
        }

        // Rejects invalid positions before saving them to character data.
        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Applies only notices for the world currently loaded by this player.
        private static void Apply(PortalNotice notice)
        {
            if (ZNet.instance == null || Player.m_localPlayer == null ||
                ZNet.instance.GetWorldUID() != notice.World)
            {
                return;
            }
            if (notice.Kind == Remove)
            {
                PortalDirectory.OnPeerRemoval(notice.Guid);
            }
            else
            {
                PortalDirectory.OnPeerPortal(notice.Id, notice.Guid,
                    notice.Name, notice.Position);
            }
        }

        // Holds a notice until the receiving character and world are ready.
        private sealed class PortalNotice
        {
            internal readonly long World;
            internal readonly int Kind;
            internal readonly ZDOID Id;
            internal readonly Guid Guid;
            internal readonly string Name;
            internal readonly Vector3 Position;

            // Captures the wire fields without changing the sender's catalog.
            internal PortalNotice(long world, int kind, ZDOID id, Guid guid,
                string name, Vector3 position)
            {
                World = world;
                Kind = kind;
                Id = id;
                Guid = guid;
                Name = name;
                Position = position;
            }
        }
    }
}
