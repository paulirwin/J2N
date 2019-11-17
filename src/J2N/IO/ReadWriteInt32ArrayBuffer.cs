﻿using System;
using System.Diagnostics.CodeAnalysis;

namespace J2N.IO
{
    /// <summary>
    /// <see cref="Int32ArrayBuffer"/>, <see cref="ReadWriteInt32ArrayBuffer"/> and <see cref="ReadOnlyInt32ArrayBuffer"/> compose
    /// the implementation of array based int buffers.
    /// <para/>
    /// <see cref="ReadWriteInt32ArrayBuffer"/> extends <see cref="Int32ArrayBuffer"/> with all the write methods.
    /// <para/>
    /// All methods are marked sealed for runtime performance.
    /// </summary>
    internal sealed class ReadWriteInt32ArrayBuffer : Int32ArrayBuffer
    {
        internal static ReadWriteInt32ArrayBuffer Copy(Int32ArrayBuffer other, int markOfOther)
        {
            return new ReadWriteInt32ArrayBuffer(other.Capacity, other.backingArray, other.offset)
            {
                limit = other.Limit,
                position = other.Position,
                mark = markOfOther
            };
        }

        internal ReadWriteInt32ArrayBuffer(int[] array)
            : base(array)
        { }

        internal ReadWriteInt32ArrayBuffer(int capacity)
            : base(capacity)
        { }

        internal ReadWriteInt32ArrayBuffer(int capacity, int[] backingArray, int arrayOffset)
            : base(capacity, backingArray, arrayOffset)
        { }

        public override Int32Buffer AsReadOnlyBuffer() => ReadOnlyInt32ArrayBuffer.Copy(this, mark);

        public override Int32Buffer Compact()
        {
            System.Array.Copy(backingArray, position + offset, backingArray, offset,
                    Remaining);
            position = limit - position;
            limit = capacity;
            mark = UnsetMark;
            return this;
        }

        public override Int32Buffer Duplicate() => Copy(this, mark);

        public override bool IsReadOnly => false;


        [SuppressMessage("Microsoft.Performance", "CA1819", Justification = "design requires some writable array properties")]
        protected override int[] ProtectedArray => backingArray;

        protected override int ProtectedArrayOffset => offset;

        protected override bool ProtectedHasArray => true;


        public override Int32Buffer Put(int value)
        {
            if (position == limit)
            {
                throw new BufferOverflowException();
            }
            backingArray[offset + position++] = value;
            return this;
        }

        public override Int32Buffer Put(int index, int value)
        {
            if (index < 0 || index >= limit)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            backingArray[offset + index] = value;
            return this;
        }

        public override Int32Buffer Put(int[] source, int offset, int length)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int len = source.Length;
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            if ((long)offset + (long)length > len)
                throw new ArgumentOutOfRangeException(string.Empty, $"{nameof(offset)} + {nameof(length)} > {nameof(source.Length)}");
            if (length > Remaining)
                throw new BufferOverflowException();

            System.Array.Copy(source, offset, backingArray, base.offset + position, length);
            position += length;
            return this;
        }

        public override Int32Buffer Slice()
        {
            return new ReadWriteInt32ArrayBuffer(Remaining, backingArray, offset + position);
        }
    }
}