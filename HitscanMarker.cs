using System.Reflection;
using MelonLoader;
using GHPC;
using GHPC.AI;
using GHPC.Camera;
using GHPC.PhysicsHelpers;
using GHPC.Player;
using GHPC.Utility;
using GHPC.Weapons;
using UnityEngine;

namespace ImprovedAI {

  internal static class HitscanMarker {
    // ── Marker state ──
    internal static Vector3? HitWorldPos = null;
    internal static float HitTime = -1f;
    internal static Texture2D MarkerTex;
    internal static Texture2D RedMarkerTex;
    internal static Vector3? RedHitWorldPos = null;
    internal static float RedHitTime = -1f;
    internal static float RaycastBlockedUntil = -1f;
    internal static Vector3? PlatoonAimTarget = null;
    internal static float PlatoonAimStartTime = -1f;
    internal static bool PlatoonHullTurn = false;
    internal const float AimDuration = 30f;
    private const float HullAlignedDot = 0.98f;
    private static PropertyInfo _usingBinosProp;

    internal static void InitTextures() {
      const int T = 48;

      MarkerTex = new Texture2D(T, T, TextureFormat.RGBA32, false);
      MarkerTex.filterMode = FilterMode.Point;
      MarkerTex.wrapMode = TextureWrapMode.Clamp;
      var mPixels = new Color[T * T];
      var triApex = new Vector2(T * 0.5f, T - 4f);
      var triBL = new Vector2(4f, 4f);
      var triBR = new Vector2(T - 4f, 4f);
      for (int i = 0; i < T * T; i++) {
        int px = i % T, py = i / T;
        Vector2 p = new Vector2(px + 0.5f, py + 0.5f);
        mPixels[i] = PointInTriangle(p, triApex, triBL, triBR)
            ? new Color(1f, 0.55f, 0f, 1f) : Color.clear;
      }

      //!
      for (int ey = 16; ey <= 30; ey++)
        for (int ex = 20; ex <= 27; ex++)
          if (PointInTriangle(new Vector2(ex + 0.5f, ey + 0.5f), triApex, triBL, triBR))
            mPixels[ey * T + ex] = Color.white;
      for (int ey = 7; ey <= 11; ey++)
        for (int ex = 20; ex <= 27; ex++)
          if (PointInTriangle(new Vector2(ex + 0.5f, ey + 0.5f), triApex, triBL, triBR))
            mPixels[ey * T + ex] = Color.white;

      MarkerTex.SetPixels(mPixels);
      MarkerTex.Apply();

      RedMarkerTex = new Texture2D(T, T, TextureFormat.RGBA32, false);
      RedMarkerTex.filterMode = FilterMode.Point;
      RedMarkerTex.wrapMode = TextureWrapMode.Clamp;
      var rPixels = new Color[T * T];
      for (int i = 0; i < T * T; i++) {
        int px = i % T, py = i / T;
        Vector2 p = new Vector2(px + 0.5f, py + 0.5f);
        rPixels[i] = PointInTriangle(p, triApex, triBL, triBR)
            ? new Color(1f, 0f, 0f, 1f) : Color.clear;
      }
      for (int ey = 16; ey <= 30; ey++)
        for (int ex = 20; ex <= 27; ex++)
          if (PointInTriangle(new Vector2(ex + 0.5f, ey + 0.5f), triApex, triBL, triBR))
            rPixels[ey * T + ex] = Color.white;
      for (int ey = 7; ey <= 11; ey++)
        for (int ex = 20; ex <= 27; ex++)
          if (PointInTriangle(new Vector2(ex + 0.5f, ey + 0.5f), triApex, triBL, triBR))
            rPixels[ey * T + ex] = Color.white;
      RedMarkerTex.SetPixels(rPixels);
      RedMarkerTex.Apply();
    }

    internal static void DrawMarkers() {
      if (MarkerTex != null && HitWorldPos != null)
        DrawMarker(HitWorldPos.Value, HitTime, MarkerTex, 2f);
      if (RedMarkerTex != null && RedHitWorldPos != null)
        DrawMarker(RedHitWorldPos.Value, RedHitTime, RedMarkerTex, 2f);
    }

