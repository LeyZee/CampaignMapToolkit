using System;
using System.Windows.Input;

namespace CAIME
{
    /// <summary>
    /// A basic command that runs an Action
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        /// <summary>
        /// The action to run
        /// </summary>
        private readonly Action<T> action;
        private readonly Predicate<T> canExecute;

        /// <summary>
        /// The event thats fired when the <see cref="CanExecute(object)"/> value has changed
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<T> action, Predicate<T> canExecute = null)
        {
            if (action == null)
            {
                throw new ArgumentNullException("RelayCommand - action is null");
            }

            this.action = action;
            this.canExecute = canExecute;
        }

        /// <summary>
        /// A relay command can always execute
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public virtual bool CanExecute(object parameter)
        {
            return this.canExecute == null || this.canExecute((T)parameter);
        }

        /// <summary>
        /// Executes the commands Action
        /// </summary>
        /// <param name="parameter"></param>
        public virtual void Execute(object parameter)
        {
            action((T)parameter);
        }
    }

    public class RelayCommand : RelayCommand<object>
    {
        public RelayCommand(Action<object> action, Predicate<object> predicate) : base(action, predicate)
        {}
    }
}
