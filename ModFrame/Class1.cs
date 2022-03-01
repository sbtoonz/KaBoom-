using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Kaboom
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Kaboom : BaseUnityPlugin
    {
        private const string ModName = "Kaboom!";
        private const string ModVersion = "1.0";
        private const string ModGUID = "Kaboom.Kaboom.Kaboom";
        private static Harmony harmony = null!;

        internal static GameObject? areaMarker;
        internal static GameObject? playerMarker;
        internal static CircleProjector? _projector;

        internal static ConfigEntry<int>? Radius;
        

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony = new(ModGUID);
            harmony.PatchAll(assembly);

            Radius = Config.Bind("General", "Radius of kabooming",
                10, "How big of a circle to kaboom");
        }
        
        
        public void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        public static class MarkerGrabber
        {
            public static void Postfix(ZNetScene __instance)
            {
                if(__instance.m_prefabs.Count <= 0 || __instance.GetPrefab("Wood") ==null) return;
                areaMarker = Instantiate(__instance.GetPrefab("piece_workbench").transform.Find("AreaMarker").gameObject);
                var temp = areaMarker.transform.Find("Particle System");
                DestroyImmediate(temp.gameObject);
                areaMarker.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        public static class projecterthign
        {
            public static void Postfix(Player __instance)
            {
                playerMarker = Instantiate(areaMarker, __instance.transform, false);
                _projector = playerMarker!.GetComponent<CircleProjector>();
                playerMarker.SetActive(false);
            }
        }
        [HarmonyPatch(typeof(Player), nameof(Player.RemovePiece))]
        [HarmonyWrapSafe]
        public static class KaboomPatch
        {
            [UsedImplicitly]
            public static void Prefix(Player __instance)
            {
               

                if (ZInput.GetButton("AltPlace"))
                {
                     Collider[] colliders = Physics.OverlapSphere(Player.m_localPlayer.transform.position,
                         Radius!.Value, __instance.m_removeRayMask);
                foreach (var collider in colliders)
                {
                 Piece piece = collider.GetComponentInParent<Piece>();
                        if (piece == null && (bool)collider.GetComponent<Heightmap>())
                        {
                            piece = TerrainModifier.FindClosestModifierPieceInRange(collider.transform.position, 2.5f);
                        }
                        if ((bool)piece)
                        {
                            if (!piece!.m_canBeRemoved)
                            {
                                break;
                            }
                            if (Location.IsInsideNoBuildLocation(piece.transform.position))
                            {
                                __instance.Message(MessageHud.MessageType.Center, "$msg_nobuildzone");
                                break;
                            }
                            if (!PrivateArea.CheckAccess(piece.transform.position))
                            {
                                __instance.Message(MessageHud.MessageType.Center, "$msg_privatezone");
                                break;
                            }
                            if (!__instance.CheckCanRemovePiece(piece))
                            {
                                break;
                            }
                            ZNetView component = piece.GetComponent<ZNetView>();
                            if (component == null)
                            {
                                break;
                            }
                            if (!piece.CanBeRemoved())
                            {
                                __instance.Message(MessageHud.MessageType.Center, "$msg_cantremovenow");
                                break;
                            }
                            WearNTear component2 = piece.GetComponent<WearNTear>();
                            if ((bool)component2)
                            {
                                component2.Remove();
                            }
                            else
                            {
                                ZLog.Log("Removing non WNT object with hammer " + piece.name);
                                component.ClaimOwnership();
                                piece.DropResources();
                                piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation, piece.gameObject.transform);
                                __instance.m_removeEffects.Create(piece.transform.position, Quaternion.identity);
                                ZNetScene.instance.Destroy(piece.gameObject);
                            }
                            ItemDrop.ItemData rightItem = __instance.GetRightItem();
                            if (rightItem != null)
                            {
                                __instance.FaceLookDirection();
                                __instance.m_zanim.SetTrigger(rightItem.m_shared.m_attack.m_attackAnimation);
                            }
                        }
                }
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdatePlacementGhost))]
        public static class kaboomcirclepatch
        {
            public static void Postfix(Player __instance)
            {
                if (ZInput.GetButton("AltPlace"))
                {
                    playerMarker!.SetActive(true);
                    _projector!.m_radius = Radius!.Value;
                    _projector.m_nrOfSegments = Radius.Value * 4;
                }
                else
                {
                    playerMarker!.SetActive(false);
                }
            }
        }
    }
}
