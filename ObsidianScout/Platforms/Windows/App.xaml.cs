using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Windows.Graphics;
using WinUIColor = Windows.UI.Color;
using System.Runtime.InteropServices;

// To learn more about WinUI, the WinUI project structure,
// and more about WinUI project templates, see: http://aka.ms/winui-project-info.

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
   
            // CRITICAL: Handle unhandled exceptions to prevent debug breaks during startup
            // This overrides the auto-generated handler that breaks into debugger
    this.UnhandledException += App_UnhandledException;

            // Subscribe to domain and task exceptions to catch Windows-specific non-fatal errors
      AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

   private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            try
       {
     var ex = e.ExceptionObject as Exception;
     System.Diagnostics.Debug.WriteLine($"[WinUI App] AppDomain.UnhandledException: {ex?.Message}");
        System.Diagnostics.Debug.WriteLine($"[WinUI App] Type: {ex?.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"[WinUI App] Stack: {ex?.StackTrace}");
                
      // If non-fatal, swallow to avoid process termination when possible (can't change e.IsTerminating here)
        if (ex != null && !IsFatalException(ex))
           {
   System.Diagnostics.Debug.WriteLine("[WinUI App] AppDomain non-fatal exception logged");
   }
            }
    catch { }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
   {
            try
   {
          System.Diagnostics.Debug.WriteLine($"[WinUI App] TaskScheduler.UnobservedTaskException: {e.Exception?.Message}");
    System.Diagnostics.Debug.WriteLine($"[WinUI App] Exception type: {e.Exception?.GetType().FullName}");
         
  // Log inner exceptions for AggregateException
  if (e.Exception is AggregateException aggEx)
    {
        foreach (var inner in aggEx.InnerExceptions)
      {
       System.Diagnostics.Debug.WriteLine($"[WinUI App]   Inner: {inner.GetType().Name}: {inner.Message}");
    }
     }
       
     if (!IsFatalException(e.Exception))
 {
               e.SetObserved(); // prevent process termination on .NET events
        System.Diagnostics.Debug.WriteLine("[WinUI App] Unobserved task exception observed and suppressed (non-fatal)");
           }
 }
            catch { }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
         // Log the exception but don't break into debugger for non-fatal exceptions
           System.Diagnostics.Debug.WriteLine($"[WinUI App] Unhandled exception: {e.Exception?.Message}");
     System.Diagnostics.Debug.WriteLine($"[WinUI App] Exception type: {e.Exception?.GetType().FullName}");
   System.Diagnostics.Debug.WriteLine($"[WinUI App] Stack trace: {e.Exception?.StackTrace}");
      
         // Check if this is a "fatal" exception that should crash the app
        var isFatal = IsFatalException(e.Exception);
     
                if (!isFatal)
                {
     // Mark as handled to prevent the app from crashing
e.Handled = true;
     System.Diagnostics.Debug.WriteLine("[WinUI App] Non-fatal exception handled - app will continue");
      }
         else
        {
  System.Diagnostics.Debug.WriteLine("[WinUI App] Fatal exception - app will terminate");
                }
 }
         catch (Exception ex)
  {
        System.Diagnostics.Debug.WriteLine($"[WinUI App] App_UnhandledException handler failed: {ex.Message}");
            }
        }

        private static bool IsFatalException(Exception? exception)
        {
        if (exception == null) return false;

 // Treat certain system exceptions as fatal
       if (exception is OutOfMemoryException || 
      exception is StackOverflowException ||
   exception is AccessViolationException)
         {
       return true;
            }

     // Treat COM/WinRT exceptions as non-fatal unless they indicate a critical failure
     if (exception is COMException comEx)
            {
   // Log HRESULT for diagnostics
System.Diagnostics.Debug.WriteLine($"[WinUI App] COMException HResult: 0x{comEx.HResult:X8}");

                // Known non-fatal HRESULTs:
              // 0x80070490 = Element not found
       // 0x80070057 = E_INVALIDARG
     // 0x8001010E = RPC_E_WRONG_THREAD (common during async XAML operations)
    // 0x802B000A = Frame navigation in progress
          return false;
         }

   // Handle AggregateException from async tasks
    if (exception is AggregateException aggEx)
 {
           // Check if ALL inner exceptions are non-fatal
   foreach (var inner in aggEx.InnerExceptions)
                {
            if (IsFatalException(inner))
          {
           return true;
               }
       }
      return false;
            }

     // Common non-fatal managed exception types
  var nonFatalTypes = new[]
         {
     typeof(InvalidOperationException),
          typeof(ArgumentException),
      typeof(ArgumentNullException),
  typeof(NullReferenceException), // Often from XAML binding
                typeof(ObjectDisposedException),
  typeof(TaskCanceledException),
      typeof(OperationCanceledException),
   typeof(System.Net.Http.HttpRequestException),
         typeof(TimeoutException),
      typeof(KeyNotFoundException),
      typeof(IndexOutOfRangeException),
 typeof(FormatException),
       typeof(NotSupportedException),
   };

            var exceptionType = exception.GetType();
            
  // Check if it's a known non-fatal type
            foreach (var nonFatalType in nonFatalTypes)
       {
        if (nonFatalType.IsAssignableFrom(exceptionType))
  {
            return false;
      }
            }

            // Check for common non-fatal exception patterns by message
            var message = exception.Message?.ToLowerInvariant() ?? "";
 var nonFatalPatterns = new[]
          {
       "binding",
    "xaml",
      "navigation",
      "element",
             "property",
  "collection was modified",
     "sequence contains no",
    "object reference not set",
    "value cannot be null",
                "the operation was canceled",
"task was canceled",
    "error trying to write application data container value",
 "applicationdata",
      "localcontainer",
  "roamingcontainer",
              "cannot find resource",
        "resource not found",
                "dispatcher",
     "thread",
   "synchronization context",
         "shell",
                "route",
      "flyout",
     "tabbar",
            };

     foreach (var pattern in nonFatalPatterns)
    {
   if (message.Contains(pattern))
                {
      return false;
                }
   }

       // Check inner exception recursively
  if (exception.InnerException != null && !IsFatalException(exception.InnerException))
            {
     return false;
            }

            // Default: treat unknown exceptions as non-fatal (log and continue)
      return false;
        }

        protected override MauiApp CreateMauiApp()
        {
            var mauiApp = MauiProgram.CreateMauiApp();
            
            // Configure title bar after MAUI app is created - wrapped in try-catch for safety
            try
            {
             Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
             {
          try
         {
    if (handler.PlatformView is Microsoft.UI.Xaml.Window window)
       {
     ConfigureTitleBar(window);
         
   // Listen for theme changes
     if (view is Microsoft.Maui.Controls.Window mauiWindow)
                {
   mauiWindow.PropertyChanged += (s, e) =>
       {
     try
         {
    if (e.PropertyName == nameof(Microsoft.Maui.Controls.Window.Page))
               {
   ConfigureTitleBar(window);
        }
           }
      catch (Exception ex)
            {
              System.Diagnostics.Debug.WriteLine($"[WinUI App] PropertyChanged handler error: {ex.Message}");
            }
  };
    }
         }
          }
               catch (Exception ex)
       {
    System.Diagnostics.Debug.WriteLine($"[WinUI App] WindowHandler mapping error: {ex.Message}");
         }
                });
     }
     catch (Exception ex)
            {
 System.Diagnostics.Debug.WriteLine($"[WinUI App] Failed to configure window handler mapping: {ex.Message}");
            }
 
            return mauiApp;
        }

      private void ConfigureTitleBar(Microsoft.UI.Xaml.Window window)
        {
   try
          {
      if (window == null)
             {
     System.Diagnostics.Debug.WriteLine("[WinUI App] ConfigureTitleBar: window is null");
  return;
   }

                var hwnd = WindowNative.GetWindowHandle(window);
       if (hwnd == IntPtr.Zero)
                {
        System.Diagnostics.Debug.WriteLine("[WinUI App] ConfigureTitleBar: hwnd is zero");
         return;
  }

       var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
      var appWindow = AppWindow.GetFromWindowId(windowId);

             if (appWindow == null)
   {
      System.Diagnostics.Debug.WriteLine("[WinUI App] ConfigureTitleBar: appWindow is null");
  return;
           }

    var titleBar = appWindow.TitleBar;
 if (titleBar == null)
                {
System.Diagnostics.Debug.WriteLine("[WinUI App] ConfigureTitleBar: titleBar is null");
          return;
  }
  
          // Determine if we're in light or dark mode - with null safety
     AppTheme currentTheme;
         try
        {
    currentTheme = Microsoft.Maui.Controls.Application.Current?.RequestedTheme ?? AppTheme.Unspecified;
       }
       catch
              {
                    currentTheme = AppTheme.Unspecified;
              }

   var isLightMode = currentTheme == AppTheme.Light || 
            (currentTheme == AppTheme.Unspecified && this.RequestedTheme == ApplicationTheme.Light);
      
     if (isLightMode)
             {
// Light mode colors - High contrast
   // Use slight transparency for modern look while keeping contrast
   titleBar.BackgroundColor = WinUIColor.FromArgb(240, 255, 255, 255); // 94% white (subtle transparency)
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
        // Use slightly translucent dark background to add depth
        titleBar.BackgroundColor = WinUIColor.FromArgb(230, 15, 23, 42); // ~90% #0F172A
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

  System.Diagnostics.Debug.WriteLine($"[WinUI App] Title bar configured for {(isLightMode ? "light" : "dark")} mode");
        }
        catch (Exception ex)
   {
      System.Diagnostics.Debug.WriteLine($"[WinUI App] Error configuring title bar: {ex.Message}");
}
        }
    }
}
