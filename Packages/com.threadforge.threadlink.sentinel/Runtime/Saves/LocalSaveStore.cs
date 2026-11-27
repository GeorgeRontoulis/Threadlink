namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// Generation-based local save store.
    ///
    /// Published save bytes are immutable. A transaction prepares a complete new generation
    /// under .staging, flushes it, then moves that generation into the immutable generations
    /// directory. The only publication mutation is an atomic replacement of current.ptr.
    ///
    /// Therefore:
    /// - preparation never renames/overwrites/deletes the currently published generation;
    /// - publication failure leaves current.ptr unchanged;
    /// - old generations remain valid for readers that already observed them;
    /// - logical deletion is a pointer tombstone, not destructive deletion.
    /// </summary>
    internal sealed class LocalSaveStore : IDisposable
    {
        internal const string DELETED = "@deleted";

        internal string Root { get; }
        internal int MaximumFileBytes { get; }
        private readonly PublicationState publication;
        internal bool IsDisposed { get; private set; }
        private static readonly object ActivityLock = new();
        private static readonly Dictionary<string, int> ActiveRoots = new(
            Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal
        );
        private static readonly Dictionary<string, PublicationState> Publications = new(
            Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal
        );

        private sealed class PublicationState
        {
            internal int Stores;
            internal readonly Dictionary<int, SemaphoreSlim> Gates = new();
        }

        private static void ReleasePublicationIfIdle(string root)
        {
            // Called under ActivityLock. Admitted readers/transactions retain the shared gates
            // even after their store is disposed, so a replacement service joins the same gate.
            if (!ActiveRoots.ContainsKey(root) && Publications[root].Stores == 0)
                Publications.Remove(root);
        }

        internal LocalSaveStore(string root, int maximumFileBytes = 16 * 1024 * 1024)
        {
            if (maximumFileBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));
            MaximumFileBytes = maximumFileBytes;
            string fullPath = Path.GetFullPath(root);
            string volumeRoot = Path.GetPathRoot(fullPath);
            string trimmed = fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
            Root = trimmed.Length < volumeRoot.Length ? volumeRoot : trimmed;
            lock (ActivityLock)
            {
                if (!Publications.TryGetValue(Root, out publication))
                    Publications.Add(Root, publication = new PublicationState());
                publication.Stores++;
            }
        }

        internal bool TryAcquire(out IDisposable lease)
        {
            lock (ActivityLock)
            {
                lease = null;
                if (IsDisposed)
                    return false;
                ActiveRoots.TryGetValue(Root, out int count);
                ActiveRoots[Root] = count + 1;
                lease = new Lease(Root);
                return true;
            }
        }

        private sealed class Lease : IDisposable
        {
            private string root;

            internal Lease(string root) => this.root = root;

            public void Dispose()
            {
                var released = Interlocked.Exchange(ref root, null);
                if (released == null)
                    return;
                lock (ActivityLock)
                {
                    int remaining = ActiveRoots[released] - 1;
                    if (remaining == 0)
                        ActiveRoots.Remove(released);
                    else
                        ActiveRoots[released] = remaining;
                    ReleasePublicationIfIdle(released);
                }
            }
        }

        internal PersistenceResult<SaveStorageReport> InspectStorage(int saveId)
        {
            if (saveId == 0)
                return PersistenceResult<SaveStorageReport>.Failure(
                    PersistenceError.InvalidArgument
                );
            lock (ActivityLock)
            {
                if (IsDisposed)
                    return PersistenceResult<SaveStorageReport>.Failure(
                        PersistenceError.NotInitialized
                    );
                try
                {
                    var directories = GenerationDirectories(saveId);
                    long bytes = 0;
                    foreach (var directory in directories)
                    foreach (
                        var file in Directory.GetFiles(
                            directory,
                            "*.sent",
                            SearchOption.TopDirectoryOnly
                        )
                    )
                        bytes += new FileInfo(file).Length;
                    return PersistenceResult<SaveStorageReport>.Success(
                        new(directories.Count, bytes)
                    );
                }
                catch (IOException e)
                {
                    return PersistenceResult<SaveStorageReport>.Failure(
                        PersistenceError.NativeFailure,
                        e.Message
                    );
                }
                catch (UnauthorizedAccessException e)
                {
                    return PersistenceResult<SaveStorageReport>.Failure(
                        PersistenceError.PermissionDenied,
                        e.Message
                    );
                }
            }
        }

        internal PersistenceResult<int> PruneHistory(int saveId)
        {
            if (saveId == 0)
                return PersistenceResult<int>.Failure(PersistenceError.InvalidArgument);
            lock (ActivityLock)
            {
                if (IsDisposed)
                    return PersistenceResult<int>.Failure(PersistenceError.NotInitialized);
                if (ActiveRoots.ContainsKey(Root))
                    return PersistenceResult<int>.Failure(
                        PersistenceError.Busy,
                        "Save readers or transactions are still active."
                    );
                try
                {
                    string pointer = GetPointerPath(saveId);
                    if (!File.Exists(pointer))
                        return PersistenceResult<int>.Success(0);
                    var parsed = ParsePointer(saveId, File.ReadAllText(pointer));
                    if (!parsed.Succeeded)
                        return PersistenceResult<int>.Failure(parsed.Error, parsed.Message);
                    string current = parsed.Value;
                    // A tombstone has no current generation: retain all history until an explicit replacement save exists.
                    if (current == DELETED)
                        return PersistenceResult<int>.Success(0);
                    var directories = GenerationDirectories(saveId);
                    directories.Sort(
                        (a, b) =>
                        {
                            int order = Directory
                                .GetCreationTimeUtc(b)
                                .CompareTo(Directory.GetCreationTimeUtc(a));
                            return order == 0 ? string.CompareOrdinal(a, b) : order;
                        }
                    );
                    int previous = 0,
                        removed = 0;
                    foreach (var directory in directories)
                    {
                        if (Path.GetFileName(directory) == current || previous++ == 0)
                            continue;
                        // Only validated, direct GUID children of this save's generation directory are eligible.
                        Directory.Delete(directory, true);
                        removed++;
                    }
                    return PersistenceResult<int>.Success(removed);
                }
                catch (IOException e)
                {
                    return PersistenceResult<int>.Failure(
                        PersistenceError.NativeFailure,
                        e.Message
                    );
                }
                catch (UnauthorizedAccessException e)
                {
                    return PersistenceResult<int>.Failure(
                        PersistenceError.PermissionDenied,
                        e.Message
                    );
                }
            }
        }

        private List<string> GenerationDirectories(int saveId)
        {
            var result = new List<string>();
            string parent = Path.GetFullPath(GetGenerationRoot(saveId));
            if (!Directory.Exists(parent))
                return result;
            if (
                (File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(GetSaveDirectory(saveId)) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0
            )
                throw new IOException("Save maintenance refuses linked directories.");
            foreach (var directory in Directory.GetDirectories(parent))
            {
                string full = Path.GetFullPath(directory);
                if (
                    Path.GetDirectoryName(full) != parent
                    || !Guid.TryParseExact(Path.GetFileName(full), "N", out _)
                )
                    continue;
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                    continue;
                // Local generations contain files only. Preserve unfamiliar layouts rather than deleting recursively through them.
                if (Directory.GetDirectories(full).Length != 0)
                    continue;
                result.Add(full);
            }
            return result;
        }

        internal SemaphoreSlim GetPublishLock(int saveId)
        {
            lock (ActivityLock)
            {
                if (!publication.Gates.TryGetValue(saveId, out var gate))
                {
                    gate = new SemaphoreSlim(1, 1);
                    publication.Gates.Add(saveId, gate);
                }

                return gate;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string GetSaveDirectory(int saveId) => Path.Combine(Root, saveId.ToString());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string GetGenerationRoot(int saveId) =>
            Path.Combine(GetSaveDirectory(saveId), "generations");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string GetGenerationDirectory(int saveId, string generation) =>
            Path.Combine(GetGenerationRoot(saveId), generation);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string GetPointerPath(int saveId) =>
            Path.Combine(GetSaveDirectory(saveId), "current.ptr");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal string GetStagingDirectory(int saveId, string generation) =>
            Path.Combine(Root, ".staging", saveId + "-" + generation);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string GetFilePath(string generationDirectory, int fileId) =>
            Path.Combine(generationDirectory, fileId + ".sent");

        internal async UniTask<PersistenceResult<string>> ReadPointerAsync(int saveId)
        {
            var result = await ReadRawPointerAsync(saveId);
            if (result.Succeeded && (result.Value == null || result.Value == DELETED))
                return PersistenceResult<string>.Failure(PersistenceError.NotFound);
            return result;
        }

        private PersistenceResult<string> ParsePointer(int saveId, string text)
        {
            string value = text.Trim();
            if (value == DELETED)
                return PersistenceResult<string>.Success(value);
            // Validate before path resolution: a generation is a direct GUID child only.
            if (!Guid.TryParseExact(value, "N", out _))
                return PersistenceResult<string>.Failure(
                    PersistenceError.CorruptData,
                    "The save generation pointer is not an N-format GUID."
                );
            if (!Directory.Exists(GetGenerationDirectory(saveId, value)))
                return PersistenceResult<string>.Failure(
                    PersistenceError.CorruptData,
                    $"The published save generation '{value}' is missing."
                );
            return PersistenceResult<string>.Success(value);
        }

        internal async UniTask<PersistenceResult<string>> ReadRawPointerAsync(int saveId)
        {
            var pointer = GetPointerPath(saveId);

            if (!File.Exists(pointer))
                return PersistenceResult<string>.Success(null);

            try
            {
                return ParsePointer(saveId, await File.ReadAllTextAsync(pointer));
            }
            catch (UnauthorizedAccessException exception)
            {
                return PersistenceResult<string>.Failure(
                    PersistenceError.PermissionDenied,
                    exception.Message
                );
            }
            catch (IOException exception)
            {
                return PersistenceResult<string>.Failure(
                    PersistenceError.NativeFailure,
                    exception.Message
                );
            }
        }

        internal async UniTask<PersistenceResult> PublishPointerAsync(int saveId, string value)
        {
            string saveDirectory = GetSaveDirectory(saveId);
            string pointer = GetPointerPath(saveId);
            string temp = Path.Combine(saveDirectory, $"current.{Guid.NewGuid():N}.tmp");

            try
            {
                Directory.CreateDirectory(saveDirectory);

                await WriteDurableTextAsync(temp, value);

                if (IsDisposed)
                    return PersistenceResult.Failure(
                        PersistenceError.NotInitialized,
                        "Storage owner closed before publication."
                    );

                if (File.Exists(pointer))
                {
                    // File.Replace is the publication primitive for an existing save.
                    // If the platform cannot provide atomic replacement, fail rather than
                    // falling back to a destructive/non-atomic sequence.
                    File.Replace(temp, pointer, null);
                }
                else
                {
                    // First publication: same-filesystem rename creates the pointer in one step.
                    File.Move(temp, pointer);
                }

                return PersistenceResult.Success();
            }
            catch (PlatformNotSupportedException exception)
            {
                return PersistenceResult.Failure(
                    PersistenceError.Unsupported,
                    "The local filesystem does not support atomic save publication: "
                        + exception.Message
                );
            }
            catch (UnauthorizedAccessException exception)
            {
                return PersistenceResult.Failure(
                    PersistenceError.PermissionDenied,
                    exception.Message
                );
            }
            catch (IOException exception)
            {
                return PersistenceResult.Failure(PersistenceError.NativeFailure, exception.Message);
            }
            finally
            {
                TryDeleteFile(temp);
            }
        }

        internal static async UniTask WriteDurableBytesAsync(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                true
            );

            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
            stream.Flush(true);
        }

        internal async UniTask<byte[]> ReadBytesAsync(string path)
        {
            using var input = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true
            );
            if (input.Length > MaximumFileBytes)
                throw new InvalidDataException("Save file exceeds the storage limit.");
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaximumFileBytes)
                    throw new InvalidDataException("Save file exceeds the storage limit.");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }

        private static async UniTask WriteDurableTextAsync(string path, string value)
        {
            var data = Encoding.UTF8.GetBytes(value);

            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                true
            );

            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
            stream.Flush(true);
        }

        internal static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (
                var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)
            )
            {
                var relative = directory[source.Length..]
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = file[source.Length..]
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.Combine(destination, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, false);
            }
        }

        internal static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch { }
        }

        internal static void TryDeleteFile(string file)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch { }
        }

        public void Dispose()
        {
            // Stop admitting work, but retain the state needed by operations already awaiting I/O.
            lock (ActivityLock)
            {
                if (IsDisposed)
                    return;
                IsDisposed = true;
                publication.Stores--;
                ReleasePublicationIfIdle(Root);
            }
        }
    }
}
