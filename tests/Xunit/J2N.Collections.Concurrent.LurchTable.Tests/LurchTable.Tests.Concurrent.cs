﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using J2N.Collections.Generic;
using J2N.Collections.Tests;
using J2N.Diagnostics;
using J2N.TestUtilities.Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SCG = System.Collections.Generic;

namespace J2N.Collections.Concurrent.Tests
{
    public class LurchTable_Tests_Concurrent // From ConcurrentDictionaryTests
    {
        [Fact]
        public void TestBasicScenarios()
        {
            LurchTable<int, int> cd = new LurchTable<int, int>();

            Task[] tks = new Task[2];
            tks[0] = Task.Run(() =>
            {
                var ret = cd.TryAdd(1, 11);
                if (!ret)
                {
                    ret = cd.TryUpdate(1, 11, 111);
                    Assert.True(ret);
                }

                ret = cd.TryAdd(2, 22);
                if (!ret)
                {
                    ret = cd.TryUpdate(2, 22, 222);
                    Assert.True(ret);
                }
            });

            tks[1] = Task.Run(() =>
            {
                var ret = cd.TryAdd(2, 222);
                if (!ret)
                {
                    ret = cd.TryUpdate(2, 222, 22);
                    Assert.True(ret);
                }

                ret = cd.TryAdd(1, 111);
                if (!ret)
                {
                    ret = cd.TryUpdate(1, 111, 11);
                    Assert.True(ret);
                }
            });

            Task.WaitAll(tks);
        }

        [Fact]
        public void TestAddNullValue_ConcurrentDictionaryOfString_null()
        {
            // using ConcurrentDictionary<TKey, TValue> class
            LurchTable<string, string> dict1 = new LurchTable<string, string>();
            dict1["key"] = null;
        }

        [Fact]
        public void TestAddNullValue_IDictionaryOfString_null()
        {
            // using IDictionary<TKey, TValue> interface
            IDictionary<string, string> dict2 = new LurchTable<string, string>();
            dict2["key"] = null;
            dict2.Add("key2", null);
        }

        [Fact]
        public void TestAddNullValue_IDictionary_ReferenceType_null()
        {
            // using IDictionary interface
            IDictionary dict3 = new LurchTable<string, string>();
            dict3["key"] = null;
            dict3.Add("key2", null);
        }

        [Fact]
        public void TestAddNullValue_IDictionary_ValueType_null_indexer()
        {
            // using IDictionary interface and value type values
            Action action = () =>
            {
                IDictionary dict4 = new LurchTable<string, int>();
                dict4["key"] = null;
            };
            Assert.Throws<ArgumentNullException>(action);
        }

        [Fact]
        public void TestAddNullValue_IDictionary_ValueType_null_add()
        {
            Action action = () =>
            {
                IDictionary dict5 = new LurchTable<string, int>();
                dict5.Add("key", null);
            };
            Assert.Throws<ArgumentNullException>(action);
        }

        [Fact]
        public void TestAddValueOfDifferentType()
        {
            Action action = () =>
            {
                IDictionary dict = new LurchTable<string, string>();
                dict["key"] = 1;
            };
            Assert.Throws<ArgumentException>(action);

            action = () =>
            {
                IDictionary dict = new LurchTable<string, string>();
                dict.Add("key", 1);
            };
            Assert.Throws<ArgumentException>(action);
        }

        [Fact]
        public void TestAdd()
        {
            TestAdd1(1, 1, 1, 10000);
            TestAdd1(5, 1, 1, 10000);
            TestAdd1(1, 1, 2, 5000);
            TestAdd1(1, 1, 5, 2000);
            TestAdd1(4, 0, 4, 2000);
            TestAdd1(16, 31, 4, 2000);
            TestAdd1(64, 5, 5, 5000);
            TestAdd1(5, 5, 5, 2500);
        }

        private static void TestAdd1(int cLevel, int initSize, int threads, int addsPerThread)
        {
            LurchTable<int, int> dictConcurrent = new LurchTable<int, int>(/*cLevel,*/ 1);
            IDictionary<int, int> dict = dictConcurrent;

            int count = threads;
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                for (int i = 0; i < threads; i++)
                {
                    int ii = i;
                    Task.Run(
                        () =>
                        {
                            for (int j = 0; j < addsPerThread; j++)
                            {
                                dict.Add(j + ii * addsPerThread, -(j + ii * addsPerThread));
                            }
                            if (Interlocked.Decrement(ref count) == 0) mre.Set();
                        });
                }
                mre.WaitOne();
            }

            foreach (var pair in dict)
            {
                Assert.Equal(pair.Key, -pair.Value);
            }

            SCG.List<int> gotKeys = new SCG.List<int>();
            foreach (var pair in dict)
                gotKeys.Add(pair.Key);

            gotKeys.Sort();

            SCG.List<int> expectKeys = new SCG.List<int>();
            int itemCount = threads * addsPerThread;
            for (int i = 0; i < itemCount; i++)
                expectKeys.Add(i);

            Assert.Equal(expectKeys.Count, gotKeys.Count);

