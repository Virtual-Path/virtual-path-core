using System;
using System.Collections.Generic;

namespace VirtualPathCore.Services.UndoRedo;

public class UndoRedoManager
{
    private static readonly UndoRedoManager _instance = new();
    public static UndoRedoManager Instance => _instance;

    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private const int MaxUndoLevels = 100;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event Action? StateChanged;

    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        TrimStacks();
        StateChanged?.Invoke();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        StateChanged?.Invoke();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        StateChanged?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke();
    }

    private void TrimStacks()
    {
        if (_undoStack.Count > MaxUndoLevels)
        {
            var temp = new Stack<IUndoableCommand>(_undoStack.ToArray()[^MaxUndoLevels..]);
            _undoStack.Clear();
            foreach (var cmd in temp)
                _undoStack.Push(cmd);
        }
    }
}
