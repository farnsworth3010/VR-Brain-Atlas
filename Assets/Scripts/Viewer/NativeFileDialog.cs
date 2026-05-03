using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

public static class NativeFileDialog
{
    private struct OpenFileName
    {
        public int structSize;
        public IntPtr dlgOwner;
        public IntPtr instance;
        [MarshalAs(UnmanagedType.LPTStr)] public string filter;
        [MarshalAs(UnmanagedType.LPTStr)] public string customFilter;
        public int maxCustFilter;
        public int filterIndex;
        public IntPtr file;
        public int maxFile;
        [MarshalAs(UnmanagedType.LPTStr)] public string fileTitle;
        public int maxFileTitle;
        [MarshalAs(UnmanagedType.LPTStr)] public string initialDir;
        [MarshalAs(UnmanagedType.LPTStr)] public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        [MarshalAs(UnmanagedType.LPTStr)] public string defExt;
        public IntPtr custData;
        public IntPtr hook;
        [MarshalAs(UnmanagedType.LPTStr)] public string templateName;
        public IntPtr reservedPtr;
        public int reservedInt;
        public int flagsEx;
    }

    [DllImport("Comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_ALLOWMULTISELECT = 0x00000200;
    private const int OFN_EXPLORER = 0x00080000;

    public static string[] OpenFileDialog(string filter, bool allowMultiSelect)
    {
        int flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
        if (allowMultiSelect)
        {
            flags |= OFN_ALLOWMULTISELECT | OFN_EXPLORER;
        }

        IntPtr fileBufferPtr = IntPtr.Zero;

        try
        {
            const int maxFileChars = 16384;
            fileBufferPtr = Marshal.AllocHGlobal(maxFileChars * sizeof(char));
            for (int i = 0; i < maxFileChars; i++)
            {
                Marshal.WriteInt16(fileBufferPtr, i * sizeof(char), 0);
            }

            OpenFileName ofn = new OpenFileName
            {
                structSize = Marshal.SizeOf(typeof(OpenFileName)),
                filter = filter,
                file = fileBufferPtr,
                maxFile = maxFileChars,
                fileTitle = new string('\0', 256),
                maxFileTitle = 256,
                initialDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VR Brain Atlas"),
                title = "Выберите модель",
                flags = flags
            };

            bool result = GetOpenFileName(ref ofn);
            if (!result)
            {
                return Array.Empty<string>();
            }

            string rawBuffer = Marshal.PtrToStringAuto(fileBufferPtr, maxFileChars) ?? string.Empty;
            string[] rawParts = rawBuffer
              .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
              .Where(part => !string.IsNullOrWhiteSpace(part))
              .ToArray();

            if (rawParts.Length == 0)
            {
                return Array.Empty<string>();
            }

            if (rawParts.Length == 1)
            {
                return new[] { rawParts[0] };
            }

            string directory = rawParts[0];
            string[] files = new string[rawParts.Length - 1];
            for (int i = 1; i < rawParts.Length; i++)
            {
                files[i - 1] = Path.Combine(directory, rawParts[i]);
            }

            return files;
        }
        finally
        {
            if (fileBufferPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(fileBufferPtr);
            }
        }
    }
}
