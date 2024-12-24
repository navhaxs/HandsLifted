namespace HandsLiftedApp.Core.Models.UI
{
    public class OutputDisplayConfigurationChangeMessage
    {
        public enum Display
        {
            Projector,
            StageDisplay
        }

        public Display ChangedDisplay { get; set; }
    }
}