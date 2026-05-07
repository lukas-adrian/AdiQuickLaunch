using System.Windows;
using Microsoft.Win32;

namespace AdiQuickLaunchManager;

public partial class EditItemDialog : Window
{
    public string ItemName { get; private set; }
    public string ItemPath { get; private set; }
    public string ItemParameters { get; private set; }
        
    private readonly bool _isDirectory;

    
    public EditItemDialog(bool isDirectory, string name = "", string path = "", string parameters = "")
    {
        InitializeComponent();
        _isDirectory = isDirectory;
        Title = isDirectory ? "Edit Folder" : "Edit Item";
        NameBox.Text = name;
        PathBox.Text = path;
        ParametersBox.Text = parameters;
            
        // Parameters only make sense for files
        ParametersBox.IsEnabled = !isDirectory;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDirectory)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                PathBox.Text = dialog.FolderName;
                if (string.IsNullOrEmpty(NameBox.Text))
                    NameBox.Text = System.IO.Path.GetFileName(dialog.FolderName);
            }
        }
        else
        {
            var dialog = new OpenFileDialog
            {
                Filter = "All Files (*.*)|*.*|Executables (*.exe)|*.exe"
            };
            if (dialog.ShowDialog() == true)
            {
                PathBox.Text = dialog.FileName;
                if (string.IsNullOrEmpty(NameBox.Text))
                    NameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text) || string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show("Name and Path are required.", "Validation", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ItemName = NameBox.Text.Trim();
        ItemPath = PathBox.Text.Trim();
        ItemParameters = ParametersBox.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}