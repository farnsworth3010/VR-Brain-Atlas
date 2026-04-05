using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using System.Collections.Generic;

public class InputSystemDpadLayoutFix : MonoBehaviour
{
  private static bool isSubscribed;
  private static bool isRepairInProgress;

  private void Awake()
  {
    EnsureDpadIsControlLayout();
  }

  private void Update()
  {
    if (IsDpadOverriddenByOpenXR())
    {
      FixOpenXRBindingOverride();
    }
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void EnsureDpadIsControlLayout()
  {
    if (!isSubscribed)
    {
      InputSystem.onLayoutChange += OnLayoutChange;
      isSubscribed = true;
    }

    RepairIfDpadWasOverridden();
  }

  private static void OnLayoutChange(string layoutName, InputControlLayoutChange change)
  {
    if (change != InputControlLayoutChange.Added &&
        change != InputControlLayoutChange.Replaced)
    {
      return;
    }

    if (!string.Equals(layoutName, "Dpad", System.StringComparison.OrdinalIgnoreCase))
    {
      return;
    }

    RepairIfDpadWasOverridden();
  }

  private static bool IsDpadOverriddenByOpenXR()
  {
    string baseLayoutName = InputSystem.GetNameOfBaseLayout("DPad");
    if (string.Equals(baseLayoutName, "XRController", System.StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    InputControlLayout dpadLayout = InputSystem.LoadLayout("DPad");
    return dpadLayout != null && dpadLayout.isDeviceLayout;
  }

  private static void RepairIfDpadWasOverridden()
  {
    if (!IsDpadOverriddenByOpenXR())
    {
      return;
    }

    FixOpenXRBindingOverride();
  }

  private static void FixOpenXRBindingOverride()
  {
    if (isRepairInProgress)
    {
      return;
    }

    isRepairInProgress = true;
    try
    {
      List<InputDevice> addedDevices = new List<InputDevice>();
      foreach (InputDevice device in InputSystem.devices)
      {
        if (device.added)
        {
          addedDevices.Add(device);
        }
      }

      InputSystem.RemoveLayout("DPad");
      InputSystem.RegisterLayout<DpadControl>("DPad");

      foreach (InputDevice device in addedDevices)
      {
        if (!device.added)
        {
          InputSystem.AddDevice(device);
          InputSystem.ResetDevice(device);
          device.MakeCurrent();
        }
      }

      Debug.LogWarning("InputSystem: fixed Dpad layout conflict (device layout replaced with DpadControl).");
    }
    finally
    {
      isRepairInProgress = false;
    }
  }
}
