﻿using System;
using System.Collections;
using System.Collections.Generic;
using Harmony;
using spaar.ModLoader;
using UnityEngine;

namespace spaar.Mods.CameraOverlay.Patches
{
  public class FixedCameraBlockPatches
  {
    static bool IsOverlayCam(FixedCameraBlock cam)
    {
      return cam.Toggles.Find(t => t.Key == "overlay").IsActive;
    }

    static class CameraHolder
    {
      public static Dictionary<FixedCameraBlock, Camera> AllCameras = new Dictionary<FixedCameraBlock, Camera>();

      private static int _counter = 0;
      public static int Counter
      {
        get { return _counter++; }
      }
    }

    [HarmonyPatch(typeof(FixedCameraBlock), "Awake")]
    class Awake
    {
      static void Postfix(FixedCameraBlock __instance)
      {
        var toggle = __instance.CallPrivateMethod<MToggle>("AddToggle", new Type[]
        {
          typeof(string), typeof(string), typeof(bool)
        }, new object[]
        {
          "Overlay", "overlay", false
        });

        if (!CameraHolder.AllCameras.ContainsKey(__instance) && StatMaster.isSimulating)
        {
          var mo = MouseOrbit.Instance.transform;
          var camGO = new GameObject("Camera " + CameraHolder.Counter);
          var cam = camGO.AddComponent<Camera>();
          cam.enabled = false;
          cam.depth = 1;
          cam.rect = new Rect(0.8f, 0.8f, 0.2f, 0.2f);

          var oCam = MouseOrbit.Instance.cam;
          cam.nearClipPlane = oCam.nearClipPlane;
          cam.farClipPlane = oCam.farClipPlane;
          cam.renderingPath = oCam.renderingPath;
          cam.cullingMask = oCam.cullingMask;
          cam.useOcclusionCulling = oCam.useOcclusionCulling;
          cam.fieldOfView = oCam.fieldOfView;

          CameraHolder.AllCameras.Add(__instance, cam);
        }
      }
    }

    [HarmonyPatch(typeof(FixedCameraBlock), "Start")]
    class Start
    {
      static void Postfix(FixedCameraBlock __instance)
      {
        if (IsOverlayCam(__instance))
          __instance.SetPrivateField("cameraTransform", CameraHolder.AllCameras[__instance].transform);
      }
    }

    [HarmonyPatch(typeof(FixedCameraBlock), "Update")]
    class Update
    {
      static bool Prefix(FixedCameraBlock __instance)
      {
        // Don't modify behaviour if we aren't simulating
        if (!StatMaster.isSimulating)
          return true;

        // Don't modify behaviour if the overlay toggle is off
        if (!IsOverlayCam(__instance))
          return true;


        // Activate new Camera object
        var cam = CameraHolder.AllCameras[__instance];
        cam.enabled = __instance.isActive;

        // Don't do all the normal stuff
        return false;
      }
    }

    [HarmonyPatch(typeof(FixedCameraBlock), "Simulation")]
    class Simulation
    {
      static void Postfix(FixedCameraBlock __instance)
      {
        if (!IsOverlayCam(__instance)) return;

        if (__instance.GetPrivateField<MKey>("activateKey").IsPressed)
        {
          __instance.StartCoroutine(FixPositionAndFOV(__instance, MouseOrbit.Instance.cam.fieldOfView));
        }
      }

      static IEnumerator FixPositionAndFOV(FixedCameraBlock instance, float originalFov)
      {
        yield return null;
        FixedCameraController.Instance.SetPrivateField("isDirty", true);
        FixedCameraController.Instance.SetPrivateField("lastKey", instance.KeyCode);
        CameraHolder.AllCameras[instance].transform.parent =
          Game.MachineObjectTracker.ActiveMachine.SimulationMachine.GetChild(0);
        yield return null;
        MouseOrbit.Instance.isActive = true;
        MouseOrbit.Instance.cam.fieldOfView = originalFov;
      }
    }

    [HarmonyPatch(typeof(FixedCameraBlock), "OnDestroy")]
    class OnDestroy
    {
      static void Prefix(FixedCameraBlock __instance)
      {
        if (__instance.GetPrivateField<bool>("simulationClone"))
        {
          GameObject.Destroy(CameraHolder.AllCameras[__instance].gameObject);
        }
      }
    }
  }

}