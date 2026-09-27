using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using GHPC.AI;
using GHPC.AI.BehaviorTrees.Actions.Movement;
using GHPC.Crew;
using GHPC.AI.Platoons;
using GHPC;
using GHPC.Player;
using GHPC.Utility;
using GHPC.Weapons;
using UnityEngine;

[assembly: MelonInfo(typeof(ImprovedAI.ImprovedAIMod), "Improved AI", "1.0.0", "Qwertyryo")]
[assembly: MelonGame("Radian Simulations LLC", "GHPC")]

namespace ImprovedAI {

  public class ImprovedAIMod : MelonMod {
    private static readonly FieldInfo _stateField = typeof(DriverAI).GetField("state",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _unitAIField = typeof(DriverAI).GetField("_unitAI",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    internal static readonly HashSet<DriverAI> _allDrivers = new HashSet<DriverAI>();
    private float _lastCacheRefresh = 0f;
    private readonly Dictionary<DriverAI, string> _prevStates
        = new Dictionary<DriverAI, string>();
    private readonly Dictionary<DriverAI, float> _lastDiagTime
        = new Dictionary<DriverAI, float>();

    // Lazily-initialized chassis reflection
    private static readonly FieldInfo _dbChassisFld = typeof(DriverBrain).GetField("_chassis",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    private static PropertyInfo _suspendedProp;
    private static FieldInfo _nwhIntendedSpeedFld;
    private static FieldInfo _nwhBrakingFld;
    private static MethodInfo _nwhActualSpeedMth;
    private static FieldInfo _nwhRbFld;
    private static PropertyInfo _rbVelocityProp;
    private static PropertyInfo _rbAngVelocityProp;
    public override void OnInitializeMelon() {
      try {
        HarmonyInstance.PatchAll();
      } catch (System.Exception ex) {
        MelonLogger.Error($"Patching failed: {ex}");
      }

      HitscanMarker.InitTextures();
    }

    public override void OnGUI() {
      HitscanMarker.DrawMarkers();
    }

    public override void OnLateUpdate() {


      float now = Time.time;
      if (now - _lastCacheRefresh >= 10f) {
        _lastCacheRefresh = now;
        _allDrivers.Clear();
        foreach (var d in GameObject.FindObjectsOfType<DriverAI>())
          _allDrivers.Add(d);
      }


      HitscanMarker.HandleCancelInput();
      HitscanMarker.HandleHitscanInput();
      HitscanMarker.UpdatePlatoonAiming();



      foreach (var driver in new List<DriverAI>(_allDrivers)) {
        if (driver == null) { _allDrivers.Remove(driver); continue; }
        object rawState = _stateField?.GetValue(driver);
        if (rawState == null) continue;
        string state = (string)rawState;
        var unitAI = _unitAIField?.GetValue(driver) as UnitAI;
        if (PlatoonHelper.IsPlayerPlatoonMember(unitAI)) continue;


        if (state == "APPROACH_TARGET" && unitAI != null && unitAI.HasTarget) {
          var brain = unitAI.UCI.DriverBrain as DriverBrain;
          var chassis = brain != null && _dbChassisFld != null
              ? _dbChassisFld.GetValue(brain) : null;



          if (chassis != null && _nwhRbFld == null) {
            var ct = chassis.GetType();
            _nwhRbFld = ct.GetField("_rb",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _nwhIntendedSpeedFld = ct.GetField("_intendedSpeed",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _nwhBrakingFld = ct.GetField("_braking",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _nwhActualSpeedMth = ct.GetMethod("GetActualSpeed",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
          }

          
          Vector3 unitTargetHeading = unitAI.TargetHeading;
          float dot = Vector3.Dot(unitAI.Unit.transform.forward, unitTargetHeading.normalized);
          bool shouldTurn = !unitAI.Entrenched && dot < 0.98f;

          if (shouldTurn) {
            HitscanMarker.ForceTurn(unitAI, unitTargetHeading);
          } else {
            unitAI.UCI.DriverBrain.ForceSpeed(0f, true);
            unitAI.UCI.DriverBrain.ForceSteerAmount(0f);
            var rb = _nwhRbFld?.GetValue(chassis);
            if (rb != null) {
              if (_rbVelocityProp == null) {
                var rt = rb.GetType();
                _rbVelocityProp = rt.GetProperty("velocity");
                _rbAngVelocityProp = rt.GetProperty("angularVelocity");
              }
              _rbVelocityProp?.SetValue(rb, Vector3.zero);
              _rbAngVelocityProp?.SetValue(rb, Vector3.zero);
            }
          }

          float current_time = Time.time;
          if (!_lastDiagTime.TryGetValue(driver, out float lastT) || current_time - lastT >= 2f) {
            _lastDiagTime[driver] = current_time;
            string vname = unitAI.Unit?.FriendlyName ?? driver.gameObject.name;

            if (_suspendedProp == null) {
              var t = typeof(DriverBrain);
              while (t != null && _suspendedProp == null) {
                _suspendedProp = t.GetProperty("Suspended",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                t = t.BaseType;
              }
            }

            bool suspended = brain != null && _suspendedProp != null
                && (bool)(_suspendedProp.GetValue(brain) ?? false);
            string chassisType = chassis?.GetType().Name ?? "null";
            float intendedSpeed = chassis != null && _nwhIntendedSpeedFld != null
                ? (float)(_nwhIntendedSpeedFld.GetValue(chassis) ?? -99f) : -99f;
            bool braking = chassis != null && _nwhBrakingFld != null
                && (bool)(_nwhBrakingFld.GetValue(chassis) ?? false);
            float actualSpeed = chassis != null && _nwhActualSpeedMth != null
                ? (float)(_nwhActualSpeedMth.Invoke(chassis, null) ?? -99f) : -99f;
          }
        }

        var unit = unitAI?.Unit;
        string name = (unit != null) ? unit.gameObject.name : driver.gameObject.name;

        if (_prevStates.TryGetValue(driver, out var prev) && prev != state)
          MelonLogger.Msg($"{name}: {prev} -> {state}");
        _prevStates[driver] = state;
      }

      foreach (var key in new List<DriverAI>(_prevStates.Keys)) {
        if (!_allDrivers.Contains(key)) {
          _prevStates.Remove(key);
          _lastDiagTime.Remove(key);
        }
      }
    }
  }

  internal static class PlatoonHelper {
    internal static bool IsPlayerPlatoonMember(UnitAI unitAI) {
      if (unitAI?.Unit == null) return false;
      try {
        var player = PlayerInput.Instance;
        if (player == null) return false;
        var playerUnit = player.CurrentPlayerUnit;
        if (playerUnit == null) return false;
        // Always skip the player's own vehicle
        if (unitAI.Unit == playerUnit) return true;
        // Skip AI wingmen in the same platoon
        if (!playerUnit.PlatoonInitialized) return false;
        var platoon = playerUnit.Platoon;
        return platoon?.Units != null && platoon.Units.Contains(unitAI.Unit);
      } catch { return false; }
    }
  }

  //Modifies Approach_Target code specifically
  [HarmonyPatch(typeof(DriverAI), "RunTask")]
  public static class RunTaskPatch {
    private static readonly FieldInfo _driverStateField = typeof(DriverAI).GetField("state",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo _driverUnitAIField = typeof(DriverAI).GetField("_unitAI",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPrefix]
    static bool Prefix(DriverAI __instance, AIVehicleBase.VehicleAITasks __0) {
      if (__0 != AIVehicleBase.VehicleAITasks.APPROACH_TARGET) return true;

      var unitAI = _driverUnitAIField?.GetValue(__instance) as UnitAI;
      if (PlatoonHelper.IsPlayerPlatoonMember(unitAI)) return true;
      if (unitAI != null) {
        unitAI.UCI.VehicleAIController.TargetSpeed = 0f;
        unitAI.UCI.VehicleAIController.FireSpeedStop = false;
        var dac = unitAI.UCI.VehicleAIController as DriverAIController;
        if (dac != null) dac.StopAndEngaging = true;
      }
      ((AIVehicleBase)__instance).StopAndEngage();
      _driverStateField?.SetValue(__instance, "APPROACH_TARGET");
      return false;
    }
  }

  // Only one of the StopAndEngage functions should run, this disables the Behavior Tree StopAndEngage
  [HarmonyPatch(typeof(StopAndEngage), nameof(StopAndEngage.OnUpdate))]
  public static class StopAndEngagePatch {
    private static PropertyInfo FindUnitAIProp() {
      Type t = typeof(StopAndEngage);
      while (t != null) {
        var p = t.GetProperty("UnitAI",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (p != null) return p;
        t = t.BaseType;
      }
      return null;
    }
    private static readonly PropertyInfo _unitAIProp = FindUnitAIProp();

    [HarmonyPrefix]
    static bool Prefix(StopAndEngage __instance) {
      var unitAI = _unitAIProp?.GetValue(__instance) as UnitAI;
      if (PlatoonHelper.IsPlayerPlatoonMember(unitAI)) return true;
      // suppress BT StopAndEngage while APPROACH_TARGET is active (HasTarget drives that task)
      return unitAI == null || !unitAI.HasTarget;
    }
  }


  [HarmonyPatch(typeof(AIVehicleBase), "StopAndEngage")]
  public static class StopAndEngageConditionsPatch {
    private static readonly FieldInfo _baseUnitAIField = typeof(AIVehicleBase).GetField("_unitAI",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Dictionary<AIVehicleBase, string> _lastLog
        = new Dictionary<AIVehicleBase, string>();

    [HarmonyPrefix]
    static bool Prefix(AIVehicleBase __instance) {
      var unitAI = _baseUnitAIField?.GetValue(__instance) as UnitAI;
      if (PlatoonHelper.IsPlayerPlatoonMember(unitAI)) return true;
      if (unitAI == null || !unitAI.HasDriver || unitAI.Target == null) return false;

      Vector3 targetHeading = unitAI.TargetHeading;
      float dot = Vector3.Dot(unitAI.Unit.transform.forward, targetHeading.normalized);
      string name = unitAI.Unit?.FriendlyName ?? __instance.gameObject.name;

      bool notEntrenched = !unitAI.Entrenched;
      bool dotOk = dot < 0.98f;
      bool isTracked= unitAI.Chassis.IsTracked;
      bool willForce = notEntrenched && dotOk && isTracked;

      string key = $"{notEntrenched}|{dotOk}|{isTracked}";
      if (!_lastLog.TryGetValue(__instance, out var prev) || prev != key) {
        _lastLog[__instance] = key;
      }

      unitAI.UCI.DriverBrain.ForceSpeed(0f, false);
      if (willForce)
        unitAI.UCI.DriverBrain.ForceHeading(MathUtil.GetCompassHeading(targetHeading));

      return false; 
    }
  }

  [HarmonyPatch(typeof(DriverAI), "ScheduleTasks")]
  public static class ScheduleTasksPatch {
    private static readonly FieldInfo _schedUnitAIField = typeof(DriverAI).GetField("_unitAI",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPostfix]
    static void Postfix(DriverAI __instance) {
      var unitAI = _schedUnitAIField?.GetValue(__instance) as UnitAI;
      if (PlatoonHelper.IsPlayerPlatoonMember(unitAI)) return;
      if (unitAI == null || unitAI.HasTarget) return;
      var dac = unitAI.UCI.VehicleAIController as DriverAIController;
      if (dac != null) dac.StopAndEngaging = false;
    }
  }
}
