using System;
using System.Collections.Generic;
using System.Windows.Input;
using HandsLiftedApp.Core.Undo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Undo;

[TestClass]
public class UndoStackTests
{
    private sealed class LogAction : IUndoableAction
    {
        private readonly List<string> _log;
        private readonly string _name;
        public LogAction(List<string> log, string name) { _log = log; _name = name; }
        public string Description => _name;
        public void Do() => _log.Add("do:" + _name);
        public void Undo() => _log.Add("undo:" + _name);
    }

    /// <summary>Sets a value; merges with a following action on the same target (like typing).</summary>
    private sealed class SetValueAction : IUndoableAction
    {
        private readonly int[] _target;
        private readonly int _before;
        private int _after;
        public SetValueAction(int[] target, int after) { _target = target; _before = target[0]; _after = after; }
        public string Description => "Set value";
        public void Do() => _target[0] = _after;
        public void Undo() => _target[0] = _before;
        public bool TryMerge(IUndoableAction next)
        {
            if (next is SetValueAction n && ReferenceEquals(n._target, _target)) { _after = n._after; return true; }
            return false;
        }
    }

    private sealed class ThrowingUndoAction : IUndoableAction
    {
        public string Description => "Boom";
        public void Do() { }
        public void Undo() => throw new InvalidOperationException("cannot undo");
    }

    [TestMethod]
    public void Execute_AppliesImmediately_AndIsUndoableAndRedoable()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        stack.Execute(new LogAction(log, "a"));
        Assert.IsTrue(stack.CanUndo);
        Assert.IsFalse(stack.CanRedo);

        Assert.IsTrue(stack.Undo());
        Assert.IsFalse(stack.CanUndo);
        Assert.IsTrue(stack.CanRedo);

