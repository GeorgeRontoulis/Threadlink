# Memory

`Threadlink.Memory` (namespace `Threadlink.Memory`) is for long-lived state that is copied, hashed and saved as raw bytes, such as a simulation's frames. It allocates native memory, hashes it the same way on every machine, and lays collections out inside it, so a collection is copied, hashed and saved along with the bytes that hold it.

The assembly depends on nothing in Threadlink, and nothing in the framework references it. To use it, reference `Threadlink.Memory` from your assembly definition and enable unsafe code.

## NativeAllocator

| Member | Semantics |
|---|---|
| `AllocateZeroed(bytes, alignment = DEFAULT_ALIGNMENT)` | A zeroed block of `bytes` bytes, aligned to `alignment` (a power of two; `DEFAULT_ALIGNMENT` is 64, a cache line). Zeroing is always done, because a block hashed whole must not contain leftover bytes. |
| `Free(block)` | Frees a block from `AllocateZeroed`. A null block is ignored. |
| `Copy(destination, source, bytes)` | Copies between non-overlapping ranges. |
| `Clear(block, bytes)` | Zeroes a range. |
| `Equal(a, b, bytes)` | Whether two ranges hold the same bytes. |

Blocks come from Unity's tracked persistent allocator, so the Editor's leak detection reports any block never freed. Invalid sizes and alignments throw `ArgumentOutOfRangeException`; a failed allocation throws `OutOfMemoryException`.

## MemoryHash

Hashes of raw memory. The same bytes give the same hash on every machine and runtime, which a type's own `GetHashCode` does not promise.

| Member | Use |
|---|---|
| `XxHash64(byte* data, long length, long seed = 0)` | Fingerprints whole blocks, such as a simulation frame, to detect divergence. Returns exactly what `System.IO.Hashing.XxHash64` would, but works directly on raw memory and is many times faster in the Editor's Mono runtime. Big-endian runtimes fall back to the library. |
| `XxHash32(byte* data, int length)`, `XxHash32<T>(in T value)` | Places keys in hash tables. A value's padding must be zeroed. |

## Inline collections

Inline collections are views over a fixed number of slots inside another value's memory, such as a simulation component. Each is a `ref struct` over a pointer, because that memory can be copied or moved (a frame rolled back). It can't be stored, boxed or captured, so no pointer outlives the call that made it. Elements are `unmanaged`.

| Type | Behaviour |
|---|---|
| `InlineArray<T>(memory, length)` | `Length` slots, always present. Indexer by reference, `Clear()`, enumeration. |
| `InlineList<T>(memory, capacity)` | `Count`, indexer, `Add`, `RemoveAt` (moves the last element into the gap), `Clear()`, enumeration. |
| `InlineQueue<T>(memory, capacity)` | A first-in, first-out ring: `Enqueue`, `Dequeue`, `Peek`, `Clear()`. |
| `InlineStack<T>(memory, capacity)` | Last in, first out: `Push`, `Pop`, `Peek`, `Clear()`. |
| `InlineHashSet<T>(memory, capacity)` | `Contains`, `Add` and `Remove` (each reporting whether it changed the set), `Clear()`, enumeration. |
| `InlineDictionary<TKey, TValue>(memory, capacity)` | `ContainsKey`, `TryGetValue`, indexer by reference (throws if absent), `TryAdd`, `Remove`, `Clear()`, enumeration of `(Key, Value)`. |

Adding past capacity throws. Element order depends only on the operations performed, as it must for anything hashed or compared byte for byte.

Sets and dictionaries use open addressing with linear probing over a fixed table. Keys are hashed and compared as raw bytes, never through `GetHashCode` or `Equals`, so a table holds the same bytes on every machine; zero a key's padding for this reason. Removal leaves a marker that a later insertion reuses, so churn never fills the table. Capacity is the table size: leave headroom above the most entries you expect, because a nearly full table probes further.

### Layout

`InlineLayout` defines the byte layout; a layout generator must produce exactly these sizes:

- Every header, slot and value starts on an 8-byte boundary (`InlineLayout.Align(size)` rounds up to a multiple of 8).
- A list, queue, stack, set or dictionary starts with an 8-byte header (`HEADER_BYTES`) holding its count; a queue's header also holds its head. An array has no header.
- A set or dictionary slot is an 8-byte state (empty, occupied or removed), then the key, then the value.

| Member | Bytes of |
|---|---|
| `ArrayBytes(capacity, elementSize)` | An array. |
| `SequenceBytes(capacity, elementSize)` | A list, queue or stack. |
| `SetSlotBytes(elementSize)`, `SetBytes(capacity, elementSize)` | One hash-set slot; a hash set. |
| `DictionarySlotBytes(keySize, valueSize)`, `DictionaryBytes(capacity, keySize, valueSize)` | One dictionary slot; a dictionary. |

```csharp
int bytes = InlineLayout.SequenceBytes(capacity: 16, sizeof(int));
byte* block = NativeAllocator.AllocateZeroed(bytes);

var list = new InlineList<int>(block, capacity: 16);
list.Add(42);
ulong fingerprint = MemoryHash.XxHash64(block, bytes);

NativeAllocator.Free(block);
```
