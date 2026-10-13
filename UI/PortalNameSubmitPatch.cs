using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Keeps the name dialog open when the chosen name is unavailable.
    [HarmonyPatch]
    internal static class PortalNameSubmitPatch
    {
        // Checks the OK button before Valheim saves and hides the dialog.
        [HarmonyPatch(typeof(TextInput), nameof(TextInput.OnInput))]
        [HarmonyPrefix]
        private static bool BeforeInput(TextInput __instance)
        {
            return PortalNameConfirmPatch.CanSubmit(__instance);
        }

        // Applies the same check when the player submits with Enter.
        [HarmonyPatch(typeof(TextInput), nameof(TextInput.OnEnter))]
        [HarmonyPrefix]
        private static bool BeforeEnter(TextInput __instance)
        {
            return PortalNameConfirmPatch.CanSubmit(__instance);
        }
    }
}
