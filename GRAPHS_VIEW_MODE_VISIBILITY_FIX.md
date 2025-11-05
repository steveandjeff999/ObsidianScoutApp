# Fix: View Mode and Graph Type Selectors Always Visible

## Problem
The view mode selector (Match-by-Match / Team Averages) and graph type selector (Line Chart / Radar Chart) are hidden when displaying server-generated images because they have `IsVisible="{Binding ShowMicrocharts}"` which is set to `false` when showing server images.

## Solution
Remove the `IsVisible` binding from these controls so users can always switch views and graph types, triggering new server image requests.

## Files to Modify

### 1. `ObsidianScout/Views/GraphsPage.xaml`

**Location:** After the server image Border (around line 212-260)

**Find this section:**
```xaml
<!-- Data View Selector -->
<Border Style="{StaticResource CompactGlassCard}"
        Margin="0,0,0,10"
        Padding="10"
        IsVisible="{Binding ShowMicrocharts}"  <!-- REMOVE THIS LINE -->
 BackgroundColor="{AppThemeBinding Light={StaticResource LightSurfaceVariant}, Dark={StaticResource DarkSurfaceVariant}}">
```

**Change to:**
```xaml
<!-- Data View Selector - ALWAYS VISIBLE -->
<Border Style="{StaticResource CompactGlassCard}"
        Margin="0,0,0,10"
        Padding="10"
        BackgroundColor="{AppThemeBinding Light={StaticResource LightSurfaceVariant}, Dark={StaticResource DarkSurfaceVariant}}">
```

**Find this section:**
```xaml
<!-- Graph Type Selector - REMOVED BAR CHART OPTION -->
<Grid ColumnDefinitions="*,*" 
      ColumnSpacing="10"
 IsVisible="{Binding ShowMicrocharts}">  <!-- REMOVE THIS LINE -->
```

**Change to:**
```xaml
<!-- Graph Type Selector - ALWAYS VISIBLE -->
<Grid ColumnDefinitions="*,*" ColumnSpacing="10">
```

**Find this section:**
```xaml
<!-- Team Comparison Summary -->
<CollectionView ItemsSource="{Binding ComparisonData.Teams}"
                IsVisible="{Binding ShowMicrocharts}">  <!-- REMOVE THIS LINE -->
```

**Change to:**
```xaml
<!-- Team Comparison Summary - ALWAYS VISIBLE -->
<CollectionView ItemsSource="{Binding ComparisonData.Teams}">
```

### 2. Keep Microcharts Hidden When Server Image Shows

The actual chart displays (TeamChartsWithInfo CollectionView and single Chart Border) should **KEEP** their `IsVisible="{Binding ShowMicrocharts}"` bindings so they're hidden when showing server images.

**Keep this as-is:**
```xaml
<!-- Team Charts Display - ONLY FOR LOCAL CHARTS -->
<CollectionView ItemsSource="{Binding TeamChartsWithInfo}"
         IsVisible="{Binding ShowMicrocharts}">  <!-- KEEP THIS -->
```

**Keep this as-is:**
```xaml
<!-- Single Chart Display - ONLY FOR LOCAL CHARTS -->
<Border Style="{StaticResource ElevatedGlassCard}"
        IsVisible="{Binding ShowMicrocharts}">  <!-- KEEP THIS -->
```

## Complete XAML Section (Lines 202-380 approximately)

Replace the entire "Graph Display" Border section with this:

