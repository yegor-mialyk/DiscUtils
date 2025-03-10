//
// Copyright (c) 2016-2017, Bianco Veigel
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


using System.IO;
using DiscUtils.Vfs;
using DiscUtils.Streams;

namespace DiscUtils.Xfs;
internal class Symlink : File, IVfsSymlink<DirEntry, File>
{
    public Symlink(Context context, Inode inode)
        : base(context, inode)
    {
    }

    public string TargetPath
    {
        get
        {
            if (Inode.Format is not InodeFormat.Local and not InodeFormat.Extents)
            {
                throw new IOException("invalid Inode format for symlink");
            }

            var content = FileContent;
            var data = content.ReadExactly(0, (int)Inode.Length);

            return Context.Options.FileNameEncoding.GetString(data, 0, data.Length).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
