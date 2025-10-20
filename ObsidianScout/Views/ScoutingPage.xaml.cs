using ObsidianScout.Models;
using ObsidianScout.ViewModels;
using ObsidianScout.Converters;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using System.Text.Json;

namespace ObsidianScout.Views;

public partial class ScoutingPage : ContentPage
{
    private readonly ScoutingViewModel _viewModel;
    private ScrollView? _mainScrollView;
    private readonly Dictionary<string, Label> _counterLabels = new();

    public ScoutingPage(ScoutingViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = _viewModel;
        
        InitializeComponent();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // Try building immediately if config already exists
        if (_viewModel.GameConfig != null)
        {
            BuildDynamicForm();
        }
        
        // Also trigger after delay as fallback
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1000), () =>
        {
            if (_viewModel.GameConfig != null && _mainScrollView == null)
            {
                Dispatcher.Dispatch(() => BuildDynamicForm());
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Page is disappearing - timer continues in background for updates
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScoutingViewModel.GameConfig))
        {
            Dispatcher.Dispatch(() => BuildDynamicForm());
        }
        else if (e.PropertyName == nameof(ScoutingViewModel.IsLoading))
        {
            // Rebuild form when loading state changes (to show/hide loading indicator)
            Dispatcher.Dispatch(() => BuildDynamicForm());
        }
        else if (e.PropertyName == nameof(ScoutingViewModel.StatusMessage))
        {
            // Rebuild when status changes (to show error messages)
            Dispatcher.Dispatch(() =>
            {
                if (_viewModel.GameConfig == null)
                {
                    BuildDynamicForm();
                }
            });
        }
        else if (e.PropertyName == "FieldValuesChanged")
        {
            // Update all counter labels
            foreach (var kvp in _counterLabels)
            {
                var value = _viewModel.GetFieldValue(kvp.Key);
                kvp.Value.Text = value?.ToString() ?? "0";
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Call initial load to auto-load matches
        _ = _viewModel.InitialLoadAsync();
        
        if (_viewModel.GameConfig != null && _mainScrollView == null)
        {
            BuildDynamicForm();
        }
    }

    private Color GetBackgroundColor()
    {
        // Use system theme-aware colors
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2C2C2C") // Dark gray for dark mode
            : Colors.White;
    }

    private Color GetTextColor()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Colors.White
            : Colors.Black;
    }

    private Color GetSecondaryTextColor()
    {
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#B0B0B0")
            : Color.FromArgb("#404040");
    }