```xaml
<!-- Graph Display -->
<Border Style="{StaticResource ElevatedGlassCard}"
        IsVisible="{Binding HasGraphData}"
   BackgroundColor="{AppThemeBinding Light={StaticResource GlassOverlayLight}, Dark={StaticResource GlassOverlayDark}}">
    <VerticalStackLayout Spacing="15">
        <Label Text="Comparison Results"
          FontSize="20"
     FontAttributes="Bold"
           TextColor="{AppThemeBinding Light={StaticResource LightTextPrimary}, Dark={StaticResource DarkTextPrimary}}" />

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

        <!-- Data View Selector - ALWAYS VISIBLE -->
        <Border Style="{StaticResource CompactGlassCard}"
                Margin="0,0,0,10"
       Padding="10"
     BackgroundColor="{AppThemeBinding Light={StaticResource LightSurfaceVariant}, Dark={StaticResource DarkSurfaceVariant}}">
            <VerticalStackLayout Spacing="10">
    <Label Text="View Mode"
   FontSize="14"
      FontAttributes="Bold"
     TextColor="{AppThemeBinding Light={StaticResource LightTextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
             
    <Grid ColumnDefinitions="*,10,*" ColumnSpacing="10">
       <Button Grid.Column="0"
             Text="Match-by-Match"
         Command="{Binding ChangeDataViewCommand}"
        CommandParameter="match_by_match"
         Style="{StaticResource OutlineGlassButton}"
          FontSize="13"
   Padding="8"
     BackgroundColor="{Binding SelectedDataView, Converter={StaticResource DataViewToColorConverter}, ConverterParameter='match_by_match'}" />
        
        <Button Grid.Column="2"
      Text="Team Averages"
      Command="{Binding ChangeDataViewCommand}"
       CommandParameter="averages"
          Style="{StaticResource OutlineGlassButton}"
         FontSize="13"
        Padding="8"
      BackgroundColor="{Binding SelectedDataView, Converter={StaticResource DataViewToColorConverter}, ConverterParameter='averages'}" />
            </Grid>
                
                <Label Text="{Binding SelectedDataView, StringFormat='Current: {0}'}"
        FontSize="11"
      TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}"
       HorizontalTextAlignment="Center" />
      </VerticalStackLayout>
        </Border>

        <!-- Graph Type Selector - ALWAYS VISIBLE -->
        <Grid ColumnDefinitions="*,*" ColumnSpacing="10">
      <Button Grid.Column="0"
            Text="Line Chart"
       Command="{Binding ChangeGraphTypeCommand}"
           CommandParameter="line"
            Style="{StaticResource OutlineGlassButton}" />
      
      <Button Grid.Column="1"
      Text="Radar Chart"
        Command="{Binding ChangeGraphTypeCommand}"
              CommandParameter="radar"
        Style="{StaticResource OutlineGlassButton}" />
        </Grid>

        <!-- Team Comparison Summary - ALWAYS VISIBLE -->
        <CollectionView ItemsSource="{Binding ComparisonData.Teams}">
            <CollectionView.ItemTemplate>
    <DataTemplate x:DataType="models:TeamComparisonData">
     <Border Style="{StaticResource CardBorderStyle}"
     Margin="0,5"
               Padding="15">
             <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="15">
  <BoxView Grid.Column="0"
             WidthRequest="4"
       HeightRequest="40"
           Color="{Binding Color}"
          CornerRadius="2"
       VerticalOptions="Center" />
         
     <VerticalStackLayout Grid.Column="1" VerticalOptions="Center">
          <Label Text="{Binding TeamName, StringFormat='#{0}'}"
 FontSize="14"
            FontAttributes="Bold"
  TextColor="{AppThemeBinding Light={StaticResource LightTextPrimary}, Dark={StaticResource DarkTextPrimary}}" />
       <Label Text="{Binding MatchCount, StringFormat='{0} matches'}"
      FontSize="12"
            TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}" />
       </VerticalStackLayout>
   
     <VerticalStackLayout Grid.Column="2" HorizontalOptions="End" VerticalOptions="Center">
           <Label Text="{Binding Value, StringFormat='{0:F1}'}"
       FontSize="20"
    FontAttributes="Bold"
    TextColor="{StaticResource Primary}"
             HorizontalTextAlignment="End" />
   <Label Text="{Binding StdDev, StringFormat='±{0:F1}'}"
       FontSize="12"
     TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}"
         HorizontalTextAlignment="End" />
          </VerticalStackLayout>
     </Grid>
               </Border>
    </DataTemplate>
         </CollectionView.ItemTemplate>
        </CollectionView>

     <!-- Team Charts Display - Shows ALL team charts for match-by-match - ONLY FOR LOCAL CHARTS -->
    <CollectionView ItemsSource="{Binding TeamChartsWithInfo}"
   IsVisible="{Binding ShowMicrocharts}">
            <CollectionView.ItemTemplate>
    <DataTemplate x:DataType="vm:TeamChartInfo">
    <VerticalStackLayout Spacing="10" Margin="0,10">
        <!-- Team Header -->
      <Grid ColumnDefinitions="Auto,*" ColumnSpacing="10">
    <BoxView Grid.Column="0"
        WidthRequest="4"
            HeightRequest="30"
        Color="{Binding Color}"
   CornerRadius="2"
             VerticalOptions="Center" />
    
            <Label Grid.Column="1"
   FontSize="18"
     FontAttributes="Bold"
      VerticalOptions="Center"
               TextColor="{AppThemeBinding Light={StaticResource LightTextPrimary}, Dark={StaticResource DarkTextPrimary}}">
     <Label.FormattedText>
            <FormattedString>
      <Span Text="Team #" />
       <Span Text="{Binding TeamNumber}" />
   <Span Text=" - " />
   <Span Text="{Binding TeamName}" />
        </FormattedString>
       </Label.FormattedText>
                </Label>
  </Grid>

          <!-- Team Chart -->
     <Border Style="{StaticResource ElevatedGlassCard}"
              BackgroundColor="{AppThemeBinding Light={StaticResource GlassOverlayLight}, Dark={StaticResource GlassOverlayDark}}"
     Padding="10"
             HeightRequest="350">
     <Border.StrokeShape>
  <RoundRectangle CornerRadius="10" />
           </Border.StrokeShape>
           
   <microcharts:ChartView Chart="{Binding Chart}"
              HeightRequest="330" />
            </Border>
          </VerticalStackLayout>
       </DataTemplate>
            </CollectionView.ItemTemplate>
        </CollectionView>

<!-- Single Chart Display (for non-match-by-match views) - ONLY FOR LOCAL CHARTS -->
        <Border Style="{StaticResource ElevatedGlassCard}"
      BackgroundColor="{AppThemeBinding Light={StaticResource GlassOverlayLight}, Dark={StaticResource GlassOverlayDark}}"
     Padding="10"
            HeightRequest="350"
                IsVisible="{Binding ShowMicrocharts}">
            <Border.StrokeShape>
    <RoundRectangle CornerRadius="10" />
            </Border.StrokeShape>
            
      <microcharts:ChartView x:Name="chartView"
       Chart="{Binding CurrentChart}"
         HeightRequest="330" />
    </Border>

        <Label Text="{Binding SelectedMetric.Name, StringFormat='Metric: {0}'}"
       FontSize="12"
     TextColor="{AppThemeBinding Light={StaticResource LightTextSecondary}, Dark={StaticResource DarkTextSecondary}}"
     HorizontalTextAlignment="Center"
        FontAttributes="Italic" />
    </VerticalStackLayout>
</Border>
```

