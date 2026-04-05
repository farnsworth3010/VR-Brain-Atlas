using UnityEngine;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;

// Quits the player when the user hits escape
public class QuitApplication : MonoBehaviour
{
  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  private static extern int MessageBox(System.IntPtr hWnd, string text, string caption, uint type);

  private const uint MB_YESNO = 0x00000004;
  private const uint MB_ICONQUESTION = 0x00000020;
  private const int IDYES = 6;

  void Update()
  {
    if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
    {
      int result = MessageBox(System.IntPtr.Zero, "Вы уверены, что хотите выйти?", "Выход", MB_YESNO | MB_ICONQUESTION);
      if (result == IDYES)
      {
        Application.Quit();
      }
    }
  }
}