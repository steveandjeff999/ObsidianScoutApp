using System;

namespace ObsidianScout.Models
{
 public class MatchCard
 {
 public string EventName { get; set; } = string.Empty;
 public Match Match { get; set; } = new Match();
 }
}