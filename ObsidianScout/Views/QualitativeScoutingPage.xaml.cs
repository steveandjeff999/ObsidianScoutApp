using ObsidianScout.ViewModels;
using System.Collections.Specialized;

namespace ObsidianScout.Views;

public partial class QualitativeScoutingPage : ContentPage
{
    private QualitativeScoutingViewModel? _vm;

    public QualitativeScoutingPage(QualitativeScoutingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _vm = viewModel;

        // Watch for team card changes
        _vm.TeamCards.CollectionChanged += TeamCards_CollectionChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_vm != null)
            _vm.TeamCards.CollectionChanged -= TeamCards_CollectionChanged;
    }

    private void TeamCards_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RebuildTeamCardViews);
    }

    private void OnAllianceClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && _vm != null)
        {
            _vm.SelectedAlliance = btn.AutomationId;
        }
    }

    /// <summary>
    /// Rebuild the team card views in the TeamCardsContainer.
    /// </summary>
    private void RebuildTeamCardViews()
    {
        TeamCardsContainer.Children.Clear();
        if (_vm == null) return;

        foreach (var teamData in _vm.TeamCards)
        {
            var card = BuildTeamCard(teamData);
            TeamCardsContainer.Children.Add(card);
        }
    }

    /// <summary>
    /// Build a full team card UI for a single QualitativeTeamData.
    /// </summary>
    private View BuildTeamCard(QualitativeTeamData data)
    {
        var allianceColor = data.Alliance switch
        {
            "red" => Color.FromArgb("#DC3545"),
            "blue" => Color.FromArgb("#0D6EFD"),
            _ => Color.FromArgb("#198754")
        };

        var cardBg = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#1A1A1A")
            : Color.FromArgb("#FFFFFF");
        var borderColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#444444")
            : Color.FromArgb("#DEE2E6");
        var textColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#E6E6E6")
            : Color.FromArgb("#333333");
        var textSecondary = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#AAAAAA")
            : Color.FromArgb("#666666");
        var sectionBg = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#2A2A2A")
            : Color.FromArgb("#F8F9FA");

        var card = new Border
        {
            BackgroundColor = cardBg,
            Stroke = new SolidColorBrush(borderColor),
            StrokeThickness = 2,
            Padding = new Thickness(16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) }
        };

        var stack = new VerticalStackLayout { Spacing = 12 };

        // ── HEADER ──
        var headerGrid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        headerGrid.Add(new Label
        {
            Text = $"🤖 Team {data.TeamNumber}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = allianceColor,
            VerticalOptions = LayoutOptions.Center
        }, 0);

        // Beached toggle
        var beachedCheck = CreateCheckBox(data.GotBeached, (s, e) => data.GotBeached = ((CheckBox)s!).IsChecked);
        var beachedStack = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        beachedStack.Add(beachedCheck);
        beachedStack.Add(new Label { Text = "⚠ Beached", FontSize = 12, TextColor = Color.FromArgb("#FFC107"), VerticalOptions = LayoutOptions.Center });
        headerGrid.Add(beachedStack, 1);
        stack.Add(headerGrid);

        // ── ROLES ──
        stack.Add(new Label { Text = "Roles", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = textSecondary });
        var rolesWrap = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
        rolesWrap.Add(CreateRoleCheck("Cycling", data.Cycling, (s, e) => data.Cycling = ((CheckBox)s!).IsChecked));
        rolesWrap.Add(CreateRoleCheck("Stealing", data.Stealing, (s, e) => data.Stealing = ((CheckBox)s!).IsChecked));
        rolesWrap.Add(CreateRoleCheck("Scoring", data.Scoring, (s, e) => data.Scoring = ((CheckBox)s!).IsChecked));
        rolesWrap.Add(CreateRoleCheck("Feeding", data.Feeding, (s, e) =>
        {
            data.Feeding = ((CheckBox)s!).IsChecked;
            // Show/hide feeder section
            var feederSection = stack.Children.OfType<VerticalStackLayout>().FirstOrDefault(v => v.AutomationId == "FeederSection");
            if (feederSection != null) feederSection.IsVisible = data.Feeding;
        }));
        rolesWrap.Add(CreateRoleCheck("Defending", data.Defending, (s, e) => data.Defending = ((CheckBox)s!).IsChecked));
        rolesWrap.Add(CreateRoleCheck("No Contribution", data.DidNotContribute, (s, e) => data.DidNotContribute = ((CheckBox)s!).IsChecked));
        rolesWrap.Add(CreateRoleCheck("Scores while moving", data.CanScoreWhileMoving, (s, e) => data.CanScoreWhileMoving = ((CheckBox)s!).IsChecked));
        stack.Add(rolesWrap);

        // ── FEEDER TYPE (hidden until Feeding checked) ──
        var feederSection = new VerticalStackLayout { Spacing = 4, AutomationId = "FeederSection", IsVisible = data.Feeding };
        var feederLabel = new Label { Text = "↳ Feeder Type:", FontSize = 12, TextColor = textSecondary };
        feederSection.Add(feederLabel);
        var feederWrap = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        feederWrap.Add(CreateRoleCheck("Continuous", data.FeederTypeContinuous, (s, e) => data.FeederTypeContinuous = ((CheckBox)s!).IsChecked));
        feederWrap.Add(CreateRoleCheck("Stop & Shoot", data.FeederTypeStopToShoot, (s, e) => data.FeederTypeStopToShoot = ((CheckBox)s!).IsChecked));
        feederWrap.Add(CreateRoleCheck("Dump", data.FeederTypeDump, (s, e) => data.FeederTypeDump = ((CheckBox)s!).IsChecked));
        feederSection.Add(feederWrap);
        stack.Add(feederSection);

        // ── RATING DROPDOWNS (2x2 grid) ──
        var ratingsGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        ratingsGrid.Add(CreateRatingPicker("Driver Ability", new[] { "1 – Poor", "2 – Below avg", "3 – Average", "4 – Good", "5 – Excellent" },
            data.DriverRating, v => data.DriverRating = v, sectionBg, textColor), 0, 0);
        ratingsGrid.Add(CreateRatingPicker("Defense Effectiveness", new[] { "1 – None", "2 – Minor", "3 – Moderate", "4 – Effective", "5 – Dominant" },
            data.DefenseEffectiveness, v => data.DefenseEffectiveness = v, sectionBg, textColor), 1, 0);
        ratingsGrid.Add(CreateRatingPicker("Shot Accuracy", new[] { "0 – No shots", "1 – <25%", "2 – 25–50%", "3 – 50–75%", "4 – 75–90%", "5 – >90%" },
            data.ShotAccuracy, v => data.ShotAccuracy = v, sectionBg, textColor), 0, 1);
        ratingsGrid.Add(CreateRatingPicker("Robot Rating", new[] { "1 – Poor", "2 – Below avg", "3 – Average", "4 – Good", "5 – Excellent" },
            data.RobotRating, v => data.RobotRating = v, sectionBg, textColor), 1, 1);
        stack.Add(ratingsGrid);

        // ── OVERALL RATING (required, 1-5 buttons) ──
        var overallBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#1AFFC107"),
            Stroke = new SolidColorBrush(Color.FromArgb("#FFC107")),
            StrokeThickness = 1,
            Padding = new Thickness(12, 8),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(8) }
        };
        var overallStack = new VerticalStackLayout { Spacing = 8 };
        var overallLabel = new HorizontalStackLayout { Spacing = 4 };
        overallLabel.Add(new Label { Text = "Overall Rating", FontAttributes = FontAttributes.Bold, FontSize = 13, TextColor = textColor });
        overallLabel.Add(new Label { Text = "*", TextColor = Color.FromArgb("#DC3545"), FontSize = 13 });
        overallLabel.Add(new Label { Text = "(1=poor, 5=excellent)", FontSize = 11, TextColor = textSecondary });
        overallStack.Add(overallLabel);

        var overallButtons = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 4 };
        for (int i = 1; i <= 5; i++)
        {
            var rating = i;
            var btn = new Button
            {
                Text = i.ToString(),
                BackgroundColor = data.OverallRating == i ? Color.FromArgb("#FFC107") : Color.FromArgb("#40FFC107"),
                TextColor = data.OverallRating == i ? Colors.Black : textColor,
                FontAttributes = data.OverallRating == i ? FontAttributes.Bold : FontAttributes.None,
                CornerRadius = 6,
                FontSize = 14,
                HeightRequest = 40,
                Padding = new Thickness(0)
            };
            btn.Clicked += (s, e) =>
            {
                data.OverallRating = rating;
                // Update button states
                foreach (var child in overallButtons.Children.OfType<Button>())
                {
                    var r = int.Parse(child.Text);
                    child.BackgroundColor = r == rating ? Color.FromArgb("#FFC107") : Color.FromArgb("#40FFC107");
                    child.TextColor = r == rating ? Colors.Black : textColor;
                    child.FontAttributes = r == rating ? FontAttributes.Bold : FontAttributes.None;
                }
            };
            overallButtons.Add(btn, i - 1);
        }
        overallStack.Add(overallButtons);
        overallBorder.Content = overallStack;
        stack.Add(overallBorder);

        // ── RANKING (1st, 2nd, 3rd buttons) ──
        var rankStack = new VerticalStackLayout { Spacing = 6 };
        rankStack.Add(new Label { Text = "Ranking (1=best)", FontSize = 12, TextColor = textSecondary });
        var rankButtons = new HorizontalStackLayout { Spacing = 6 };
        var rankColors = new[] { "#198754", "#0D6EFD", "#6C757D" };
        var rankLabels = new[] { "1st", "2nd", "3rd" };
        for (int i = 0; i < 3; i++)
        {
            var rank = i + 1;
            var baseColor = Color.FromArgb(rankColors[i]);
            var btn = new Button
            {
                Text = rankLabels[i],
                BackgroundColor = data.Ranking == rank ? baseColor : Color.FromArgb("#40" + rankColors[i][1..]),
                TextColor = Colors.White,
                FontAttributes = data.Ranking == rank ? FontAttributes.Bold : FontAttributes.None,
                CornerRadius = 6,
                FontSize = 13,
                HeightRequest = 36,
                Padding = new Thickness(16, 0)
            };
            btn.Clicked += (s, e) =>
            {
                data.Ranking = rank;
                for (int j = 0; j < rankButtons.Children.Count; j++)
                {
                    if (rankButtons.Children[j] is Button b)
                    {
                        var r = j + 1;
                        var c = Color.FromArgb(rankColors[j]);
                        b.BackgroundColor = r == rank ? c : Color.FromArgb("#40" + rankColors[j][1..]);
                        b.FontAttributes = r == rank ? FontAttributes.Bold : FontAttributes.None;
                    }
                }
            };
            rankButtons.Add(btn);
        }
        rankStack.Add(rankButtons);
        stack.Add(rankStack);

        // ── ENDGAME CLIMB ──
        var endgameStack = new VerticalStackLayout { Spacing = 6 };
        var endgameHeader = new HorizontalStackLayout { Spacing = 8 };
        endgameHeader.Add(new Label { Text = "Endgame:", FontAttributes = FontAttributes.Bold, FontSize = 13, TextColor = textSecondary, VerticalOptions = LayoutOptions.Center });

        var egSuccessBtn = new Button { Text = "✓", BackgroundColor = Color.FromArgb("#198754"), TextColor = Colors.White, CornerRadius = 6, FontSize = 14, HeightRequest = 34, WidthRequest = 44, Padding = new Thickness(0) };
        var egFailBtn = new Button { Text = "✗", BackgroundColor = Color.FromArgb("#DC3545"), TextColor = Colors.White, CornerRadius = 6, FontSize = 14, HeightRequest = 34, WidthRequest = 44, Padding = new Thickness(0) };

        egSuccessBtn.Clicked += (s, e) => { data.EndgameClimbResult = "success"; UpdateEndgameButtons(egSuccessBtn, egFailBtn, "success"); };
        egFailBtn.Clicked += (s, e) => { data.EndgameClimbResult = "fail"; UpdateEndgameButtons(egSuccessBtn, egFailBtn, "fail"); };
        endgameHeader.Add(egSuccessBtn);
        endgameHeader.Add(egFailBtn);

        var egLevelPicker = new Picker
        {
            Title = "Level",
            ItemsSource = new[] { "Low", "Mid", "High" },
            WidthRequest = 100,
            BackgroundColor = sectionBg,
            TextColor = textColor
        };
        egLevelPicker.SelectedIndexChanged += (s, e) =>
        {
            if (egLevelPicker.SelectedIndex >= 0)
                data.EndgameClimbLevel = ((string[])egLevelPicker.ItemsSource)[egLevelPicker.SelectedIndex].ToLowerInvariant();
        };
        endgameHeader.Add(egLevelPicker);
        endgameStack.Add(endgameHeader);
        stack.Add(endgameStack);

        // ── AUTO CLIMB ──
        var autoStack = new HorizontalStackLayout { Spacing = 8 };
        autoStack.Add(new Label { Text = "Auto:", FontAttributes = FontAttributes.Bold, FontSize = 13, TextColor = textSecondary, VerticalOptions = LayoutOptions.Center });

        var acSuccessBtn = new Button { Text = "✓ Climbed", BackgroundColor = Color.FromArgb("#198754"), TextColor = Colors.White, CornerRadius = 6, FontSize = 12, HeightRequest = 34, Padding = new Thickness(10, 0) };
        var acFailBtn = new Button { Text = "✗ Failed", BackgroundColor = Color.FromArgb("#DC3545"), TextColor = Colors.White, CornerRadius = 6, FontSize = 12, HeightRequest = 34, Padding = new Thickness(10, 0) };

        acSuccessBtn.Clicked += (s, e) => { data.AutoClimbResult = "success"; UpdateEndgameButtons(acSuccessBtn, acFailBtn, "success"); };
        acFailBtn.Clicked += (s, e) => { data.AutoClimbResult = "fail"; UpdateEndgameButtons(acSuccessBtn, acFailBtn, "fail"); };
        autoStack.Add(acSuccessBtn);
        autoStack.Add(acFailBtn);
        stack.Add(autoStack);

        // ── NOTES ──
        var notesEditor = new Editor
        {
            Placeholder = "Notes...",
            Text = data.Notes,
            HeightRequest = 60,
            BackgroundColor = sectionBg,
            TextColor = textColor,
            PlaceholderColor = textSecondary,
            FontSize = 14
        };
        notesEditor.TextChanged += (s, e) => data.Notes = notesEditor.Text ?? string.Empty;
        stack.Add(notesEditor);

        card.Content = stack;
        return card;
    }

    private static void UpdateEndgameButtons(Button successBtn, Button failBtn, string value)
    {
        successBtn.Opacity = value == "success" ? 1.0 : 0.5;
        failBtn.Opacity = value == "fail" ? 1.0 : 0.5;
    }

    private static View CreateRoleCheck(string label, bool initialValue, EventHandler<CheckedChangedEventArgs> handler)
    {
        var stack = new HorizontalStackLayout { Spacing = 2, Margin = new Thickness(0, 0, 10, 4) };
        var cb = CreateCheckBox(initialValue, handler);
        stack.Add(cb);
        stack.Add(new Label
        {
            Text = label,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#E6E6E6") : Color.FromArgb("#333333")
        });
        return stack;
    }

    private static CheckBox CreateCheckBox(bool initialValue, EventHandler<CheckedChangedEventArgs> handler)
    {
        var cb = new CheckBox
        {
            IsChecked = initialValue,
            Color = Color.FromArgb("#0D6EFD"),
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 30,
            HeightRequest = 30
        };
        cb.CheckedChanged += handler;
        return cb;
    }

    private static View CreateRatingPicker(string label, string[] options, int? currentValue, Action<int?> onChanged, Color bg, Color textColor)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Add(new Label { Text = label, FontSize = 11, TextColor = textColor });
        var picker = new Picker
        {
            Title = $"— {label} —",
            ItemsSource = options,
            BackgroundColor = bg,
            TextColor = textColor,
            FontSize = 13
        };

        // Set initial value
        if (currentValue.HasValue)
        {
            // Find the option that starts with the value
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].StartsWith(currentValue.Value.ToString()))
                {
                    picker.SelectedIndex = i;
                    break;
                }
            }
        }

        picker.SelectedIndexChanged += (s, e) =>
        {
            if (picker.SelectedIndex >= 0)
            {
                var text = options[picker.SelectedIndex];
                // Parse the number from the beginning of the option text
                var numStr = text.Split(' ', '–', '—')[0].Trim();
                if (int.TryParse(numStr, out int val))
                    onChanged(val);
            }
            else
            {
                onChanged(null);
            }
        };
        stack.Add(picker);
        return stack;
    }
}
