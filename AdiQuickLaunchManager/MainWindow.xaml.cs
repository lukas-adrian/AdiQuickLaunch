using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AdiQuickLaunchLib;
using Microsoft.Win32;
using IWSRL = IWshRuntimeLibrary;

namespace AdiQuickLaunchManager
{
   /// <summary>
   /// Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class MainWindow : Window
   {
      public ObservableCollection<QuickLauncher> Launchers { get; set; }
      private readonly Dictionary<QuickLauncher, string> _editOriginals = new();

      public MainWindow()
      {
         InitializeComponent();


         Launchers = LoadLaunchers();
         DataContext = this;
      }


      private string GetUniqueLauncherName()
      {
         int counter = Launchers.Count + 1;
         string baseName = "New Launcher";
         string newName;

         do
         {
            newName = $"{baseName} {counter}";
            counter++;
         }
         while (Launchers.Any(l => string.Equals(l.Name, newName, StringComparison.OrdinalIgnoreCase)));

         return newName;
      }

      private void AddLauncher_Click(object sender, RoutedEventArgs e)
      {

         string uniqueName = GetUniqueLauncherName();


         var launcher = new QuickLauncher { Name = uniqueName }; // start empty
         Launchers.Add(launcher);
         LaunchersList.SelectedItem = launcher;

         // mark editing and remember original (empty)
         _editOriginals[launcher] = "";
         launcher.IsEditing = true;

         // ensure the visual exists and focus the editor
         Dispatcher.BeginInvoke(new Action(() =>
         {
            if (LaunchersList.ItemContainerGenerator.ContainerFromItem(launcher) is ListBoxItem item)
            {
               var tb = FindVisualChild<TextBox>(item);
               if (tb != null)
               {
                  tb.Focus();
                  tb.SelectAll();
                  LaunchersList.ScrollIntoView(launcher);
               }
            }
         }), System.Windows.Threading.DispatcherPriority.ContextIdle);
      }

      private void EditLauncherName()
      {
         if (LaunchersList.SelectedItem is QuickLauncher launcher)
         {
            if (!_editOriginals.ContainsKey(launcher))
               _editOriginals[launcher] = launcher.Name;

            launcher.IsEditing = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
               var listBoxItem = (ListBoxItem)LaunchersList.ItemContainerGenerator.ContainerFromItem(launcher);
               if (listBoxItem != null)
               {
                  var textBox = FindVisualChild<TextBox>(listBoxItem);
                  if (textBox != null)
                  {
                     textBox.Focus();
                     textBox.SelectAll();
                     LaunchersList.ScrollIntoView(launcher);
                  }
               }
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
         }
      }

      private void EditorTextBox_Loaded(object sender, RoutedEventArgs e)
      {
         if (sender is TextBox tb && tb.DataContext is QuickLauncher launcher)
         {
            // keep original if not stored yet
            if (!_editOriginals.ContainsKey(launcher))
               _editOriginals[launcher] = launcher.Name;

            // if the model says we're editing, focus/select
            if (launcher.IsEditing)
            {
               Dispatcher.BeginInvoke(new Action(() =>
               {
                  tb.Focus();
                  tb.SelectAll();
                  LaunchersList.ScrollIntoView(launcher);
               }), System.Windows.Threading.DispatcherPriority.Input);
            }
         }
      }

      private void EditorTextBox_KeyDown(object sender, KeyEventArgs e)
      {
         if (sender is TextBox tb && tb.DataContext is QuickLauncher launcher)
         {
            if (e.Key == Key.Enter)
            {
               // commit
               launcher.IsEditing = false;
               e.Handled = true;
               _editOriginals.Remove(launcher);
               LaunchersList.Focus(); // move focus away so visual swaps back
            }
            else if (e.Key == Key.Escape)
            {
               // cancel - restore original
               if (_editOriginals.TryGetValue(launcher, out var orig))
                  launcher.Name = orig;
               launcher.IsEditing = false;
               e.Handled = true;
               _editOriginals.Remove(launcher);
               LaunchersList.Focus();
            }
         }
      }

      private void EditorTextBox_LostFocus(object sender, RoutedEventArgs e)
      {
         if (sender is TextBox tb && tb.DataContext is QuickLauncher launcher)
         {
            // If user left it blank and this was a brand-new empty item, remove it.
            if (string.IsNullOrWhiteSpace(launcher.Name))
            {
               if (_editOriginals.TryGetValue(launcher, out var orig) && string.IsNullOrWhiteSpace(orig))
               {
                  // new empty launcher -> remove
                  Launchers.Remove(launcher);
               }
               else if (_editOriginals.TryGetValue(launcher, out var orig2))
               {
                  // restore previous value if we have it
                  launcher.Name = orig2;
               }
            }

            launcher.IsEditing = false;
            _editOriginals.Remove(launcher);
         }
      }

      private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
      {
         for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
         {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T correctlyTyped)
               return correctlyTyped;

            var result = FindVisualChild<T>(child);
            if (result != null)
               return result;
         }
         return null;
      }

      private void CreateLauncher_Click(object sender, RoutedEventArgs e)
      {
         if (LaunchersList.SelectedItem is QuickLauncher launcher)
         {
            CreateExecutableAndShortcut(launcher);
         }
         else
         {
            MessageBox.Show("Please select a launcher first.",
               "No Launcher Selected",
               MessageBoxButton.OK,
               MessageBoxImage.Warning);
         }
      }

      private void CreateExecutableAndShortcut(QuickLauncher launcher)
      {
         string manufacturer = "AdiSoft";
         string productName = "AdiQuickLauncher";

         string sQLauncherPath = GetApplicationInstallPath();
#if DEBUG
         sQLauncherPath = @"D:\Development\AdiSoft\AdiQuickLaunch\AdiQuickLaunch\bin\x64\Debug\net8.0-windows";
#endif
         string sQLaunchAppl = Path.Combine(sQLauncherPath, @"AdiQuickLaunch.exe");



         string sDestLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"{launcher.Name}.lnk");

