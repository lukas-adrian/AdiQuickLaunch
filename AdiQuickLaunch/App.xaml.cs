using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using AdiQuickLaunch;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace AdiQuickLanuch
{
   /// <summary>
   /// Interaction logic for App.xaml
   /// </summary>
   public partial class App : Application
   {
      protected override void OnStartup(StartupEventArgs e)
      {
         var sw = System.Diagnostics.Stopwatch.StartNew();
         base.OnStartup(e);

         System.Diagnostics.Debug.WriteLine($"base.OnStartup: {sw.ElapsedMilliseconds}ms");

         string jsonPath = e.Args.FirstOrDefault();
         sw.Restart();
         MainWindow wnd = new MainWindow(jsonPath);
         System.Diagnostics.Debug.WriteLine($"MainWindow constructor: {sw.ElapsedMilliseconds}ms");

         sw.Restart();
         wnd.Show();
         System.Diagnostics.Debug.WriteLine($"wnd.Show(): {sw.ElapsedMilliseconds}ms");
         //File.AppendAllText(@"C:\AB_DATE\timing.txt", $"wnd.Show(): {sw.ElapsedMilliseconds}ms\n");
      }

   }

}
