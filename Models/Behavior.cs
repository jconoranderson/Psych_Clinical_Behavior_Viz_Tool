using System;

namespace PsychDashboard.Models
{
    public class Behavior
    {
        public DateTime Date { get; set; }
        public string Shift { get; set; } = string.Empty;
        public string BehaviorType { get; set; } = string.Empty;
        public int? Intensity { get; set; }
        public int? Duration { get; set; }
    }
}
