# Light Mode Text Color Fix

## Issue Summary
In light mode, text colors appear as white-on-white in some places, making them unreadable. This is caused by using incorrect color resource names in XAML bindings.

## Root Cause
The `SettingsPage.xaml` file uses `{StaticResource DarkText}` and `{StaticResource LightText}` which were not initially defined in `Colors.xaml`.

## Fix Applied

### 1. Updated Colors.xaml
Added simplified text color aliases for easier XAML binding:

```xml
<!-- Light Mode -->
<Color x:Key="LightText">#0F172A</Color>  <!-- Dark text for light backgrounds -->

<!-- Dark Mode -->
<Color x:Key="DarkText">#F8FAFC</Color>   <!-- Light text for dark backgrounds -->
```

### 2. SettingsPage.xaml Needs Update
The SettingsPage.xaml file needs to have its text color bindings corrected. All instances of:

**INCORRECT (causes white-on-white in light mode):**
```xml
TextColor="{AppThemeBinding Light={StaticResource DarkText}, Dark={StaticResource LightText}}"
```

**Should be changed to CORRECT:**
```xml
TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}"
```

## Correct Pattern for AppThemeBinding

The pattern should be:
- **Light mode** ? Use **Light** prefix colors (Light color text on Light background)
- **Dark mode** ? Use **Dark** prefix colors (Dark color text on Dark background)

```xml
<!-- Correct Pattern -->
<Label TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}" />
<Border BackgroundColor="{AppThemeBinding Light={StaticResource LightSurface}, Dark={StaticResource DarkSurface}}" />
```

## Manual Fix Needed

### Option 1: Edit SettingsPage.xaml directly
1. Open `ObsidianScout/Views/SettingsPage.xaml`
2. Find all instances of:
   ```xml
   TextColor="{AppThemeBinding Light={StaticResource DarkText}, Dark={StaticResource LightText}}"
   ```
3. Replace with:
   ```xml
   TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}"
   ```

### Option 2: Delete and Recreate
If the file is corrupted:
1. Delete `ObsidianScout/Views/SettingsPage.xaml`
2. Create a new file with the content provided below

## Corrected SettingsPage.xaml

