using HarmonyLib;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Adds Shift+E to portals and uses their shared destination fields.
    [HarmonyPatch]
    internal static class PortalInteraction
    {
        // Displays the extra interaction in the native hover text.
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        [HarmonyPostfix]
        private static void HoverText(TeleportWorld __instance, ref string __result)
        {
            ZDO zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null)
            {
                return;
            }
            string name = PlayerPortalNames.Get(zdo.m_uid).RemoveRichTextTags();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Portal";
            }
            ZDOID selected = Selected(__instance);
            string title = name;
            if (selected != ZDOID.None)
            {
                string destinationName = PlayerPortalNames.Get(selected)
                    .RemoveRichTextTags();
                if (string.IsNullOrWhiteSpace(destinationName))
                {
                    destinationName = "?";
                }
                title += "  <voffset=-3px>→</voffset>  <color=#FFA500>" +
                    destinationName + "</color>";
            }
            string statusKey = selected != ZDOID.None ? "$piece_portal_connected" :
                "$piece_portal_unconnected";
            string status = Localization.instance.Localize(statusKey);
            __result = title + "\n[" + status + "]\n" +
                "[<color=yellow><b>" +
                Localization.instance.Localize("$KEY_Use") +
                "</b></color>] Rename portal" +
                "\n[<color=yellow><b>Shift+E</b></color>] Change destination";
        }

        // Mirrors vanilla HaveTarget: a stored partner ID is enough to connect.
        [HarmonyPatch(typeof(TeleportWorld), "HaveTarget")]
        [HarmonyPostfix]
        private static void HaveTarget(TeleportWorld __instance, ref bool __result)
        {
            ZDOID selected = Selected(__instance);
            __result = selected != ZDOID.None;
        }

        // Mirrors vanilla: request a missing target and wait for its ZDO.
        [HarmonyPatch(typeof(TeleportWorld), "TargetFound")]
        [HarmonyPrefix]
        private static bool TargetFound(TeleportWorld __instance, ref bool __result)
        {
            ZDOID selected = Selected(__instance);
            __result = PortalConnection.TargetFound(selected);
            return false;
        }

        // Opens the selector instead of the tag editor when Shift is held.
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Interact))]
        [HarmonyPrefix]
        private static bool Interact(TeleportWorld __instance, Humanoid human,
            bool hold, ref bool __result)
        {
            if (hold || human != Player.m_localPlayer)
            {
                return true;
            }
            __result = true;
            if (!PrivateArea.CheckAccess(__instance.transform.position))
            {
                human.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                return false;
            }
            ZDO zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
            if (zdo != null)
            {
                PortalFields.Claim(zdo.m_uid);
                if (ShiftHeld())
                {
                    PortalDirectory.Request(zdo.m_uid);
                }
                else
                {
                    PortalDirectory.ScanNow();
                    TextInput.instance.RequestText(new PortalNameReceiver(zdo.m_uid),
                        "Portal name", PlayerPortalNames.MaxLength);
                }
            }
            return false;
        }

        // Routes the local player's passage through the shared destination.
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        [HarmonyPrefix]
        private static bool Teleport(TeleportWorld __instance, Player player)
        {
            if (player != Player.m_localPlayer)
            {
                return true;
            }
            ZDOID selected = Selected(__instance);
            if (selected == ZDOID.None)
            {
                Plugin.DebugLog("Teleport: no destination.");
                player.Message(MessageHud.MessageType.Center,
                    "Choose a destination with Shift+E.");
                return false;
            }
            if (!CanTeleport(player, __instance))
            {
                return false;
            }
            ZDO target = PortalConnection.GetConfirmedTarget(selected);
            if (target == null)
            {
                PortalConnection.Request(selected);
                return false;
            }
            if (!PortalDirectory.Remember(target))
            {
                Plugin.DebugLog("Teleport: received zdo=" + selected +
                    " could not be recorded.");
                return false;
            }
            Travel(player, target);
            return false;
        }

        // Sends the player to the confirmed destination portal.
        private static void Travel(Player player, ZDO target)
        {
            TeleportWorld destination = ZNetScene.instance?.GetPrefab(
                target.GetPrefab())?.GetComponent<TeleportWorld>();
            if (destination == null)
            {
                Plugin.DebugLog("Teleport: missing portal prefab zdo=" +
                    target.m_uid + ".");
                return;
            }
            Quaternion rotation = target.GetRotation();
            Vector3 exit = target.GetPosition() + rotation * Vector3.forward *
                destination.m_exitDistance + Vector3.up;
            bool accepted = player.TeleportTo(exit, rotation, true);
            Plugin.DebugLog("Teleport: zdo=" + target.m_uid +
                " accepted=" + accepted + ".");
            if (accepted)
            {
                Game.instance.IncrementPlayerStat(PlayerStatType.PortalsUsed);
            }
        }

        // Reads the destination saved on this source portal's ZDO.
        internal static ZDOID Selected(TeleportWorld portal)
        {
            if (Player.m_localPlayer == null)
            {
                return ZDOID.None;
            }
            ZDO zdo = portal.GetComponent<ZNetView>()?.GetZDO();
            return zdo == null ? ZDOID.None : PlayerDestinations.Get(zdo.m_uid);
        }

        // Preserves the vanilla world and inventory restrictions.
        private static bool CanTeleport(Player player, TeleportWorld portal)
        {
            if (ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoPortals))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_blocked");
                return false;
            }
            if (ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoBossPortals) &&
                (RandEventSystem.instance.GetBossEvent() != null ||
                 ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out float bosses)
                 && bosses > 0f))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_blockedbyboss");
                return false;
            }
            if (!player.IsTeleportable(portal.m_allowAllItems))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return false;
            }
            return true;
        }

        // Accepts either Shift key with the normal Use action.
        private static bool ShiftHeld()
        {
            return ZInput.GetKey(KeyCode.LeftShift) || ZInput.GetKey(KeyCode.RightShift);
        }
    }
}
