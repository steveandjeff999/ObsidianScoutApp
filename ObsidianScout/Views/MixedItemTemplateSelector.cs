using Microsoft.Maui.Controls;
using System;
using ObsidianScout.Models;

namespace ObsidianScout.Views;

public class MixedItemTemplateSelector : DataTemplateSelector
{
 public DataTemplate TeamTemplate { get; set; }
 public DataTemplate EventTemplate { get; set; }
 public DataTemplate MatchTemplate { get; set; }
 public DataTemplate ScoutingTemplate { get; set; }

 protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
 {
 if (item is Team) return TeamTemplate;
 if (item is Event) return EventTemplate;
 if (item is MatchCard) return MatchTemplate;
 if (item is ScoutingEntry) return ScoutingTemplate;
 return null;
 }
}