         string lauchnerProfile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            manufacturer, productName, $"{launcher.Id}.json");

         CreateShortcut(sDestLink, sQLaunchAppl, lauchnerProfile, launcher.IconPath);

         MessageBox.Show($"Add the link '{Path.GetFileName(sDestLink)}' into the Taskbar." + Environment.NewLine +
                         $"You can access your Links over the jump list" + Environment.NewLine +  
                         $"After adding you can delete the link from the desktop", "AdiQuickLauncher", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.Yes);
      }

      private  string GetApplicationInstallPath()
      {
         
         string sBaseDir = AppDomain.CurrentDomain.BaseDirectory;
         string sPath = Path.Combine(sBaseDir, "AdiQuickLaunchItem");
         return sPath;
      }

      private void CreateShortcut(string shortcutPath, string targetExe, string? arguments = null, string? iconPath = null)
      {
         
         //MessageBox.Show($"shortcutPath {shortcutPath}{Environment.NewLine}targetExe {targetExe}");

         var shell = new IWSRL.WshShell();
         IWSRL.IWshShortcut shortcut = (IWSRL.IWshShortcut)shell.CreateShortcut(shortcutPath);

         shortcut.TargetPath = targetExe;
         shortcut.WorkingDirectory = System.IO.Path.GetDirectoryName(targetExe);
         shortcut.Arguments = arguments ?? "";
         if (!string.IsNullOrEmpty(iconPath))
            shortcut.IconLocation = iconPath;

         shortcut.Save();
      }

      private void AddFolder_Click(object sender, RoutedEventArgs e)
      {
         if (LaunchersList.SelectedItem is QuickLauncher launcher)
         {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
               launcher.Items.Add(
                  new QuickLauncher.QuickItem()
                  {
                     Name = Path.GetDirectoryName(dialog.FolderName),
                     IsDirectory = true,
                     Path = dialog.FolderName
                  });
            }
            
            SaveLauncher(launcher);
         }
         else
         {
            MessageBox.Show("Please select a launcher first.",
               "No Launcher Selected",
               MessageBoxButton.OK,
               MessageBoxImage.Warning);
         }
      }
      private void AddItem_Click(object sender, RoutedEventArgs e)
      {
         if (LaunchersList.SelectedItem is QuickLauncher launcher)
         {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Multiselect = true;
            if (dialog.ShowDialog() == true)
            {
               foreach (string sFileName in dialog.FileNames)
               {
                  launcher.Items.Add(
                     new QuickLauncher.QuickItem()
                     {
                        Name = Path.GetFileName(sFileName),
                        IsDirectory = false,
                        Path = sFileName
                     });
               }
               
               SaveLauncher(launcher);
            }
         }
         else
         {
            MessageBox.Show("Please select a launcher first.",
               "No Launcher Selected",
               MessageBoxButton.OK,
               MessageBoxImage.Warning);
         }
      }

      private void RemoveFolder_Click(object sender, RoutedEventArgs e)
      {
         if (LaunchersList.SelectedItem is QuickLauncher launcher && FoldersList.SelectedItem is QuickLauncher.QuickItem item)
         {
            launcher.Items.Remove(item);
            SaveLauncher(launcher);
         }
         else
         {
            MessageBox.Show("Please select a launcher first.",
               "No Launcher Selected",
               MessageBoxButton.OK,
               MessageBoxImage.Warning);
         }
      }

      private void SaveLauncher(QuickLauncher launcher)
      {
         string baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AdiSoft", "AdiQuickLauncher");

         Directory.CreateDirectory(baseFolder); // ensure folder exists

         string filePath = Path.Combine(baseFolder, $"{launcher.Id}.json");

         string json = JsonSerializer.Serialize(launcher, new JsonSerializerOptions { WriteIndented = true });
         File.WriteAllText(filePath, json);
      }

      private  ObservableCollection<QuickLauncher> LoadLaunchers()
      {
         string baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AdiSoft", "AdiQuickLauncher");

         if (!Directory.Exists(baseFolder))
            return new ObservableCollection<QuickLauncher>();

         var launchers = new ObservableCollection<QuickLauncher>();

         foreach (var file in Directory.GetFiles(baseFolder, "*.json"))
         {
            try
            {
               string json = File.ReadAllText(file);
               QuickLauncher? launcher = JsonSerializer.Deserialize<QuickLauncher>(json);
               if (launcher != null)
                  launchers.Add(launcher);
            }
            catch
            {
               // skip corrupted files
            }
         }

         return launchers;
      }

      private void ChangeIcon_Click(object sender, RoutedEventArgs e)
      {
         if (LaunchersList.SelectedItem is QuickLauncher launcher)
         {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
               Title = "Select Icon",
               Filter = "Icon Files (*.ico)|*.ico|Image Files (*.png;*.jpg)|*.png;*.jpg|Executable Files (*.exe)|*.exe|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
               // Save the selected icon path
               launcher.IconPath = dialog.FileName;
               SaveLauncher(launcher);
            }
         }
         else
         {
            MessageBox.Show("Please select a launcher first.",
               "No Launcher Selected",
               MessageBoxButton.OK,
               MessageBoxImage.Warning);
         }
      }

      private void MainWindow_Closing(object? sender, CancelEventArgs e)
      {
         foreach (QuickLauncher laucher in LaunchersList.Items)
         {
            if(laucher.CheckIsDirty())
               SaveLauncher(laucher);
         }
      }
   }
}