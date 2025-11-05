using Android.Content;
using Android.Preferences;

namespace ObsidianScout.Platforms.Android
{
    public static class PersistentPreferences
    {
        private const string PREFS_NAME = "obsidian_scout_persistent_prefs";
        private const string KEY_APP_LAUNCHED = "app_launched_once";
 
      public static void SetAppLaunched(Context context, bool launched)
        {
       try
      {
     System.Diagnostics.Debug.WriteLine($"[PersistentPreferences] Setting app launched flag to: {launched}");
  
   // Method 1: SharedPreferences (primary)
    var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
        var editor = prefs.Edit();
       editor.PutBoolean(KEY_APP_LAUNCHED, launched);
   editor.Apply();
          editor.Commit(); // Force synchronous write
       
                // Method 2: PreferenceManager (backup)
         var defaultPrefs = PreferenceManager.GetDefaultSharedPreferences(context);
           var defaultEditor = defaultPrefs.Edit();
          defaultEditor.PutBoolean(KEY_APP_LAUNCHED, launched);
        defaultEditor.Apply();
  defaultEditor.Commit();
     
    // Method 3: File-based (tertiary backup)
    var flagFile = System.IO.Path.Combine(context.FilesDir.AbsolutePath, ".app_launched");
     System.IO.File.WriteAllText(flagFile, launched.ToString());
         
    System.Diagnostics.Debug.WriteLine($"[PersistentPreferences] ? App launched flag saved to all storage locations");
 }
            catch (System.Exception ex)
            {
         System.Diagnostics.Debug.WriteLine($"[PersistentPreferences] ? Error setting flag: {ex.Message}");
          }
        }
      
   public static bool GetAppLaunched(Context context)
        {
            try
  {
          // Check Method 1: SharedPreferences
var prefs = context.GetSharedPreferences(PREFS_NAME, FileCreationMode.Private);
   if (prefs.GetBoolean(KEY_APP_LAUNCHED, false))
         {
    System.Diagnostics.Debug.WriteLine("[PersistentPreferences] ? Found flag in SharedPreferences");
        return true;
        }

        // Check Method 2: PreferenceManager
     var defaultPrefs = PreferenceManager.GetDefaultSharedPreferences(context);
                if (defaultPrefs.GetBoolean(KEY_APP_LAUNCHED, false))
       {
       System.Diagnostics.Debug.WriteLine("[PersistentPreferences] ? Found flag in PreferenceManager");
    return true;
     }
   
         // Check Method 3: File-based
     var flagFile = System.IO.Path.Combine(context.FilesDir.AbsolutePath, ".app_launched");
       if (System.IO.File.Exists(flagFile))
  {
   var content = System.IO.File.ReadAllText(flagFile);
     if (bool.TryParse(content, out var result) && result)
         {
         System.Diagnostics.Debug.WriteLine("[PersistentPreferences] ? Found flag in file");
                return true;
 }
             }
       
    System.Diagnostics.Debug.WriteLine("[PersistentPreferences] ?? No flag found in any storage");
       return false;
            }
            catch (System.Exception ex)
   {
     System.Diagnostics.Debug.WriteLine($"[PersistentPreferences] ? Error getting flag: {ex.Message}");
  return false;
  }
        }
    }
}
