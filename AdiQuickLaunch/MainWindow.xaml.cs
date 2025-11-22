using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using AdiQuickLaunchLib;
using Application = System.Windows.Application;
using IWSRL = IWshRuntimeLibrary;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace AdiQuickLaunch
{
   public partial class MainWindow : Window
   {
      private List<FileSystemItem> items;

      public MainWindow()
      {
         var sw = System.Diagnostics.Stopwatch.StartNew();

         InitializeComponent();
         System.Diagnostics.Debug.WriteLine($"InitializeComponent: {sw.ElapsedMilliseconds}ms");

         sw.Restart();
         Init(null);
         System.Diagnostics.Debug.WriteLine($"Init method: {sw.ElapsedMilliseconds}ms");

      }

      public MainWindow(string? jsonPath)
      {
         InitializeComponent();

         Init(jsonPath);
      }

      private async void Init(string? jsonPath)
      {
         var sw = System.Diagnostics.Stopwatch.StartNew();


         List<QuickLauncher.QuickItem> lstFolder = new List<QuickLauncher.QuickItem>();
         if (!string.IsNullOrEmpty(jsonPath) || File.Exists(jsonPath))
            lstFolder = LoadFolders(jsonPath);

         System.Diagnostics.Debug.WriteLine($"Init Function - LoadFolders: {sw.ElapsedMilliseconds}ms");
         sw.Restart();
         string sCurDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

         // Set icon
         string iconPath = Path.Combine(sCurDirectory, "app.ico");
         if (File.Exists(iconPath))
         {
            //this.Icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
            Task.Run(() => {
               if (File.Exists(iconPath))
               {
                  Dispatcher.Invoke(() => {
                     this.Icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                  });
               }
            });
         }

         System.Diagnostics.Debug.WriteLine($"Init Function - this.Icon: {sw.ElapsedMilliseconds}ms");
         sw.Restart();
         LoadFileList(lstFolder);

         System.Diagnostics.Debug.WriteLine($"Init Function - LoadFileList: {sw.ElapsedMilliseconds}ms");
         sw.Restart();
         var cvs = new CollectionViewSource { Source = items };
         cvs.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
         FileListBox.ItemsSource = cvs.View;

         System.Diagnostics.Debug.WriteLine($"Init Function - FileListBox.ItemsSource = cvs.View: {sw.ElapsedMilliseconds}ms");
         sw.Restart();
         //Task.Run(() => CreateJumpList(items));
         ////CreateJumpList(items);

         //this.Loaded += (s, e) => PositionWindow();

         this.Loaded += (s, e) => {
            PositionWindow();

            // Queue jumplist creation at lower priority
            Dispatcher.BeginInvoke(new Action(() => {
               CreateJumpList(items);
            }), DispatcherPriority.Background);
         };

         System.Diagnostics.Debug.WriteLine($"Init Function - this.Loaded: {sw.ElapsedMilliseconds}ms");

      }

      public List<AdiQuickLaunchLib.QuickLauncher.QuickItem> LoadFolders(string filePath)
      {
         if (!File.Exists(filePath))
            return new List<QuickLauncher.QuickItem>();

         try
         {
            string json = File.ReadAllText(filePath);

            // Deserializing the entire JSON file into the QuickLauncher class
            // which is the class that holds the 'Id', 'Name', and 'Items'.
            var launcherData = JsonSerializer.Deserialize<QuickLauncher>(json);
      
            // Now, safely return the Items list from the fully deserialized object.
            if (launcherData != null && launcherData.Items != null)
            {
               return launcherData.Items.ToList();
            }
         }
         catch (JsonException ex)
         {
            Console.WriteLine($"Error deserializing JSON: {ex.Message}");
         }
         catch (Exception ex)
         {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
         }

         // Return an empty list on failure
         return new List<QuickLauncher.QuickItem>();
      }


      private void PositionWindow()
      {
         var mousePos = System.Windows.Forms.Cursor.Position;
         this.Left = mousePos.X - (this.ActualWidth / 2);
         this.Top = mousePos.Y - this.ActualHeight - 10;

         // Keep on screen
         if (this.Left < 0) this.Left = 10;
         if (this.Top < 0) this.Top = 10;
      }

      private void CreateJumpList(List<FileSystemItem> items)
      {
         try
         {
            JumpList jumpList = new JumpList
            {
               ShowFrequentCategory = false,
               ShowRecentCategory = false
            };

            //bool useCategories = items.Select(i => i.Category).Distinct().Count() > 1;

            foreach (FileSystemItem item in items)
            {
               if (!File.Exists(item.FullPath) && !Directory.Exists(item.FullPath))
               {
                  // Handle missing path
                  jumpList.JumpItems.Add(new JumpTask
                  {
                     Title = $"{item.Name} (Missing)",
                     Description = "The path does not exist",
                     ApplicationPath = Application.ResourceAssembly.Location,
                     Arguments = "--error"
                  });
                  continue;
               }

               var task = new JumpTask
               {
                  Title = item.Name,
                  Description = item.IsDirectory
                     ? $"Open folder: {item.Name}"
                     : $"Open: {item.Name}",
                  ApplicationPath = "explorer.exe",
                  Arguments = $"\"{item.FullPath}\"",
                  WorkingDirectory = Path.GetDirectoryName(item.FullPath)
               };

               if (item.IsDirectory)
               {
                  task.IconResourcePath = "shell32.dll";
                  task.IconResourceIndex = 3; // folder
               }
               else if (Path.GetExtension(item.FullPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(item.FullPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
               {
                  task.IconResourcePath = item.FullPath;
                  task.IconResourceIndex = 0;
               }
               else if (Path.GetExtension(item.FullPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
               {
                  string exePath = ResolveShortcut(item.FullPath);
                  task.ApplicationPath = exePath;
                  task.Arguments = "";
                  task.IconResourcePath = exePath;
                  task.IconResourceIndex = 0;
               }
               else
               {
                  task.IconResourcePath = "shell32.dll";
                  task.IconResourceIndex = 1; // generic doc
               }



               //if (useCategories && !string.IsNullOrWhiteSpace(item.Category))
               //{
               //   //JumpList.AddToRecentCategory(item.FullPath);
               //   //task.CustomCategory = item.Category;
               //}

               jumpList.JumpItems.Add(task);
            }

            JumpList.SetJumpList(Application.Current, jumpList);
         }
         catch (Exception ex)
         {
            MessageBox.Show(
                $"Error creating jump list: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
         }

         string ResolveShortcut(string shortcutPath)
         {
            var shell = new IWshRuntimeLibrary.WshShell();
            var link = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutPath);
            return link.TargetPath; // path to exe, doc, etc.
         }
      }


      private void CreateJumpListOld(List<string> lstFolderIn)
      {
         try
         {
            var jumpList = new JumpList();
            jumpList.ShowFrequentCategory = false;
            jumpList.ShowRecentCategory = false;

            bool useCategories = lstFolderIn.Count > 1;

            foreach (FileSystemItem item in items)
            {
               if (Directory.Exists(item.FullPath))
               {
                  // Get files and directories
                  string sFolderPath = item.FullPath;

                  var files = Directory.EnumerateFiles(sFolderPath).Take(10);
                  var directories = Directory.EnumerateDirectories(sFolderPath).Take(10);

                  var sortedDirectories = directories.OrderBy(d => Path.GetFileName(d));
                  var sortedFiles = files.OrderBy(f => Path.GetFileName(f));

                  //var sortedDirectories = Directory.EnumerateDirectories(sFolderPath)
                  //   .Take(10)
                  //   .OrderBy(d => Path.GetFileName(d));

                  //string category = useCategories ? Path.GetFileName(sFolderPath) : null;

                  // Directories
                  foreach (String dirPath in sortedDirectories)
                  {
                     var jumpTask = new JumpTask
                     {
                        Title = item.Name,
                        Description = $"Open folder: {Path.GetFileName(dirPath)}",
                        ApplicationPath = "explorer.exe",
                        Arguments = $"\"{dirPath}\"",
                        WorkingDirectory = Path.GetDirectoryName(dirPath),
                        IconResourcePath = "shell32.dll", // contains default folder icons
                        IconResourceIndex = 3             // 3 = standard folder
                        //CustomCategory = category
                     };
                     jumpList.JumpItems.Add(jumpTask);
                  }

                  // Files
                  foreach (var filePath in sortedFiles)
                  {
                     var jumpTask = new JumpTask
                     {
                        Title = item.Name,
                        Description = $"Open: {Path.GetFileName(filePath)}",
                        ApplicationPath = "explorer.exe",
                        Arguments = $"\"{filePath}\"",
                        WorkingDirectory = Path.GetDirectoryName(filePath),
                        IconResourcePath = filePath,
                        IconResourceIndex = 0
                        //CustomCategory = category
                     };
                     jumpList.JumpItems.Add(jumpTask);
                  }

                  // Main folder link
                  var openFolderTask = new JumpTask
                  {
                     Title = "Open Main Folder",
                     Description = $"Open {Path.GetFileName(sFolderPath)}",
                     ApplicationPath = "explorer.exe",
                     Arguments = $"\"{sFolderPath}\"",
                     WorkingDirectory = Path.GetDirectoryName(sFolderPath),
                     IconResourcePath = "shell32.dll", // contains default folder icons
                     IconResourceIndex = 3             // 3 = standard folder
                     //CustomCategory = category
                  };
                  jumpList.JumpItems.Add(openFolderTask);
               }
               else
               {
                  var errorTask = new JumpTask
                  {
                     Title = "Folder Not Found",
                     Description = "The monitored folder does not exist",
                     ApplicationPath = Application.ResourceAssembly.Location,
                     Arguments = "--error"
                  };
                  jumpList.JumpItems.Add(errorTask);
               }
            }

            //foreach (string sFolderPath in lstFolderIn)
            //{
            //   if (Directory.Exists(sFolderPath))
            //   {
            //      // Get files and directories
            //      var files = Directory.EnumerateFiles(sFolderPath).Take(10);
            //      var directories = Directory.EnumerateDirectories(sFolderPath).Take(10);

            //      var sortedDirectories = directories.OrderBy(d => Path.GetFileName(d));
            //      var sortedFiles = files.OrderBy(f => Path.GetFileName(f));

            //      //string category = useCategories ? Path.GetFileName(sFolderPath) : null;

            //      // Directories
            //      foreach (var dirPath in sortedDirectories)
            //      {
            //         var jumpTask = new JumpTask
            //         {
            //            Title = Path.GetFileName(dirPath) + " (Folder)",
            //            Description = $"Open folder: {Path.GetFileName(dirPath)}",
            //            ApplicationPath = "explorer.exe",
            //            Arguments = $"\"{dirPath}\"",
            //            WorkingDirectory = Path.GetDirectoryName(dirPath),
            //            IconResourcePath = "shell32.dll", // contains default folder icons
            //            IconResourceIndex = 3             // 3 = standard folder
            //            //CustomCategory = category
            //         };
            //         jumpList.JumpItems.Add(jumpTask);
            //      }

            //      // Files
            //      foreach (var filePath in sortedFiles)
            //      {
            //         var jumpTask = new JumpTask
            //         {
            //            Title = Path.GetFileName(filePath),
            //            Description = $"Open: {Path.GetFileName(filePath)}",
            //            ApplicationPath = "explorer.exe",
            //            Arguments = $"\"{filePath}\"",
            //            WorkingDirectory = Path.GetDirectoryName(filePath),
            //            IconResourcePath = filePath,
            //            IconResourceIndex = 0
            //            //CustomCategory = category
            //         };
            //         jumpList.JumpItems.Add(jumpTask);
            //      }

            //      // Main folder link
            //      var openFolderTask = new JumpTask
            //      {
            //         Title = "Open Main Folder",
            //         Description = $"Open {Path.GetFileName(sFolderPath)}",
            //         ApplicationPath = "explorer.exe",
            //         Arguments = $"\"{sFolderPath}\"",
            //         WorkingDirectory = Path.GetDirectoryName(sFolderPath),
            //         IconResourcePath = "shell32.dll", // contains default folder icons
            //         IconResourceIndex = 3             // 3 = standard folder
            //         //CustomCategory = category
            //      };
            //      jumpList.JumpItems.Add(openFolderTask);
            //   }
            //   else
            //   {
            //      var errorTask = new JumpTask
            //      {
            //         Title = "Folder Not Found",
            //         Description = "The monitored folder does not exist",
            //         ApplicationPath = Application.ResourceAssembly.Location,
            //         Arguments = "--error"
            //      };
            //      jumpList.JumpItems.Add(errorTask);
            //   }
            //}

            JumpList.SetJumpList(Application.Current, jumpList);
         }
         catch (Exception ex)
         {
            MessageBox.Show($"Error creating jump list: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
         }
      }


      private void LoadFileList(List<QuickLauncher.QuickItem> lstFolder)
      {
         items = new List<FileSystemItem>();

         foreach (QuickLauncher.QuickItem cItem in lstFolder)
         {
            FileAttributes attr = File.GetAttributes(cItem.Path);
            if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
            {
               items.Add(new FileSystemItem
               {
                  Name = cItem.Name,
                  FullPath = cItem.Path,
                  IsDirectory = true,
                  Category = "Direcotries",
                  Icon = AdiQuickLaunchLib.IconHelper.GetIcon(cItem.Path, true)
               });
            }
            else
            {
               items.Add(new FileSystemItem
               {
                  Name = cItem.Name,
                  FullPath = cItem.Path,
                  IsDirectory = false,
                  Category = "Files",
                  Icon = AdiQuickLaunchLib.IconHelper.GetIcon(cItem.Path, false)
               });
            }
            
            
            // if (Directory.Exists(sFolder))
            // {
            //    // Add directories first
            //    var directories = Directory.EnumerateDirectories(sFolder)
            //       .OrderBy(d => Path.GetFileName(d));
            //
            //    foreach (String dir in directories)
            //    {
            //       items.Add(new FileSystemItem
            //       {
            //          Name = Path.GetFileName(dir),
            //          FullPath = dir,
            //          IsDirectory = true,
            //          Category = Path.GetFileName(sFolder),
            //          Icon = IconHelper.GetIcon(dir, true)
            //       });
            //    }
            //
            //    // Add files
            //    var files = Directory.EnumerateFiles(sFolder)
            //       .OrderBy(f => Path.GetFileName(f));
            //
            //    foreach (var file in files)
            //    {
            //       items.Add(new FileSystemItem
            //       {
            //          Name = Path.GetFileName(file),
            //          FullPath = file,
            //          IsDirectory = false,
            //          Category = Path.GetFileName(sFolder),
            //          Icon = IconHelper.GetIcon(file, false)
            //       });
            //    }
            // }
         }

         FileListBox.ItemsSource = items;
      }

      //private string GetFileIcon(string extension)
      //{
      //   return extension switch
      //   {
      //      ".txt" => "📄",
      //      ".pdf" => "📋",
      //      ".doc" or ".docx" => "📝",
      //      ".xls" or ".xlsx" => "📊",
      //      ".ppt" or ".pptx" => "📈",
      //      ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
      //      ".mp3" or ".wav" or ".flac" => "🎵",
      //      ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
      //      ".zip" or ".rar" or ".7z" => "📦",
      //      ".exe" => "⚙️",
      //      _ => "📄"
      //   };
      //}

      private void OpenItem(FileSystemItem item)
      {
         try
         {
            if (item.IsDirectory)
            {
               Process.Start("explorer.exe", $"\"{item.FullPath}\"");
            }
            else
            {
               Process.Start(new ProcessStartInfo
               {
                  FileName = item.FullPath,
                  UseShellExecute = true
               });
            }
            this.Close();
         }
         catch (Exception ex)
         {
            MessageBox.Show($"Error opening item: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
         }
      }

      private void Window_Deactivated(object sender, EventArgs e)
      {
         this.Hide(); // Instead of Close()
         Task.Run(() =>
         {
            Thread.Sleep(100);
            Dispatcher.Invoke(() => this.Close());
         });
      }

      private void Window_KeyDown(object sender, KeyEventArgs e)
      {
         if (e.Key == Key.Escape)
         {
            this.Close();
         }
         else if (e.Key == Key.Enter && FileListBox.SelectedItem is FileSystemItem item)
         {
            OpenItem(item);
         }
      }

      private void CloseButton_Click(object sender, RoutedEventArgs e)
      {
         this.Close();
      }

      private void FileListBox_MouseClick(Object sender, MouseButtonEventArgs e)
      {
         if (FileListBox.SelectedItem is FileSystemItem item)
         {
            OpenItem(item);
         }
      }
   }

   public class FileSystemItem
   {
      public string Name { get; set; }
      public string FullPath { get; set; }
      public bool IsDirectory { get; set; }
      public string Category { get; set; }
      public ImageSource Icon { get; set; }
   }
}