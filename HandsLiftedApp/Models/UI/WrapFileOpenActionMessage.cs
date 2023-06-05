using System;

namespace HandsLiftedApp.Models.UI
{
    internal class WrapFileOpenActionMessage
    {
        public Action<string?> CallbackAction { get; set; }
    }
}
