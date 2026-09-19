using System;
using System.Collections.Generic;
using System.Windows;

namespace CAIME
{
    public class RenameGroundTypeEvent : RoutedEventArgs
    {
        public string   OldGroundTypeName   { get; private set; }
        public string   NewGroundTypeName   { get; private set; }
        public sbyte    OldGroundTypeIndex  { get; private set; }
        public sbyte    NewGroundTypeIndex  { get; private set; }

        public RenameGroundTypeEvent(string oldGroundTypeName, string newGroundTypeName, sbyte oldGroundTypeIndex, sbyte newGroundTypeIndex)
        {
            OldGroundTypeName   = oldGroundTypeName;
            NewGroundTypeName   = newGroundTypeName;
            OldGroundTypeIndex  = oldGroundTypeIndex;
            NewGroundTypeIndex  = newGroundTypeIndex;
        }
    }
    public class CreateGroundTypeEvent : RoutedEventArgs
    {
        public string   NewGroundTypeName   { get; private set; }
        public sbyte    NewGroundTypeIndex  { get; private set; }
        public bool     IsSea               { get; private set; }

        public CreateGroundTypeEvent(string newGroundTypeName, sbyte newGroundTypeIndex, bool isSea)
        {
            NewGroundTypeName   = newGroundTypeName;
            NewGroundTypeIndex  = newGroundTypeIndex;
            IsSea               = isSea;
        }
    }
    public class RemoveGroundTypeEvent : RoutedEventArgs
    {
        public string   GroundTypeName      { get; private set; }
        public sbyte    GroundTypeIndex     { get; private set; }

        public RemoveGroundTypeEvent(string groundTypeName, sbyte groundTypeIndex)
        {
            GroundTypeName  = groundTypeName;
            GroundTypeIndex = groundTypeIndex;
        }
    }

    public delegate void RenameGroundTypeHandler(object sender, RenameGroundTypeEvent e);
    public delegate void CreateGroundTypeHandler(object sender, CreateGroundTypeEvent e);
    public delegate void RemoveGroundTypeHandler(object sender, RemoveGroundTypeEvent e);

    public partial class MapHexEditor
    {
        public event RenameGroundTypeHandler OnGroundTypeRenamed;
        public event CreateGroundTypeHandler OnGroundTypeCreated;
        public event RemoveGroundTypeHandler OnGroundTypeRemoved;

        public sbyte CreateNewGroundType(string name, bool isSea)
        {
            var landIndex   = MapHexFile.LandGroundTypes.IndexOf(name);
            var seaIndex    = MapHexFile.SeaGroundTypes.IndexOf(name);

            if (landIndex != Hex.INVALID_GROUND_TYPE_INDEX)
            {
                return Hex.INVALID_GROUND_TYPE_INDEX;
            }

            if (seaIndex != Hex.INVALID_GROUND_TYPE_INDEX)
            {
                return Hex.INVALID_GROUND_TYPE_INDEX;
            }

            //There's a limit on the number of ground types with unique movment costs
            //I think this check is conservative since it's possible to have groundtypes with the same costs
            //But 59 ground types is a ludicrous amount, doubt anyone will run into this problem
            if (MapHexFile.LandGroundTypes.Count + MapHexFile.SeaGroundTypes.Count >= 58)
            {
                return Hex.INVALID_GROUND_TYPE_INDEX;
            }

            sbyte newGroundTypeIndex;

            if (isSea)
            {
                MapHexFile.SeaGroundTypes.Add(name);

                this.SortGroundTypes(true);

                newGroundTypeIndex = (sbyte)(MapHexFile.SeaGroundTypes.IndexOf(name) + MapHexFile.LandGroundTypes.Count);
            }
            else
            {
                MapHexFile.LandGroundTypes.Add(name);

                this.SortGroundTypes(false);

                newGroundTypeIndex = (sbyte)MapHexFile.LandGroundTypes.IndexOf(name);
            }

            this.UpdateGroundTypeIndices(newGroundTypeIndex, ChangeOperation.Create);
            OnGroundTypeCreated?.Invoke(this, new CreateGroundTypeEvent(name, newGroundTypeIndex, isSea));

            MapHexFile.SetDirty();
            return newGroundTypeIndex;
        }

