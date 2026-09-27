using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using GHPC;
using GHPC.AI.Platoons;
using GHPC.Player;
using GHPC.UI.ContextMenu;
using GHPC.UnitMapControl;
using GHPC.Equipment;
using GHPC.Equipment.Lamps;
using GHPC.Crew;
using UnityEngine;
using UnityEngine.Events;

namespace ImprovedAI {
  internal static class IrLightsState {
    internal static readonly HashSet<Unit> Suppressed = new HashSet<Unit>();

    internal static void SetPlatoonIR(PlatoonData platoon, bool on) {
      if (platoon?.Units == null) return;
      foreach (var unit in platoon.Units) {
        if (unit == null) continue;
        if (on) {
          Suppressed.Remove(unit);
        } else {
          Suppressed.Add(unit);
          if (unit != PlayerInput.Instance?.CurrentPlayerUnit)
          {
            var activator = unit.GetComponent<EquipmentManager>() as IIlluminationActivator;
            activator?.SetIllumination(LampType.GunnerSpotlight, false);
          }
        }
      }
    }
  }

  //map context menu add button
  [HarmonyPatch(typeof(GroupContextMenuController), "AddHaltContinueButton")]
  public static class PlatoonContextMenuPatch {
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
        _dataFld    = baseType?.GetField("_data",    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      }

      var builder = _builderFld?.GetValue(__instance) as ContextMenuBuilder;
      var data    = _dataFld?.GetValue(__instance);
      var platoon = data as PlatoonData;
      if (builder == null || platoon == null) return;

      builder.AddButton(
        new UnityAction(builder.Close),
        "IR Lights/On",
        delegate { IrLightsState.SetPlatoonIR(platoon, true); },
        false
      );
      builder.AddButton(
        new UnityAction(builder.Close),
        "IR Lights/Off",
        delegate { IrLightsState.SetPlatoonIR(platoon, false); },
        false
      );
    }
  }

  //Q context menu add button
  [HarmonyPatch(typeof(PlayerInput), "AddBailoutButton")]
  public static class VehicleContextMenuPatch {
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
        "Platoon/IR Lights/On",
        new UnityAction(() => IrLightsState.SetPlatoonIR(platoon, true))
      });
      _addMenuButtonMth?.Invoke(__instance, new object[] {
        "Platoon/IR Lights/Off",
        new UnityAction(() => IrLightsState.SetPlatoonIR(platoon, false))
      });
    }
  }



  [HarmonyPatch(typeof(EquipmentManager), "SetIllumination")]
  public static class BlockEquipmentIlluminationPatch {
    [HarmonyPrefix]
    static bool Prefix(EquipmentManager __instance, LampType type, bool active) {
      if (!active || type != LampType.GunnerSpotlight) return true;
      var unit = __instance.GetComponent<Unit>();
      if (unit == null || !IrLightsState.Suppressed.Contains(unit)) return true;
      var pi = PlayerInput.Instance;
      var playerUnit = (pi != null) ? pi.CurrentPlayerUnit : null;
      if (pi != null && unit == playerUnit) return true;
      return false;
    }
  }

}
