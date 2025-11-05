# Server Graph Image - XAML Addition

Add this section **immediately after** the "Comparison Results" Label (line 210) and **before** the "Data View Selector" Border (line 212):

```xaml
<!-- Server Graph Image (when online and available) -->
<Border Style="{StaticResource ElevatedGlassCard}"
        IsVisible="{Binding UseServerImage}"
        BackgroundColor="{AppThemeBinding Light={StaticResource GlassOverlayLight}, Dark={StaticResource GlassOverlayDark}}"
        Padding="10"
        Margin="0,0,0,10">
    <VerticalStackLayout Spacing="10">
        <Label Text="?? Server-Generated Graph"
           FontSize="14"
    FontAttributes="Italic"
     HorizontalTextAlignment="Center"
               TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
        
        <Image Source="{Binding ServerGraphImage}"
         Aspect="AspectFit"
               HeightRequest="400"
               HorizontalOptions="FillAndExpand" />
    </VerticalStackLayout>
</Border>
```

Then update the Data View Selector, Graph Type Selector, and Team Comparison Summary to be hidden when server image is shown by adding `IsVisible="{Binding ShowMicrocharts}"` to:
- The "Data View Selector" Border (line 212)
- The "Graph Type Selector" Grid (line 247) 
- The "Team Comparison Summary" CollectionView (line 260)

The server image will be displayed when:
1. Offline mode is disabled
2. Device is connected to the internet
3. Server successfully returns a PNG image

Otherwise it falls back to local Microcharts rendering.
