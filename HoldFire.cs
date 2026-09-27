using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using GHPC;
using GHPC.AI.Platoons;
using GHPC.Player;
using GHPC.UI.ContextMenu;
using GHPC.Weapons;
using UnityEngine;
using UnityEngine.Events;

namespace ImprovedAI {

  internal static class HoldFireState {
    internal static readonly HashSet<Unit> HoldFire = new HashSet<Unit>();

    internal static void SetPlatoonHoldFire(PlatoonData platoon, bool hold) {
      if (platoon?.Units == null) return;
      foreach (var unit in platoon.Units) {
        if (unit == null) continue;
        if (hold) {
          if (unit != PlayerInput.Instance?.CurrentPlayerUnit)
            HoldFire.Add(unit);
        } else {
          HoldFire.Remove(unit);
        }
      }
    }
  }

  //map context menu
  [HarmonyPatch(typeof(GroupContextMenuController), "AddHaltContinueButton")]
  public static class HoldFireMapContextMenuPatch {
    private static FieldInfo _builderFld;
    private static FieldInfo _dataFld;
    private static float _lastAdded = -1f;

    [HarmonyPostfix]
    static void Postfix(GroupContextMenuController __instance) {
      float now = Time.time;
      if (now - _lastAdded < 0.2f) return;
      _lastAdded = now;
      if (_builderFld == null) {
        var baseType = typeof(GroupContextMenuController).BaseType;
        _builderFld = baseType?.GetField("_builder", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        _dataFld = baseType?.GetField("_data", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      }

      var builder = _builderFld?.GetValue(__instance) as ContextMenuBuilder;
      var data = _dataFld?.GetValue(__instance);
      var platoon = data as PlatoonData;
      if (builder == null || platoon == null) return;

      builder.AddButton(
        new UnityAction(builder.Close),
        "Hold Fire/Yes", delegate { HoldFireState.SetPlatoonHoldFire(platoon, true); }, false
      );
      builder.AddButton(
        new UnityAction(builder.Close), "Hold Fire/No", delegate { HoldFireState.SetPlatoonHoldFire(platoon, false); }, false
      );
    }
  }

  //vehicle context menu

  [HarmonyPatch(typeof(PlayerInput), "AddBailoutButton")]
  public static class HoldFireVehicleContextMenuPatch {
    private static MethodInfo _addMenuButtonMth;
    private static float _lastAdded = -1f;

    [HarmonyPostfix]
    static void Postfix(PlayerInput __instance) {
      float now = Time.time;
      if (now - _lastAdded < 0.1f) return;
      _lastAdded = now;

      var playerUnit = __instance.CurrentPlayerUnit;
      if (playerUnit == null || !playerUnit.PlatoonInitialized) return;
      if (!PlatoonManager.IsPlatoonLeader(playerUnit)) return;
      var platoon = playerUnit.Platoon;
      if (platoon == null) return;

      if (_addMenuButtonMth == null)
        _addMenuButtonMth = typeof(PlayerInput).GetMethod("AddMenuButton",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(string), typeof(UnityAction) },
            null);

      _addMenuButtonMth?.Invoke(__instance, new object[] {
        "Platoon/Hold Fire/Yes", new UnityAction(() => HoldFireState.SetPlatoonHoldFire(platoon, true))
      });
      _addMenuButtonMth?.Invoke(__instance, new object[] {
        "Platoon/Hold Fire/No", new UnityAction(() => HoldFireState.SetPlatoonHoldFire(platoon, false))
      });
    }
  }

  [HarmonyPatch(typeof(WeaponSystem), "get_AbleToFire")]
  public static class AbleToFirePatch {
    [HarmonyPostfix]
    static void Postfix(WeaponSystem __instance, ref bool __result) {
      if (!__result) return;
      var unit = __instance.GetComponentInParent<Unit>();
      if (unit != null && HoldFireState.HoldFire.Contains(unit))
        __result = false;
    }
  }

}