## How It Works After the Fix

### When Viewing Server Images:
1. **Server image displays** at the top
2. **View Mode buttons** (Match-by-Match / Team Averages) are **VISIBLE**
3. **Graph Type buttons** (Line / Radar) are **VISIBLE**
4. **Team summary** is **VISIBLE**
5. **Local Microcharts** are **HIDDEN**

### When Viewing Local Microcharts:
1. **Server image** is **HIDDEN**
2. **View Mode buttons** are **VISIBLE**
3. **Graph Type buttons** are **VISIBLE**
4. **Team summary** is **VISIBLE**
5. **Local Microcharts** are **VISIBLE**

### User Experience:
- Click "Match-by-Match" ? Server requests new image with `mode=match_by_match`
- Click "Team Averages" ? Server requests new image with `mode=averages`
- Click "Line Chart" ? Server requests new image with `graph_type=line`
- Click "Radar Chart" ? Server requests new image with `graph_type=radar`
- All buttons work whether viewing server or local charts

## Testing Checklist

After making these changes:

- [ ] Build succeeds without errors
- [ ] Generate graphs (server image should appear)
- [ ] View Mode buttons are visible below the server image
- [ ] Click "Match-by-Match" - new server image loads
- [ ] Click "Team Averages" - new server image loads
- [ ] Graph Type buttons are visible
- [ ] Click "Line Chart" - new server image loads
- [ ] Click "Radar Chart" - new server image loads
- [ ] Team summary shows below the buttons
- [ ] When offline, Microcharts appear and buttons still work
- [ ] Status message shows correct mode

## ViewModel Changes (Already Complete)

The ViewModel already handles this correctly:
- `ChangeDataView` clears server image and regenerates
- `ChangeGraphType` clears server image and regenerates
- `GenerateGraphsAsync` sends correct `Mode` and `GraphType` parameters

No ViewModel changes needed!

## Quick Fix Steps

1. Open `ObsidianScout/Views/GraphsPage.xaml`
2. Find line ~213: `<Border Style="{StaticResource CompactGlassCard}"` (Data View Selector)
3. **Remove** the line: `IsVisible="{Binding ShowMicrocharts}"`
4. Find line ~247: `<Grid ColumnDefinitions="*,*"` (Graph Type Selector)
5. **Remove** the line: `IsVisible="{Binding ShowMicrocharts}"`
6. Find line ~260: `<CollectionView ItemsSource="{Binding ComparisonData.Teams}"`
7. **Remove** the line: `IsVisible="{Binding ShowMicrocharts}"`
8. **Keep** `IsVisible="{Binding ShowMicrocharts}"` on:
   - Line ~310: `<CollectionView ItemsSource="{Binding TeamChartsWithInfo}"`
   - Line ~360: `<Border Style="{StaticResource ElevatedGlassCard}"` (single chart)
9. Save and rebuild

## Result

Users can now:
- See and use View Mode selector with server images ?
- See and use Graph Type selector with server images ?
- Switch between views dynamically ?
- Server generates appropriate images based on selection ?
- Local fallback still works ?
