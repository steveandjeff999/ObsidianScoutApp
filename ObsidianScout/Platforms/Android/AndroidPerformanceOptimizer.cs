using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Platform;

namespace ObsidianScout.Platforms.Android
{
    /// <summary>
    /// Android-specific UI optimizations for smooth scrolling
    /// </summary>
    public static class AndroidPerformanceOptimizer
    {
        /// <summary>
        /// Optimize CollectionView for smooth scrolling
        /// </summary>
        public static void OptimizeCollectionView(global::Android.Views.View nativeView)
        {
            if (nativeView is AndroidX.RecyclerView.Widget.RecyclerView recyclerView)
            {
                // Enable item animator optimizations
                recyclerView.SetItemViewCacheSize(20); // Cache 20 items
                recyclerView.DrawingCacheEnabled = true;
                recyclerView.DrawingCacheQuality = global::Android.Views.DrawingCacheQuality.High;

                // Enable nested scrolling for smooth scroll
                recyclerView.NestedScrollingEnabled = true;

                // Optimize layout manager
                if (recyclerView.GetLayoutManager() is AndroidX.RecyclerView.Widget.LinearLayoutManager layoutManager)
                {
                    layoutManager.InitialPrefetchItemCount = 4;
                    layoutManager.SmoothScrollbarEnabled = true;
                }

                // Hardware acceleration
                recyclerView.SetLayerType(global::Android.Views.LayerType.Hardware, null);
            }
        }

        /// <summary>
        /// Optimize ListView for smooth scrolling
        /// </summary>
        public static void OptimizeListView(global::Android.Views.View nativeView)
        {
            if (nativeView is global::Android.Widget.ListView listView)
            {
                // Enable smooth scrolling
                listView.SmoothScrollbarEnabled = true;

                // Cache colored background
                listView.CacheColorHint = global::Android.Graphics.Color.Transparent;

                // Enable fast scroll
                listView.FastScrollEnabled = true;
                listView.FastScrollAlwaysVisible = false;

                // Optimize drawing
                listView.DrawingCacheEnabled = true;
                listView.DrawingCacheQuality = global::Android.Views.DrawingCacheQuality.High;

                // Hardware acceleration
                listView.SetLayerType(global::Android.Views.LayerType.Hardware, null);

                // Fading edge for better UX
                listView.VerticalFadingEdgeEnabled = true;
                listView.SetFadingEdgeLength(20);
            }
        }

        /// <summary>
        /// Optimize ScrollView for smooth scrolling
        /// </summary>
        public static void OptimizeScrollView(global::Android.Views.View nativeView)
        {
            if (nativeView is global::Android.Widget.ScrollView scrollView)
            {
                // Enable smooth scrolling
                scrollView.SmoothScrollingEnabled = true;

                // Enable nested scrolling
                scrollView.NestedScrollingEnabled = true;

                // Hardware acceleration
                scrollView.SetLayerType(global::Android.Views.LayerType.Hardware, null);

                // Fading edge
                scrollView.VerticalFadingEdgeEnabled = true;
                scrollView.SetFadingEdgeLength(20);

                // Optimize drawing
                scrollView.DrawingCacheEnabled = true;
                scrollView.DrawingCacheQuality = global::Android.Views.DrawingCacheQuality.High;
            }
            else if (nativeView is AndroidX.Core.Widget.NestedScrollView nestedScrollView)
            {
                // Enable smooth scrolling
                nestedScrollView.SmoothScrollingEnabled = true;

                // Hardware acceleration
                nestedScrollView.SetLayerType(global::Android.Views.LayerType.Hardware, null);

                // Fading edge
                nestedScrollView.VerticalFadingEdgeEnabled = true;
                nestedScrollView.SetFadingEdgeLength(20);
            }
        }

        /// <summary>
        /// Apply global Android optimizations
        /// </summary>
        public static void ApplyGlobalOptimizations(Activity activity)
        {
            try
            {
                // Enable hardware acceleration at activity level
                if (activity.Window != null)
                {
                    activity.Window.SetFlags(
                        global::Android.Views.WindowManagerFlags.HardwareAccelerated,
                        global::Android.Views.WindowManagerFlags.HardwareAccelerated);
                }

                System.Diagnostics.Debug.WriteLine("[AndroidPerformanceOptimizer] Global optimizations applied");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AndroidPerformanceOptimizer] Error applying optimizations: {ex.Message}");
            }
        }
    }
}
