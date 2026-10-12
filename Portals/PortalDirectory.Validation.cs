using System.Collections.Generic;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Checks known portal objects without blocking the main loop.
    internal static partial class PortalDirectory
    {
        // Schedules linked portals through the existing 250 ms request queue.
        private static void QueueValidation(ZDOID id)
        {
            if (!PortalConnection.TimedOut(id) &&
                !PendingValidation.Contains(id))
            {
                PendingValidation.Enqueue(id);
                _validating = true;
                _validationDeadline = Time.unscaledTime + 10f;
            }
        }

        // Opens immediately with destinations confirmed in this session.
        internal static void Request(ZDOID origin)
        {
            _nextScan = 0f;
            Update();
            List<PortalRecord> available = new List<PortalRecord>();
            foreach (PortalRecord record in Records.Values)
            {
                if (record.Id == origin || Confirmed.Contains(record.Id))
                {
                    available.Add(record);
                }
            }
            if (available.Count == 1 && _validating)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center,
                    "Checking destinations in the background. Try again shortly.");
                return;
            }
            PortalDialog.Open(origin, UniqueRecords(origin, available));
        }

        // Requests the saved portal IDs from the world host once per spawn.
        private static void StartValidation()
        {
            _beginRequested = false;
            _validationDeadline = Time.unscaledTime + 10f;
            _nextValidationRequest = Time.unscaledTime;
            _validating = true;
            foreach (PortalRecord record in Records.Values)
            {
                if (!Confirmed.Contains(record.Id))
                {
                    QueueValidation(record.Id);
                }
            }
            _nextConfirmationCheck = 0f;
        }

        // Sends at most one catalog request every 250 milliseconds.
        private static void DispatchValidation()
        {
            if (!_validating || PendingValidation.Count == 0 ||
                Time.unscaledTime < _nextValidationRequest)
            {
                return;
            }
            ZDOID id = PendingValidation.Dequeue();
            _nextValidationRequest = Time.unscaledTime + 0.25f;
            if (!Confirmed.Contains(id) && PortalConnection.GetTarget(id) == null)
            {
                PortalConnection.Request(id, FindById(id) != null);
                _validationDeadline = Time.unscaledTime + 10f;
            }
        }

        // Adds native portal objects received during the background check.
        private static void RefreshConfirmed()
        {
            if (Time.unscaledTime < _nextConfirmationCheck)
            {
                return;
            }
            _nextConfirmationCheck = Time.unscaledTime + (_validating ? 0.5f : 5f);
            List<ZDOID> expired = new List<ZDOID>();
            foreach (PortalRecord record in Records.Values)
            {
                if (PortalConnection.GetTarget(record.Id) != null)
                {
                    Confirmed.Add(record.Id);
                }
                else if (PortalConnection.TimedOut(record.Id))
                {
                    expired.Add(record.Id);
                }
            }
            foreach (ZDOID id in expired)
            {
                Forget(id, false);
            }
            if (_validating && PendingValidation.Count == 0 &&
                Time.unscaledTime >= _validationDeadline)
            {
                _validating = false;
            }
        }

    }
}
