﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using J2N.Collections.Generic;
using J2N.TestUtilities.Xunit;
using System;
using System.Linq;
using Xunit;

namespace J2N.Collections.Tests
{
    /// <summary>
    /// Contains tests that ensure the correctness of the List class.
    /// </summary>
    public abstract partial class List_Generic_Tests<T> : IList_Generic_Tests<T>
    {
        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Reverse(int listLength)
        {
            List<T> list = GenericListFactory(listLength);
            List<T> listBefore = list.ToList();

            list.Reverse();

            for (int i = 0; i < listBefore.Count; i++)
            {
                Assert.Equal(list[i], listBefore[listBefore.Count - (i + 1)]); //"Expect them to be the same."
            }
        }

        [Theory]
        [InlineData(10, 0, 10)]
        [InlineData(10, 3, 3)]
        [InlineData(10, 10, 0)]
        [InlineData(10, 5, 5)]
        [InlineData(10, 0, 5)]
        [InlineData(10, 1, 9)]
        [InlineData(10, 9, 1)]
        [InlineData(10, 2, 8)]
        [InlineData(10, 8, 2)]
        public void Reverse_int_int(int listLength, int index, int count)
        {
            List<T> list = GenericListFactory(listLength);
            List<T> listBefore = list.ToList();

            list.Reverse(index, count);

            for (int i = 0; i < index; i++)
            {
                Assert.Equal(list[i], listBefore[i]); //"Expect them to be the same."
            }

            int j = 0;
            for (int i = index; i < index + count; i++)
            {
                Assert.Equal(list[i], listBefore[index + count - (j + 1)]); //"Expect them to be the same."
                j++;
            }

            for (int i = index + count; i < listBefore.Count; i++)
            {
                Assert.Equal(list[i], listBefore[i]); //"Expect them to be the same."
            }
        }

        [Theory]
        [InlineData(10, 3, 3)]
        [InlineData(10, 0, 10)]
        [InlineData(10, 10, 0)]
        [InlineData(10, 5, 5)]
        [InlineData(10, 0, 5)]
        [InlineData(10, 1, 9)]
        [InlineData(10, 9, 1)]
        [InlineData(10, 2, 8)]
        [InlineData(10, 8, 2)]
        public void Reverse_RepeatedValues(int listLength, int index, int count)
        {
            List<T> list = GenericListFactory(1);
            for (int i = 1; i < listLength; i++)
                list.Add(list[0]);
            List<T> listBefore = list.ToList();

            list.Reverse(index, count);

            for (int i = 0; i < index; i++)
            {
                Assert.Equal(list[i], listBefore[i]); //"Expect them to be the same."
            }

            int j = 0;
            for (int i = index; i < index + count; i++)
            {
                Assert.Equal(list[i], listBefore[index + count - (j + 1)]); //"Expect them to be the same."
                j++;
            }

            for (int i = index + count; i < listBefore.Count; i++)
            {
                Assert.Equal(list[i], listBefore[i]); //"Expect them to be the same."
            }
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Reverse_InvalidParameters(int listLength)
        {
            if (listLength % 2 != 0)
                listLength++;
            List<T> list = GenericListFactory(listLength);
            Tuple<int, int>[] InvalidParameters = new Tuple<int, int>[]
            {
                Tuple.Create(listLength     ,1             ),
                Tuple.Create(listLength+1   ,0             ),
                Tuple.Create(listLength+1   ,1             ),
                Tuple.Create(listLength     ,2             ),
                Tuple.Create(listLength/2   ,listLength/2+1),
                Tuple.Create(listLength-1   ,2             ),
                Tuple.Create(listLength-2   ,3             ),
                Tuple.Create(1              ,listLength    ),
                Tuple.Create(0              ,listLength+1  ),
                Tuple.Create(1              ,listLength+1  ),
                Tuple.Create(2              ,listLength    ),
                Tuple.Create(listLength/2+1 ,listLength/2  ),
                Tuple.Create(2              ,listLength-1  ),
                Tuple.Create(3              ,listLength-2  ),
            };

            Assert.All(InvalidParameters, invalidSet =>
            {
                if (invalidSet.Item1 >= 0 && invalidSet.Item2 >= 0)
                    AssertExtensions.Throws<ArgumentException>(null, () => list.Reverse(invalidSet.Item1, invalidSet.Item2));
            });
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Reverse_NegativeParameters(int listLength)
        {
            if (listLength % 2 != 0)
                listLength++;
            List<T> list = GenericListFactory(listLength);
            Tuple<int, int>[] InvalidParameters = new Tuple<int, int>[]
            {
                Tuple.Create(-1,-1),
                Tuple.Create(-1, 0),
                Tuple.Create(-1, 1),
                Tuple.Create(-1, 2),
                Tuple.Create(0 ,-1),
                Tuple.Create(1 ,-1),
                Tuple.Create(2 ,-1),
            };

            Assert.All(InvalidParameters, invalidSet =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => list.Reverse(invalidSet.Item1, invalidSet.Item2));
            });
        }
    }
}
