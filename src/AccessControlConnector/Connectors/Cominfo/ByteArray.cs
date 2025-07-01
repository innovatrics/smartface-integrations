using System;

namespace Innovatrics.SmartFace.Integrations.AccessControlConnector.Connectors.Cominfo
{
    internal class ByteArray
    {
        private byte[] buffer;

        public ByteArray()
        {
            buffer = new byte[0];
        }

        public void Append(byte[] data, int length)
        {
            byte[] newbuffer = new byte[buffer.Length + length];
            Array.Copy(buffer, newbuffer, buffer.Length);
            Array.Copy(data, 0, newbuffer, buffer.Length, length);
            buffer = newbuffer;
        }

        public void Clear()
        {
            buffer = new byte[0];
        }

        public byte[] SubArray(long startPoz)
        {
            byte[] removedArray = new byte[startPoz];
            byte[] newbuffer = new byte[buffer.Length - startPoz];
            Array.Copy(buffer, removedArray, removedArray.Length);
            Array.Copy(buffer, startPoz, newbuffer, 0, newbuffer.Length);
            buffer = newbuffer;
            return removedArray;
        }

        public int IndexOfNewLine()
        {
            int i = IndexOf(10, 0);
            if ((i > 0) && (buffer[i - 1] == 13))
                return i - 1;
            else
                return -1;
        }

        public int IndexOf(byte value, int offset)
        {
            int ai = offset;
            while ((ai < buffer.Length) && (buffer[ai] != value))
                ai++;
            if (ai < buffer.Length)
                return ai;
            else
                return -1;
        }

        public byte[] GetData()
        {
            return buffer;
        }
        
        public int Length => buffer.Length;
    }
}