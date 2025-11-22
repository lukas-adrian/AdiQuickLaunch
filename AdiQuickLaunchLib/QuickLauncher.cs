using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AdiQuickLaunchLib
{
   public class QuickLauncher : INotifyPropertyChanged
   {
      public class QuickItem : INotifyPropertyChanged
      {
         private string _name;
         private bool _isDirty; // The new tracking flag

         public string Name
         {
            get => _name;
            set
            {
               if (_name != value)
               {
                  _name = value;
                  OnPropertyChanged(nameof(Name));
                
                  // Set the flag whenever a change occurs
                  IsDirty = true; 
               }
            }
         }
         
         [JsonIgnore]
         public bool IsDirty
         {
            get => _isDirty;
            private set // Private setter so only the class can set it
            {
               if (_isDirty != value)
               {
                  _isDirty = value;
                  OnPropertyChanged(nameof(IsDirty));
               }
            }
         }
         
         public string Path { get; set; }
         public bool IsDirectory { get; set; }

         public override string ToString()
         {
            return Path;
         }

         private ImageSource _iconSource;
         [JsonIgnore]
         public ImageSource IconSource
         {
            get
            {
               // 1. If already cached, return the cached value.
               if (_iconSource != null)
               {
                  return _iconSource;
               }

               // 2. Attempt to load the specific icon dynamically.
               // NOTE: IconHelper.GetIcon returns null on failure.
               ImageSource loadedIcon = IconHelper.GetIcon(this.Path, this.IsDirectory);
         
               if (loadedIcon != null)
               {
                  // Success: Cache and return the dynamically loaded icon.
                  _iconSource = loadedIcon;
               }
               else
               {
                  // Failure: Cache and return a hardcoded fallback icon.
                  // You need to replace this with your actual fallback logic (e.g., loading a simple PNG).
                  _iconSource = GetEmojiFallback(this.IsDirectory); 
               }

               return _iconSource;
            }
            set
            {
               _iconSource = value;
            }
         }
         
         public event PropertyChangedEventHandler PropertyChanged;
         protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
      
      private static ImageSource GetEmojiFallback(bool isDirectory)
      {
         // Fallback URI for a simple folder/file icon stored in resources
         string fallbackUri = isDirectory 
            ? "pack://application:,,,/Assets/DefaultFolder.png" 
            : "pack://application:,,,/Assets/DefaultFile.png";
           
         return new BitmapImage(new Uri(fallbackUri));
      }

      public Guid Id { get; set; } = Guid.NewGuid();

      private string _name;
      private bool _isEditing;
      private string _iconPath;

      public string Name
      {
         get => _name;
         set { _name = value; OnPropertyChanged(nameof(Name)); }
      }

      public string IconPath
      {
         get => _iconPath;
         set { _iconPath = value; OnPropertyChanged(nameof(IconPath)); }
      }
      public ObservableCollection<QuickItem> Items { get; set; } = new();
      
      [JsonIgnore]
      public bool IsEditing
      {
         get => _isEditing;
         set { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }
      }

      public bool CheckIsDirty()
      {
         foreach (QuickItem quickItem in Items)
         {
            if (quickItem.IsDirty)
               return true;
         }

         return false;
      }
      
      public event PropertyChangedEventHandler PropertyChanged;
      protected void OnPropertyChanged(string propertyName) =>
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
   }
}
