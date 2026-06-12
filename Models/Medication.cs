using System;

namespace PsychDashboard.Models
{
    public class Medication
    {
        public string Name { get; set; } = string.Empty;
        public string Dose { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
