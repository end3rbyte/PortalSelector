using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Checks saved portals once when the player first spawns in a connection.
    internal static partial class PortalDirectory
    {
        private const float ValidationTimeout = 10f;
        private static readonly Queue<string> ValidationQueue = new Queue<string>();
        private static readonly Dictionary<string, float> ValidationPending =
            new Dictionary<string, float>();
        private static ZNet _validationSession;
        private static bool _validationRequested;
        private static bool _validationStarted;
        private static float _nextValidationRequest;

        // The game raises this event for the first player spawn.
        internal static void BeginValidation()
        {
            if (ZNet.instance != _validationSession)
            {
                ResetValidation();
                _validationSession = ZNet.instance;
            }
            if (!_validationStarted)
            {
                _validationRequested = true;
            }
        }

        // Drops a pending pass when the client changes or leaves a world.
        internal static void ResetValidation()
        {
            ValidationQueue.Clear();
            ValidationPending.Clear();
            _validationSession = null;
            _validationRequested = false;
            _validationStarted = false;
            _nextValidationRequest = 0f;
        }

        // Runs after the catalog has been loaded and the local player is ready.
        private static void UpdateValidation()
        {
            if (ZNet.instance != _validationSession)
            {
                ResetValidation();
                return;
            }
            if (_validationRequested && ZRoutedRpc.instance != null)
            {
                StartValidation();
            }
            DispatchValidation();
            CheckValidation();
        }

        // Snapshots GUIDs so an ID change cannot validate another portal.
        private static void StartValidation()
        {
            _validationRequested = false;
            _validationStarted = true;
            _nextValidationRequest = Time.unscaledTime;
            foreach (PortalRecord record in Records.Values)
            {
                ValidationQueue.Enqueue(PortalIdentity.Key(record));
            }
            Plugin.DebugLog("Validation: first spawn, checking " +
                ValidationQueue.Count + " saved portals.");
        }

        // Requests one saved portal every 250 milliseconds.
        private static void DispatchValidation()
        {
            if (ValidationQueue.Count == 0 ||
                Time.unscaledTime < _nextValidationRequest)
            {
                return;
            }
            string guid = ValidationQueue.Dequeue();
            _nextValidationRequest = Time.unscaledTime + 0.25f;
            PortalRecord record = FindByGuid(guid);
            if (record == null)
            {
                return;
            }
            if (PortalConnection.GetConfirmedTarget(record.Id) != null)
            {
                Plugin.DebugLog("Validation: already available guid=" + guid +
                    " zdo=" + record.Id + ".");
                return;
            }
            ValidationPending[guid] = Time.unscaledTime + ValidationTimeout;
            Plugin.DebugLog("Validation: requesting guid=" + guid +
                " zdo=" + record.Id + " timeout=10s.");
            PortalConnection.Request(record.Id);
        }

        // Keeps the native request active until the object arrives or its deadline passes.
        private static void CheckValidation()
        {
            foreach (string guid in new List<string>(ValidationPending.Keys))
            {
                PortalRecord record = FindByGuid(guid);
                if (record == null)
                {
                    ValidationPending.Remove(guid);
                    continue;
                }
                if (PortalConnection.GetConfirmedTarget(record.Id) != null)
                {
                    ValidationPending.Remove(guid);
                    Plugin.DebugLog("Validation: confirmed guid=" + guid +
                        " zdo=" + record.Id + ".");
                    continue;
                }
                if (Time.unscaledTime < ValidationPending[guid])
                {
                    PortalConnection.Request(record.Id);
                    continue;
                }
                ValidationPending.Remove(guid);
                Plugin.DebugLog("Validation: timed out guid=" + guid +
                    " zdo=" + record.Id + ".");
                MarkInactive(record.Id);
            }
        }
    }
}
