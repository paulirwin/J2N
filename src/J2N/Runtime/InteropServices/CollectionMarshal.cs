﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using J2N.Collections.Generic;
using J2N.Runtime.CompilerServices;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace J2N.Runtime.InteropServices
{
    /// <summary>
    /// An unsafe class that provides a set of methods to access the underlying data representations of collections.
    /// </summary>
    public static class CollectionMarshal
    {
        /// <summary>
        /// Get a <see cref="Span{T}"/> view over a <see cref="List{T}"/>'s data.
        /// Items should not be added or removed from the <see cref="List{T}"/> while the <see cref="Span{T}"/> is in use.
        /// </summary>
        /// <param name="list">The list to get the data view over.</param>
        /// <typeparam name="T">The type of the elements in the list.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(List<T>? list) // J2N NOTE: This implementation is from .NET 9 RC1
        {
            Span<T> span = default;
            if (list is not null)
            {
                int size = list._size;
                T[] items = list._items;
                Debug.Assert(items is not null, "Implementation depends on List<T> always having an array.");

                if ((uint)size > (uint)items!.Length)
                {
                    // List<T> was erroneously mutated concurrently with this call, leading to a count larger than its array.
                    throw new InvalidOperationException(SR.InvalidOperation_ConcurrentOperationsNotSupported);
                }

                Debug.Assert(typeof(T[]) == list._items.GetType(), "Implementation depends on List<T> always using a T[] and not U[] where U : T.");
#if FEATURE_MEMORYMARSHAL_GETARRAYDATAREFERENCE
                // Per the comments, this is the equivalent of the Span<T>(ref T reference, int length) constructor.
                span = MemoryMarshal.CreateSpan<T>(ref MemoryMarshal.GetArrayDataReference(items), size);
#else
                span = new Span<T>(items, 0, size);
#endif
            }

            return span;
        }

        /// <summary>
        /// Gets either a ref to a <typeparamref name="TValue"/> in the <see cref="Dictionary{TKey, TValue}"/> or a ref null if it does not exist in the <paramref name="dictionary"/>.
        /// </summary>
        /// <param name="dictionary">The dictionary to get the ref to <typeparamref name="TValue"/> from.</param>
        /// <param name="key">The key used for lookup.</param>
        /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
        /// <remarks>
        /// Items should not be added or removed from the <see cref="Dictionary{TKey, TValue}"/> while the ref <typeparamref name="TValue"/> is in use.
        /// The ref null can be detected using System.Runtime.CompilerServices.Unsafe.IsNullRef
        /// </remarks>
        public static ref TValue GetValueRefOrNullRef<TKey, TValue>(Dictionary<TKey, TValue> dictionary, [AllowNull] TKey key)
            => ref dictionary.FindValue(key);

        /// <summary>
        /// Gets a ref to a <typeparamref name="TValue"/> in the <see cref="Dictionary{TKey, TValue}"/>, adding a new entry with a default value if it does not exist in the <paramref name="dictionary"/>.
        /// </summary>
        /// <param name="dictionary">The dictionary to get the ref to <typeparamref name="TValue"/> from.</param>
        /// <param name="key">The key used for lookup.</param>
        /// <param name="exists">Whether or not a new entry for the given key was added to the dictionary.</param>
        /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
        /// <remarks>Items should not be added to or removed from the <see cref="Dictionary{TKey, TValue}"/> while the ref <typeparamref name="TValue"/> is in use.</remarks>
        public static ref TValue? GetValueRefOrAddDefault<TKey, TValue>(Dictionary<TKey, TValue> dictionary, [AllowNull] TKey key, out bool exists)
            => ref Dictionary<TKey, TValue>.CollectionsMarshalHelper.GetValueRefOrAddDefault(dictionary, key, out exists);

        /// <summary>
        /// Sets the count of the <see cref="List{T}"/> to the specified value.
        /// </summary>
        /// <param name="list">The list to set the count of.</param>
        /// <param name="count">The value to set the list's count to.</param>
        /// <typeparam name="T">The type of the elements in the list.</typeparam>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="list"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="count"/> is negative.
        /// </exception>
        /// <remarks>
        /// When increasing the count, uninitialized data is being exposed.
        /// </remarks>
        public static void SetCount<T>(List<T> list, int count)
        {
            if (list is null)
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.list);
            if (count < 0)
                ThrowHelper.ThrowArgumentOutOfRange_MustBeNonNegative(count, ExceptionArgument.count);

            list._version++;

            if (count > list.Capacity)
            {
                list.Grow(count);
            }
            else if (count < list._size && RuntimeHelper.IsReferenceOrContainsReferences<T>())
            {
                Array.Clear(list._items, count, list._size - count);
            }

            list._size = count;
        }
    }
}
