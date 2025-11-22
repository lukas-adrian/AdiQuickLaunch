using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using AdiQuickLaunchLib;
using Application = System.Windows.Application;
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
         //var sw = System.Diagnostics.Stopwatch.StartNew();

         InitializeComponent();
         //System.Diagnostics.Debug.WriteLine($"InitializeComponent: {sw.ElapsedMilliseconds}ms");

         //sw.Restart();
         Init(null);
         //System.Diagnostics.Debug.WriteLine($"Init method: {sw.ElapsedMilliseconds}ms");

      }

      public MainWindow(string? jsonPath)
      {
         InitializeComponent();

         Init(jsonPath);
      }

      private async void Init(string? jsonPath)
      {
         //var sw = System.Diagnostics.Stopwatch.StartNew();

         List<QuickLauncher.QuickItem> lstFolder = new List<QuickLauncher.QuickItem>();
         if (!string.IsNullOrEmpty(jsonPath) || File.Exists(jsonPath))
            lstFolder = LoadFolders(jsonPath);

         //System.Diagnostics.Debug.WriteLine($"Init Function - LoadFolders: {sw.ElapsedMilliseconds}ms");
         //sw.Restart();
         string sCurDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

         // Set icon
         string iconPath = Path.Combine(sCurDirectory, "app.ico");
         
         if (File.Exists(iconPath))
         {
            Task.Run(() => {
               if (File.Exists(iconPath))
               {
                  Dispatcher.Invoke(() => {
                     this.Icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                  });
               }
            });
         }

         
         //System.Diagnostics.Debug.WriteLine($"Init Function - this.Icon: {sw.ElapsedMilliseconds}ms");
         //sw.Restart();
         
         LoadFileList(lstFolder);
         
         //System.Diagnostics.Debug.WriteLine($"Init Function - LoadFileList: {sw.ElapsedMilliseconds}ms");
         //sw.Restart();
         var cvs = new CollectionViewSource { Source = items };
         cvs.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
         FileListBox.ItemsSource = cvs.View;

         this.Loaded += (s, e) => {
            PositionWindow();

            // Queue jumplist creation at lower priority
            Dispatcher.BeginInvoke(new Action(() => {
               CreateJumpList(items);
            }), DispatcherPriority.Background);
         };

         //System.Diagnostics.Debug.WriteLine($"Init Function - this.Loaded: {sw.ElapsedMilliseconds}ms");

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
                     Title = $"{item.Name}",
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

      private void LoadFileList(List<QuickLauncher.QuickItem> lstFolder)
      {
         items = new List<FileSystemItem>();


         foreach (QuickLauncher.QuickItem cItem in lstFolder)
         {
            try
            {
               //FileAttributes attr = File.GetAttributes(cItem.Path);
               //if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
               if(cItem.IsDirectory)
               {
                  if (!Directory.Exists(cItem.Path))
                  {
                     string iconPath = "/Assets/foldernotexists.ico";
                     Uri iconUri = new Uri(iconPath, UriKind.Relative);
                     BitmapImage iconSource = new BitmapImage(iconUri);

                     items.Add(new FileSystemItem
                     {
                        Name = $"{cItem.Name} (Missing)",
                        FullPath = cItem.Path,
                        IsDirectory = false,
                        Category = "Files",
                        Icon = iconSource
                     });
                  }
                  else
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
               }
               else
               {
                  if (!File.Exists(cItem.Path))
                  {
                     string iconPath = "/Assets/filenotexists.ico";
                     Uri iconUri = new Uri(iconPath, UriKind.Relative);
                     BitmapImage iconSource = new BitmapImage(iconUri);

                     items.Add(new FileSystemItem
                     {
                        Name = $"{cItem.Name} (Missing)",
                        FullPath = cItem.Path,
                        IsDirectory = false,
                        Category = "Files",
                        Icon = iconSource
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
               }
            }
            catch (Exception e)
            {
               Console.WriteLine($"{cItem.Path} =  {e.Message}");
            }
            

         }

         FileListBox.ItemsSource = items;
      }

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