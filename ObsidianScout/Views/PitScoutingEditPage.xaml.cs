using ObsidianScout.ViewModels;
using ObsidianScout.Models;
using Microsoft.Maui.Controls;

namespace ObsidianScout.Views;

[QueryProperty(nameof(EntryId), "EntryId")]
public partial class PitScoutingEditPage : ContentPage
{
 private readonly PitScoutingEditViewModel _viewModel;
    private int _entryId;

    public int EntryId
    {
        get => _entryId;
        set
        {
   _entryId = value;
    _ = LoadEntryAsync(value);
        }
    }

    public PitScoutingEditPage(PitScoutingEditViewModel viewModel)
    {
     _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();

  // Subscribe to property changes to rebuild form when config/entry loads
_viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

 private async Task LoadEntryAsync(int entryId)
  {
        if (entryId > 0)
  {
            await _viewModel.LoadEntryAsync(entryId);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PitScoutingEditViewModel.PitConfig) ||
      e.PropertyName == nameof(PitScoutingEditViewModel.PitEntry))
        {
      BuildDynamicForm();
        }
    }

    private void BuildDynamicForm()
    {
        if (_viewModel.PitConfig?.PitScouting?.Sections == null || _viewModel.PitEntry == null)
      {
            System.Diagnostics.Debug.WriteLine("[PitScoutingEdit] BuildDynamicForm: Config or entry not ready");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] BuildDynamicForm: Building form with {_viewModel.PitConfig.PitScouting.Sections.Count} sections");

        FormContainer.Children.Clear();

        foreach (var section in _viewModel.PitConfig.PitScouting.Sections)
        {
            System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Section: {section.Name} with {section.Elements.Count} elements");

            // Section Header
            var sectionLabel = new Label
    {
     Text = section.Name,
     FontSize = 20,
        FontAttributes = FontAttributes.Bold,
    Margin = new Thickness(0, 16, 0, 8)
      };

          FormContainer.Children.Add(sectionLabel);

            // Section Elements
  foreach (var element in section.Elements)
     {
     System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit]   Element: {element.Name} (Type: {element.Type}, Options: {element.Options?.Count ?? 0})");
     AddFormElement(element);
            }
        }
    }

    private void AddFormElement(PitElement element)
    {
        var container = new VerticalStackLayout
{
            Spacing = 4,
            Margin = new Thickness(0, 8, 0, 8)
        };

        // Label
        var label = new Label
      {
    Text = element.Name + (element.Required ? " *" : ""),
      FontSize = 16,
        FontAttributes = FontAttributes.Bold
        };
     container.Add(label);

        // Input control based on type
        View? inputControl = element.Type.ToLower() switch
        {
  "number" => CreateNumberEntry(element),
 "text" => CreateTextEntry(element),
 "textarea" => CreateTextAreaEntry(element),
         "boolean" => CreateCheckBox(element),
            "select" => CreatePicker(element),
      "multiselect" => CreateMultiSelectPicker(element),
            _ => CreateTextEntry(element)
        };

        if (inputControl != null)
        {
  container.Add(inputControl);
        }

        FormContainer.Children.Add(container);
    }

    private Entry CreateNumberEntry(PitElement element)
    {
   var entry = new Entry
        {
            Placeholder = element.Placeholder ?? $"Enter {element.Name}",
      Keyboard = Keyboard.Numeric,
       FontSize = 16
        };

        // Bind to ViewModel field value
        var initialValue = _viewModel.GetFieldValue(element.Id);
        if (initialValue != null)
  {
            entry.Text = initialValue.ToString();
        }

        entry.TextChanged += (s, e) =>
      {
  if (int.TryParse(e.NewTextValue, out var value))
       {
        // Validate min/max
  if (element.Validation?.Min.HasValue == true && value < element.Validation.Min.Value)
                {
        entry.Text = element.Validation.Min.Value.ToString();
     return;
           }
     if (element.Validation?.Max.HasValue == true && value > element.Validation.Max.Value)
 {
           entry.Text = element.Validation.Max.Value.ToString();
         return;
  }
                _viewModel.SetFieldValue(element.Id, value);
            }
        };

        return entry;
    }

    private Entry CreateTextEntry(PitElement element)
  {
        var entry = new Entry
        {
    Placeholder = element.Placeholder ?? $"Enter {element.Name}",
   FontSize = 16
        };

        // Bind to ViewModel field value
        var initialValue = _viewModel.GetFieldValue(element.Id);
        if (initialValue != null)
        {
    entry.Text = initialValue.ToString();
        }

        entry.TextChanged += (s, e) =>
        {
            _viewModel.SetFieldValue(element.Id, e.NewTextValue);
 };

        // Disable editing if view model is not in edit mode
        try { entry.IsEnabled = _viewModel.IsEditMode; } catch { }

        return entry;
  }

    private Editor CreateTextAreaEntry(PitElement element)
    {
        var editor = new Editor
        {
   Placeholder = element.Placeholder ?? $"Enter {element.Name}",
            FontSize = 16,
   HeightRequest = 100
        };

        // Bind to ViewModel field value
      var initialValue = _viewModel.GetFieldValue(element.Id);
        if (initialValue != null)
        {
          editor.Text = initialValue.ToString();
        }

        editor.TextChanged += (s, e) =>
        {
            _viewModel.SetFieldValue(element.Id, e.NewTextValue);
        };

        // Disable editing if view model is not in edit mode
        try { editor.IsEnabled = _viewModel.IsEditMode; } catch { }

        return editor;
    }

    private CheckBox CreateCheckBox(PitElement element)
    {
        var checkBox = new CheckBox
      {
    VerticalOptions = LayoutOptions.Center
     };

        // Bind to ViewModel field value
     var initialValue = _viewModel.GetFieldValue(element.Id);
     if (initialValue is bool boolValue)
 {
            checkBox.IsChecked = boolValue;
        }

        checkBox.CheckedChanged += (s, e) =>
        {
       _viewModel.SetFieldValue(element.Id, e.Value);
   };

        // Disable editing if view model is not in edit mode
        try { checkBox.IsEnabled = _viewModel.IsEditMode; } catch { }

        return checkBox;
    }

  private Picker CreatePicker(PitElement element)
    {
        System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] CreatePicker for {element.Name}: Options={element.Options?.Count ?? 0}");
    
        if (element.Options == null)
        {
  System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] WARNING: Options is NULL for {element.Name}");
       return new Picker 
{ 
    Title = $"No options configured for {element.Name}", 
     IsEnabled = false,
                FontSize = 16,
TextColor = Colors.Red
     };
 }

        if (element.Options.Count == 0)
        {
     System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] WARNING: Options is EMPTY for {element.Name}");
            return new Picker 
   { 
         Title = $"No options configured for {element.Name}", 
            IsEnabled = false,
           FontSize = 16,
       TextColor = Colors.Red
          };
        }

        var picker = new Picker
        {
          Title = $"Select {element.Name}",
   FontSize = 16
        };

        foreach (var option in element.Options)
        {
            System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit]   Adding option: {option.Label} (value: {option.Value})");
            picker.Items.Add(option.Label);
    }

  System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] Picker for {element.Name} now has {picker.Items.Count} items");

        // Bind to ViewModel field value
        var initialValue = _viewModel.GetFieldValue(element.Id);
        if (initialValue != null && initialValue is string strValue)
        {
 var matchingOption = element.Options.FirstOrDefault(o => o.Value == strValue);
 if (matchingOption != null)
 {
 var index = element.Options.IndexOf(matchingOption);
 if (index >= 0)
 {
 picker.SelectedIndex = index;
 }
 }
 }

        picker.SelectedIndexChanged += (s, e) =>
        {
         if (picker.SelectedIndex >= 0 && picker.SelectedIndex < element.Options.Count)
            {
      _viewModel.SetFieldValue(element.Id, element.Options[picker.SelectedIndex].Value);
            }
        };

  return picker;
  }

    private VerticalStackLayout CreateMultiSelectPicker(PitElement element)
    {
        System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] CreateMultiSelectPicker for {element.Name}: Options={element.Options?.Count ?? 0}");
        
     var container = new VerticalStackLayout { Spacing = 8 };

        if (element.Options == null || element.Options.Count == 0)
        {
 System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit] WARNING: No options for multiselect {element.Name}");
    container.Add(new Label 
      { 
    Text = $"No options configured for {element.Name}", 
   FontSize = 14,
         TextColor = Colors.Red
            });
      return container;
        }

      var selectedValues = new List<string>();

        // Initialize from ViewModel
        var initialValue = _viewModel.GetFieldValue(element.Id);
        if (initialValue is List<string> list)
   {
        selectedValues = new List<string>(list);
     }

     foreach (var option in element.Options)
        {
     System.Diagnostics.Debug.WriteLine($"[PitScoutingEdit]   Adding multiselect option: {option.Label}");
         
            var checkBoxLayout = new HorizontalStackLayout { Spacing = 8 };
     
            var checkBox = new CheckBox
       {
           IsChecked = selectedValues.Contains(option.Value),
         VerticalOptions = LayoutOptions.Center
      };

       var optionLabel = new Label
     {
   Text = option.Label,
      FontSize = 16,
      VerticalOptions = LayoutOptions.Center
    };

            checkBox.CheckedChanged += (s, e) =>
       {
       if (e.Value)
                {
      if (!selectedValues.Contains(option.Value))
    {
           selectedValues.Add(option.Value);
   }
       }
    else
 {
          selectedValues.Remove(option.Value);
        }
         _viewModel.SetFieldValue(element.Id, new List<string>(selectedValues));
            };

            try { checkBox.IsEnabled = _viewModel.IsEditMode; } catch { }

      checkBoxLayout.Add(checkBox);
            checkBoxLayout.Add(optionLabel);
   container.Add(checkBoxLayout);
        }

        return container;
    }
}
