using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;
using MewSwitchManager.Models;

namespace MewSwitchManager.Hardware;

/// <summary>
/// Writes an image directly to a Windows volume handle. The writer deliberately
/// opens the target as a raw volume and dismounts it before streaming the image.
/// </summary>
public sealed class NativeVolumeWriter
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const uint FileFlagOverlapped = 0x40000000;

    private const uint FsctlLockVolume = 0x00090018;
    private const uint FsctlDismountVolume = 0x00090020;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);

    public async Task WriteAsync(
        string volumePath,
        string sourcePath,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Direct volume writing requires Windows.");
        if (string.IsNullOrWhiteSpace(volumePath))
            throw new ArgumentException("A Windows volume path is required.", nameof(volumePath));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The Linux image was not found.", sourcePath);

        using var handle = OpenVolumeWithFallbacks(volumePath);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Windows could not open the target volume: {volumePath}");

        LockAndDismount(handle);
        await WriteHandleAsync(handle, sourcePath, progress, ct);
        if (!FlushFileBuffers(handle))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0)
                throw new Win32Exception(error, "Windows could not flush the target volume.");
        }
    }

    private static SafeFileHandle OpenVolumeWithFallbacks(string path)
    {
        foreach (var candidate in GetVolumePathCandidates(path))
        {
            var handle = CreateFile(
                candidate,
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagWriteThrough | FileFlagOverlapped,
                IntPtr.Zero);

            if (!handle.IsInvalid)
                return handle;

            handle.Dispose();
        }

        return CreateFile(
            NormalizeVolumePath(path),
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagWriteThrough | FileFlagOverlapped,
            IntPtr.Zero);
    }

    private static IEnumerable<string> GetVolumePathCandidates(string path)
    {
        path = path.Trim();
        var normalized = NormalizeVolumePath(path);
        yield return normalized;

        if (normalized.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
        {
            var alternate = @"\\.\" + normalized[4..];
            yield return alternate;
        }

        if (normalized.StartsWith(@"\\.\Volume{", StringComparison.OrdinalIgnoreCase))
        {
            var alternate = @"\\?\" + normalized[4..];
            yield return alternate;
        }
    }

    private static async Task WriteHandleAsync(
        SafeFileHandle handle,
        string sourcePath,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var streamHandle = new SafeFileHandle(handle.DangerousGetHandle(), ownsHandle: false);
        await using var destination = new FileStream(streamHandle, FileAccess.Write, 4 * 1024 * 1024, isAsync: true);

        var total = source.Length;
        long written = 0;
        var buffer = new byte[4 * 1024 * 1024];
        var watch = Stopwatch.StartNew();
        long lastBytes = 0;
        double lastSeconds = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0) break;
            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            written += read;

            if (watch.ElapsedMilliseconds >= 250)
            {
                var seconds = watch.Elapsed.TotalSeconds;
                var deltaSeconds = Math.Max(0.001, seconds - lastSeconds);
                var speed = (written - lastBytes) / deltaSeconds;
                var eta = speed > 1 && total > written
                    ? TimeSpan.FromSeconds((total - written) / speed)
                    : (TimeSpan?)null;
                progress?.Report(new DownloadProgress(written, total, speed, eta, "FLASHING USB"));
                lastBytes = written;
                lastSeconds = seconds;
                watch.Restart();
            }
        }

        await destination.FlushAsync(ct);
        progress?.Report(new DownloadProgress(total, total, 0, TimeSpan.Zero, "USB FLASH COMPLETE"));
    }

    private static void LockAndDismount(SafeFileHandle handle)
    {
        if (!DeviceIoControl(handle, FsctlLockVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "Windows could not lock the target volume. Close any Explorer window or application using the USB and try again.");
        }

        if (!DeviceIoControl(handle, FsctlDismountVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, "Windows could not dismount the target volume safely.");
        }
    }

    private static string NormalizeVolumePath(string path)
    {
        path = path.Trim();
        if (path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
            return path.TrimEnd('\\') + "\\";
        if (path.StartsWith(@"\\.\Volume{", StringComparison.OrdinalIgnoreCase))
            return path.TrimEnd('\\') + "\\";
        return path;
    }
}