    private static void DrawMarker(Vector3 worldPos, float hitTime, Texture2D tex, float duration) {
      float elapsed = Time.time - hitTime;
      if (elapsed > duration) return;
      var cam = CameraManager.MainCam;
      if (cam == null) return;

      const float baseSize = 24f, edge = 20f;
      float t = Mathf.Clamp01(elapsed / 0.2f);
      float scale = t < 0.5f ? Mathf.Lerp(0.75f, 0.85f, t * 2f) : Mathf.Lerp(0.85f, 1.0f, (t - 0.5f) * 2f);
      float bright = Mathf.Lerp(0.4f, 1.0f, t);
      float fadeOut = Mathf.Clamp01((duration - elapsed) / 0.3f);
      float alpha = Mathf.Lerp(0.3f, 1.0f, t) * fadeOut;
      float drawSize = baseSize * scale;
      float half = drawSize * 0.5f;

      Vector3 sp = cam.WorldToScreenPoint(worldPos);
      Vector3 vp = cam.WorldToViewportPoint(worldPos);

      bool onScreen = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f && vp.z > 0f;

      if (!onScreen) {
        float hw = Screen.width * 0.5f, hh = Screen.height * 0.5f;
        float sx = sp.x, sy = sp.y;
        if (vp.z < 0f) { sx = Screen.width - sx; sy = Screen.height - sy; }
        float dx = sx - hw, dy = sy - hh;
        float rx = dx != 0f ? (hw - edge) / Mathf.Abs(dx) : float.MaxValue;
        float ry = dy != 0f ? (hh - edge) / Mathf.Abs(dy) : float.MaxValue;
        float r = Mathf.Min(rx, ry);
        sp.x = Mathf.Clamp(hw + dx * r, edge, Screen.width- edge);
        sp.y = Mathf.Clamp(hh + dy * r, edge, Screen.height - edge);
      }

      float gx = sp.x - half;
      float gy = Screen.height - sp.y - half;

      int prevDepth = GUI.depth;
      GUI.depth = 1000000;
      var prevColor = GUI.color;
      GUI.color = new Color(bright, bright, bright, alpha);
      GUI.DrawTexture(new Rect(gx, gy, drawSize, drawSize), tex, ScaleMode.StretchToFill, true);
      GUI.color = prevColor;
      GUI.depth = prevDepth;
    }

    internal static void HandleHitscanInput() {
      if (!((Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Mouse2)) && Time.time >= RaycastBlockedUntil))
        return;

      var cam = CameraManager.MainCam;
      var bcf = cam != null ? cam.GetComponentInParent<BufferedCameraFollow>() : null;
      if (_usingBinosProp == null && bcf != null)
        _usingBinosProp = bcf.GetType().GetProperty("UsingBinoculars", BindingFlags.Public | BindingFlags.Instance);
      bool binosActive = bcf != null && _usingBinosProp != null && (bool)(_usingBinosProp.GetValue(bcf) ?? false);
      bool isGunnerView = !CameraSlot.CurrentSlotIsExterior && !binosActive;
      if (isGunnerView) return;

      Vector3 fwd = (cam != null) ? cam.transform.forward : Vector3.zero;
      MelonLogger.Msg($"[Hitscan] forward=({fwd.x:F4}, {fwd.y:F8}, {fwd.z:F8})");
      var binos = FirstPersonBinocularsAnimator.Instance;
      string armyName = "null";
      if (binos != null) {
        var armyFld = typeof(FirstPersonBinocularsAnimator).GetField("_army",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var army = armyFld?.GetValue(binos) as UnityEngine.Object;
        armyName = army != null ? army.name : "null";
      }

      //nato binocs need offset
      bool applyOffset = armyName == "null" || armyName == "FRG" || armyName == "USA";
      float pitchDeg = applyOffset ? 0.655f : 0f;

      //draw ray
      if (CameraUtils.Hitscan(out Vector3 hitPoint, pitchOffsetDeg: pitchDeg)) {
        MelonLogger.Msg($"[Hitscan] hitPoint={hitPoint}");
        bool triggeredRed = false;
        if (HitWorldPos.HasValue && Time.time - HitTime <= 2f) {
          Vector3 diff = hitPoint - HitWorldPos.Value;
          if (Mathf.Abs(diff.x) <= 10f && Mathf.Abs(diff.y) <= 10f && Mathf.Abs(diff.z) <= 10f) {
            bool redActive = RedHitWorldPos.HasValue && Time.time - RedHitTime <= 2f;
            if (!redActive) {
              RedHitWorldPos = HitWorldPos.Value;
              RedHitTime = Time.time;
              HitWorldPos = null;
              RaycastBlockedUntil = Time.time + 2f;
              triggeredRed = true;
              // Red: aim + hull turn toward the original position
              PlatoonAimTarget = RedHitWorldPos;
              PlatoonAimStartTime = Time.time;
              PlatoonHullTurn = true;
              MelonLogger.Msg($"[PlatoonAim] RED triggered, aimTarget={RedHitWorldPos}, hullTurn=true");
            }
          }
        }
        if (!triggeredRed) {
          HitWorldPos = hitPoint;
          HitTime = Time.time;
          PlatoonAimTarget = hitPoint;
          PlatoonAimStartTime = Time.time;
          PlatoonHullTurn = false;
          MelonLogger.Msg($"[PlatoonAim] ORANGE triggered, aimTarget={hitPoint}, hullTurn=false");
        }
      } else {
        HitWorldPos = null;
      }
    }

    internal static void HandleCancelInput() {
      var mp = InputUtil.MainPlayer;
      if (mp != null && (mp.GetButtonDown(InputActionConstants.ACTION_CANCEL) || mp.GetButtonDown("Map") || mp.GetButtonDown("Change Vehicle")))
        ClearAll();
    }

