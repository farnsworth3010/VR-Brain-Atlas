using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using System.Collections.Generic;

/// <summary>
/// Исправляет конфликт между стандартной раскладкой D-pad и OpenXR.
/// 
/// Проблема: OpenXR может переопределить раскладку D-pad как устройство вместо контрола,
/// что вызывает проблемы с Input System. Этот скрипт следит за изменениями раскладок
/// и восстанавливает правильную конфигурацию D-pad, когда она нарушена.
/// 
/// Использование: Прикрепить к GameO/bject в сцене. Скрипт автоматически
/// инициализируется при загрузке сцены.
/// </summary>
public class InputSystemDpadLayoutFix : MonoBehaviour
{
  private static bool isSubscribed;

  private static bool isRepairInProgress;

  private static bool hasLoggedFix;

  private void Awake()
  {
    EnsureDpadIsControlLayout();
  }

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void EnsureDpadIsControlLayout()
  {
    if (!isSubscribed)
    {
      InputSystem.onLayoutChange += OnLayoutChange;
      isSubscribed = true;
    }

    // Проверяем, не была ли раскладка уже переопределена
    RepairIfDpadWasOverridden();
  }

  private static void OnLayoutChange(string layoutName, InputControlLayoutChange change)
  {
    // Нас интересуют только события добавления и замены раскладок
    if (change != InputControlLayoutChange.Added &&
        change != InputControlLayoutChange.Replaced)
    {
      return;
    }

    // Проверяем, что изменилась именно раскладка D-pad
    if (!string.Equals(layoutName, "Dpad", System.StringComparison.OrdinalIgnoreCase))
    {
      return;
    }

    // Если D-pad была переопределена - исправляем
    RepairIfDpadWasOverridden();
  }

  /// <summary>
  /// Проверяет, была ли раскладка D-pad переопределена OpenXR в устройство.
  /// </summary>
  /// <returns>true если D-pad переопределена как устройство OpenXR, иначе false</returns>
  private static bool IsDpadOverriddenByOpenXR()
  {
    // Проверяем базовую раскладку D-pad
    string baseLayoutName = InputSystem.GetNameOfBaseLayout("DPad");
    if (string.Equals(baseLayoutName, "XRController", System.StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    // Загружаем раскладку и проверяем, является ли она устройством
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

  /// <summary>
  /// Исправляет переопределение D-pad раскладки OpenXR.
  /// Переустанавливает D-pad как контрол и переинициализирует все активные устройства.
  /// </summary>
  private static void FixOpenXRBindingOverride()
  {
    // Предотвращаем рекурсивное исправление
    if (isRepairInProgress)
    {
      return;
    }

    isRepairInProgress = true;
    try
    {
      // Собираем список уже подключённых устройств
      List<InputDevice> addedDevices = new List<InputDevice>();
      foreach (InputDevice device in InputSystem.devices)
      {
        if (device.added)
        {
          addedDevices.Add(device);
        }
      }

      // Удаляем переопределённую раскладку D-pad
      InputSystem.RemoveLayout("DPad");

      // Переустанавливаем стандартную раскладку D-pad как контрол
      InputSystem.RegisterLayout<DpadControl>("DPad");

      // Переинициализируем все подключённые устройства
      foreach (InputDevice device in addedDevices)
      {
        if (!device.added)
        {
          InputSystem.AddDevice(device);
          InputSystem.ResetDevice(device);
          device.MakeCurrent();
        }
      }

#if UNITY_EDITOR
      if (!hasLoggedFix)
      {
        Debug.Log("InputSystem: Dpad layout conflict fixed (OpenXR override corrected).");
        hasLoggedFix = true;
      }
#endif
    }
    finally
    {
      isRepairInProgress = false;
    }
  }
}