            for (int i = 0; i < expectKeys.Count; i++)
            {
                Assert.True(expectKeys[i].Equals(gotKeys[i]),
                    string.Format("The set of keys in the dictionary is are not the same as the expected" + Environment.NewLine +
                            "TestAdd1(cLevel={0}, initSize={1}, threads={2}, addsPerThread={3})", cLevel, initSize, threads, addsPerThread)
                   );
            }

            // Finally, let's verify that the count is reported correctly.
            int expectedCount = threads * addsPerThread;
            Assert.Equal(expectedCount, dict.Count);
            Assert.Equal(expectedCount, dictConcurrent.ToArray().Length);
        }

        [Fact]
        public void TestUpdate()
        {
            TestUpdate1(1, 1, 10000);
            TestUpdate1(5, 1, 10000);
            TestUpdate1(1, 2, 5000);
            TestUpdate1(1, 5, 2001);
            TestUpdate1(4, 4, 2001);
            TestUpdate1(15, 5, 2001);
            TestUpdate1(64, 5, 5000);
            TestUpdate1(5, 5, 25000);
        }

        private static void TestUpdate1(int cLevel, int threads, int updatesPerThread)
        {
            IDictionary<int, int> dict = new LurchTable<int, int>(/*cLevel,*/ 1);

            for (int i = 1; i <= updatesPerThread; i++) dict[i] = i;

            int running = threads;
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                for (int i = 0; i < threads; i++)
                {
                    int ii = i;
                    Task.Run(
                        () =>
                        {
                            for (int j = 1; j <= updatesPerThread; j++)
                            {
                                dict[j] = (ii + 2) * j;
                            }
                            if (Interlocked.Decrement(ref running) == 0) mre.Set();
                        });
                }
                mre.WaitOne();
            }

            foreach (var pair in dict)
            {
                var div = pair.Value / pair.Key;
                var rem = pair.Value % pair.Key;

                Assert.Equal(0, rem);
                Assert.True(div > 1 && div <= threads + 1,
                    string.Format("* Invalid value={3}! TestUpdate1(cLevel={0}, threads={1}, updatesPerThread={2})", cLevel, threads, updatesPerThread, div));
            }

            SCG.List<int> gotKeys = new SCG.List<int>();
            foreach (var pair in dict)
                gotKeys.Add(pair.Key);
            gotKeys.Sort();

            SCG.List<int> expectKeys = new SCG.List<int>();
            for (int i = 1; i <= updatesPerThread; i++)
                expectKeys.Add(i);

            Assert.Equal(expectKeys.Count, gotKeys.Count);

