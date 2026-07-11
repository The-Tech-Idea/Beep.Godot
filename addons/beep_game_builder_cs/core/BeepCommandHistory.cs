using System;
using System.Collections.Generic;

namespace Beep.GameBuilder;

/// <summary>Undo/redo command pattern. Push commands, then Undo() / Redo().</summary>
public class BeepCommandHistory
{
    private Stack<ICommand> _undoStack = new();
    private Stack<ICommand> _redoStack = new();
    private int _maxHistory = 50;

    public int MaxHistory { get => _maxHistory; set => _maxHistory = value; }
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public event Action Changed;

    public interface ICommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    public void Execute(ICommand cmd)
    {
        cmd.Execute();
        _undoStack.Push(cmd);
        _redoStack.Clear();
        while (_undoStack.Count > _maxHistory) { /* Would need a proper deque, skip for now */ }
        Changed?.Invoke();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
        Changed?.Invoke();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        Changed?.Invoke();
    }

    public void Clear() { _undoStack.Clear(); _redoStack.Clear(); Changed?.Invoke(); }

    public string UndoDescription => CanUndo ? _undoStack.Peek().Description : "";
    public string RedoDescription => CanRedo ? _redoStack.Peek().Description : "";

    /// <summary>Convenience: create a simple command from execute/undo lambdas.</summary>
    public static ICommand Create(string desc, Action execute, Action undo) => new SimpleCommand(desc, execute, undo);

    private class SimpleCommand : ICommand
    {
        private string _desc; private Action _exec, _undo;
        public SimpleCommand(string d, Action e, Action u) { _desc = d; _exec = e; _undo = u; }
        public void Execute() => _exec?.Invoke();
        public void Undo() => _undo?.Invoke();
        public string Description => _desc;
    }
}
