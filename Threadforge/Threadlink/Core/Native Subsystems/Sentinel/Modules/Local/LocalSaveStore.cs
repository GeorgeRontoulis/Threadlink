namespace Threadlink.SentinelModules.Local
{
    using Core.NativeSubsystems.Sentinel;
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.IO;
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

        private string Root { get; set; }
        private Dictionary<int, SemaphoreSlim> PublishLocks { get; set; } = new();

        internal LocalSaveStore(string root) => Root = root;

        internal SemaphoreSlim GetPublishLock(int saveID)
        {
            lock (PublishLocks)
            {
                if (!PublishLocks.TryGetValue(saveID, out var gate))
                {
                    gate = new SemaphoreSlim(1, 1);
                    PublishLocks.Add(saveID, gate);
                }

                return gate;
            }
        }

        internal string GetSaveDirectory(int saveID) =>
            Path.Combine(Root, saveID.ToString());

        internal string GetGenerationRoot(int saveID) =>
            Path.Combine(GetSaveDirectory(saveID), "generations");

        internal string GetGenerationDirectory(int saveID, string generation) =>
            Path.Combine(GetGenerationRoot(saveID), generation);

        internal string GetPointerPath(int saveID) =>
            Path.Combine(GetSaveDirectory(saveID), "current.ptr");

        internal string GetStagingDirectory(int saveID, string generation) =>
            Path.Combine(Root, ".staging", saveID + "-" + generation);

        internal static string GetFilePath(string generationDirectory, int fileID) =>
            Path.Combine(generationDirectory, fileID + ".sent");

        internal async UniTask<SentinelResult<string>> ReadPointerAsync(int saveID)
        {
            string pointer = GetPointerPath(saveID);

            if (!File.Exists(pointer))
                return SentinelResult<string>.Failure(SentinelError.NotFound);

            try
            {
                string value = await File.ReadAllTextAsync(pointer).AsUniTask();

                if (string.IsNullOrWhiteSpace(value))
                {
                    return SentinelResult<string>.Failure(
                        SentinelError.CorruptData,
                        "The save generation pointer is empty.");
                }

                value = value.Trim();

                if (value == DELETED)
                    return SentinelResult<string>.Failure(SentinelError.NotFound);

                return SentinelResult<string>.Success(value);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SentinelResult<string>.Failure(
                    SentinelError.PermissionDenied,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return SentinelResult<string>.Failure(
                    SentinelError.NativeFailure,
                    exception.Message);
            }
        }

        internal async UniTask<SentinelResult<string>> ReadRawPointerAsync(int saveID)
        {
            string pointer = GetPointerPath(saveID);

            if (!File.Exists(pointer))
                return SentinelResult<string>.Success(null);

            try
            {
                string value = (await File.ReadAllTextAsync(pointer).AsUniTask()).Trim();

                if (string.IsNullOrEmpty(value))
                {
                    return SentinelResult<string>.Failure(
                        SentinelError.CorruptData,
                        "The existing save generation pointer is empty.");
                }

                if (value != DELETED && !Directory.Exists(GetGenerationDirectory(saveID, value)))
                {
                    return SentinelResult<string>.Failure(
                        SentinelError.CorruptData,
                        $"The published save generation '{value}' is missing.");
                }

                return SentinelResult<string>.Success(value);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SentinelResult<string>.Failure(
                    SentinelError.PermissionDenied,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return SentinelResult<string>.Failure(
                    SentinelError.NativeFailure,
                    exception.Message);
            }
        }

        internal async UniTask<SentinelResult> PublishPointerAsync(int saveID, string value)
        {
            string saveDirectory = GetSaveDirectory(saveID);
            string pointer = GetPointerPath(saveID);
            string temp = Path.Combine(saveDirectory, $"current.{Guid.NewGuid():N}.tmp");

            try
            {
                Directory.CreateDirectory(saveDirectory);
                await WriteDurableTextAsync(temp, value);

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

                return SentinelResult.Success();
            }
            catch (PlatformNotSupportedException exception)
            {
                return SentinelResult.Failure(
                    SentinelError.Unsupported,
                    "The local filesystem does not support atomic save publication: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return SentinelResult.Failure(
                    SentinelError.PermissionDenied,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return SentinelResult.Failure(
                    SentinelError.NativeFailure,
                    exception.Message);
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
                true);

            await stream.WriteAsync(data, 0, data.Length).AsUniTask();
            await stream.FlushAsync().AsUniTask();
            stream.Flush(true);
        }

        private static async UniTask WriteDurableTextAsync(string path, string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(value);

            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                true);

            await stream.WriteAsync(data, 0, data.Length).AsUniTask();
            await stream.FlushAsync().AsUniTask();
            stream.Flush(true);
        }

        internal static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

                string target = Path.Combine(destination, relative);
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
            if (PublishLocks != null)
            {
                lock (PublishLocks)
                {
                    foreach (var gate in PublishLocks.Values)
                        gate.Dispose();

                    PublishLocks.Clear();
                }
            }

            PublishLocks = null;
            Root = null;
        }
    }
}
