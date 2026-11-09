using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>One reversible edit.</summary>
    public interface IUndoableAction
    {
        /// <summary>Shown in the menu: "Undo &lt;Description&gt;".</summary>
        string Description { get; }

        /// <summary>Applies the edit. Called for the first execution and for every redo.</summary>
        void Do();

        /// <summary>Reverses <see cref="Do"/>.</summary>
        void Undo();

        /// <summary>
        /// Coalescing hook (e.g. successive keystrokes / slider ticks becoming one undo step).
        /// <paramref name="next"/> has <em>already been applied</em>; return true to absorb it into this
        /// action so a single Undo reverts both. Only offered when it was recorded shortly after this one.
        /// </summary>
        bool TryMerge(IUndoableAction next) => false;
    }

    /// <summary>
    /// An undo/redo history for ONE scope (the playlist, or one editor window). Not a global singleton:
    /// the owner of the scope owns its stack, so Ctrl+Z in one scope can never undo another's edits.
    ///
    /// Edits go in through <see cref="Execute"/> (apply + record) or <see cref="Record"/> (already applied,
    /// e.g. a value the UI wrote through a binding). UI thread only.
    /// </summary>
    public sealed class UndoStack : ReactiveObject
    {
        private readonly List<IUndoableAction> _undo = new(); // oldest first; top = last
        private readonly List<IUndoableAction> _redo = new();
        private readonly Func<DateTime> _now;

        private DateTime _lastRecordedAt = DateTime.MinValue;
        private int _transactionDepth;
        private List<IUndoableAction>? _transaction;
        private string _transactionDescription = "";

        public UndoStack() : this(() => DateTime.UtcNow, TimeSpan.FromSeconds(1.5))
        {
        }

        /// <param name="now">Clock, injectable for tests.</param>
        /// <param name="mergeWindow">An action is only offered to <see cref="IUndoableAction.TryMerge"/> if it
        /// was recorded within this long of the previous one.</param>
        public UndoStack(Func<DateTime> now, TimeSpan mergeWindow)
        {
            _now = now;
            MergeWindow = mergeWindow;

            UndoCommand = ReactiveCommand.Create(() => { Undo(); }, this.WhenAnyValue(x => x.CanUndo));
            RedoCommand = ReactiveCommand.Create(() => { Redo(); }, this.WhenAnyValue(x => x.CanRedo));
        }

        public TimeSpan MergeWindow { get; }

        /// <summary>Oldest steps are dropped beyond this many.</summary>
        public int MaxDepth { get; set; } = 200;

        /// <summary>True while an Undo/Redo is running. Edits made then are not recorded.</summary>
        public bool IsApplying { get; private set; }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public string? UndoDescription => _undo.Count > 0 ? _undo[^1].Description : null;
        public string? RedoDescription => _redo.Count > 0 ? _redo[^1].Description : null;
        public string UndoMenuText => UndoDescription is { } d ? $"Undo {d}" : "Undo";
        public string RedoMenuText => RedoDescription is { } d ? $"Redo {d}" : "Redo";

        public ReactiveCommand<Unit, Unit> UndoCommand { get; }
        public ReactiveCommand<Unit, Unit> RedoCommand { get; }

        /// <summary>Applies <paramref name="action"/> and records it. If <c>Do()</c> throws, nothing is recorded.</summary>
        public void Execute(IUndoableAction action)
        {
            if (IsApplying)
            {
                // An edit triggered *by* an undo/redo (a helper that calls back in) is part of that
                // undo, not new history.
                action.Do();
                return;
            }

            action.Do();
            Push(action);
        }

        /// <summary>Records an edit that has <em>already</em> been applied.</summary>
        public void Record(IUndoableAction action)
        {
            if (IsApplying) return;
            Push(action);
        }

        /// <summary>
        /// Groups everything executed/recorded until the returned scope is disposed into ONE undo step.
        /// Nests: only the outermost scope produces a step. Dispose on <c>using</c> exit, including on an
        /// exception, so edits that did apply stay undoable.
        /// </summary>
        public IDisposable BeginTransaction(string description)
        {
            if (_transactionDepth == 0)
            {
                _transaction = new List<IUndoableAction>();
                _transactionDescription = description;
            }

            _transactionDepth++;
            return new TransactionScope(this);
        }

        public bool Undo()
        {
            if (IsApplying || _transactionDepth > 0 || _undo.Count == 0) return false;

            var action = _undo[^1];
            if (!Apply(action, undo: true)) return false;

            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(action);
            AfterApply();
            return true;
        }

        public bool Redo()
        {
            if (IsApplying || _transactionDepth > 0 || _redo.Count == 0) return false;

            var action = _redo[^1];
            if (!Apply(action, undo: false)) return false;

            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(action);
            AfterApply();
            return true;
        }

        /// <summary>Forgets all history (playlist loaded / new).</summary>
        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _lastRecordedAt = DateTime.MinValue;
            NotifyChanged();
        }

        private bool Apply(IUndoableAction action, bool undo)
        {
            IsApplying = true;
            try
            {
                if (undo) action.Undo();
                else action.Do();
                return true;
            }
            catch (Exception ex)
            {
                // A half-applied or out-of-sync history is worse than none: later steps assume the state
                // earlier ones left behind. Drop it rather than risk corrupting the playlist further.
                Log.Error(ex, "[UNDO] {Direction} of '{Description}' failed; clearing undo history",
                    undo ? "Undo" : "Redo", action.Description);
                Clear();
                return false;
            }
            finally
            {
                IsApplying = false;
            }
        }

        private void AfterApply()
        {
            _lastRecordedAt = DateTime.MinValue; // a new edit after undo/redo never merges into the stepped-over one
            NotifyChanged();
        }

        private void Push(IUndoableAction action)
        {
            if (_transactionDepth > 0)
            {
                _transaction!.Add(action);
                return;
            }

            _redo.Clear();

            var now = _now();
            if (_undo.Count > 0 && now - _lastRecordedAt <= MergeWindow && _undo[^1].TryMerge(action))
            {
                _lastRecordedAt = now;
                NotifyChanged();
                return;
            }

            _undo.Add(action);
            if (MaxDepth > 0 && _undo.Count > MaxDepth) _undo.RemoveRange(0, _undo.Count - MaxDepth);
            _lastRecordedAt = now;
            NotifyChanged();
        }

        private void EndTransaction()
        {
            if (--_transactionDepth > 0) return;

            var actions = _transaction!;
            var description = _transactionDescription;
            _transaction = null;

            if (actions.Count > 0) Push(new CompositeAction(description, actions));
        }

        private void NotifyChanged()
        {
            this.RaisePropertyChanged(nameof(CanUndo));
            this.RaisePropertyChanged(nameof(CanRedo));
            this.RaisePropertyChanged(nameof(UndoDescription));
            this.RaisePropertyChanged(nameof(RedoDescription));
            this.RaisePropertyChanged(nameof(UndoMenuText));
            this.RaisePropertyChanged(nameof(RedoMenuText));
        }

        private sealed class TransactionScope : IDisposable
        {
            private UndoStack? _owner;
            public TransactionScope(UndoStack owner) => _owner = owner;

            public void Dispose()
            {
                _owner?.EndTransaction();
                _owner = null; // idempotent
            }
        }

        /// <summary>Several already-applied actions undone/redone as one step.</summary>
        private sealed class CompositeAction : IUndoableAction
        {
            private readonly IReadOnlyList<IUndoableAction> _actions;
            public CompositeAction(string description, IReadOnlyList<IUndoableAction> actions)
            {
                Description = description;
                _actions = actions.ToList();
            }

            public string Description { get; }

            public void Do()
            {
                foreach (var a in _actions) a.Do();
            }

            public void Undo()
            {
                for (var i = _actions.Count - 1; i >= 0; i--) _actions[i].Undo();
            }
        }
    }
}
