using System.Globalization;

namespace ObsidianScout.Converters;

public class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }
}

public class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
            return !string.IsNullOrEmpty(str);
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            // Check if parameter is "Invert"
            bool invert = parameter?.ToString()?.Equals("Invert", StringComparison.OrdinalIgnoreCase) == true;
            bool result = intValue > 0;
            return invert ? !result : result;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class FuncConverter<TSource, TTarget> : IValueConverter
{
    private readonly Func<TSource?, TTarget?> _convertFunc;
    private readonly Func<TTarget?, TSource?>? _convertBackFunc;

    public FuncConverter(Func<TSource?, TTarget?> convertFunc, Func<TTarget?, TSource?>? convertBackFunc = null)
    {
        _convertFunc = convertFunc;
        _convertBackFunc = convertBackFunc;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TSource source || value == null)
        {
            return _convertFunc((TSource?)value);
        }
        return default(TTarget);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (_convertBackFunc == null)
            throw new NotImplementedException();

        if (value is TTarget target || value == null)
        {
            return _convertBackFunc((TTarget?)value);
        }
        return default(TSource);
    }
}

public class IsNotZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue > 0;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IsZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue == 0;
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TeamDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int teamNumber)
            return $"#{teamNumber}";
        return value?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TeamNumberNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This is a placeholder - actual team name would come from binding context
        if (value is int teamNumber)
            return $"#{teamNumber}";
        return value?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DataViewToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string selectedView && parameter is string buttonView)
        {
            bool isSelected = selectedView == buttonView;
            
            if (isSelected)
            {
                // Return Primary color when selected
                if (Application.Current?.Resources.TryGetValue("Primary", out var primaryColor) == true)
                {
                    return primaryColor as Color;
                }
                return Color.FromArgb("#512BD4"); // Fallback primary color
            }
            else
            {
                // Return transparent when not selected
                return Colors.Transparent;
            }
        }
        
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TeamChartHeaderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Value comes from TeamNumber property
        // We need to format it with the TeamName from the binding context
        if (value is string teamNumber)
        {
            // The StringFormat in XAML will handle combining teamNumber and teamName
            return teamNumber;
        }
        return value?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
 }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
  return value is bool b && b ? Colors.Green : Colors.Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
     throw new NotImplementedException();
    }
}

public class StringEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
  {
        return value?.ToString() == parameter?.ToString();
  }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringNotEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      return value?.ToString() != parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NotBooleanOrMultipleChoiceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string type)
     {
            return type != "boolean" && type != "multiplechoice";
  }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
  throw new NotImplementedException();
    }
}

// NEW: Converter to check if element type is select or multiselect
public class TypeIsSelectConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
   if (value is string type)
   {
       return type == "select" || type == "multiselect";
        }
 return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
  throw new NotImplementedException();
    }
}

public class BoolToScanButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isScanning && isScanning ? "? Pause" : "? Scan";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
  }
}

public class BoolToTorchButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isTorchOn && isTorchOn ? "?? Torch Off" : "?? Torch On";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToFlashlightButtonTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isFlashlightOn && isFlashlightOn ? "?? Flashlight Off" : "?? Flashlight On";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// New: converter used in XAML to check for non-null and non-empty strings
public class IsNotNullOrEmptyConverter : IValueConverter
{
 public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
 {
 if (value is string s)
 return !string.IsNullOrEmpty(s);
 return value != null; // treat non-string non-null as true
 }

 public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
 {
 throw new NotImplementedException();
 }
}