            for (int i = 0; i < expectKeys.Count; i++)
            {
                Assert.True(expectKeys[i].Equals(gotKeys[i]),
                   string.Format("The set of keys in the dictionary is are not the same as the expected." + Environment.NewLine +
                           "TestUpdate1(cLevel={0}, threads={1}, updatesPerThread={2})", cLevel, threads, updatesPerThread)
                  );
            }
        }

        [Fact]
        public void TestRead()
        {
            TestRead1(1, 1, 10000);
            TestRead1(5, 1, 10000);
            TestRead1(1, 2, 5000);
            TestRead1(1, 5, 2001);
            TestRead1(4, 4, 2001);
            TestRead1(15, 5, 2001);
            TestRead1(64, 5, 5000);
            TestRead1(5, 5, 25000);
        }

        private static void TestRead1(int cLevel, int threads, int readsPerThread)
        {
            IDictionary<int, int> dict = new LurchTable<int, int>(/*cLevel,*/ 1);

            for (int i = 0; i < readsPerThread; i += 2) dict[i] = i;

            int count = threads;
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                for (int i = 0; i < threads; i++)
                {
                    int ii = i;
                    Task.Run(
                        () =>
                        {
                            for (int j = 0; j < readsPerThread; j++)
                            {
                                int val = 0;
                                if (dict.TryGetValue(j, out val))
                                {
                                    Assert.Equal(0, j % 2);
                                    Assert.Equal(j, val);
                                }
                                else
                                {
                                    Assert.Equal(1, j % 2);
                                }
                            }
                            if (Interlocked.Decrement(ref count) == 0) mre.Set();
                        });
                }
                mre.WaitOne();
            }
        }

        [Fact]
        public void TestRemove()
        {
            TestRemove1(1, 1, 10000);
            TestRemove1(5, 1, 1000);
            TestRemove1(1, 5, 2001);
            TestRemove1(4, 4, 2001);
            TestRemove1(15, 5, 2001);
            TestRemove1(64, 5, 5000);
        }

        private static void TestRemove1(int cLevel, int threads, int removesPerThread)
        {
            LurchTable<int, int> dict = new LurchTable<int, int>(/*cLevel,*/ 1);
            string methodparameters = string.Format("* TestRemove1(cLevel={0}, threads={1}, removesPerThread={2})", cLevel, threads, removesPerThread);
            int N = 2 * threads * removesPerThread;

            for (int i = 0; i < N; i++) dict[i] = -i;

            // The dictionary contains keys [0..N), each key mapped to a value equal to the key.
            // Threads will cooperatively remove all even keys

            int running = threads;
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                for (int i = 0; i < threads; i++)
                {
                    int ii = i;
                    Task.Run(
                        () =>
                        {
                            for (int j = 0; j < removesPerThread; j++)
                            {
                                int value;
                                int key = 2 * (ii + j * threads);
                                Assert.True(dict.TryRemove(key, out value), "Failed to remove an element! " + methodparameters);

                                Assert.Equal(-key, value);
                            }

                            if (Interlocked.Decrement(ref running) == 0) mre.Set();
                        });
                }
                mre.WaitOne();
            }

            foreach (var pair in dict)
            {
                Assert.Equal(pair.Key, -pair.Value);
            }

            SCG.List<int> gotKeys = new SCG.List<int>();
            foreach (var pair in dict)
                gotKeys.Add(pair.Key);
            gotKeys.Sort();

            SCG.List<int> expectKeys = new SCG.List<int>();
            for (int i = 0; i < (threads * removesPerThread); i++)
                expectKeys.Add(2 * i + 1);

            Assert.Equal(expectKeys.Count, gotKeys.Count);

            for (int i = 0; i < expectKeys.Count; i++)
            {
                Assert.True(expectKeys[i].Equals(gotKeys[i]), "  > Unexpected key value! " + methodparameters);
            }

            // Finally, let's verify that the count is reported correctly.
            Assert.Equal(expectKeys.Count, dict.Count);
            Assert.Equal(expectKeys.Count, dict.ToArray().Length);
        }

        [Fact]
        public void TestRemove_2()
        {
            TestRemove2(1);
            TestRemove2(10);
            TestRemove2(5000);
        }

        private static void TestRemove2(int removesPerThread)
        {
            LurchTable<int, int> dict = new LurchTable<int, int>();

            for (int i = 0; i < removesPerThread; i++) dict[i] = -i;

            // The dictionary contains keys [0..N), each key mapped to a value equal to the key.
            // Threads will cooperatively remove all even keys.
            const int SIZE = 2;
            int running = SIZE;

            bool[][] seen = new bool[SIZE][];
            for (int i = 0; i < SIZE; i++) seen[i] = new bool[removesPerThread];

            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                for (int t = 0; t < SIZE; t++)
                {
                    int thread = t;
                    Task.Run(
                        () =>
                        {
                            for (int key = 0; key < removesPerThread; key++)
                            {
                                int value;
                                if (dict.TryRemove(key, out value))
                                {
                                    seen[thread][key] = true;

                                    Assert.Equal(-key, value);
                                }
                            }
                            if (Interlocked.Decrement(ref running) == 0) mre.Set();
                        });
                }
                mre.WaitOne();
            }

            Assert.Equal(0, dict.Count);

            for (int i = 0; i < removesPerThread; i++)
            {
                Assert.False(seen[0][i] == seen[1][i],
                    string.Format("> FAILED. Two threads appear to have removed the same element. TestRemove2(removesPerThread={0})", removesPerThread)
                    );
            }
        }

        [Fact]
        public void TestRemove3()
        {
            LurchTable<int, int> dict = new LurchTable<int, int>();

            dict[99] = -99;

            ICollection<KeyValuePair<int, int>> col = dict;

            // Make sure we cannot "remove" a key/value pair which is not in the dictionary
            for (int i = 0; i < 200; i++)
            {
                if (i != 99)
                {
                    Assert.False(col.Remove(new KeyValuePair<int, int>(i, -99)), "Should not remove not existing a key/value pair - new KeyValuePair<int, int>(i, -99)");
                    Assert.False(col.Remove(new KeyValuePair<int, int>(99, -i)), "Should not remove not existing a key/value pair - new KeyValuePair<int, int>(99, -i)");
                }
            }

            // Can we remove a key/value pair successfully?
            Assert.True(col.Remove(new KeyValuePair<int, int>(99, -99)), "Failed to remove existing key/value pair");

            // Make sure the key/value pair is gone
            Assert.False(col.Remove(new KeyValuePair<int, int>(99, -99)), "Should not remove the key/value pair which has been removed");

            // And that the dictionary is empty. We will check the count in a few different ways:
            Assert.Equal(0, dict.Count);
            Assert.Equal(0, dict.ToArray().Length);
        }

        [Fact]
        public void TryRemove_KeyValuePair_ArgumentValidation()
        {
            Assert.Null(Record.Exception(() => new LurchTable<string, int>().TryRemove(new KeyValuePair<string, int>(null, 42)))); // no error with null key
            new LurchTable<int, int>().TryRemove(new KeyValuePair<int, int>(0, 0)); // no error when using default value type
            new LurchTable<int?, int>().TryRemove(new KeyValuePair<int?, int>(0, 0)); // or nullable
        }

        [Fact]
        public void TryRemove_KeyValuePair_RemovesSuccessfullyAsAppropriate()
        {
            var dict = new LurchTable<string, int>();

            for (int i = 0; i < 2; i++)
            {
                Assert.False(dict.TryRemove(new KeyValuePair<string, int>("key", 42)));
                Assert.Equal(0, dict.Count);
                Assert.True(dict.TryAdd("key", 42));
                Assert.Equal(1, dict.Count);
                Assert.True(dict.TryRemove(new KeyValuePair<string, int>("key", 42)));
                Assert.Equal(0, dict.Count);
            }

            Assert.True(dict.TryAdd("key", 42));
            Assert.False(dict.TryRemove(new KeyValuePair<string, int>("key", 43))); // value doesn't match
        }

        [Fact]
        public void TryRemove_KeyValuePair_MatchesKeyWithDefaultComparer()
        {
            var dict = new LurchTable<string, string>(StringComparer.OrdinalIgnoreCase);
            dict.TryAdd("key", "value");
            Assert.False(dict.TryRemove(new KeyValuePair<string, string>("key", "VALUE")));
            Assert.True(dict.TryRemove(new KeyValuePair<string, string>("KEY", "value")));
        }

        [Fact]
        public void TestGetOrAdd()
        {
            TestGetOrAddOrUpdate(1, 1, 1, 10000, true);
            TestGetOrAddOrUpdate(5, 1, 1, 10000, true);
            TestGetOrAddOrUpdate(1, 1, 2, 5000, true);
            TestGetOrAddOrUpdate(1, 1, 5, 2000, true);
            TestGetOrAddOrUpdate(4, 0, 4, 2000, true);
            TestGetOrAddOrUpdate(16, 31, 4, 2000, true);
            TestGetOrAddOrUpdate(64, 5, 5, 5000, true);
            TestGetOrAddOrUpdate(5, 5, 5, 25000, true);
        }

        [Fact]
        public void TestAddOrUpdate()
        {
            TestGetOrAddOrUpdate(1, 1, 1, 10000, false);
            TestGetOrAddOrUpdate(5, 1, 1, 10000, false);
            TestGetOrAddOrUpdate(1, 1, 2, 5000, false);
            TestGetOrAddOrUpdate(1, 1, 5, 2000, false);
            TestGetOrAddOrUpdate(4, 0, 4, 2000, false);
            TestGetOrAddOrUpdate(16, 31, 4, 2000, false);
            TestGetOrAddOrUpdate(64, 5, 5, 5000, false);
            TestGetOrAddOrUpdate(5, 5, 5, 25000, false);
        }

        private static void TestGetOrAddOrUpdate(int cLevel, int initSize, int threads, int addsPerThread, bool isAdd)
        {
            LurchTable<int, int> dict = new LurchTable<int, int>(/*cLevel,*/ 1);

            int count = threads;
            using (ManualResetEvent mre = new ManualResetEvent(false))
            {
                for (int i = 0; i < threads; i++)
                {
                    int ii = i;
                    Task.Run(
                        () =>
                        {
                            for (int j = 0; j < addsPerThread; j++)
                            {
                                if (isAdd)
                                {
                                    //call one of the overloads of GetOrAdd
                                    switch (j % 2)
                                    {
                                        case 0:
                                            dict.GetOrAdd(j, -j);
                                            break;
                                        case 1:
                                            dict.GetOrAdd(j, x => -x);
                                            break;
                                        //case 2: // J2N TODO: Implement this overload
                                        //    dict.GetOrAdd(j, (x, m) => x * m, -1);
                                        //    break;
                                    }
                                }
                                else
                                {
                                    switch (j % 2)
                                    {
                                        case 0:
                                            dict.AddOrUpdate(j, -j, (k, v) => -j);
                                            break;
                                        case 1:
                                            dict.AddOrUpdate(j, (k) => -k, (k, v) => -k);
                                            break;
                                            //case 2: // J2N TODO: Implement this overload
                                            //    dict.AddOrUpdate(j, (k, m) => k * m, (k, v, m) => k * m, -1);
                                            //    break;
                                    }
                                }
                            }
                            if (Interlocked.Decrement(ref count) == 0) mre.Set();
                        });
                }
                mre.WaitOne();
            }

            foreach (var pair in dict)
            {
                Assert.Equal(pair.Key, -pair.Value);
            }

            SCG.List<int> gotKeys = new SCG.List<int>();
            foreach (var pair in dict)
                gotKeys.Add(pair.Key);
            gotKeys.Sort();

            SCG.List<int> expectKeys = new SCG.List<int>();
            for (int i = 0; i < addsPerThread; i++)
                expectKeys.Add(i);

            Assert.Equal(expectKeys.Count, gotKeys.Count);

            for (int i = 0; i < expectKeys.Count; i++)
            {
                Assert.True(expectKeys[i].Equals(gotKeys[i]),
                    string.Format("* Test '{4}': Level={0}, initSize={1}, threads={2}, addsPerThread={3})" + Environment.NewLine +
                    "> FAILED.  The set of keys in the dictionary is are not the same as the expected.",
                    cLevel, initSize, threads, addsPerThread, isAdd ? "GetOrAdd" : "GetOrUpdate"));
            }

            // Finally, let's verify that the count is reported correctly.
            Assert.Equal(addsPerThread, dict.Count);
            Assert.Equal(addsPerThread, dict.ToArray().Length);
        }

        [Fact]
        public void TestBugFix669376()
        {
            var cd = new LurchTable<string, int>(new OrdinalStringComparer());
            cd["test"] = 10;
            Assert.True(cd.ContainsKey("TEST"), "Customized comparer didn't work");
        }

        private class OrdinalStringComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                var xlower = x.ToLowerInvariant();
                var ylower = y.ToLowerInvariant();
                return string.CompareOrdinal(xlower, ylower) == 0;
            }

            public int GetHashCode(string obj)
            {
                return 0;
            }
        }

        [Fact]
        public void TestConstructor()
        {
            var dictionary = new LurchTable<int, int>(new[] { new KeyValuePair<int, int>(1, 1) });
            Assert.False(dictionary.IsEmpty);
            Assert.Equal(1, dictionary.Keys.Count);
            Assert.Equal(1, dictionary.Values.Count);
        }

        [Fact]
        public void TestDebuggerAttributes()
        {
            DebuggerAttributes.ValidateDebuggerDisplayReferences(new LurchTable<string, int>());
            LurchTable<string, int> dict = new LurchTable<string, int>();
            dict.TryAdd("One", 1);
            dict.TryAdd("Two", 2);
            DebuggerAttributeInfo info = DebuggerAttributes.ValidateDebuggerTypeProxyProperties(dict);
            PropertyInfo itemProperty = info.Properties.Single(pr => pr.GetCustomAttribute<DebuggerBrowsableAttribute>().State == DebuggerBrowsableState.RootHidden);
            KeyValuePair<string, int>[] items = itemProperty.GetValue(info.Instance) as KeyValuePair<string, int>[];
            Assert.Equal(dict, items);
        }

        [Fact]
        public void TestDebuggerAttributes_Null()
        {
            Type proxyType = DebuggerAttributes.GetProxyType(new LurchTable<string, int>());
            TargetInvocationException tie = Assert.Throws<TargetInvocationException>(() => Activator.CreateInstance(proxyType, (object)null));
            Assert.IsType<ArgumentNullException>(tie.InnerException);
        }

        [Fact]
        public void TestNullComparer()
        {
            AssertDefaultComparerBehavior(new LurchTable<EqualityApiSpy, int>((IEqualityComparer<EqualityApiSpy>)null));

            AssertDefaultComparerBehavior(new LurchTable<EqualityApiSpy, int>(new[] { new KeyValuePair<EqualityApiSpy, int>(new EqualityApiSpy(), 1) }, null));

            AssertDefaultComparerBehavior(new LurchTable<EqualityApiSpy, int>(/*1,*/ new[] { new KeyValuePair<EqualityApiSpy, int>(new EqualityApiSpy(), 1) }, null));

            AssertDefaultComparerBehavior(new LurchTable<EqualityApiSpy, int>(/*1,*/ 1, LurchTableOrder.None, (IEqualityComparer<EqualityApiSpy>)null));

            void AssertDefaultComparerBehavior(LurchTable<EqualityApiSpy, int> dictionary)
            {
                var spyKey = new EqualityApiSpy();

                Assert.True(dictionary.TryAdd(spyKey, 1));
                Assert.False(dictionary.TryAdd(spyKey, 1));

                Assert.False(spyKey.ObjectApiUsed);
                Assert.True(spyKey.IEquatableApiUsed);
            }
        }

        private sealed class EqualityApiSpy : IEquatable<EqualityApiSpy>
        {
            public bool ObjectApiUsed { get; private set; }
            public bool IEquatableApiUsed { get; private set; }


            public override bool Equals(object obj)
            {
                ObjectApiUsed = true;
                return ReferenceEquals(this, obj);
            }

            public override int GetHashCode() => base.GetHashCode();

            public bool Equals(EqualityApiSpy other)
            {
                IEquatableApiUsed = true;
                return ReferenceEquals(this, other);
            }
        }

        [Fact]
        public void TestConstructor_Negative()
        {
            Assert.Throws<ArgumentNullException>(
               () => new LurchTable<int, int>((ICollection<KeyValuePair<int, int>>)null));
            // "TestConstructor:  FAILED.  Constructor didn't throw ANE when null collection is passed");

            Assert.Throws<ArgumentNullException>(
               () => new LurchTable<int, int>((ICollection<KeyValuePair<int, int>>)null, J2N.Collections.Generic.EqualityComparer<int>.Default));
            // "TestConstructor:  FAILED.  Constructor didn't throw ANE when null collection and non null IEqualityComparer passed");

            var exception = Record.Exception(
               () => new LurchTable<string, int>(new[] { new KeyValuePair<string, int>(null, 1) }));
            Assert.Null(exception);
            // "TestConstructor:  FAILED.  Constructor threw ANE when collection has null key passed");

            // Duplicate keys.
            AssertExtensions.Throws<ArgumentException>(null, () => new LurchTable<int, int>(new[] { new KeyValuePair<int, int>(1, 1), new KeyValuePair<int, int>(1, 2) }));

            Assert.Throws<ArgumentNullException>(
               () => new LurchTable<int, int>(/*1,*/ null, J2N.Collections.Generic.EqualityComparer<int>.Default));
            // "TestConstructor:  FAILED.  Constructor didn't throw ANE when null collection is passed");

            Assert.Throws<ArgumentOutOfRangeException>(
               () => new LurchTable<int, int>(LurchTableOrder.None, 0));
            // "TestConstructor:  FAILED.  Constructor didn't throw AORE when <1 limit passed");

            Assert.Throws<ArgumentOutOfRangeException>(
               () => new LurchTable<int, int>(-1, LurchTableOrder.None));
            // "TestConstructor:  FAILED.  Constructor didn't throw AORE when < 0 capacity passed");
        }

        [Fact]
        public void TestExceptions()
        {
            var dictionary = new LurchTable<string, int>();
            Assert.Null(Record.Exception(
               () => dictionary.TryAdd(null, 0)));
            //  "TestExceptions:  FAILED.  TryAdd threw ANE when null key is passed");

            Assert.Null(Record.Exception(
               () => dictionary.ContainsKey(null)));
            // "TestExceptions:  FAILED.  Contains threw ANE when null key is passed");

            int item;
            Assert.Null(Record.Exception(
               () => dictionary.TryRemove(null, out item)));
            //  "TestExceptions:  FAILED.  TryRemove threw ANE when null key is passed");
            Assert.Null(Record.Exception(
               () => dictionary.TryGetValue(null, out item)));
            // "TestExceptions:  FAILED.  TryGetValue threw ANE when null key is passed");

            Assert.Throws<KeyNotFoundException>(
               () => { var x = dictionary[null]; });
            // "TestExceptions:  FAILED.  this[] threw ANE when null key is passed"); (it should throw KeyNotFoundException instead)
            Assert.Throws<KeyNotFoundException>(
               () => { var x = dictionary["1"]; });
            // "TestExceptions:  FAILED.  this[] didn't throw KeyNotFoundException!");

            Assert.Null(Record.Exception(
               () => dictionary[null] = 1));
            // "TestExceptions:  FAILED.  this[] threw ANE when null key is passed");

            //Assert.Throws<ArgumentNullException>(
            //   () => dictionary.GetOrAdd(null, (k, m) => 0, 0));
            //// "TestExceptions:  FAILED.  GetOrAdd didn't throw ANE when null key is passed");
            //Assert.Throws<ArgumentNullException>(
            //   () => dictionary.GetOrAdd("1", null, 0));
            //// "TestExceptions:  FAILED.  GetOrAdd didn't throw ANE when null valueFactory is passed");
            Assert.Null(Record.Exception(
               () => dictionary.GetOrAdd(null, (k) => 0)));
            // "TestExceptions:  FAILED.  GetOrAdd threw ANE when null key is passed");
            Assert.Throws<ArgumentNullException>(
               () => dictionary.GetOrAdd("1", null));
            // "TestExceptions:  FAILED.  GetOrAdd didn't throw ANE when null valueFactory is passed");
            Assert.Null(Record.Exception(
               () => dictionary.GetOrAdd(null, 0)));
            // "TestExceptions:  FAILED.  GetOrAdd threw ANE when null key is passed");

            //Assert.Throws<ArgumentNullException>(
            //   () => dictionary.AddOrUpdate(null, (k, m) => 0, (k, v, m) => 0, 42));
            //// "TestExceptions:  FAILED.  AddOrUpdate didn't throw ANE when null key is passed");
            //Assert.Throws<ArgumentNullException>(
            //   () => dictionary.AddOrUpdate("1", (k, m) => 0, null, 42));
            //// "TestExceptions:  FAILED.  AddOrUpdate didn't throw ANE when null updateFactory is passed");
            //Assert.Throws<ArgumentNullException>(
            //   () => dictionary.AddOrUpdate("1", null, (k, v, m) => 0, 42));
            //// "TestExceptions:  FAILED.  AddOrUpdate didn't throw ANE when null addFactory is passed");
            Assert.Null(Record.Exception(
               () => dictionary.AddOrUpdate(null, (k) => 0, (k, v) => 0)));
            // "TestExceptions:  FAILED.  AddOrUpdate threw ANE when null key is passed");
            Assert.Throws<ArgumentNullException>(
               () => dictionary.AddOrUpdate("1", null, (k, v) => 0));
            // "TestExceptions:  FAILED.  AddOrUpdate didn't throw ANE when null updateFactory is passed");
            Assert.Throws<ArgumentNullException>(
               () => dictionary.AddOrUpdate(null, (k) => 0, null));
            // "TestExceptions:  FAILED.  AddOrUpdate didn't throw ANE when null addFactory is passed");

            // Duplicate key.
            dictionary.TryAdd("1", 1);
            AssertExtensions.Throws<ArgumentException>(null, () => ((IDictionary<string, int>)dictionary).Add("1", 2));
        }

        [Fact]
        public void TestIDictionary()
        {
            IDictionary dictionary = new LurchTable<string, int>();
            Assert.False(dictionary.IsReadOnly);

            // Empty dictionary should not enumerate
            Assert.Empty(dictionary);

            const int SIZE = 10;
            for (int i = 0; i < SIZE; i++)
                dictionary.Add(i.ToString(), i);

            Assert.Equal(SIZE, dictionary.Count);

            //test contains
            Assert.False(dictionary.Contains(1), "TestIDictionary:  FAILED.  Contain returned true for incorrect key type");
            Assert.False(dictionary.Contains("100"), "TestIDictionary:  FAILED.  Contain returned true for incorrect key");
            Assert.True(dictionary.Contains("1"), "TestIDictionary:  FAILED.  Contain returned false for correct key");

            //test GetEnumerator
            int count = 0;
            foreach (var obj in dictionary)
            {
                DictionaryEntry entry = (DictionaryEntry)obj;
                string key = (string)entry.Key;
                int value = (int)entry.Value;
                int expectedValue = int.Parse(key);
                Assert.True(value == expectedValue,
                    string.Format("TestIDictionary:  FAILED.  Unexpected value returned from GetEnumerator, expected {0}, actual {1}", value, expectedValue));
                count++;
            }

            Assert.Equal(SIZE, count);
            Assert.Equal(SIZE, dictionary.Keys.Count);
            Assert.Equal(SIZE, dictionary.Values.Count);

            //Test Remove
            dictionary.Remove("9");
            Assert.Equal(SIZE - 1, dictionary.Count);

            //Test this[]
            for (int i = 0; i < dictionary.Count; i++)
                Assert.Equal(i, (int)dictionary[i.ToString()]);

            dictionary["1"] = 100; // try a valid setter
            Assert.Equal(100, (int)dictionary["1"]);

            //non-existing key
            Assert.Null(dictionary["NotAKey"]);
        }

        [Fact]
        public void TestIDictionary_Negative()
        {
            IDictionary dictionary = new LurchTable<string, int>();
            Assert.Null(Record.Exception(
               () => dictionary.Add(null, 1)));
            // "TestIDictionary:  FAILED.  Add threw ANE when null key is passed");

            // Invalid key type.
            AssertExtensions.Throws<ArgumentException>("key", () => dictionary.Add(1, 1));

            // Invalid value type.
            AssertExtensions.Throws<ArgumentException>("value", () => dictionary.Add("1", "1"));

            Assert.Null(Record.Exception(
               () => dictionary.Contains(null)));
            // "TestIDictionary:  FAILED.  Contain threw ANE when null key is passed");

            //Test Remove
            Assert.Null(Record.Exception(
               () => dictionary.Remove(null)));
            // "TestIDictionary:  FAILED.  Remove threw ANE when null key is passed");

            //Test this[]
            Assert.Null(Record.Exception(
               () => { object val = dictionary[null]; }));
            // "TestIDictionary:  FAILED.  this[] getter threw ANE when null key is passed");
            Assert.Null(Record.Exception(
               () => dictionary[null] = 0));
            // "TestIDictionary:  FAILED.  this[] setter threw ANE when null key is passed");

            // Invalid key type.
            AssertExtensions.Throws<ArgumentException>("key", () => dictionary[1] = 0);

            // Invalid value type.
            AssertExtensions.Throws<ArgumentException>("value", () => dictionary["1"] = "0");
        }

        [Fact]
        public void IDictionary_Remove_NullKeyInKeyValuePair_ThrowsArgumentNullException()
        {
            IDictionary<string, int> dictionary = new LurchTable<string, int>();
            Assert.Null(Record.Exception(() => dictionary.Remove(new KeyValuePair<string, int>(null, 0))));
        }

        [Fact]
        public void TestICollection()
        {
            ICollection dictionary = new LurchTable<int, int>();
            Assert.False(dictionary.IsSynchronized, "TestICollection:  FAILED.  IsSynchronized returned true!");

            int key = -1;
            int value = +1;
            //add one item to the dictionary
            ((LurchTable<int, int>)dictionary).TryAdd(key, value);

            var objectArray = new object[1];
            dictionary.CopyTo(objectArray, 0);

            Assert.Equal(key, ((KeyValuePair<int, int>)objectArray[0]).Key);
            Assert.Equal(value, ((KeyValuePair<int, int>)objectArray[0]).Value);

            var keyValueArray = new KeyValuePair<int, int>[1];
            dictionary.CopyTo(keyValueArray, 0);
            Assert.Equal(key, keyValueArray[0].Key);
            Assert.Equal(value, keyValueArray[0].Value);

            var entryArray = new DictionaryEntry[1];
            dictionary.CopyTo(entryArray, 0);
            Assert.Equal(key, (int)entryArray[0].Key);
            Assert.Equal(value, (int)entryArray[0].Value);
        }

        [Fact]
        public void TestICollection_Negative()
        {
            ICollection dictionary = new LurchTable<int, int>();
            Assert.False(dictionary.IsSynchronized, "TestICollection:  FAILED.  IsSynchronized returned true!");

            Assert.Null(Record.Exception(() => { var obj = dictionary.SyncRoot; }));
            // "TestICollection:  FAILED.  SyncRoot property threw");
            Assert.Throws<ArgumentNullException>(() => dictionary.CopyTo(null, 0));
            // "TestICollection:  FAILED.  CopyTo didn't throw ANE when null Array is passed");
            Assert.Throws<ArgumentOutOfRangeException>(() => dictionary.CopyTo(new object[] { }, -1));
            // "TestICollection:  FAILED.  CopyTo didn't throw AORE when negative index passed");

            //add one item to the dictionary
            ((LurchTable<int, int>)dictionary).TryAdd(1, 1);
            AssertExtensions.Throws<ArgumentException>(null, () => dictionary.CopyTo(new object[] { }, 0));
            // "TestICollection:  FAILED.  CopyTo didn't throw AE when the Array size is smaller than the dictionary count");
        }

        [Fact]
        public void TestClear()
        {
            var dictionary = new LurchTable<int, int>();
            for (int i = 0; i < 10; i++)
                dictionary.TryAdd(i, i);

            Assert.Equal(10, dictionary.Count);

            dictionary.Clear();
            Assert.Equal(0, dictionary.Count);

            int item;
            Assert.False(dictionary.TryRemove(1, out item), "TestClear: FAILED.  TryRemove succeeded after Clear");
            Assert.True(dictionary.IsEmpty, "TestClear: FAILED.  IsEmpty returned false after Clear");
        }

        [Fact]
        public void TestTryUpdate()
        {
            var dictionary = new LurchTable<string, int>();
            Assert.Null(Record.Exception(
               () => dictionary.TryUpdate(null, 0, 0)));
            // "TestTryUpdate:  FAILED.  TryUpdate threw ANE when null key is passed");

            for (int i = 0; i < 10; i++)
                dictionary.TryAdd(i.ToString(), i);

            for (int i = 0; i < 10; i++)
            {
                Assert.True(dictionary.TryUpdate(i.ToString(), i + 1, i), "TestTryUpdate:  FAILED.  TryUpdate failed!");
                Assert.Equal(i + 1, dictionary[i.ToString()]);
            }

            //test TryUpdate concurrently
            dictionary.Clear();
            for (int i = 0; i < 1000; i++)
                dictionary.TryAdd(i.ToString(), i);

            var mres = new ManualResetEventSlim();
            Task[] tasks = new Task[10];
            ThreadLocal<ThreadData> updatedKeys = new ThreadLocal<ThreadData>(true);
            for (int i = 0; i < tasks.Length; i++)
            {
                // We are creating the Task using TaskCreationOptions.LongRunning because...
                // there is no guarantee that the Task will be created on another thread.
                // There is also no guarantee that using this TaskCreationOption will force
                // it to be run on another thread.
                tasks[i] = Task.Factory.StartNew((obj) =>
                {
                    mres.Wait();
                    int index = (((int)obj) + 1) + 1000;
                    updatedKeys.Value = new ThreadData();
                    updatedKeys.Value.ThreadIndex = index;

                    for (int j = 0; j < dictionary.Count; j++)
                    {
                        if (dictionary.TryUpdate(j.ToString(), index, j))
                        {
                            if (dictionary[j.ToString()] != index)
                            {
                                updatedKeys.Value.Succeeded = false;
                                return;
                            }
                            updatedKeys.Value.Keys.Add(j.ToString());
                        }
                    }
                }, i, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            mres.Set();
            Task.WaitAll(tasks);

            int numberSucceeded = 0;
            int totalKeysUpdated = 0;
            foreach (var threadData in updatedKeys.Values)
            {
                totalKeysUpdated += threadData.Keys.Count;
                if (threadData.Succeeded)
                    numberSucceeded++;
            }

            Assert.True(numberSucceeded == tasks.Length, "One or more threads failed!");
            Assert.True(totalKeysUpdated == dictionary.Count,
               string.Format("TestTryUpdate:  FAILED.  The updated keys count doesn't match the dictionary count, expected {0}, actual {1}", dictionary.Count, totalKeysUpdated));
            foreach (var value in updatedKeys.Values)
            {
                for (int i = 0; i < value.Keys.Count; i++)
                    Assert.True(dictionary[value.Keys[i]] == value.ThreadIndex,
                       string.Format("TestTryUpdate:  FAILED.  The updated value doesn't match the thread index, expected {0} actual {1}", value.ThreadIndex, dictionary[value.Keys[i]]));
            }

            //test TryUpdate with non atomic values (intPtr > 8)
            var dict = new LurchTable<int, Struct16>();
            dict.TryAdd(1, new Struct16(1, -1));
            Assert.True(dict.TryUpdate(1, new Struct16(2, -2), new Struct16(1, -1)), "TestTryUpdate:  FAILED.  TryUpdate failed for non atomic values ( > 8 bytes)");
        }

        #region Helper Classes and Methods

        private class ThreadData
        {
            public int ThreadIndex;
            public bool Succeeded = true;
            public SCG.List<string> Keys = new SCG.List<string>();
        }

        private struct Struct16 : IEqualityComparer<Struct16>
        {
            public long L1, L2;
            public Struct16(long l1, long l2)
            {
                L1 = l1;
                L2 = l2;
            }

            public bool Equals(Struct16 x, Struct16 y)
            {
                return x.L1 == y.L1 && x.L2 == y.L2;
            }

            public int GetHashCode(Struct16 obj)
            {
                return (int)L1;
            }
        }

        #endregion
    }
}
