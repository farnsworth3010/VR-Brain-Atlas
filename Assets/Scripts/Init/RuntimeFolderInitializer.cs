using System;
using System.IO;
using UnityEngine;

/* Этот класс гарантирует, что при запуске приложения будет 
   создана папка "VR Brain Atlas" в "Мои документы", если она еще не существует.
   Из этой папки по умолчанию будут загружаться модели.
  */
public static class RuntimeFolderInitializer
{
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
  private static void EnsureDocumentsFolder()
  {
    try
    {
      string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
      if (string.IsNullOrEmpty(docs))
      {
        Debug.LogWarning("RuntimeFolderInitializer: Could not determine MyDocuments path.");
        return;
      }

      string folder = Path.Combine(docs, "VR Brain Atlas");

      if (!Directory.Exists(folder))
      {
        Directory.CreateDirectory(folder);
        Debug.Log($"RuntimeFolderInitializer: Created default models folder: {folder}");
      }
    }
    catch (Exception e)
    {
      Debug.LogException(e);
    }
  }
}
