using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static System.Windows.Media.Imaging.BitmapSizeOptions;

namespace AdiQuickLaunchLib
{
   public static class IconHelper
   {
      
      [System.Runtime.InteropServices.ComImport]
      [System.Runtime.InteropServices.Guid("00021401-0000-0000-C000-000000000046")]
      public class ShellLink { }

      [System.Runtime.InteropServices.ComImport]
      [System.Runtime.InteropServices.InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
      [System.Runtime.InteropServices.Guid("000214F9-0000-0000-C000-000000000046")]
      public interface IShellLink
      {
         void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile,
            int cchMaxPath, IntPtr pfd, uint fFlags);
         void GetIDList(out IntPtr ppidl);
         void SetIDList(IntPtr pidl);
         void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
         void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
         void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
         void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
         void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
         void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
         void GetHotkey(out short pwHotkey);
         void SetHotkey(short wHotkey);
         void GetShowCmd(out int piShowCmd);
         void SetShowCmd(int iShowCmd);
         void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath,
            int cchIconPath, out int piIcon);
         void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
         void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
         void Resolve(IntPtr hwnd, uint fFlags);
         void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
      }

      
      [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
      private static extern IntPtr SHGetFileInfo(
         string pszPath,
         uint dwFileAttributes,
         ref SHFILEINFO psfi,
         uint cbFileInfo,
         uint uFlags);

      [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
      private struct SHFILEINFO
      {
         public IntPtr hIcon;
         public int iIcon;
         public uint dwAttributes;
         [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
         public string szDisplayName;
         [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
         public string szTypeName;
      }

      private const uint SHGFI_ICON = 0x100;
      private const uint SHGFI_SMALLICON = 0x1;
      private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
      private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
      private const uint FILE_ATTRIBUTE_FILE = 0x80;

      public static ImageSource GetIcon(string path, bool isDirectory)
      {
         var shinfo = new SHFILEINFO();
         uint flags = SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES;
         uint attribute = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_FILE;

         SHGetFileInfo(path, attribute, ref shinfo,
            (uint)Marshal.SizeOf(shinfo), flags);

         if (shinfo.hIcon != IntPtr.Zero)
         {
            var img = Imaging.CreateBitmapSourceFromHIcon(
               shinfo.hIcon,
               Int32Rect.Empty,
               FromEmptyOptions());

            DestroyIcon(shinfo.hIcon);
            return img;
         }
         return null;
      }

      [DllImport("User32.dll")]
      private static extern bool DestroyIcon(IntPtr hIcon);
   }
}
