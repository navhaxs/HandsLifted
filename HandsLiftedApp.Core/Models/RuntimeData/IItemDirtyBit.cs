using System;
using System.ComponentModel;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    public interface IItemDirtyBit
    {
        public event EventHandler ItemDataModified;
    }
}