    internal static void UpdatePlatoonAiming() {
      if (PlatoonAimTarget == null) return;

      float elapsed = Time.time - PlatoonAimStartTime;
      if (elapsed > AimDuration) {
        ReleasePlatoonDriverBrains();
        PlatoonAimTarget = null;
        PlatoonHullTurn = false;
        return;
      }

      var player = PlayerInput.Instance;
      if (player == null) { 
        MelonLogger.Msg("Err: No PlayerInput"); 
        return; 
      }
      var playerUnit = player.CurrentPlayerUnit;
      if (playerUnit == null) { 
        MelonLogger.Msg("Err: No CurrentPlayerUnit"); 
        return; 
      }
      if (!playerUnit.PlatoonInitialized) { 
        MelonLogger.Msg("Err: Platoon not initialized"); return; 
      }
      var platoon = playerUnit.Platoon;
      if (platoon?.Units == null) { 
        MelonLogger.Msg("Err: Platoon.Units is null"); return; 
      }

      Vector3 aimPos = PlatoonAimTarget.Value;

      foreach (var unit in platoon.Units) {
        if (unit == null || unit == playerUnit){
          continue;
        }
        string uname = unit.FriendlyName ?? unit.gameObject.name;

        var unitAI = unit.GetComponentInChildren<UnitAI>();
        if (unitAI == null) { 
          MelonLogger.Msg($"{uname}: no UnitAI");
          continue; 
        }
        if (unitAI.HasTarget) { 
          MelonLogger.Msg($"[PlatoonAim] {uname}: skipped (HasTarget)"); continue; 
        }

        //turret aiming
        var wm = unitAI.UCI?.GunnerBrain?.WeaponsModule;
        if (wm != null) {
          var role = wm.ActiveWeapon != null ? wm.ActiveWeapon.Role : unitAI.UCI.FirstWeaponSystemRole;
          wm.SetAimPosition(role, aimPos);
          //MelonLogger.Msg($"{uname}: SetAimPosition role={role} pos={aimPos}");
        } else {
          MelonLogger.Msg($"Err: {uname}: WeaponsModule is null (UCI={unitAI.UCI != null}, GB={unitAI.UCI?.GunnerBrain != null})");
        }

        // Hull turning 
        if (PlatoonHullTurn) {
          Vector3 toTarget = aimPos - unit.transform.position;
          toTarget.y = 0f;
          Vector3 heading = toTarget.normalized;
          float dot = Vector3.Dot(unit.transform.forward, heading);
          if (dot < HullAlignedDot) {
            ForceTurn(unitAI, toTarget);
          } else {
            unitAI.UCI.DriverBrain.ForceSpeed(0f, true);
            unitAI.UCI.DriverBrain.ForceSteerAmount(0f);
          }
        }
      }
    }

    internal static void ClearAll() {
      HitWorldPos = null;
      RedHitWorldPos = null;
      if (PlatoonAimTarget != null) {
        ReleasePlatoonDriverBrains();
        PlatoonAimTarget = null;
        PlatoonHullTurn = false;
      }
    }

    private static void ReleasePlatoonDriverBrains() {
      if (!PlatoonHullTurn) return;
      var player = PlayerInput.Instance;
      if (player == null) return;
      var playerUnit = player.CurrentPlayerUnit;
      if (playerUnit == null || !playerUnit.PlatoonInitialized) return;
      var platoon = playerUnit.Platoon;
      if (platoon?.Units == null) return;
      foreach (var unit in platoon.Units) {
        if (unit == null || unit == playerUnit) continue;
        var unitAI = unit.GetComponentInChildren<UnitAI>();
        if (unitAI != null) {
          unitAI.UCI.DriverBrain.ForceSpeed(0f, true);
          unitAI.UCI.DriverBrain.ForceSteerAmount(0f);
        }
      }
    }

    internal static void ForceTurn(UnitAI unitAI, Vector3 targetHeading) {
      unitAI.UCI.DriverBrain.ForceSpeed(1f, false);
      float crossY = Vector3.Cross(unitAI.Unit.transform.forward, targetHeading.normalized).y;
      unitAI.UCI.DriverBrain.ForceSteerAmount(crossY >= 0f ? 1f : -1f);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
      return a.x * b.y - a.y*b.x;
    }
    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) {
      float c1 = Cross(b-a, p-a);
      float c2 = Cross(c-b, p-b);
      float c3 = Cross(a-c, p-c);
      return (c1 >= 0f && c2 >= 0f && c3 >= 0f) || (c1 <= 0f && c2 <= 0f && c3 <= 0f);
    }
  }

  internal static class CameraUtils {
    internal static bool Hitscan(out Vector3 hitPoint, float maxRange = 8000f, float pitchOffsetDeg = 0f) {
      hitPoint = Vector3.zero;
      var cam = CameraManager.MainCam;
      if (cam == null) return false;
      Vector3 dir = pitchOffsetDeg != 0f ? Quaternion.AngleAxis(pitchOffsetDeg, cam.transform.right) * cam.transform.forward : cam.transform.forward;
      var ray = new Ray(cam.transform.position, dir);
      if (RaycastColliderUtils.Raycast(ray, out RaycastHit hit, maxRange,
          ConstantsAndInfoManager.Instance.LaserRangefinderLayerMask)) {
        hitPoint = hit.point;
        return true;
      }
      return false;
    }
  }
}