        public bool DeleteGroundType(string name)
        {
            var landGroundTypeIndex = (sbyte)MapHexFile.LandGroundTypes.IndexOf(name);
            var seaGroundTypeIndex  = (sbyte)MapHexFile.SeaGroundTypes.IndexOf(name);

            if (landGroundTypeIndex != Hex.INVALID_GROUND_TYPE_INDEX)
            {
                if (MapHexFile.LandGroundTypes.Remove(name) == false)
                {
                    return false;
                }

                this.UpdateGroundTypeIndices(landGroundTypeIndex, ChangeOperation.Delete);
                OnGroundTypeRemoved?.Invoke(this, new RemoveGroundTypeEvent(name, landGroundTypeIndex));

                MapHexFile.SetDirty();
                return true;
            }

            if (seaGroundTypeIndex != Hex.INVALID_GROUND_TYPE_INDEX)
            {
                if (MapHexFile.SeaGroundTypes.Remove(name) == false)
                {
                    return false;
                }

                seaGroundTypeIndex += (sbyte)MapHexFile.LandGroundTypes.Count;
                this.UpdateGroundTypeIndices(seaGroundTypeIndex, ChangeOperation.Delete);
                OnGroundTypeRemoved?.Invoke(this, new RemoveGroundTypeEvent(name, seaGroundTypeIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool DeleteGroundType(sbyte groundTypeIndex)
        {
            if (groundTypeIndex >= MapHexFile.LandGroundTypes.Count + MapHexFile.SeaGroundTypes.Count || groundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX)
            {
                return false;
            }

            string groundTypeName;
            var isSea = groundTypeIndex >= MapHexFile.LandGroundTypes.Count;
            if (isSea)
            {
                groundTypeIndex -= (sbyte)MapHexFile.LandGroundTypes.Count;
                groundTypeName  = MapHexFile.SeaGroundTypes[groundTypeIndex];
                MapHexFile.SeaGroundTypes.RemoveAt(groundTypeIndex);
            }
            else
            {
                groundTypeName = MapHexFile.LandGroundTypes[groundTypeIndex];
                MapHexFile.LandGroundTypes.RemoveAt(groundTypeIndex);
            }

            this.UpdateGroundTypeIndices(groundTypeIndex, ChangeOperation.Delete);
            OnGroundTypeRemoved?.Invoke(this, new RemoveGroundTypeEvent(groundTypeName, groundTypeIndex));

            MapHexFile.SetDirty();
            return true;
        }

        public bool RenameGroundType(string oldName, string newName)
        {
            sbyte landGroundTypeIndex = (sbyte)MapHexFile.LandGroundTypes.IndexOf(oldName);
            sbyte seaGroundTypeIndex  = (sbyte)MapHexFile.SeaGroundTypes.IndexOf(oldName);

            if (landGroundTypeIndex != Hex.INVALID_GROUND_TYPE_INDEX)
            {
                MapHexFile.LandGroundTypes[landGroundTypeIndex] = newName;

                this.SortGroundTypes(false);

                var newGroundTypeIndex = (sbyte)MapHexFile.LandGroundTypes.IndexOf(newName);
                this.UpdateGroundTypeIndices(newGroundTypeIndex, ChangeOperation.Rename, landGroundTypeIndex);
                OnGroundTypeRenamed?.Invoke(this, new RenameGroundTypeEvent(oldName, newName, landGroundTypeIndex, newGroundTypeIndex));

                MapHexFile.SetDirty();
                return true;
            }

            if (seaGroundTypeIndex != Hex.INVALID_GROUND_TYPE_INDEX)
            {
                seaGroundTypeIndex += (sbyte)MapHexFile.LandGroundTypes.Count;
                MapHexFile.SeaGroundTypes[seaGroundTypeIndex] = newName;

                this.SortGroundTypes(true);

                var newGroundTypeIndex = (sbyte)(MapHexFile.SeaGroundTypes.IndexOf(newName) + MapHexFile.LandGroundTypes.Count);
                this.UpdateGroundTypeIndices(newGroundTypeIndex, ChangeOperation.Rename, seaGroundTypeIndex);
                OnGroundTypeRenamed?.Invoke(this, new RenameGroundTypeEvent(oldName, newName, seaGroundTypeIndex, newGroundTypeIndex));

                MapHexFile.SetDirty();
                return true;
            }

            return false;
        }

        public bool RenameGroundType(sbyte groundTypeIndex, string newName)
        {
            if (groundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX || groundTypeIndex >= MapHexFile.LandGroundTypes.Count + MapHexFile.SeaGroundTypes.Count)
            {
                return false;
            }

            string oldName;
            sbyte newGroundTypeIndex;

            var isSea = groundTypeIndex >= MapHexFile.LandGroundTypes.Count;
            if (isSea)
            {
                groundTypeIndex -= (sbyte)MapHexFile.LandGroundTypes.Count;
                oldName         = MapHexFile.SeaGroundTypes[groundTypeIndex];

                MapHexFile.SeaGroundTypes[groundTypeIndex] = newName;

                this.SortGroundTypes(true);

                newGroundTypeIndex = (sbyte)(MapHexFile.SeaGroundTypes.IndexOf(newName) + MapHexFile.LandGroundTypes.Count);
            }
            else
            {
                oldName = MapHexFile.LandGroundTypes[groundTypeIndex];

                MapHexFile.LandGroundTypes[groundTypeIndex] = newName;

                this.SortGroundTypes(false);

                newGroundTypeIndex = (sbyte)MapHexFile.LandGroundTypes.IndexOf(newName);
            }

            this.UpdateGroundTypeIndices(newGroundTypeIndex, ChangeOperation.Rename, groundTypeIndex);
            OnGroundTypeRenamed?.Invoke(this, new RenameGroundTypeEvent(oldName, newName, groundTypeIndex, newGroundTypeIndex));

            MapHexFile.SetDirty();
            return true;
        }

        private void UpdateGroundTypeIndices(sbyte groundTypeIndex, ChangeOperation op, sbyte oldGroundTypeIndex = Hex.INVALID_GROUND_TYPE_INDEX)
        {
            if (op == ChangeOperation.Rename && groundTypeIndex == oldGroundTypeIndex)
            {
                // No actions required
                return;
            }

            for (int hexIndex = 0; hexIndex < MapHexFile.Capacity; ++hexIndex)
            {
                var hex = MapHexFile.HexData[hexIndex];

                if (op == ChangeOperation.Delete)
                {
                    if (hex.GroundTypeIndex == groundTypeIndex)
                    {
                        hex.GroundTypeIndex = Hex.INVALID_GROUND_TYPE_INDEX;
                    }
                    else
                    if (hex.GroundTypeIndex > groundTypeIndex)
                    {
                        hex.GroundTypeIndex -= 1;
                    }
                }
                else
                if (op == ChangeOperation.Create)
                {
                    if (hex.GroundTypeIndex >= groundTypeIndex)
                    {
                        hex.GroundTypeIndex += 1;
                    }
                }
                else
                if (op == ChangeOperation.Rename)
                {
                    if (hex.GroundTypeIndex == oldGroundTypeIndex)
                    {
                        hex.GroundTypeIndex = groundTypeIndex;
                    }
                    else
                    if (hex.GroundTypeIndex >= oldGroundTypeIndex && hex.GroundTypeIndex <= groundTypeIndex)
                    {
                        hex.GroundTypeIndex -= 1;
                    }
                    else
                    if (hex.GroundTypeIndex >= groundTypeIndex && hex.GroundTypeIndex <= oldGroundTypeIndex)
                    {
                        hex.GroundTypeIndex += 1;
                    }
                }
            }
        }
    
        private void SortGroundTypes(bool isSea)
        {
            if (isSea)
            {
                MapHexFile.SeaGroundTypes.Sort(StringComparer.Ordinal);
            }
            else
            {
                MapHexFile.LandGroundTypes.Sort(StringComparer.Ordinal);
            }
        }
    }
}
