using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Exchanges portal locations after a modded player's first spawn.
    internal static partial class PortalPeerSync
    {
        private const string SnapshotRequestRpc =
            "Landoria.PortalSelector.SnapshotRequestV1";
        private const string SnapshotReplyRpc =
            "Landoria.PortalSelector.SnapshotReplyV1";
        private const int SnapshotFormat = 1;
        private const int SnapshotBatchSize = 48;

        // Exchanges saved portal locations with connected modded players.
        internal static void RequestSnapshot()
        {
            EnsureRegistered();
            if (ZNet.instance == null || Player.m_localPlayer == null ||
                _registered != ZRoutedRpc.instance)
            {
                return;
            }
            var request = new ZPackage();
            request.Write(SnapshotFormat);
            request.Write(ZNet.instance.GetWorldUID());
            Plugin.DebugLog("Peer: join snapshot exchange started.");
            _sending = true;
            try
            {
                _registered.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                    SnapshotRequestRpc, request);
                SendSnapshot(ZRoutedRpc.Everybody);
            }
            finally
            {
                _sending = false;
            }
            Plugin.DebugLog("Peer: exchanged portal snapshots.");
        }

        // Replies only to a modded player requesting this world's catalog.
        private static void ReceiveSnapshotRequest(long sender, ZPackage data)
        {
            if (_sending || sender == 0L || data == null ||
                data.GetArray().Length > 32 || ZNet.instance == null ||
                Player.m_localPlayer == null)
            {
                return;
            }
            try
            {
                if (data.ReadInt() == SnapshotFormat &&
                    data.ReadLong() == ZNet.instance.GetWorldUID())
                {
                    Plugin.DebugLog("Peer: snapshot requested by peer=" +
                        sender + ".");
                    SendSnapshot(sender);
                }
            }
            catch (Exception exception)
            {
                Plugin.WarningLog("Peer: invalid snapshot request (" +
                    exception.GetType().Name + ").");
            }
        }

        // Sends small batches so a large catalog cannot make one huge RPC.
        private static void SendSnapshot(long recipient)
        {
            List<PortalRecord> records = PortalDirectory.KnownRecords();
            for (int offset = 0; offset < records.Count;
                 offset += SnapshotBatchSize)
            {
                int count = Math.Min(SnapshotBatchSize, records.Count - offset);
                var reply = new ZPackage();
                reply.Write(SnapshotFormat);
                reply.Write(ZNet.instance.GetWorldUID());
                reply.Write(count);
                for (int index = offset; index < offset + count; index++)
                {
                    reply.Write(records[index].Guid.ToString("D"));
                    reply.Write(records[index].Position);
                }
                _registered.InvokeRoutedRPC(recipient, SnapshotReplyRpc, reply);
                Plugin.DebugLog("Peer: sent snapshot batch peer=" + recipient +
                    " offset=" + offset + " count=" + count + ".");
            }
            Plugin.DebugLog("Peer: shared catalog peer=" + recipient +
                " total=" + records.Count + ".");
        }

        // Queues received locations for live ZDO verification by GUID.
        private static void ReceiveSnapshotReply(long sender, ZPackage data)
        {
            if (_sending || sender == 0L || data == null ||
                data.GetArray().Length > 8192 || ZNet.instance == null ||
                Player.m_localPlayer == null)
            {
                return;
            }
            try
            {
                if (data.ReadInt() != SnapshotFormat ||
                    data.ReadLong() != ZNet.instance.GetWorldUID())
                {
                    return;
                }
                int count = data.ReadInt();
                if (count < 0 || count > SnapshotBatchSize)
                {
                    return;
                }
                Plugin.DebugLog("Peer: received snapshot batch peer=" +
                    sender + " count=" + count + ".");
                QueueSnapshotEntries(data, count);
            }
            catch (Exception exception)
            {
                Plugin.WarningLog("Peer: invalid snapshot reply (" +
                    exception.GetType().Name + ").");
            }
        }

        // Reads bounded entries and asks the startup pass to verify each one.
        private static void QueueSnapshotEntries(ZPackage data, int count)
        {
            for (int index = 0; index < count; index++)
            {
                string identity = data.ReadString();
                Vector3 position = data.ReadVector3();
                if (Guid.TryParse(identity, out Guid guid) &&
                    guid != Guid.Empty && Finite(position.x) &&
                    Finite(position.y) && Finite(position.z))
                {
                    PortalDirectory.QueueStartupCandidate(guid, position);
                }
                else
                {
                    Plugin.DebugLog("Peer: ignored invalid snapshot entry=" +
                        index + ".");
                }
            }
        }
    }
}
