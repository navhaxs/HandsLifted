using System;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// One property of one object changing from <c>old</c> to <c>new</c>. Recorded <em>after</em> the fact (the UI
    /// wrote the value through a binding), so it goes in with <see cref="UndoStack.Record"/>.
    ///
    /// Successive edits of the same property of the same target within the stack's merge window collapse into a
    /// single step - typing a title or dragging a slider is one undo, not one per keystroke / tick.
    /// </summary>
    internal sealed class PropertyEditAction : IUndoableAction
    {
        private readonly object _targetKey;
        private readonly string _property;
        private readonly Action<object?> _write;
        private readonly object? _oldValue;
        private object? _newValue;

        /// <param name="targetKey">Identifies the edited object across rebuilds (e.g. the item's UUID).</param>
        /// <param name="write">Sets the property on the <em>current</em> live object (looked up fresh each call).</param>
        public PropertyEditAction(object targetKey, string property, string description, object? oldValue,
            object? newValue, Action<object?> write)
        {
            _targetKey = targetKey;
            _property = property;
            Description = description;
            _oldValue = oldValue;
            _newValue = newValue;
            _write = write;
        }

        public string Description { get; }

        public void Do() => _write(_newValue);

        public void Undo() => _write(_oldValue);

        public bool TryMerge(IUndoableAction next)
        {
            if (next is not PropertyEditAction n || !Equals(n._targetKey, _targetKey) || n._property != _property)
                return false;

            _newValue = n._newValue;
            return true;
        }
    }
}
