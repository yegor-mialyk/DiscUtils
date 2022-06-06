﻿//
// Copyright (c) 2017, Quamotion
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

namespace DiscUtils.Nfs;

public class PortMap2Port : IRpcObject
{
    public PortMap2Port()
    {
    }

    public PortMap2Port(XdrDataReader reader)
    {
        Port = reader.ReadUInt32();
    }

    public uint Port { get; set; }

    public void Write(XdrDataWriter writer)
    {
        writer.Write(Port);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as PortMap2Port);
    }

    public bool Equals(PortMap2Port other)
    {
        if (other == null)
        {
            return false;
        }

        return other.Port == Port;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Port);
    }
}
