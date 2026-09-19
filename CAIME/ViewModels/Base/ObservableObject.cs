using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CAIME {
    /// <summary>
    /// Observable object class
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged {
        /// <summary>
        /// Property changed event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Call this method to notify the view that the property has changed and needs updating
        /// </summary>
        /// <param name="info">Caller member name</param>
        protected void OnPropertyChanged([CallerMemberName]string info = "") {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        /// <summary>
        /// Set new value
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="backingField">Field name</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Property name</param>
        /// <returns></returns>
        protected bool SetValue<T>(ref T backingField, T value, [CallerMemberName]string propertyName = "") {
            if (object.Equals(backingField, value)) {
                return false;
            }

            backingField = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }
    }
}
