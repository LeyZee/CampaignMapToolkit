using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace CAIME
{
    public interface IUndoRedoHandler
    {
        void RestoreState(StateSnapshot snapshot);
        void ApplyState(StateSnapshot snapshot);
    }

    public abstract class StateSnapshot
    {
        /// <summary>
        /// Roughly how many per-hex records this snapshot holds. <see cref="UndoRedoManager"/>
        /// budgets on the total, because a single flood fill can record one entry per filled hex.
        /// </summary>
        public virtual int RecordCount => 1;
    }

    public class UndoRedoAction
    {
        public IUndoRedoHandler Owner       { get; private set; }
        public StateSnapshot    NewState    { get; private set; }
        public StateSnapshot    OldState    { get; private set; }
        public string           Name        { get; private set; }

        public UndoRedoAction(IUndoRedoHandler owner, StateSnapshot newState, StateSnapshot oldState, string name)
        {
            Owner       = owner;
            NewState    = newState;
            OldState    = oldState;
            Name        = name;
        }
    }

    public class UndoRedoManager : IHotkeyHandler
    {
        private const int STACK_DEPTH = 1000;

        /// <summary>
        /// Ceiling on the per-hex records held across both stacks. A flood fill snapshot can hold
        /// one record per filled hex (up to 731,520 on a full map), so the depth limit alone lets a
        /// handful of large fills retain hundreds of megabytes until the project closes. Small
        /// edits still get the full STACK_DEPTH of history.
        /// </summary>
        private const int RECORD_BUDGET = 4_000_000;

        private List<UndoRedoAction> undoActions;
        private List<UndoRedoAction> redoActions;

        private int retainedRecords;

        public int UndoActionsCount
        {
            get => undoActions.Count;
        }

        public int RedoActionsCount
        {
            get => redoActions.Count;
        }

        public UndoRedoManager()
        {
            undoActions = new List<UndoRedoAction>();
            redoActions = new List<UndoRedoAction>();
        }

        public ProcessHotkeyResult ProcessHotkeyCombination(Key[] keys)
        {
            bool ctrlHeld = keys.Length == 2 && (keys[0] == Key.LeftCtrl || keys[0] == Key.RightCtrl);

            if (ctrlHeld && keys[1] == Key.Z)
            {
                ExecuteUndoAction();
                return ProcessHotkeyResult.Handled;
            }
            else
            if (ctrlHeld && keys[1] == Key.Y)
            {
                ExecuteRedoAction();
                return ProcessHotkeyResult.Handled;
            }

            return ProcessHotkeyResult.Passed;
        }

        public bool PushUndoAction(UndoRedoAction action, bool clearRedo = true)
        {
            if (undoActions.IndexOf(action) != -1)
            {
#if DEBUG
                LoggerViewModel.Log("UndoRedoManager: PushUndoAction - new action is already present in the stack. Aborted.", LogLevel.Debug);
#endif
                return false;
            }

            if (undoActions.Count >= STACK_DEPTH)
            {
                DropOldestUndoAction();
#if DEBUG
                LoggerViewModel.Log("UndoRedoManager: PushUndoAction - stack depth reached. Oldest item removed.", LogLevel.Debug);
#endif
            }

            undoActions.Add(action);
            retainedRecords += RecordsIn(action);

            if (clearRedo)
            {
                foreach (var redoAction in redoActions)
                {
                    retainedRecords -= RecordsIn(redoAction);
                }

                redoActions.Clear();
            }

            TrimToRecordBudget();

            this.UpdateUndoRedoMenuStates();
#if DEBUG
            LoggerViewModel.Log("UndoRedoManager: PushUndoAction - successfully added a new Undo action.", LogLevel.Debug);
#endif
            return true;
        }

        public bool PushRedoAction(UndoRedoAction action)
        {
            if (redoActions.IndexOf(action) != -1)
            {
#if DEBUG
                LoggerViewModel.Log("UndoRedoManager: PushRedoAction - new action is already present in the stack. Aborted.", LogLevel.Debug);
#endif
                return false;
            }

            if (redoActions.Count >= STACK_DEPTH)
            {
                retainedRecords -= RecordsIn(redoActions[0]);
                redoActions.RemoveAt(0);
#if DEBUG
                LoggerViewModel.Log("UndoRedoManager: PushRedoAction - stack depth reached. Oldest item removed.", LogLevel.Debug);
#endif
            }

            redoActions.Add(action);
            retainedRecords += RecordsIn(action);
            this.UpdateUndoRedoMenuStates();
#if DEBUG
            LoggerViewModel.Log("UndoRedoManager: PushRedoAction - successfully added a new Redo action.", LogLevel.Debug);
#endif
            return true;
        }

        /// <summary>
        /// Drops all recorded actions. Must be called whenever the snapshots' targets become
        /// invalid: project close/reload, map resize, or anything else that re-indexes hexes.
        /// </summary>
        public void Clear()
        {
            undoActions.Clear();
            redoActions.Clear();
            retainedRecords = 0;
            this.UpdateUndoRedoMenuStates();
        }

        private static int RecordsIn(UndoRedoAction action)
        {
            return (action.NewState?.RecordCount ?? 0) + (action.OldState?.RecordCount ?? 0);
        }

        private void DropOldestUndoAction()
        {
            retainedRecords -= RecordsIn(undoActions[0]);
            undoActions.RemoveAt(0);
        }

        /// <summary>
        /// Evicts the oldest undo entries until the retained records fit the budget. The newest
        /// entry is always kept, however large it is, so an undo is never silently unavailable for
        /// the edit the user just made.
        /// </summary>
        private void TrimToRecordBudget()
        {
            while (retainedRecords > RECORD_BUDGET && undoActions.Count > 1)
            {
                DropOldestUndoAction();
            }
        }

        public void ExecuteUndoAction()
        {
            if (undoActions.Count == 0)
            {
                // Nothing to restore
                return;
            }

            var action = undoActions.Last();

            // The action moves from one stack to the other, so it is only popped once the redo
            // stack has taken it - otherwise a rejected push loses the redo entry entirely.
            if (PushRedoAction(action) == false)
            {
                return;
            }

            retainedRecords -= RecordsIn(action);
            undoActions.RemoveAt(undoActions.Count - 1);

            action.Owner.RestoreState(action.OldState);
            this.UpdateUndoRedoMenuStates();
        }

        public void ExecuteRedoAction()
        {
            if (redoActions.Count == 0)
            {
                // Nothing to re-apply
                return;
            }

            var action = redoActions.Last();

            if (PushUndoAction(action, clearRedo: false) == false)
            {
                return;
            }

            retainedRecords -= RecordsIn(action);
            redoActions.RemoveAt(redoActions.Count - 1);

            action.Owner.ApplyState(action.NewState);
            this.UpdateUndoRedoMenuStates();
        }

        private void UpdateUndoRedoMenuStates()
        {
            AppStateContext.Instance.IsUndoItemEnabled = UndoActionsCount > 0;
            AppStateContext.Instance.IsRedoItemEnabled = RedoActionsCount > 0;
        }
    }
}
