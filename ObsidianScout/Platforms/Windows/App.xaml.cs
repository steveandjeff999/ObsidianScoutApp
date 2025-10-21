using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
using WinUIColor = Windows.UI.Color;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ObsidianScout.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
   /// </summary>
        public App()
        {
 this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp()
        {
       var mauiApp = MauiProgram.CreateMauiApp();
 
            // Configure title bar after MAUI app is created
       Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
     {
        if (handler.PlatformView is Microsoft.UI.Xaml.Window window)
     {
           ConfigureTitleBar(window);
           
  // Listen for theme changes
    if (view is Microsoft.Maui.Controls.Window mauiWindow)
         {
            mauiWindow.PropertyChanged += (s, e) =>
                {
    if (e.PropertyName == nameof(Microsoft.Maui.Controls.Window.Page))
          {
       ConfigureTitleBar(window);
   }
           };
   }
 }
            });
            
     return mauiApp;
        }

        private void ConfigureTitleBar(Microsoft.UI.Xaml.Window window)
        {
      try
  {
                var hwnd = WindowNative.GetWindowHandle(window);
         var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

 if (appWindow != null)
          {
       var titleBar = appWindow.TitleBar;
           
       // Determine if we're in light or dark mode
   var currentTheme = Microsoft.Maui.Controls.Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
     var isLightMode = currentTheme == AppTheme.Light || 
 (currentTheme == AppTheme.Unspecified && this.RequestedTheme == ApplicationTheme.Light);
           
           if (isLightMode)
         {
      // Light mode colors - High contrast
       titleBar.BackgroundColor = WinUIColor.FromArgb(255, 255, 255, 255); // White
        titleBar.ForegroundColor = WinUIColor.FromArgb(255, 28, 28, 30); // #1C1C1E
   titleBar.InactiveBackgroundColor = WinUIColor.FromArgb(255, 245, 245, 247); // #F5F5F7
    titleBar.InactiveForegroundColor = WinUIColor.FromArgb(255, 142, 142, 147); // #8E8E93
          
             // Button colors
              titleBar.ButtonBackgroundColor = WinUIColor.FromArgb(0, 255, 255, 255); // Transparent
            titleBar.ButtonForegroundColor = WinUIColor.FromArgb(255, 28, 28, 30); // #1C1C1E
  titleBar.ButtonHoverBackgroundColor = WinUIColor.FromArgb(255, 232, 232, 237); // #E8E8ED
         titleBar.ButtonHoverForegroundColor = WinUIColor.FromArgb(255, 28, 28, 30); // #1C1C1E
            titleBar.ButtonPressedBackgroundColor = WinUIColor.FromArgb(255, 209, 209, 214); // #D1D1D6
                titleBar.ButtonPressedForegroundColor = WinUIColor.FromArgb(255, 28, 28, 30); // #1C1C1E
  
           // Inactive button colors
      titleBar.ButtonInactiveBackgroundColor = WinUIColor.FromArgb(0, 255, 255, 255); // Transparent
            titleBar.ButtonInactiveForegroundColor = WinUIColor.FromArgb(255, 142, 142, 147); // #8E8E93
            }
        else
  {
         // Dark mode colors
    titleBar.BackgroundColor = WinUIColor.FromArgb(255, 15, 23, 42); // #0F172A
 titleBar.ForegroundColor = WinUIColor.FromArgb(255, 248, 250, 252); // #F8FAFC
         titleBar.InactiveBackgroundColor = WinUIColor.FromArgb(255, 30, 41, 59); // #1E293B
   titleBar.InactiveForegroundColor = WinUIColor.FromArgb(255, 100, 116, 139); // #64748B
       
             // Button colors
        titleBar.ButtonBackgroundColor = WinUIColor.FromArgb(0, 255, 255, 255); // Transparent
      titleBar.ButtonForegroundColor = WinUIColor.FromArgb(255, 248, 250, 252); // #F8FAFC
     titleBar.ButtonHoverBackgroundColor = WinUIColor.FromArgb(255, 51, 65, 85); // #334155
   titleBar.ButtonHoverForegroundColor = WinUIColor.FromArgb(255, 248, 250, 252); // #F8FAFC
    titleBar.ButtonPressedBackgroundColor = WinUIColor.FromArgb(255, 71, 85, 105); // #475569
           titleBar.ButtonPressedForegroundColor = WinUIColor.FromArgb(255, 248, 250, 252); // #F8FAFC
            
   // Inactive button colors
       titleBar.ButtonInactiveBackgroundColor = WinUIColor.FromArgb(0, 255, 255, 255); // Transparent
         titleBar.ButtonInactiveForegroundColor = WinUIColor.FromArgb(255, 100, 116, 139); // #64748B
         }
  }
            }
      catch (Exception ex)
            {
           System.Diagnostics.Debug.WriteLine($"Error configuring title bar: {ex.Message}");
    }
        }
    }
}
