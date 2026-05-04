using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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
using ComTypes = System.Runtime.InteropServices.ComTypes;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace AdiQuickLaunch
{
   public partial class MainWindow : Window
   {
      private ObservableCollection<FileSystemItem> items;
      private Point _dragStartPoint;
      private FileSystemItem _draggedItem;
      private QuickLauncher? _currentLauncher;
      private bool _isPinned = false;
      
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
            _currentLauncher  = JsonSerializer.Deserialize<QuickLauncher>(json);
      
            // Now, safely return the Items list from the fully deserialized object.
            if (_currentLauncher  != null && _currentLauncher .Items != null)
            {
               return _currentLauncher .Items.ToList();
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

      private void CreateJumpList(ObservableCollection<FileSystemItem> items)
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
                  ApplicationPath = Environment.ProcessPath,
                  //ApplicationPath = "explorer.exe",
                  Arguments = $"\"{item.FullPath}\"",
                  WorkingDirectory = Path.GetDirectoryName(item.FullPath),
                  //CustomCategory = item.IsDirectory ? "Folders" : "Files"
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
               //task.CustomCategory = item.IsDirectory ? "Folders" : "Files";
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
            var shellLink = (AdiQuickLaunchLib.IconHelper.IShellLink)new AdiQuickLaunchLib.IconHelper.ShellLink();
            var persist = (ComTypes.IPersistFile)shellLink;
    
            persist.Load(shortcutPath, 0); // 0 = STGM_READ
    
            var sb = new System.Text.StringBuilder(260); // MAX_PATH
            shellLink.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
    
            return sb.ToString();
         }
      }

      // private void LoadFileList(List<QuickLauncher.QuickItem> lstFolder)
      // {
      //    items = new ObservableCollection<FileSystemItem>();
      //
      //    foreach (QuickLauncher.QuickItem cItem in lstFolder)
      //    {
      //       try
      //       {
      //          //FileAttributes attr = File.GetAttributes(cItem.Path);
      //          //if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
      //          if(cItem.IsDirectory)
      //          {
      //             if (!Directory.Exists(cItem.Path))
      //             {
      //                string iconPath = "/Assets/foldernotexists.ico";
      //                Uri iconUri = new Uri(iconPath, UriKind.Relative);
      //                BitmapImage iconSource = new BitmapImage(iconUri);
      //
      //                items.Add(new FileSystemItem
      //                {
      //                   Name = $"{cItem.Name} (Missing)",
      //                   FullPath = cItem.Path,
      //                   IsDirectory = false,
      //                   Category = "Files",
      //                   Icon = iconSource
      //                });
      //             }
      //             else
      //             {
      //                items.Add(new FileSystemItem
      //                {
      //                   Name = cItem.Name,
      //                   FullPath = cItem.Path,
      //                   IsDirectory = true,
      //                   Category = "Direcotries",
      //                   Icon = AdiQuickLaunchLib.IconHelper.GetIcon(cItem.Path, true)
      //                });
      //             }
      //          }
      //          else
      //          {
      //             if (!File.Exists(cItem.Path))
      //             {
      //                string iconPath = "/Assets/filenotexists.ico";
      //                Uri iconUri = new Uri(iconPath, UriKind.Relative);
      //                BitmapImage iconSource = new BitmapImage(iconUri);
      //
      //                items.Add(new FileSystemItem
      //                {
      //                   Name = $"{cItem.Name} (Missing)",
      //                   FullPath = cItem.Path,
      //                   IsDirectory = false,
      //                   Category = "Files",
      //                   Icon = iconSource
      //                });
      //             }
      //             else
      //             {
      //                items.Add(new FileSystemItem
      //                {
      //                   Name = cItem.Name,
      //                   FullPath = cItem.Path,
      //                   IsDirectory = false,
      //                   Category = "Files",
      //                   Icon = AdiQuickLaunchLib.IconHelper.GetIcon(cItem.Path, false)
      //                });
      //             }
      //          }
      //       }
      //       catch (Exception e)
      //       {
      //          Console.WriteLine($"{cItem.Path} =  {e.Message}");
      //       }
      //       
      //
      //    }
      //
      //    FileListBox.ItemsSource = items;
      // }
      
      private void LoadFileList(List<QuickLauncher.QuickItem> lstFolder)
      {
         items = new ObservableCollection<FileSystemItem>();

         // Respect saved order
         //var sorted = lstFolder.OrderBy(i => i.Order).ToList();
         var sorted = lstFolder
            .OrderBy(i => i.IsDirectory ? 0 : 1)
            .ThenBy(i => i.Order)
            .ToList();

         foreach (QuickLauncher.QuickItem cItem in sorted)
         {
            try
            {
               if (cItem.IsDirectory)
               {
                  string iconPath = Directory.Exists(cItem.Path)
                     ? null
                     : "/Assets/foldernotexists.ico";

                  items.Add(new FileSystemItem
                  {
                     Name = Directory.Exists(cItem.Path) ? cItem.Name : $"{cItem.Name} (Missing)",
                     FullPath = cItem.Path,
                     IsDirectory = true,
                     Icon = Directory.Exists(cItem.Path)
                        ? AdiQuickLaunchLib.IconHelper.GetIcon(cItem.Path, true)
                        : new BitmapImage(new Uri(iconPath, UriKind.Relative))
                  });
               }
               else
               {
                  string iconPath = File.Exists(cItem.Path)
                     ? null
                     : "/Assets/filenotexists.ico";

                  items.Add(new FileSystemItem
                  {
                     Name = File.Exists(cItem.Path) ? cItem.Name : $"{cItem.Name} (Missing)",
                     FullPath = cItem.Path,
                     IsDirectory = false,
                     Icon = File.Exists(cItem.Path)
                        ? AdiQuickLaunchLib.IconHelper.GetIcon(cItem.Path, false)
                        : new BitmapImage(new Uri(iconPath, UriKind.Relative))
                  });
               }
            }
            catch (Exception ex)
            {
               Console.WriteLine($"{cItem.Path} = {ex.Message}");
            }
         }

         FileListBox.ItemsSource = items;
      }

      private void OpenItem(FileSystemItem item)
      {
         if (item.IsDirectory && !Directory.Exists(item.FullPath) ||
             !item.IsDirectory && !File.Exists(item.FullPath))
         {
            var result = MessageBox.Show(
               $"'{item.Name}' does not exist anymore.\n\nRemove it from the list?",
               "Not Found",
               MessageBoxButton.YesNo,
               MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
               items.Remove(item);
               var match = _currentLauncher.Items.FirstOrDefault(q => q.Path == item.FullPath);
               if (match != null)
               {
                  _currentLauncher.Items.Remove(match);
                  Shared.SaveLauncher(_currentLauncher);
               }
            }
            return;
         }

         try
         {
            if (item.IsDirectory)
               Process.Start("explorer.exe", $"\"{item.FullPath}\"");
            else
               Process.Start(new ProcessStartInfo
               {
                  FileName = item.FullPath,
                  UseShellExecute = true
               });

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
         if (_isPinned) return;
         
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
      
      private void FileListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         _dragStartPoint = e.GetPosition(null);
         _draggedItem = (e.OriginalSource as FrameworkElement)?.DataContext as FileSystemItem;
      }

      private void FileListBox_PreviewMouseMove(object sender, MouseEventArgs e)
      {
         if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;

         var diff = _dragStartPoint - e.GetPosition(null);
         if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
             Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

         DragDrop.DoDragDrop(FileListBox, new DataObject(typeof(FileSystemItem), _draggedItem), DragDropEffects.Move);
      }

      private void FileListBox_Drop(object sender, DragEventArgs e)
      {
         var draggedItem = e.Data.GetData(typeof(FileSystemItem)) as FileSystemItem;
         if (draggedItem == null) return;

         var element = e.OriginalSource as DependencyObject;

         while (element != null && !(element is ListBoxItem))
         {
            System.Diagnostics.Debug.WriteLine($"Walking: {element.GetType().Name}");
            element = VisualTreeHelper.GetParent(element);
         }
         var target = (element as ListBoxItem)?.DataContext as FileSystemItem;
         if (target == null || target == draggedItem) return;
         
         // Block cross-type drops
         if (draggedItem.IsDirectory != target.IsDirectory) return;

         int oldIndex = items.IndexOf(draggedItem);
         int newIndex = items.IndexOf(target);

         if (oldIndex < 0 || newIndex < 0) return;

         items.Move(oldIndex, newIndex);
         
         if (_currentLauncher != null)
         {
            for (int i = 0; i < items.Count; i++)
            {
               var match = _currentLauncher.Items.FirstOrDefault(q => q.Path == items[i].FullPath);
               if (match != null)
                  match.Order = i;
            }
            Shared.SaveLauncher(_currentLauncher);
         }
      }
      
      private void PinButton_Checked(object sender, RoutedEventArgs e)
      {
         _isPinned = true;
         this.Topmost = true;
      }

      private void PinButton_Unchecked(object sender, RoutedEventArgs e)
      {
         _isPinned = false;
         this.Topmost = false;
      }

      private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         if (e.ButtonState == MouseButtonState.Pressed)
            this.DragMove();
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