    // Helper method to safely convert object to boolean
    private bool ConvertToBoolean(object? value)
    {
        if (value == null) return false;

        try
        {
            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.True) return true;
                if (jsonElement.ValueKind == JsonValueKind.False) return false;
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    return bool.TryParse(str, out var result) && result;
                }
                return false;
            }

            // Handle direct boolean
            if (value is bool boolValue) return boolValue;

            // Try string conversion
            if (value is string strValue)
            {
                return bool.TryParse(strValue, out var result) && result;
            }

            // Try general conversion
            return Convert.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }

    // Helper method to safely convert object to int
    private int ConvertToInt(object? value, int defaultValue = 0)
    {
        if (value == null) return defaultValue;

        try
        {
            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    return jsonElement.GetInt32();
                }
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    var str = jsonElement.GetString();
                    return int.TryParse(str, out var result) ? result : defaultValue;
                }
                return defaultValue;
            }

            // Handle direct int
            if (value is int intValue) return intValue;

            // Try string conversion
            if (value is string strValue)
            {
                return int.TryParse(strValue, out var result) ? result : defaultValue;
            }

            // Try general conversion
            return Convert.ToInt32(value);
        }
        catch
        {
            return defaultValue;
        }
    }

    // Helper method to safely convert object to string
    private string ConvertToString(object? value, string defaultValue = "")
    {
        if (value == null) return defaultValue;

        try
        {
            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return jsonElement.GetString() ?? defaultValue;
                }
                return jsonElement.ToString() ?? defaultValue;
            }

            return value.ToString() ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private void BuildDynamicForm()
    {
        _counterLabels.Clear();

        var mainLayout = new VerticalStackLayout
        {
            Padding = new Thickness(20),
            Spacing = 20
        };

        // Show loading or error state if no config
        if (_viewModel.GameConfig == null)
        {
            var statusStack = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 20,
                Padding = new Thickness(20)
            };

            if (_viewModel.IsLoading)
            {
                var loadingIndicator = new ActivityIndicator
                {
                    IsRunning = true,
                    Color = (Color)Application.Current!.Resources["Primary"],
                    HeightRequest = 50,
                    WidthRequest = 50
                };
                statusStack.Add(loadingIndicator);

                var loadingLabel = new Label
                {
                    Text = "Loading game configuration...",
                    FontSize = 16,
                    TextColor = GetTextColor(),
                    HorizontalTextAlignment = TextAlignment.Center
                };
                statusStack.Add(loadingLabel);
            }
            else
            {
                var errorLabel = new Label
                {
                    Text = !string.IsNullOrEmpty(_viewModel.StatusMessage) 
                        ? _viewModel.StatusMessage 
                        : "Game configuration not loaded. Please check your connection and try again.",
                    FontSize = 16,
                    TextColor = (Color)Application.Current.Resources["Tertiary"],
                    HorizontalTextAlignment = TextAlignment.Center
                };
                statusStack.Add(errorLabel);

                var retryButton = new Button
                {
                    Text = "Retry Loading Configuration",
                    BackgroundColor = (Color)Application.Current.Resources["Primary"],
                    TextColor = Colors.White,
                    CornerRadius = 10
                };
                retryButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.RefreshCommand));
                statusStack.Add(retryButton);
            }

            mainLayout.Add(statusStack);
            _mainScrollView = new ScrollView { Content = mainLayout };
            Content = _mainScrollView;
            return;
        }

        // Scout Name Section (AT THE TOP)
        mainLayout.Add(CreateScoutNameSection());

        // Match Info Section
        mainLayout.Add(CreateMatchInfoSection());

        // Auto Period
        if (_viewModel.AutoElements?.Count > 0)
        {
            mainLayout.Add(CreateSectionHeader("Autonomous"));
            mainLayout.Add(CreatePeriodSection(_viewModel.AutoElements));
        }

        // Teleop Period
        if (_viewModel.TeleopElements?.Count > 0)
        {
            mainLayout.Add(CreateSectionHeader("Teleop"));
            mainLayout.Add(CreatePeriodSection(_viewModel.TeleopElements));
        }

        // Endgame Period
        if (_viewModel.EndgameElements?.Count > 0)
        {
            mainLayout.Add(CreateSectionHeader("Endgame"));
            mainLayout.Add(CreatePeriodSection(_viewModel.EndgameElements));
        }

        // Post Match
        if ((_viewModel.RatingElements?.Count > 0) || (_viewModel.TextElements?.Count > 0))
        {
            mainLayout.Add(CreateSectionHeader("Post Match"));
            mainLayout.Add(CreatePostMatchSection());
        }

        // Status and Submit (without scout name since it's at top now)
        mainLayout.Add(CreateSubmitSection());

        _mainScrollView = new ScrollView
        {
            Content = mainLayout
        };

        Content = _mainScrollView;
    }

    private Label CreateSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["Primary"]
        };
    }

    private View CreateScoutNameSection()
    {
        var border = new Border
        {
            BackgroundColor = GetBackgroundColor(),
            StrokeThickness = 1,
            Stroke = GetSecondaryTextColor(),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10)
        };
        border.StrokeShape = new RoundRectangle { CornerRadius = 10 };

        var mainLayout = new VerticalStackLayout { Spacing = 10 };

        // Title with icon
        var titleLabel = new Label
        {
            Text = "👤 Scout Name",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor(),
            Margin = new Thickness(0, 0, 0, 5)
        };
        mainLayout.Add(titleLabel);

        // Scout Name Entry
        var scoutNameEntry = new Entry
        {
            Placeholder = "Enter your name",
            TextColor = GetTextColor(),
            PlaceholderColor = GetSecondaryTextColor(),
            FontSize = 16
        };
        scoutNameEntry.SetBinding(Entry.TextProperty, nameof(ScoutingViewModel.ScoutName));
        mainLayout.Add(scoutNameEntry);

        // Helper text
        var helperLabel = new Label
        {
            Text = "Auto-filled from your login",
            FontSize = 11,
            TextColor = GetSecondaryTextColor(),
            FontAttributes = FontAttributes.Italic
        };
        mainLayout.Add(helperLabel);

        border.Content = mainLayout;
        return border;
    }

    private View CreateMatchInfoSection()
    {
        var border = new Border
        {
            BackgroundColor = GetBackgroundColor(),
            StrokeThickness = 1,
            Stroke = GetSecondaryTextColor(),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10)
        };
        border.StrokeShape = new RoundRectangle { CornerRadius = 10 };

        var mainLayout = new VerticalStackLayout { Spacing = 10 };

        // Title
        var titleLabel = new Label
        {
            Text = "Match Information",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor(),
            Margin = new Thickness(0, 0, 0, 5)
        };
        mainLayout.Add(titleLabel);

        // Status Message (visible in match info section too)
        var statusLabel = new Label
        {
            FontSize = 12,
            TextColor = (Color)Application.Current!.Resources["Tertiary"],
            IsVisible = false,
            Margin = new Thickness(0, 0, 0, 5)
        };
        statusLabel.SetBinding(Label.TextProperty, nameof(ScoutingViewModel.StatusMessage));
        statusLabel.SetBinding(IsVisibleProperty, new Binding(nameof(ScoutingViewModel.StatusMessage),
            converter: Application.Current.Resources["StringNotEmptyConverter"] as IValueConverter));
        mainLayout.Add(statusLabel);

        // Team Selection Row
        var teamLayout = new VerticalStackLayout { Spacing = 5 };
        
        var teamHeaderGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var teamLabel = new Label
        {
            Text = "Team",
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor(),
            VerticalOptions = LayoutOptions.Center
        };
        teamHeaderGrid.Add(teamLabel, 0, 0);

        var refreshTeamsBtn = new Button
        {
            Text = "↻",
            FontSize = 16,
            WidthRequest = 35,
            HeightRequest = 35,
            CornerRadius = 5,
            Padding = 0
        };
        refreshTeamsBtn.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.RefreshTeamsCommand));
        teamHeaderGrid.Add(refreshTeamsBtn, 1, 0);

        teamLayout.Add(teamHeaderGrid);

        var teamPicker = new Picker
        {
            Title = "Select Team",
            TextColor = GetTextColor(),
            TitleColor = GetSecondaryTextColor()
        };
        teamPicker.SetBinding(Picker.ItemsSourceProperty, nameof(ScoutingViewModel.Teams));
        teamPicker.SetBinding(Picker.SelectedItemProperty, nameof(ScoutingViewModel.SelectedTeam));
        teamPicker.ItemDisplayBinding = new Binding(".", 
            converter: new FuncConverter<Team, string>(team => 
                team != null ? $"{team.TeamNumber} - {team.TeamName}" : ""));
        teamLayout.Add(teamPicker);

        mainLayout.Add(teamLayout);

        // Match Selection Row
        var matchLayout = new VerticalStackLayout { Spacing = 5 };
        
        var matchHeaderGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var matchLabel = new Label
        {
            Text = "Match",
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor(),
            VerticalOptions = LayoutOptions.Center
        };
        matchHeaderGrid.Add(matchLabel, 0, 0);

        var loadMatchesBtn = new Button
        {
            Text = "Load",
            FontSize = 12,
            WidthRequest = 70,
            HeightRequest = 35,
            CornerRadius = 5,
            Padding = new Thickness(5, 0)
        };
        loadMatchesBtn.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.LoadMatchesCommand));
        loadMatchesBtn.SetBinding(Button.IsEnabledProperty, new Binding(nameof(ScoutingViewModel.IsLoading),
            converter: Application.Current.Resources["InvertedBoolConverter"] as IValueConverter));
        matchHeaderGrid.Add(loadMatchesBtn, 1, 0);

        matchLayout.Add(matchHeaderGrid);

        var matchPicker = new Picker
        {
            Title = "Select Match",
            TextColor = GetTextColor(),
            TitleColor = GetSecondaryTextColor()
        };
        matchPicker.SetBinding(Picker.ItemsSourceProperty, nameof(ScoutingViewModel.Matches));
        matchPicker.SetBinding(Picker.SelectedItemProperty, nameof(ScoutingViewModel.SelectedMatch));
        // Fixed: Display match type and number, not team info
        matchPicker.ItemDisplayBinding = new Binding(".", 
            converter: new FuncConverter<Match, string>(match =>
                match != null ? $"{match.MatchType} {match.MatchNumber}" : ""));
        matchLayout.Add(matchPicker);

        mainLayout.Add(matchLayout);

        // Match count display - simplified without converter
        var matchCountLabel = new Label
        {
            FontSize = 12,
            TextColor = GetSecondaryTextColor(),
            Margin = new Thickness(0, 5, 0, 0),
            Text = _viewModel.Matches.Count > 0 
                ? $"{_viewModel.Matches.Count} matches available" 
                : "No matches loaded"
        };
        
        // Subscribe to collection changes to update count
        _viewModel.Matches.CollectionChanged += (s, e) =>
        {
            Dispatcher.Dispatch(() =>
            {
                matchCountLabel.Text = _viewModel.Matches.Count > 0 
                    ? $"{_viewModel.Matches.Count} matches available" 
                    : "No matches loaded";
            });
        };
        
        mainLayout.Add(matchCountLabel);

        // Event Info (if available)
        if (_viewModel.GameConfig != null && !string.IsNullOrEmpty(_viewModel.GameConfig.CurrentEventCode))
        {
            var eventInfoLabel = new Label
            {
                Text = $"Current Event: {_viewModel.GameConfig.CurrentEventCode}",
                FontSize = 12,
                TextColor = GetSecondaryTextColor(),
                Margin = new Thickness(0, 0, 0, 0)
            };
            mainLayout.Add(eventInfoLabel);
        }

        border.Content = mainLayout;
        return border;
    }

    private View CreatePeriodSection(IEnumerable<ScoringElement> elements)
    {
        var border = new Border
        {
            BackgroundColor = GetBackgroundColor(),
            StrokeThickness = 1,
            Stroke = GetSecondaryTextColor(),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10)
        };
        border.StrokeShape = new RoundRectangle { CornerRadius = 10 };

        var layout = new VerticalStackLayout { Spacing = 15 };

        foreach (var element in elements)
        {
            var elementView = CreateElementView(element);
            if (elementView != null)
            {
                layout.Add(elementView);
            }
        }

        border.Content = layout;
        return border;
    }

    private View? CreateElementView(ScoringElement element)
    {
        return element.Type.ToLower() switch
        {
            "counter" => CreateCounterView(element),
            "boolean" => CreateBooleanView(element),
            "multiple_choice" => CreateMultipleChoiceView(element),
            _ => new Label
            {
                Text = $"Unsupported type: {element.Type} ({element.Name})",
                TextColor = (Color)Application.Current!.Resources["Tertiary"]
            }
        };
    }

    private View CreateCounterView(ScoringElement element)
    {
        var mainLayout = new VerticalStackLayout { Spacing = 5 };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 5
        };

        var labelLayout = new VerticalStackLayout { Spacing = 2 };
        
        var label = new Label
        {
            Text = element.Name,
            VerticalOptions = LayoutOptions.Center,
            TextColor = GetTextColor()
        };
        labelLayout.Add(label);

        // Add points display
        if (element.Points > 0)
        {
            var pointsLabel = new Label
            {
                Text = $"({element.Points} pts each)",
                FontSize = 12,
                TextColor = GetSecondaryTextColor(),
                VerticalOptions = LayoutOptions.Center
            };
            labelLayout.Add(pointsLabel);
        }

        grid.Add(labelLayout, 0, 0);

        var decrementBtn = new Button
        {
            Text = "-",
            WidthRequest = 50,
            HeightRequest = 40,
            CornerRadius = 5
        };
        decrementBtn.Clicked += (s, e) => 
        {
            _viewModel.DecrementCounter(element.Id);
        };
        grid.Add(decrementBtn, 1, 0);

        var valueLabel = new Label
        {
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 50,
            TextColor = GetTextColor()
        };
        
        // Store reference for updates
        _counterLabels[element.Id] = valueLabel;
        var currentValue = _viewModel.GetFieldValue(element.Id);
        valueLabel.Text = currentValue?.ToString() ?? "0";
        
        grid.Add(valueLabel, 2, 0);

        var incrementBtn = new Button
        {
            Text = "+",
            WidthRequest = 50,
            HeightRequest = 40,
            CornerRadius = 5
        };
        incrementBtn.Clicked += (s, e) => 
        {
            _viewModel.IncrementCounter(element.Id);
        };
        grid.Add(incrementBtn, 3, 0);

        mainLayout.Add(grid);

        // Add total points for this element
        if (element.Points > 0)
        {
            var totalPointsLabel = new Label
            {
                FontSize = 12,
                FontAttributes = FontAttributes.Italic,
                TextColor = (Color)Application.Current!.Resources["Primary"],
                HorizontalOptions = LayoutOptions.End
            };

            // Calculate and display total points
            var updateTotalPoints = () =>
            {
                var value = _viewModel.GetFieldValue(element.Id);
                var count = value switch
                {
                    int i => i,
                    string s when int.TryParse(s, out var parsed) => parsed,
                    _ => 0
                };
                var totalPoints = count * element.Points;
                totalPointsLabel.Text = $"Total: {totalPoints} pts";
            };

            // Subscribe to viewmodel changes
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "FieldValuesChanged")
                {
                    updateTotalPoints();
                }
            };

            updateTotalPoints();
            mainLayout.Add(totalPointsLabel);
        }

        return mainLayout;
    }

    private View CreateBooleanView(ScoringElement element)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var label = new Label
        {
            Text = element.Name,
            VerticalOptions = LayoutOptions.Center,
            TextColor = GetTextColor()
        };
        grid.Add(label, 0, 0);

        // Use the safe conversion method
        var checkBox = new CheckBox
        {
            IsChecked = ConvertToBoolean(element.Default)
        };
        checkBox.CheckedChanged += (s, e) =>
        {
            _viewModel.SetFieldValue(element.Id, e.Value);
        };
        grid.Add(checkBox, 1, 0);

        return grid;
    }

    private View CreateMultipleChoiceView(ScoringElement element)
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var label = new Label
        {
            Text = element.Name,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor()
        };
        layout.Add(label);

        var picker = new Picker
        {
            TextColor = GetTextColor()
        };
        
        if (element.Options != null && element.Options.Count > 0)
        {
            foreach (var option in element.Options)
            {
                // Add points to option display
                var displayText = option.Points > 0 
                    ? $"{option.Name} ({option.Points} pts)" 
                    : option.Name;
                picker.Items.Add(displayText);
            }

            // Set default selection using safe string conversion
            var defaultIndex = 0;
            if (element.Default != null)
            {
                var defaultName = ConvertToString(element.Default);
                defaultIndex = element.Options.FindIndex(o => o.Name == defaultName);
                if (defaultIndex < 0) defaultIndex = 0;
            }
            picker.SelectedIndex = defaultIndex;

            // Set initial value
            if (defaultIndex >= 0 && defaultIndex < element.Options.Count)
            {
                _viewModel.SetFieldValue(element.Id, element.Options[defaultIndex].Name);
            }

            picker.SelectedIndexChanged += (s, e) =>
            {
                if (picker.SelectedIndex >= 0 && picker.SelectedIndex < element.Options.Count)
                {
                    _viewModel.SetFieldValue(element.Id, element.Options[picker.SelectedIndex].Name);
                }
            };
        }

        layout.Add(picker);
        return layout;
    }

    private View CreatePostMatchSection()
    {
        var border = new Border
        {
            BackgroundColor = GetBackgroundColor(),
            StrokeThickness = 1,
            Stroke = GetSecondaryTextColor(),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10)
        };
        border.StrokeShape = new RoundRectangle { CornerRadius = 10 };

        var layout = new VerticalStackLayout { Spacing = 15 };

        if (_viewModel.RatingElements != null)
        {
            foreach (var element in _viewModel.RatingElements)
            {
                layout.Add(CreateRatingView(element));
            }
        }

        if (_viewModel.TextElements != null)
        {
            foreach (var element in _viewModel.TextElements)
            {
                layout.Add(CreateTextView(element));
            }
        }

        border.Content = layout;
        return border;
    }

    private View CreateRatingView(RatingElement element)
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var label = new Label
        {
            Text = element.Name,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor()
        };
        layout.Add(label);

        var slider = new Slider
        {
            Minimum = element.Min,
            Maximum = element.Max,
            Value = element.Default
        };

        var valueLabel = new Label
        {
            Text = element.Default.ToString(),
            HorizontalOptions = LayoutOptions.Center,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor()
        };

        slider.ValueChanged += (s, e) =>
        {
            var intValue = (int)Math.Round(e.NewValue);
            valueLabel.Text = intValue.ToString();
            _viewModel.SetFieldValue(element.Id, intValue);
        };

        // Set initial value
        _viewModel.SetFieldValue(element.Id, element.Default);

        layout.Add(slider);
        layout.Add(valueLabel);

        return layout;
    }

    private View CreateTextView(TextElement element)
    {
        var layout = new VerticalStackLayout { Spacing = 10 };

        var label = new Label
        {
            Text = element.Name,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor()
        };
        layout.Add(label);

        if (element.Multiline)
        {
            var editor = new Editor
            {
                Placeholder = $"Enter {element.Name}",
                HeightRequest = 100,
                TextColor = GetTextColor(),
                PlaceholderColor = GetSecondaryTextColor()
            };
            editor.TextChanged += (s, e) =>
            {
                _viewModel.SetFieldValue(element.Id, e.NewTextValue ?? string.Empty);
            };
            layout.Add(editor);
        }
        else
        {
            var entry = new Entry
            {
                Placeholder = $"Enter {element.Name}",
                TextColor = GetTextColor(),
                PlaceholderColor = GetSecondaryTextColor()
            };
            entry.TextChanged += (s, e) =>
            {
                _viewModel.SetFieldValue(element.Id, e.NewTextValue ?? string.Empty);
            };
            layout.Add(entry);
        }

        return layout;
    }

    private View CreateSubmitSection()
    {
        var layout = new VerticalStackLayout { Spacing = 15, Margin = new Thickness(0, 10, 0, 0) };

        // Status message
        var statusLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 16,
            TextColor = (Color)Application.Current!.Resources["Tertiary"]
        };
        statusLabel.SetBinding(Label.TextProperty, nameof(ScoutingViewModel.StatusMessage));
        statusLabel.SetBinding(IsVisibleProperty, new Binding(nameof(ScoutingViewModel.StatusMessage),
            converter: Application.Current.Resources["StringNotEmptyConverter"] as IValueConverter));
        layout.Add(statusLabel);

        // Button Grid for Submit and QR Code
        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(10) },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        var submitButton = new Button
        {
            Text = "Submit",
            BackgroundColor = (Color)Application.Current.Resources["Primary"],
            TextColor = Colors.White,
            HeightRequest = 50,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10
        };
        submitButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.SubmitCommand));
        submitButton.SetBinding(Button.IsEnabledProperty, new Binding(nameof(ScoutingViewModel.IsLoading),
            converter: Application.Current.Resources["InvertedBoolConverter"] as IValueConverter));
        buttonGrid.Add(submitButton, 0, 0);

        var qrCodeButton = new Button
        {
            Text = "Save with QR",
            BackgroundColor = (Color)Application.Current.Resources["Secondary"],
            TextColor = Colors.White,
            HeightRequest = 50,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10
        };
        qrCodeButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.SaveWithQRCodeCommand));
        qrCodeButton.SetBinding(Button.IsEnabledProperty, new Binding(nameof(ScoutingViewModel.IsLoading),
            converter: Application.Current.Resources["InvertedBoolConverter"] as IValueConverter));
        buttonGrid.Add(qrCodeButton, 2, 0);

        layout.Add(buttonGrid);

        // Export JSON Button
        var exportJsonButton = new Button
        {
            Text = "📄 Export as JSON",
            BackgroundColor = (Color)Application.Current.Resources["Info"],
            TextColor = Colors.White,
            HeightRequest = 50,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 10,
            Margin = new Thickness(0, 10, 0, 0)
        };
        exportJsonButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.ExportJsonCommand));
        exportJsonButton.SetBinding(Button.IsEnabledProperty, new Binding(nameof(ScoutingViewModel.IsLoading),
            converter: Application.Current.Resources["InvertedBoolConverter"] as IValueConverter));
        layout.Add(exportJsonButton);

        var activityIndicator = new ActivityIndicator
        {
            Color = (Color)Application.Current.Resources["Primary"]
        };
        activityIndicator.SetBinding(ActivityIndicator.IsRunningProperty, nameof(ScoutingViewModel.IsLoading));
        activityIndicator.SetBinding(IsVisibleProperty, nameof(ScoutingViewModel.IsLoading));
        layout.Add(activityIndicator);

        // QR Code Display Section - Modern fullscreen overlay
        var qrCodeSection = new Grid
        {
            BackgroundColor = Color.FromArgb("#E0000000"), // Semi-transparent dark overlay
            Padding = new Thickness(20),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star } // Center content
            }
        };
        qrCodeSection.SetBinding(IsVisibleProperty, nameof(ScoutingViewModel.IsQRCodeVisible));

        // QR Code Card
        var qrCard = new Border
        {
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Color.FromArgb("#2C2C2C") 
                : Colors.White,
            StrokeThickness = 0,
            Padding = new Thickness(20),
            MaximumWidthRequest = 500,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Shadow = new Shadow
            {
                Brush = Colors.Black,
                Opacity = 0.3f,
                Radius = 20,
                Offset = new Point(0, 5)
            }
        };
        qrCard.StrokeShape = new RoundRectangle { CornerRadius = 20 };

        var qrCardLayout = new VerticalStackLayout { Spacing = 20 };

        // Header with close button
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var qrCodeTitle = new Label
        {
            Text = "📱 QR Code Ready",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
            VerticalOptions = LayoutOptions.Center
        };
        headerGrid.Add(qrCodeTitle, 0, 0);

        var closeButton = new Button
        {
            Text = "✕",
            FontSize = 20,
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 20,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#40FFFFFF"),
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        };
        closeButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.CloseQRCodeCommand));
        headerGrid.Add(closeButton, 1, 0);

        qrCardLayout.Add(headerGrid);

        // Summary Information Card
        var summaryBorder = new Border
        {
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Color.FromArgb("#1E1E1E") 
                : Color.FromArgb("#F5F5F5"),
            StrokeThickness = 0,
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10)
        };
        summaryBorder.StrokeShape = new RoundRectangle { CornerRadius = 10 };

        var summaryLayout = new VerticalStackLayout { Spacing = 8 };

        // Team info
        var teamInfoLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        };
        teamInfoLabel.SetBinding(Label.TextProperty, new Binding("SelectedTeam", 
            converter: new FuncConverter<Team?, string>(team => 
                team != null ? $"🎯 Team {team.TeamNumber} - {team.TeamName}" : "Team: Not Selected")));
        summaryLayout.Add(teamInfoLabel);

        // Match info
        var matchInfoLabel = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black
        };
        matchInfoLabel.SetBinding(Label.TextProperty, new Binding("SelectedMatch",
            converter: new FuncConverter<Match?, string>(match =>
                match != null ? $"🏆 {match.MatchType} Match {match.MatchNumber}" : "Match: Not Selected")));
        summaryLayout.Add(matchInfoLabel);

        // Scout name
        var scoutInfoLabel = new Label
        {
            FontSize = 14,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Color.FromArgb("#B0B0B0") 
                : Color.FromArgb("#606060")
        };
        scoutInfoLabel.SetBinding(Label.TextProperty, new Binding("ScoutName",
            converter: new FuncConverter<string, string>(name =>
                !string.IsNullOrEmpty(name) ? $"👤 Scout: {name}" : "👤 Scout: Anonymous")));
        summaryLayout.Add(scoutInfoLabel);

        summaryBorder.Content = summaryLayout;
        qrCardLayout.Add(summaryBorder);

        // QR Code Image with border
        var qrImageBorder = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 2,
            Stroke = Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Color.FromArgb("#404040") 
                : Color.FromArgb("#E0E0E0"),
            Padding = new Thickness(15),
            HorizontalOptions = LayoutOptions.Center
        };
        qrImageBorder.StrokeShape = new RoundRectangle { CornerRadius = 15 };

        var qrCodeImage = new Image
        {
            HeightRequest = 300,
            WidthRequest = 300,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center
        };
        qrCodeImage.SetBinding(Image.SourceProperty, nameof(ScoutingViewModel.QrCodeImage));
        
        qrImageBorder.Content = qrCodeImage;
        qrCardLayout.Add(qrImageBorder);

        // Instructions
        var qrCodeInfo = new Label
        {
            Text = "📸 Scan this QR code with another device to transfer the scouting data",
            FontSize = 13,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Color.FromArgb("#B0B0B0") 
                : Color.FromArgb("#606060"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };
        qrCardLayout.Add(qrCodeInfo);

        // Timestamp
        var timestampLabel = new Label
        {
            FontSize = 11,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark 
                ? Color.FromArgb("#808080") 
                : Color.FromArgb("#909090"),
            HorizontalTextAlignment = TextAlignment.Center,
            Text = $"Generated: {DateTime.Now:h:mm:ss tt}"
        };
        qrCardLayout.Add(timestampLabel);

        // Action buttons
        var buttonLayout = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(10) },
                new ColumnDefinition { Width = GridLength.Star }
            },
            Margin = new Thickness(0, 10, 0, 0)
        };

        var resetAndCloseButton = new Button
        {
            Text = "Reset & Close",
            BackgroundColor = Color.FromArgb("#FF6B6B"),
            TextColor = Colors.White,
            CornerRadius = 10,
            HeightRequest = 45,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        resetAndCloseButton.Clicked += (s, e) =>
        {
            _viewModel.CloseQRCodeCommand.Execute(null);
            _viewModel.ResetFormCommand.Execute(null);
        };
        buttonLayout.Add(resetAndCloseButton, 0, 0);

        var doneButton = new Button
        {
            Text = "Done",
            BackgroundColor = (Color)Application.Current.Resources["Primary"],
            TextColor = Colors.White,
            CornerRadius = 10,
            HeightRequest = 45,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        doneButton.SetBinding(Button.CommandProperty, nameof(ScoutingViewModel.CloseQRCodeCommand));
        buttonLayout.Add(doneButton, 2, 0);

        qrCardLayout.Add(buttonLayout);

        qrCard.Content = qrCardLayout;
        qrCodeSection.Add(qrCard);

        layout.Add(qrCodeSection);

        return layout;
    }
}
