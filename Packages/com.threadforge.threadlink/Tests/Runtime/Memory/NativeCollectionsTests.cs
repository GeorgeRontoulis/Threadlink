namespace Threadforge.Threadlink.Tests
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;

    public unsafe class NativeCollectionsTests
    {
        [Test]
        public void HashCollectionsRejectOverflowAndReuseDeletedSlots()
        {
            byte* memory = NativeAllocator.AllocateZeroed(128);
            try
            {
                var set = new InlineHashSet<int>(memory, 1);
                int first = 1,
                    second = 2;
                Assert.That(set.Add(in first), Is.True);
                Assert.That(set.Add(in first), Is.False);
                bool threw = false;
                try
                {
                    set.Add(in second);
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }
                Assert.That(threw, Is.True);
                Assert.That(set.Count, Is.EqualTo(1));
                Assert.That(set.Remove(in first), Is.True);
                Assert.That(set.Add(in second), Is.True);
                Assert.That(set.Contains(in second), Is.True);
                set.Clear();
                var array = new InlineArray<int>(memory, 4);
                array[3] = 19;
                Assert.That(array[3], Is.EqualTo(19));
                threw = false;
                try
                {
                    array[4] = 99;
                }
                catch (IndexOutOfRangeException)
                {
                    threw = true;
                }
                Assert.That(threw, Is.True);
            }
            finally
            {
                NativeAllocator.Free(memory);
            }
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(64)]
        public void SetAndDictionaryMatchManagedModelsUnderChurn(int capacity)
        {
            byte* a = NativeAllocator.AllocateZeroed(InlineLayout.SetBytes(capacity, sizeof(int)));
            byte* b = NativeAllocator.AllocateZeroed(
                InlineLayout.DictionaryBytes(capacity, sizeof(int), sizeof(int))
            );
            try
            {
                var set = new InlineHashSet<int>(a, capacity);
                var map = new InlineDictionary<int, int>(b, capacity);
                var expected = new Dictionary<int, int>();
                var random = new Random(914);
                for (int i = 0; i < 3000; i++)
                {
                    int key = random.Next(capacity * 3),
                        value = i;
                    if ((i & 3) == 0)
                    {
                        bool removed = expected.Remove(key);
                        Assert.That(set.Remove(in key), Is.EqualTo(removed));
                        Assert.That(map.Remove(in key), Is.EqualTo(removed));
                    }
                    else if (expected.Count < capacity || expected.ContainsKey(key))
                    {
                        bool added = expected.TryAdd(key, value);
                        Assert.That(set.Add(in key), Is.EqualTo(added));
                        Assert.That(map.TryAdd(in key, in value), Is.EqualTo(added));
                    }
                    Assert.That(map.Count, Is.EqualTo(expected.Count));
                    Assert.That(set.Count, Is.EqualTo(expected.Count));
                    foreach (var pair in expected)
                    {
                        int k = pair.Key;
                        Assert.That(set.Contains(in k), Is.True);
                        Assert.That(map.TryGetValue(in k, out int actual), Is.True);
                        Assert.That(actual, Is.EqualTo(pair.Value));
                    }
                }
                set.Clear();
                map.Clear();
                Assert.That(set.Count, Is.Zero);
                Assert.That(map.Count, Is.Zero);
            }
            finally
            {
                NativeAllocator.Free(a);
                NativeAllocator.Free(b);
            }
        }

        [Test]
        public void SuccessfulHashInsertionAndChurnAllocateNothing()
        {
            byte* a = NativeAllocator.AllocateZeroed(InlineLayout.SetBytes(8, 4));
            byte* b = NativeAllocator.AllocateZeroed(InlineLayout.DictionaryBytes(8, 4, 4));
            try
            {
                var set = new InlineHashSet<int>(a, 8);
                var map = new InlineDictionary<int, int>(b, 8);
                int key = 42,
                    value = 99;
                set.Add(in key);
                map.TryAdd(in key, in value);
                set.Clear();
                map.Clear();
                using var measurement = AllocationMeasurement.Start();
                for (int i = 0; i < 10000; i++)
                {
                    set.Add(in key);
                    map.TryAdd(in key, in value);
                    set.Remove(in key);
                    map.Remove(in key);
                }
                measurement.AssertNoAllocations();
            }
            finally
            {
                NativeAllocator.Free(a);
                NativeAllocator.Free(b);
            }
        }

        [Test]
        public void QueueWrapsAndStackAndListPreserveTheirContracts()
        {
            byte* p = NativeAllocator.AllocateZeroed(128);
            try
            {
                var queue = new InlineQueue<int>(p, 4);
                for (int round = 0; round < 20; round++)
                {
                    for (int i = 0; i < 4; i++)
                        queue.Enqueue(in i);
                    for (int i = 0; i < 4; i++)
                        Assert.That(queue.Dequeue(), Is.EqualTo(i));
                }
                queue.Clear();
                var stack = new InlineStack<int>(p, 4);
                for (int i = 0; i < 4; i++)
                    stack.Push(in i);
                for (int i = 3; i >= 0; i--)
                    Assert.That(stack.Pop(), Is.EqualTo(i));
                var list = new InlineList<int>(p, 4);
                for (int i = 0; i < 4; i++)
                    list.Add(in i);
                list.RemoveAt(1);
                Assert.That(list[1], Is.EqualTo(3));
                Assert.That(list.Count, Is.EqualTo(3));
                list.Clear();
                Assert.That(list.Count, Is.Zero);
            }
            finally
            {
                NativeAllocator.Free(p);
            }
        }

        [Test]
        public void AllocatorZerosAlignsCopiesAndRejectsInvalidSizes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NativeAllocator.AllocateZeroed(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => NativeAllocator.AllocateZeroed(8, 3));
            Assert.Throws<OverflowException>(() => InlineLayout.ArrayBytes(int.MaxValue, 8));
            byte* a = NativeAllocator.AllocateZeroed(128),
                b = NativeAllocator.AllocateZeroed(128);
            try
            {
                Assert.That((ulong)a % 64, Is.Zero);
                for (int i = 0; i < 128; i++)
                    Assert.That(a[i], Is.Zero);
                a[33] = 19;
                NativeAllocator.Copy(b, a, 128);
                Assert.That(NativeAllocator.Equal(a, b, 128), Is.True);
                NativeAllocator.Clear(b, 128);
                Assert.That(NativeAllocator.Equal(a, b, 128), Is.False);
            }
            finally
            {
                NativeAllocator.Free(a);
                NativeAllocator.Free(b);
            }
        }
    }
}