Save this as `ObsidianScout/Views/SettingsPage.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
     xmlns:viewModels="clr-namespace:ObsidianScout.ViewModels"
      x:Class="ObsidianScout.Views.SettingsPage"
 x:DataType="viewModels:SettingsViewModel"
             Title="Settings"
             BackgroundColor="{AppThemeBinding Light={StaticResource LightBackground}, Dark={StaticResource DarkBackground}}">

  <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="30">

         <!-- Header -->
         <Label Text="?? Settings"
  FontSize="32"
              FontAttributes="Bold"
      TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}"
              Margin="0,10,0,20" />

     <!-- Status Message -->
        <Border IsVisible="{Binding StatusMessage, Converter={StaticResource StringNotEmptyConverter}}"
        BackgroundColor="{AppThemeBinding Light={StaticResource LightSurfaceVariant}, Dark={StaticResource DarkSurface}}"
   StrokeThickness="0"
        Padding="15"
       Margin="0,0,0,10">
                <Border.StrokeShape>
   <RoundRectangle CornerRadius="10" />
     </Border.StrokeShape>
                <Label Text="{Binding StatusMessage}"
   FontSize="14"
        TextColor="{StaticResource Primary}"
       HorizontalTextAlignment="Center" />
 </Border>

     <!-- Appearance Section -->
       <Border BackgroundColor="{AppThemeBinding Light={StaticResource LightSurface}, Dark={StaticResource DarkSurface}}"
     StrokeThickness="1"
          Stroke="{AppThemeBinding Light={StaticResource LightBorder}, Dark={StaticResource DarkBorder}}"
    Padding="20">
          <Border.StrokeShape>
              <RoundRectangle CornerRadius="15" />
</Border.StrokeShape>
 <Border.Shadow>
      <Shadow Brush="{AppThemeBinding Light=#40000000, Dark=#00000000}"
  Opacity="0.1"
        Radius="10"
               Offset="0,2" />
    </Border.Shadow>

      <VerticalStackLayout Spacing="15">
          <Label Text="?? Appearance"
        FontSize="20"
            FontAttributes="Bold"
   TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}" />

           <BoxView HeightRequest="1"
              Color="{AppThemeBinding Light={StaticResource LightDivider}, Dark={StaticResource DarkDivider}}"
    Margin="0,5,0,10" />

   <Grid ColumnDefinitions="*,Auto" ColumnSpacing="15">
          <VerticalStackLayout Grid.Column="0" Spacing="5">
     <Label Text="Dark Mode"
         FontSize="16"
          FontAttributes="Bold"
           TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}" />
           <Label Text="Switch between light and dark theme"
        FontSize="12"
   TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
       </VerticalStackLayout>

            <Switch Grid.Column="1"
   IsToggled="{Binding IsDarkMode}"
                  OnColor="{StaticResource Primary}"
            ThumbColor="White"
          VerticalOptions="Center" />
     </Grid>

        <Label Text="{Binding IsDarkMode, StringFormat='Current theme: {0}'}"
           FontSize="12"
   TextColor="{AppThemeBinding Light={StaticResource LightTextTertiary}, Dark={StaticResource DarkTextTertiary}}"
    HorizontalTextAlignment="Center"
 Margin="0,10,0,0">
           <Label.Triggers>
           <DataTrigger TargetType="Label"
           Binding="{Binding IsDarkMode}"
                Value="True">
               <Setter Property="Text" Value="Current theme: Dark ??" />
         </DataTrigger>
              <DataTrigger TargetType="Label"
        Binding="{Binding IsDarkMode}"
          Value="False">
 <Setter Property="Text" Value="Current theme: Light ??" />
  </DataTrigger>
  </Label.Triggers>
       </Label>
</VerticalStackLayout>
  </Border>

            <!-- Cache Management Section -->
            <Border BackgroundColor="{AppThemeBinding Light={StaticResource LightSurface}, Dark={StaticResource DarkSurface}}"
           StrokeThickness="1"
Stroke="{AppThemeBinding Light={StaticResource LightBorder}, Dark={StaticResource DarkBorder}}"
           Padding="20">
<Border.StrokeShape>
          <RoundRectangle CornerRadius="15" />
                </Border.StrokeShape>
           <Border.Shadow>
        <Shadow Brush="{AppThemeBinding Light=#40000000, Dark=#00000000}"
       Opacity="0.1"
     Radius="10"
  Offset="0,2" />
</Border.Shadow>

           <VerticalStackLayout Spacing="15">
        <Label Text="?? Cache Management"
   FontSize="20"
          FontAttributes="Bold"
       TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}" />

    <BoxView HeightRequest="1"
                   Color="{AppThemeBinding Light={StaticResource LightDivider}, Dark={StaticResource DarkDivider}}"
      Margin="0,5,0,10" />

        <!-- Cache Status -->
            <Border BackgroundColor="{AppThemeBinding Light={StaticResource LightSurfaceVariant}, Dark={StaticResource DarkSurfaceVariant}}"
        StrokeThickness="0"
 Padding="15"
          Margin="0,0,0,10">
  <Border.StrokeShape>
             <RoundRectangle CornerRadius="10" />
        </Border.StrokeShape>
     <Grid ColumnDefinitions="*,Auto" ColumnSpacing="10">
<Label Grid.Column="0"
      Text="{Binding CacheStatus}"
FontSize="14"
 TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}"
      VerticalOptions="Center" />
   <Button Grid.Column="1"
    Text="?"
         FontSize="16"
               WidthRequest="35"
      HeightRequest="35"
        CornerRadius="17"
       Padding="0"
         BackgroundColor="{StaticResource Primary}"
        TextColor="White"
        Command="{Binding RefreshCacheStatusCommand}"
   VerticalOptions="Center" />
    </Grid>
   </Border>

             <Label Text="Clear all cached data including teams, events, matches, and scouting information. The app will reload data when needed."
             FontSize="12"
   TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}"
      Margin="0,0,0,10" />

           <Button Text="Clear Cache"
     Command="{Binding ClearCacheCommand}"
          IsEnabled="{Binding IsClearing, Converter={StaticResource InvertedBoolConverter}}"
               BackgroundColor="#FF6B6B"
      TextColor="White"
  FontSize="16"
             FontAttributes="Bold"
     HeightRequest="50"
       CornerRadius="10">
            <Button.Shadow>
          <Shadow Brush="#40000000"
               Opacity="0.2"
            Radius="8"
   Offset="0,2" />
           </Button.Shadow>
        </Button>

       <ActivityIndicator IsRunning="{Binding IsClearing}"
            IsVisible="{Binding IsClearing}"
      Color="{StaticResource Primary}"
          Margin="0,10,0,0" />
    </VerticalStackLayout>
 </Border>

        <!-- App Info Section -->
     <Border BackgroundColor="{AppThemeBinding Light={StaticResource LightSurface}, Dark={StaticResource DarkSurface}}"
    StrokeThickness="1"
          Stroke="{AppThemeBinding Light={StaticResource LightBorder}, Dark={StaticResource DarkBorder}}"
       Padding="20">
 <Border.StrokeShape>
     <RoundRectangle CornerRadius="15" />
 </Border.StrokeShape>
     <Border.Shadow>
    <Shadow Brush="{AppThemeBinding Light=#40000000, Dark=#00000000}"
              Opacity="0.1"
       Radius="10"
    Offset="0,2" />
   </Border.Shadow>

     <VerticalStackLayout Spacing="10">
            <Label Text="?? About"
      FontSize="20"
    FontAttributes="Bold"
              TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}" />

           <BoxView HeightRequest="1"
        Color="{AppThemeBinding Light={StaticResource LightDivider}, Dark={StaticResource DarkDivider}}"
       Margin="0,5,0,10" />

           <Label Text="ObsidianScout"
          FontSize="18"
      FontAttributes="Bold"
    TextColor="{StaticResource Primary}" />

    <Label Text="FRC Scouting System"
      FontSize="14"
       TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}" />

           <Label Text="Version 1.0.0"
    FontSize="12"
           TextColor="{AppThemeBinding Light={StaticResource LightTextTertiary}, Dark={StaticResource DarkTextTertiary}}"
     Margin="0,10,0,0" />
     </VerticalStackLayout>
       </Border>

        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

## Color Usage Guide

### For Backgrounds:
```xml
<Border BackgroundColor="{AppThemeBinding Light={StaticResource LightSurface}, Dark={StaticResource DarkSurface}}" />
```

### For Primary Text:
```xml
<Label TextColor="{AppThemeBinding Light={StaticResource LightText}, Dark={StaticResource DarkText}}" />
```

### For Secondary Text:
```xml
<Label TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
```

### For Tertiary Text (Captions, Hints):
```xml
<Label TextColor="{AppThemeBinding Light={StaticResource LightTextTertiary}, Dark={StaticResource DarkTextTertiary}}" />
```

### For Borders:
```xml
<Border Stroke="{AppThemeBinding Light={StaticResource LightBorder}, Dark={StaticResource DarkBorder}}" />
```

## Testing

After applying the fix:
1. Set app to Light mode
2. Verify all text is dark/readable on light backgrounds
3. Switch to Dark mode
4. Verify all text is light/readable on dark backgrounds
5. Check Settings page specifically

## Color Reference Chart

| Element | Light Mode | Dark Mode |
|---------|------------|-----------|
| Background | `#F8FAFC` (Light Gray) | `#0F172A` (Dark Blue) |
| Surface | `#FFFFFF` (White) | `#1E293B` (Dark Gray) |
| Primary Text | `#0F172A` (Dark) | `#F8FAFC` (Light) |
| Secondary Text | `#475569` (Gray) | `#CBD5E1` (Light Gray) |
| Tertiary Text | `#94A3B8` (Light Gray) | `#64748B` (Gray) |

## Benefits of Fix

? **Readable Light Mode** - All text is now dark on light backgrounds
? **Readable Dark Mode** - All text remains light on dark backgrounds  
? **Consistent** - Uses the same color scheme as rest of app
? **Maintainable** - Simple, clear naming convention

## Quick Fix Steps

1. Open `ObsidianScout/Views/SettingsPage.xaml` in Visual Studio
2. Use Find & Replace (Ctrl+H):
   - **Find:** `Light={StaticResource DarkText}, Dark={StaticResource LightText}`
   - **Replace:** `Light={StaticResource LightText}, Dark={StaticResource DarkText}`
3. Click "Replace All"
4. Save file
5. Rebuild solution
6. Test in both Light and Dark modes

That's it! The text colors will now be correct in both themes.