        Assert.IsTrue(stack.Redo());
        CollectionAssert.AreEqual(new[] { "do:a", "undo:a", "do:a" }, log);
    }

    [TestMethod]
    public void Undo_RunsNewestFirst()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.Execute(new LogAction(log, "a"));
        stack.Execute(new LogAction(log, "b"));

        stack.Undo();
        stack.Undo();

        CollectionAssert.AreEqual(new[] { "do:a", "do:b", "undo:b", "undo:a" }, log);
    }

    [TestMethod]
    public void NewEdit_DiscardsRedoHistory()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.Execute(new LogAction(log, "a"));
        stack.Undo();
        Assert.IsTrue(stack.CanRedo);

        stack.Execute(new LogAction(log, "b"));

        Assert.IsFalse(stack.CanRedo);
    }

    [TestMethod]
    public void UndoRedo_OnEmptyStack_ReturnFalse()
    {
        var stack = new UndoStack();

        Assert.IsFalse(stack.Undo());
        Assert.IsFalse(stack.Redo());
    }

    [TestMethod]
    public void Record_DoesNotApply_ButIsUndoable()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        stack.Record(new LogAction(log, "a")); // already applied elsewhere
        Assert.AreEqual(0, log.Count);

        stack.Undo();
        CollectionAssert.AreEqual(new[] { "undo:a" }, log);
    }

    [TestMethod]
    public void Execute_WhenDoThrows_RecordsNothing()
    {
        var stack = new UndoStack();

        Assert.ThrowsException<InvalidOperationException>(() => stack.Execute(new ThrowingDoAction()));

        Assert.IsFalse(stack.CanUndo);
    }

    private sealed class ThrowingDoAction : IUndoableAction
    {
        public string Description => "x";
        public void Do() => throw new InvalidOperationException();
        public void Undo() { }
    }

    [TestMethod]
    public void Transaction_GroupsActionsIntoOneStep_UndoneInReverse()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        using (stack.BeginTransaction("Delete 2 slides"))
        {
            stack.Execute(new LogAction(log, "a"));
            stack.Execute(new LogAction(log, "b"));
        }

        Assert.AreEqual("Delete 2 slides", stack.UndoDescription);
        stack.Undo();
        Assert.IsFalse(stack.CanUndo, "the two actions were a single step");
        stack.Redo();

        CollectionAssert.AreEqual(new[] { "do:a", "do:b", "undo:b", "undo:a", "do:a", "do:b" }, log);
    }

    [TestMethod]
    public void Transaction_Nested_OnlyOutermostMakesAStep()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        using (stack.BeginTransaction("outer"))
        {
            stack.Execute(new LogAction(log, "a"));
            using (stack.BeginTransaction("inner"))
            {
                stack.Execute(new LogAction(log, "b"));
            }

            Assert.IsFalse(stack.CanUndo, "nothing is a step until the outermost scope closes");
        }

        Assert.AreEqual("outer", stack.UndoDescription);
        stack.Undo();
        Assert.IsFalse(stack.CanUndo);
    }

    [TestMethod]
    public void Transaction_Empty_RecordsNothing()
    {
        var stack = new UndoStack();

        using (stack.BeginTransaction("nothing happened")) { }

        Assert.IsFalse(stack.CanUndo);
    }

    [TestMethod]
    public void Transaction_ExceptionMidway_KeepsAppliedEditsUndoable()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        try
        {
            using (stack.BeginTransaction("partial"))
            {
                stack.Execute(new LogAction(log, "a"));
                throw new InvalidOperationException();
            }
        }
        catch (InvalidOperationException)
        {
        }

        Assert.IsTrue(stack.CanUndo, "'a' really happened, so it has to be undoable");
        stack.Undo();
        CollectionAssert.AreEqual(new[] { "do:a", "undo:a" }, log);
    }

    [TestMethod]
    public void UndoRedo_IgnoredWhileTransactionOpen()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.Execute(new LogAction(log, "a"));

        using (stack.BeginTransaction("t"))
        {
            Assert.IsFalse(stack.Undo());
        }
    }

    [TestMethod]
    public void Merge_WithinWindow_BecomesOneStep()
    {
        var now = new DateTime(2026, 1, 1);
        var stack = new UndoStack(() => now, TimeSpan.FromSeconds(1));
        var value = new[] { 0 };

        stack.Execute(new SetValueAction(value, 1));
        now = now.AddMilliseconds(300);
        stack.Execute(new SetValueAction(value, 2));
        now = now.AddMilliseconds(300);
        stack.Execute(new SetValueAction(value, 3));

        Assert.AreEqual(3, value[0]);
        stack.Undo();
        Assert.AreEqual(0, value[0], "one Undo reverts the whole burst");
        Assert.IsFalse(stack.CanUndo);
    }

    [TestMethod]
    public void Merge_AfterWindowElapsed_StaysSeparate()
    {
        var now = new DateTime(2026, 1, 1);
        var stack = new UndoStack(() => now, TimeSpan.FromSeconds(1));
        var value = new[] { 0 };

        stack.Execute(new SetValueAction(value, 1));
        now = now.AddSeconds(5);
        stack.Execute(new SetValueAction(value, 2));

        stack.Undo();
        Assert.AreEqual(1, value[0]);
        stack.Undo();
        Assert.AreEqual(0, value[0]);
    }

    [TestMethod]
    public void Merge_NeverAcrossAnUndo()
    {
        var now = new DateTime(2026, 1, 1);
        var stack = new UndoStack(() => now, TimeSpan.FromSeconds(10));
        var value = new[] { 0 };

        stack.Execute(new SetValueAction(value, 1));
        stack.Execute(new SetValueAction(value, 2)); // merged: one step 0 -> 2
        stack.Undo();                                  // back to 0
        stack.Execute(new SetValueAction(value, 5));   // must not fold into anything stepped over

        stack.Undo();
        Assert.AreEqual(0, value[0]);
        Assert.IsFalse(stack.CanUndo);
    }

    [TestMethod]
    public void MaxDepth_DropsOldestSteps()
    {
        var log = new List<string>();
        var stack = new UndoStack { MaxDepth = 3 };

        for (var i = 0; i < 5; i++) stack.Execute(new LogAction(log, i.ToString()));

        var undone = 0;
        while (stack.Undo()) undone++;
        Assert.AreEqual(3, undone);
        Assert.AreEqual("undo:2", log[^1], "steps 0 and 1 were dropped, 2 is the oldest left");
    }

    [TestMethod]
    public void EditsMadeDuringUndo_AreNotRecorded()
    {
        var stack = new UndoStack();
        var inner = new LogAction(new List<string>(), "inner");
        stack.Execute(new CallbackAction(() => { }, () => stack.Execute(inner))); // undo triggers a nested Execute

        stack.Undo();

        Assert.IsFalse(stack.CanUndo);
        Assert.IsTrue(stack.CanRedo, "only the original step is on the redo stack, not the nested edit");
        Assert.IsTrue(stack.Redo());
        Assert.IsFalse(stack.CanRedo);
    }

    private sealed class CallbackAction : IUndoableAction
    {
        private readonly Action _do;
        private readonly Action _undo;
        public CallbackAction(Action @do, Action undo) { _do = @do; _undo = undo; }
        public string Description => "callback";
        public void Do() => _do();
        public void Undo() => _undo();
    }

    [TestMethod]
    public void FailedUndo_ClearsHistory_AndReportsFalse()
    {
        var log = new List<string>();
        var stack = new UndoStack();
        stack.Execute(new LogAction(log, "a"));
        stack.Execute(new ThrowingUndoAction());

        Assert.IsFalse(stack.Undo());

        Assert.IsFalse(stack.CanUndo);
        Assert.IsFalse(stack.CanRedo, "an out-of-sync history is dropped entirely");
        Assert.IsFalse(stack.IsApplying);
    }

    [TestMethod]
    public void Clear_EmptiesBothStacks()
    {
        var stack = new UndoStack();
        stack.Execute(new LogAction(new List<string>(), "a"));
        stack.Execute(new LogAction(new List<string>(), "b"));
        stack.Undo();

        stack.Clear();

        Assert.IsFalse(stack.CanUndo);
        Assert.IsFalse(stack.CanRedo);
    }

    [TestMethod]
    public void Descriptions_AndMenuText_TrackTheTopOfEachStack()
    {
        var stack = new UndoStack();
        Assert.AreEqual("Undo", stack.UndoMenuText);
        Assert.AreEqual("Redo", stack.RedoMenuText);

        stack.Execute(new LogAction(new List<string>(), "Move Item Up"));
        Assert.AreEqual("Undo Move Item Up", stack.UndoMenuText);

        stack.Undo();
        Assert.AreEqual("Undo", stack.UndoMenuText);
        Assert.AreEqual("Redo Move Item Up", stack.RedoMenuText);
    }

    [TestMethod]
    public void RaisesPropertyChanged_ForBindableState()
    {
        var stack = new UndoStack();
        var changed = new List<string>();
        stack.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        stack.Execute(new LogAction(new List<string>(), "a"));

        CollectionAssert.IsSubsetOf(new[] { "CanUndo", "UndoMenuText" }, changed);
    }

    [TestMethod]
    public void Commands_ReflectCanUndoCanRedo_AndDriveTheStack()
    {
        var stack = new UndoStack();
        var undo = (ICommand)stack.UndoCommand;
        var redo = (ICommand)stack.RedoCommand;
        Assert.IsFalse(undo.CanExecute(null));
        Assert.IsFalse(redo.CanExecute(null));

        stack.Execute(new LogAction(new List<string>(), "a"));
        Assert.IsTrue(undo.CanExecute(null));

        undo.Execute(null);
        Assert.IsFalse(stack.CanUndo);
        Assert.IsTrue(redo.CanExecute(null));
    }
}
