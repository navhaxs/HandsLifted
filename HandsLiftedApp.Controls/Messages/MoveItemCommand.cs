using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Controls.Messages
{
    public class MoveItemCommand
    {
        public Item SourceItem { get; set; }
        public DirectionValue Direction { get; set; }
        public enum DirectionValue
        {
            UP,
            DOWN,
            REMOVE
        }
    }
}
