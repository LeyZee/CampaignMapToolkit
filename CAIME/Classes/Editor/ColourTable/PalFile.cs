using System;
using System.IO;

namespace CAIME
{
    public class ColoursContainer
    {
        public bool     IsDirty { get; private set; }
        public int[]    Colours { get; private set; }

        public ColoursContainer(int[] colours = null)
        {
            IsDirty = false;
            Colours = colours;

            if (Colours == null)
            {
                Colours = new int[0];
            }
        }

        public void Reserve(int newSize)
        {
            Colours = new int[newSize];
            IsDirty = true;
        }

        public void SetDirty(bool dirty)
        {
            IsDirty = dirty;
        }

        public void SetColours(int[] colours)
        {
            Colours = colours;
            IsDirty = true;
        }

        public bool AppendColours(int[] coloursToAppend, bool replaceDuplicateWithRandom = false)
        {
            var newColours = new int[Colours.Length + coloursToAppend.Length];
            Array.Copy(Colours, newColours, Colours.Length);

            for (int i = 0; i < coloursToAppend.Length; ++i)
            {
                var colour = coloursToAppend[i];
                if (replaceDuplicateWithRandom)
                {
                    if (this.ContainsColour(colour))
                    {
                        colour = ColourTable.GenerateRandomColour();
                    }
                }

                newColours[Colours.Length + i] = colour;
            }

            Colours = newColours;
            IsDirty = true;

            return true;
        }

        public bool AppendColour(int newColour)
        {
            if (this.ContainsColour(newColour))
            {
                return false;
            }

            var newColours = new int[Colours.Length + 1];

            Array.Copy(Colours, newColours, Colours.Length);
            newColours[Colours.Length] = newColour;

            Colours = newColours;
            IsDirty = true;

            return true;
        }

        public bool InsertColour(int newColour, int index)
        {
            if (this.ContainsColour(newColour))
            {
                return false;
            }

            var newColours = new int[Colours.Length + 1];

            Array.Copy(Colours, 0, newColours, 0, index);
            newColours[index] = newColour;
            Array.Copy(Colours, index, newColours, index + 1, Colours.Length-index);

            Colours = newColours;
            IsDirty = true;

            return true;
        }

        public bool DeleteColour(int indexToDelete)
        {
            if (indexToDelete < 0 || indexToDelete >= Colours.Length)
            {
                return false;
            }

            var newColours = new int[Colours.Length - 1];

            for (int index = 0; index < newColours.Length; ++index)
            {
                if (index < indexToDelete)
                {
                    newColours[index] = Colours[index];
                }
                else
                {
                    newColours[index] = Colours[index + 1];
                }
            }

            Colours = newColours;
            IsDirty = true;

            return true;
        }

        public bool ReplaceColour(int oldColour, int newColour)
        {
            if (this.ContainsColour(oldColour) == false)
            {
                return false;
            }

            if (this.ContainsColour(newColour))
            {
                return false;
            }

            var colourIndex = this.IndexOf(oldColour);
            if (colourIndex == -1)
            {
                return false;
            }

            Colours[colourIndex] = newColour;
            IsDirty = true;

            return true;
        }

        public void Trim(int startTrimIndex)
        {
            if (Colours.Length == startTrimIndex)
            {
                return;
            }

            var newColours = new int[startTrimIndex];
            Array.Copy(Colours, newColours, newColours.Length);

            Colours = newColours;
            IsDirty = true;
        }

        public bool ContainsColour(int colour)
        {
            return IndexOf(colour) != -1;
        }

        private int IndexOf(int colour)
        {
            for (int index = 0; index < Colours.Length; ++index)
            {
                if (Colours[index] == colour)
                {
                    return index;
                }
            }

            return -1;
        }
    }

    public class PalFile
    {
        private const int       FILE_SIGNATURE      = 0x46464952;
        private const int       FILE_TAG            = 0x204C4150;
        private const int       CHUNK_SIGNATURE     = 0x61746164;
        private const ushort    FILE_VERSION        = 768;

        private readonly string _filePath;

        public bool IsDirty
        {
            get => Container.IsDirty;
        }
        public int[] Colours
        {
            get => Container.Colours;
        }

        public ColoursContainer Container { get; private set; }

        public PalFile(string filePath, int[] colours = null)
        {
            _filePath = filePath;
            Container = new ColoursContainer(colours);
        }

        public bool Load()
        {
            if (_filePath == null || _filePath.Length == 0)
            {
                return false;
            }

            using (var br = new BinaryReader(new FileStream(_filePath, FileMode.Open, FileAccess.Read)))
            {
                int fileSignature = br.ReadInt32();
                if (fileSignature != FILE_SIGNATURE)
                {
                    return false;
                }

                var fileLength = br.ReadInt32();
                if (fileLength == 0)
                {
                    return false;
                }

                var fileTag = br.ReadInt32();
                if (fileTag != FILE_TAG)
                {
                    return false;
                }

                var chunkSignature = br.ReadInt32();
                if (chunkSignature != CHUNK_SIGNATURE)
                {
                    return false;
                }

                var chunkSize = br.ReadInt32();
                if (chunkSize == 0)
                {
                    return false;
                }

                var fileVersion = br.ReadUInt16();
                if (fileVersion != FILE_VERSION)
                {
                    return false;
                }

                var coloursCount = br.ReadUInt16();
                Container.Reserve(coloursCount);
                for (int index = 0; index < coloursCount; ++index)
                {
                    Container.Colours[index] = br.ReadInt32();
                }
            }

            return true;
        }

        public bool Save()
        {
            if (_filePath == null || _filePath.Length == 0)
            {
                return false;
            }

            using (var bw = new BinaryWriter(new FileStream(_filePath, FileMode.Create, FileAccess.Write)))
            {
                int fileLength = 16 + Colours.Length * 4;
                int chunkSize  = 4  + Colours.Length * 4;

                bw.Write(FILE_SIGNATURE);
                bw.Write(fileLength);
                bw.Write(FILE_TAG);
                bw.Write(CHUNK_SIGNATURE);
                bw.Write(chunkSize);
                bw.Write(FILE_VERSION);
                bw.Write((ushort)Colours.Length);

                for (int index = 0; index < Colours.Length; ++index)
                {
                    bw.Write(Colours[index]);
                }
            }

            Container.SetDirty(false);
            return true;
        }

        public void SetColours(int[] newColours)
        {
            Container.SetColours(newColours);
        }
    }
}
