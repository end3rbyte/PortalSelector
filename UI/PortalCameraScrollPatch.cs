using HarmonyLib;

namespace Landoria.PortalSelector
{
    // Prevents the camera from zooming while the destination dialog is open.
    [HarmonyPatch]
    internal static class PortalCameraScrollPatch
    {
        private static bool _readingCameraScroll;

        // Marks camera input while the destination dialog is visible.
        [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
        [HarmonyPrefix]
        private static void BeforeCamera()
        {
            _readingCameraScroll = PortalDialog.IsOpen;
        }

        // Restores wheel input for UI scrolling after the camera update.
        [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
        [HarmonyPostfix]
        private static void AfterCamera()
        {
            _readingCameraScroll = false;
        }

        // Gives the camera no wheel movement while the destination dialog is open.
        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
        [HarmonyPrefix]
        private static bool CameraWheel(ref float __result)
        {
            if (!_readingCameraScroll)
            {
                return true;
            }
            __result = 0f;
            return false;
        }
    }
}
