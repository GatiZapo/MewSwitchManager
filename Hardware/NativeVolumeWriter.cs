using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MewNX.Models;

namespace MewNX.Hardware;

/// <summary>
/// Writes verified images directly to Windows raw storage targets.
/// Volume writing remains available for filesystem-level operations, while disk-image
/// flashing uses the physical-disk device so partition tables contained in .raw images
/// are written correctly.
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
    private const uint IoctlDiskUpdateProperties = 0x0007C018;

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

    public Task WritePhysicalDiskAsync(int diskNumber, string sourcePath, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        if (diskNumber < 0) throw new ArgumentOutOfRangeException(nameof(diskNumber));
        return WriteRawTargetAsync($@"\\.\PhysicalDrive{diskNumber}", sourcePath, progress, ct, dismount: false);
    }

    public Task WriteAsync(string volumePath, string sourcePath, IProgress<DownloadProgress>? progress, CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Direct volume writing requires Windows.");
        if (string.IsNullOrWhiteSpace(volumePath)) throw new ArgumentException("A Windows volume path is required.", nameof(volumePath));
        return WriteRawTargetAsync(volumePath, sourcePath, progress, ct, dismount: true);
    }

    private async Task WriteRawTargetAsync(string targetPath, string sourcePath, IProgress<DownloadProgress>? progress, CancellationToken ct, bool dismount)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Direct storage writing requires Windows.");
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("The image was not found.", sourcePath);

        var normalized = NormalizeTargetPath(targetPath);
        using var handle = CreateFile(normalized, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileFlagWriteThrough | FileFlagOverlapped, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Windows could not open the target storage: {normalized}");
        if (dismount) LockAndDismount(handle);
        await WriteHandleAsync(handle, sourcePath, progress, ct);

        if (!FlushFileBuffers(handle))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0) throw new Win32Exception(error, "Windows could not flush the target storage.");
        }

        if (!dismount && !DeviceIoControl(handle, IoctlDiskUpdateProperties, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0) throw new Win32Exception(error, "Windows could not refresh the written disk properties.");
        }
    }

    private static async Task WriteHandleAsync(SafeFileHandle handle, string sourcePath, IProgress<DownloadProgress>? progress, CancellationToken ct)
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
                var eta = speed > 1 && total > written ? TimeSpan.FromSeconds((total - written) / speed) : (TimeSpan?)null;
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
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not lock the target volume. Close any Explorer window or application using the USB and try again.");
        if (!DeviceIoControl(handle, FsctlDismountVolume, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not dismount the target volume safely.");
    }

    private static string NormalizeTargetPath(string path)
    {
        path = path.Trim();
        if (path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase) || path.StartsWith(@"\\.\Volume{", StringComparison.OrdinalIgnoreCase))
            return path.TrimEnd('\\') + "\\";
        return path;
    }
}
