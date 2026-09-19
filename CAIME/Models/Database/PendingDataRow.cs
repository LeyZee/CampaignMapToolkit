using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CAIME
{
    public enum MarkedState
    {
        Unchanged,
        Added,
        Modified,
        Removed,

        Count,
    }

    public class DelayedDataTable : DataTable
    {
        public DelayedDataTable()
            : base()
        {}

        public DelayedDataTable(string tableName)
            : base(tableName)
        {}

        public DelayedDataTable(string tableName, string tableNamespace)
            : base(tableName, tableNamespace)
        {}

        public DelayedDataTable(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {}
        
        protected override Type GetRowType()
        {
            return typeof(PendingDataRow);
        }

        protected override DataRow NewRowFromBuilder(DataRowBuilder builder)
        {
            return new PendingDataRow(builder);
        }
    }

    public class PendingDataRow : DataRow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private MarkedState _markedState;
        public MarkedState MarkedState
        {
            get
            {
                return _markedState;
            }
            protected set
            {
                _markedState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkedState)));
            }
        }

        public PendingDataRow()
            : base(null)
        {
            MarkedState = MarkedState.Unchanged;
        }

        public PendingDataRow(DataRowBuilder builder)
            : base(builder)
        {
            MarkedState = MarkedState.Unchanged;
        }

        public void MarkAsModified()
        {
            MarkedState = MarkedState.Modified;
        }

        public void MarkAsRemoved()
        {
            MarkedState = MarkedState.Removed;
        }

        public void MarkAsAdded()
        {
            MarkedState = MarkedState.Added;
        }

        public void MarkAsUnchanged()
        {
            MarkedState = MarkedState.Unchanged;
        }
    }
}
