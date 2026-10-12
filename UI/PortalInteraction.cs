using HarmonyLib;
using UnityEngine;

namespace Landoria.PortalSelector
{
    // Adds Shift+E to portals and uses each player's chosen destination.
    [HarmonyPatch]
    internal static class PortalInteraction
    {
        // Displays the extra interaction in the native hover text.
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        [HarmonyPostfix]
        private static void HoverText(TeleportWorld __instance, ref string __result)
        {
            ZDO zdo = __instance.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null) { return; }
            string personal = PlayerPortalNames.Get(zdo.m_uid).RemoveRichTextTags();
            if (string.IsNullOrWhiteSpace(personal)) { personal = "Portail"; }
            string shared = PortalDisplay.SharedTag(
                zdo.GetString(ZDOVars.s_tag));
            ZDOID selected = Selected(__instance);
            string title = personal;
            if (selected != ZDOID.None)
            {
                PortalRecord destination = PortalDirectory.Find(selected);
                string destinationName = destination == null ? "?" :
                    PortalDisplay.PersonalName(selected);
                title += " -> " + destinationName;
            }
            string status = selected != ZDOID.None &&
                PortalConnection.GetState(selected) ==
                PortalConnection.State.Connected ? "CONNECTÉ" : "NON CONNECTÉ";
            __result = title + " [" + status + "]\n" +
                "[<color=yellow><b>" +
                Localization.instance.Localize("$KEY_Use") +
                "</b></color>] Renommer le portail" +
                "\n[<color=yellow><b>Shift+E</b></color>] Changer la destination" +
                "\nRepère: " + shared;
        }

        // Shows a personal portal as linked only after its destination is confirmed.
        [HarmonyPatch(typeof(TeleportWorld), "HaveTarget")]
        [HarmonyPostfix]
        private static void HaveTarget(TeleportWorld __instance, ref bool __result)
        {
            ZDOID selected = Selected(__instance);
            __result = selected != ZDOID.None &&
                PortalConnection.GetState(selected) == PortalConnection.State.Connected;
        }

        // Enables the local portal effect when its chosen destination is live.
        [HarmonyPatch(typeof(TeleportWorld), "TargetFound")]
        [HarmonyPostfix]
        private static void TargetFound(TeleportWorld __instance, ref bool __result)
        {
            ZDOID selected = Selected(__instance);
            __result = selected != ZDOID.None &&
                PortalConnection.GetState(selected) == PortalConnection.State.Connected;
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
                if (ShiftHeld())
                {
                    PortalLog.Detail("interaction", "Shift+E at " +
                        PortalLog.Describe(PortalDirectory.Find(zdo.m_uid)));
                    PortalDirectory.Request(zdo.m_uid);
                }
                else
                {
                    PortalLog.Detail("interaction", "E opens personal rename at " +
                        PortalLog.Describe(PortalDirectory.Find(zdo.m_uid)));
                    string shared = PortalDisplay.SharedTag(
                        zdo.GetString(ZDOVars.s_tag));
                    TextInput.instance.RequestText(new PortalNameReceiver(zdo.m_uid),
                        "Nom personnel | Repère partagé : " + shared,
                        PlayerPortalNames.MaxLength);
                }
            }
            return false;
        }

        // Routes only the local player's passage through their chosen portal.
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        [HarmonyPrefix]
        private static bool Teleport(TeleportWorld __instance, Player player)
        {
            if (player != Player.m_localPlayer)
            {
                return true;
            }
            ZDOID selected = Selected(__instance);
            ZDOID source = __instance.GetComponent<ZNetView>()?.GetZDO()?.m_uid ??
                ZDOID.None;
            PortalLog.Throttled("travel:" + source + ":" + selected,
                "travel", "Attempt " + source + " -> " + selected + ".");
            if (selected == ZDOID.None)
            {
                PortalLog.Throttled("travel:none:" + source, "travel",
                    "Blocked: no personal destination for " + source + ".");
                player.Message(MessageHud.MessageType.Center,
                    "Choose a personal destination with Shift+E.");
                return false;
            }
            ZDO target = PortalConnection.GetTarget(selected);
            PortalRecord details = PortalDirectory.Find(selected);
            if (target == null || details == null)
            {
                PortalLog.Throttled("travel:missing:" + selected, "travel",
                    "Waiting for target ZDO " + selected + ".");
                PortalConnection.Request(selected);
                player.Message(MessageHud.MessageType.Center,
                    "Selected portal is not connected yet.");
                return false;
            }
            if (!CanTeleport(player, __instance))
            {
                return false;
            }
            Quaternion rotation = target.GetRotation();
            Vector3 exit = target.GetPosition() + rotation * Vector3.forward *
                details.ExitDistance + Vector3.up;
            if (player.TeleportTo(exit, rotation, true))
            {
                PortalLog.Info("travel", "Teleported " + source + " -> " +
                    PortalLog.Describe(details));
                Game.instance.IncrementPlayerStat(PlayerStatType.PortalsUsed);
            }
            else
            {
                PortalLog.Throttled("travel:failed:" + selected, "travel",
                    "TeleportTo returned false for target " + selected + ".");
            }
            return false;
        }

        // Reads the local player's saved destination for this source portal.
        private static ZDOID Selected(TeleportWorld portal)
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
                PortalLog.Throttled("travel:world-disabled", "travel",
                    "Blocked by NoPortals world key.");
                player.Message(MessageHud.MessageType.Center, "$msg_blocked");
                return false;
            }
            if (ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoBossPortals) &&
                (RandEventSystem.instance.GetBossEvent() != null ||
                 ZoneSystem.instance.GetGlobalKey(GlobalKeys.activeBosses, out float bosses)
                 && bosses > 0f))
            {
                PortalLog.Throttled("travel:boss", "travel",
                    "Blocked by NoBossPortals restriction.");
                player.Message(MessageHud.MessageType.Center, "$msg_blockedbyboss");
                return false;
            }
            if (!player.IsTeleportable(portal.m_allowAllItems))
            {
                PortalLog.Throttled("travel:items", "travel",
                    "Blocked by non-teleportable inventory items.");
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
