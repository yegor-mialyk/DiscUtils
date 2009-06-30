//
// Copyright (c) 2008-2009, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DirectoryIndexEntry = System.Collections.Generic.KeyValuePair<DiscUtils.Ntfs.FileNameRecord, DiscUtils.Ntfs.FileReference>;

namespace DiscUtils.Ntfs
{
    internal class Directory : File
    {
        private IndexView<FileNameRecord, FileReference> _index;


        public Directory(INtfsContext context, FileRecord baseRecord)
            : base(context, baseRecord)
        {
        }

        internal DirectoryEntry GetEntryByName(string name)
        {
            string searchName = name;

            int streamSepPos = name.IndexOf(':');
            if (streamSepPos >= 0)
            {
                searchName = name.Substring(0, streamSepPos);
            }

            DirectoryIndexEntry entry = Index.FindFirst(new FileNameQuery(searchName, _context.UpperCase));
            if (entry.Key != null && entry.Value != null)
            {
                return new DirectoryEntry(this, entry.Value, entry.Key);
            }
            else
            {
                return null;
            }
        }

        public bool IsEmpty
        {
            get { return Index.Count == 0; }
        }

        public IEnumerable<DirectoryEntry> GetAllEntries()
        {
            List<DirectoryIndexEntry> entries = FilterEntries(Index.Entries);

            foreach (var entry in entries)
            {
                yield return new DirectoryEntry(this, entry.Value, entry.Key);
            }
        }

        public void UpdateEntry(DirectoryEntry entry)
        {
            Index[entry.Details] = entry.Reference;
            UpdateRecordInMft();
        }


        internal DirectoryEntry AddEntry(File file, string name)
        {
            if (name.Length > 255)
            {
                throw new IOException("Invalid file name, more than 255 characters: " + name);
            }
            else if(name.IndexOfAny(new char[] { '\0', '/' }) != -1)
            {
                throw new IOException(@"Invalid file name, contains '\0' or '/': " + name);
            }

            FileNameRecord newNameRecord = file.GetFileNameRecord(null, true);
            newNameRecord.FileNameNamespace = FileNameNamespace.Posix;
            newNameRecord.FileName = name;
            newNameRecord.ParentDirectory = MftReference;

            ushort newNameAttrId = file.CreateAttribute(AttributeType.FileName);
            file.SetAttributeContent(newNameAttrId, newNameRecord);

            file.HardLinkCount++;
            file.UpdateRecordInMft();

            Index[newNameRecord] = file.MftReference;

            Modified();
            UpdateRecordInMft();

            return new DirectoryEntry(this, file.MftReference, newNameRecord);
        }

        internal void RemoveEntry(DirectoryEntry dirEntry)
        {
            File file = _context.GetFileByRef(dirEntry.Reference);

            FileNameRecord nameRecord = dirEntry.Details;

            Index.Remove(dirEntry.Details);

            foreach (StructuredNtfsAttribute<FileNameRecord> fnrAttr in file.GetAttributes(AttributeType.FileName))
            {
                if (nameRecord.Equals(fnrAttr.Content))
                {
                    file.RemoveAttribute(fnrAttr.Id);
                }
            }

            file.HardLinkCount--;
            file.UpdateRecordInMft();

            Modified();
            UpdateRecordInMft();
        }

        internal new static Directory CreateNew(INtfsContext context)
        {
            DateTime now = DateTime.UtcNow;

            Directory dir = (Directory)context.AllocateFile(FileRecordFlags.IsDirectory);

            ushort attrId = dir.CreateAttribute(AttributeType.StandardInformation);
            StandardInformation si = new StandardInformation();
            si.CreationTime = now;
            si.ModificationTime = now;
            si.MftChangedTime = now;
            si.LastAccessTime = now;
            si.FileAttributes = FileAttributeFlags.Archive;
            dir.SetAttributeContent(attrId, si);

            // Create the index root attribute by instantiating a new index
            dir.CreateIndex("$I30", AttributeType.FileName, AttributeCollationRule.Filename);

            dir.UpdateRecordInMft();

            return dir;
        }

        public override void Dump(TextWriter writer, string indent)
        {
            writer.WriteLine(indent + "DIRECTORY (" + base.ToString() + ")");
            writer.WriteLine(indent + "  File Number: " + IndexInMft);

            if (Index != null)
            {
                foreach (var entry in Index.Entries)
                {
                    writer.WriteLine(indent + "  DIRECTORY ENTRY (" + entry.Key.FileName + ")");
                    writer.WriteLine(indent + "    MFT Ref: " + entry.Value);
                    entry.Key.Dump(writer, indent + "    ");
                }
            }
        }

        public override string ToString()
        {
            return base.ToString() + @"\";
        }

        private IndexView<FileNameRecord, FileReference> Index
        {
            get
            {
                if (_index == null && GetAttribute(AttributeType.IndexRoot, "$I30") != null)
                {
                    _index = new IndexView<FileNameRecord, FileReference>(GetIndex("$I30"));
                }

                return _index;
            }
        }

        private List<DirectoryIndexEntry> FilterEntries(IEnumerable<DirectoryIndexEntry> entriesIter)
        {
            List<DirectoryIndexEntry> entries = new List<DirectoryIndexEntry>(entriesIter);

            // Weed out short-name entries for files and any hidden / system / metadata files.
            int i = 0;
            while (i < entries.Count)
            {
                DirectoryIndexEntry entry = entries[i];

                if (((entry.Key.Flags & FileAttributeFlags.Hidden) != 0) && _context.Options.HideHiddenFiles)
                {
                    entries.RemoveAt(i);
                }
                else if (((entry.Key.Flags & FileAttributeFlags.System) != 0) && _context.Options.HideSystemFiles)
                {
                    entries.RemoveAt(i);
                }
                else if (entry.Value.MftIndex < 24 && _context.Options.HideMetafiles)
                {
                    entries.RemoveAt(i);
                }
                else if (entry.Key.FileNameNamespace == FileNameNamespace.Dos && _context.Options.HideDosFileNames)
                {
                    entries.RemoveAt(i);
                }
                else
                {
                    ++i;
                }
            }

            return entries;
        }

        private sealed class FileNameQuery : IComparable<byte[]>
        {
            private byte[] _query;
            private UpperCase _upperCase;

            public FileNameQuery(string query, UpperCase upperCase)
            {
                _query = Encoding.Unicode.GetBytes(query);
                _upperCase = upperCase;
            }

            public int CompareTo(byte[] buffer)
            {
                // Note: this is internal knowledge of FileNameRecord structure - but for performance
                // reasons, we don't want to decode the entire structure.  In fact can avoid the string
                // conversion as well.
                byte fnLen = buffer[0x40];
                return _upperCase.Compare(_query, 0, _query.Length, buffer, 0x42, fnLen * 2);
            }
        }
    }
}
