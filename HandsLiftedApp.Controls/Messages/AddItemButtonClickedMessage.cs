namespace HandsLiftedApp.Controls.Messages
{
    public class AddItemButtonClickedMessage
    {
        public int? insertIndex { get; }

        public AddItemButtonClickedMessage(int? insertIndex = null)
        {
            this.insertIndex = insertIndex;
        }
    }
}