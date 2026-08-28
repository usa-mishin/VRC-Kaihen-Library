using ImageMagick;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace VrcKaihenLibrary.Services;

internal static class ImagePreviewService
{
    private const long MaximumEmbeddedPreviewBytes = 64L * 1024 * 1024;
    private static readonly SemaphoreSlim PhotoshopDecodeGate = new(1, 1);

    private static readonly string[] NativeExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp"];

    private static readonly string[] EditingExtensions =
        [".psd", ".psb", ".clip", ".kra", ".xcf", ".sai", ".sai2", ".afphoto"];

    public static bool SupportsPreview(string path)
    {
        var extension = Path.GetExtension(path);
        return Array.Exists(NativeExtensions, x => x.Equals(extension, StringComparison.OrdinalIgnoreCase))
            || Array.Exists(EditingExtensions, x => x.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<BitmapImage?> LoadAsync(string path, uint requestedSize)
    {
        if (!File.Exists(path)) return null;
        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (Array.Exists(NativeExtensions, x => x.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            return await LoadNativeAsync(path, requestedSize);

        if (extension == ".kra")
        {
            var kraPreview = await LoadKritaPreviewAsync(path, requestedSize);
            if (kraPreview is not null) return kraPreview;
        }

        if (extension is ".psd" or ".psb")
        {
            await PhotoshopDecodeGate.WaitAsync();
            try
            {
                var encoded = await Task.Run(() => RenderPhotoshopPreview(path, requestedSize));
                if (encoded is not null) return await LoadEncodedAsync(encoded, requestedSize);
            }
            finally
            {
                PhotoshopDecodeGate.Release();
            }
        }

        // Proprietary formats such as CLIP, SAI and Affinity Photo can expose a
        // thumbnail through an installed Windows shell extension. This remains a
        // best-effort fallback and never launches the associated application.
        return await LoadShellThumbnailAsync(path, requestedSize);
    }

    private static async Task<BitmapImage?> LoadNativeAsync(string path, uint requestedSize)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await storageFile.OpenReadAsync();
            var image = new BitmapImage { DecodePixelWidth = (int)requestedSize };
            await image.SetSourceAsync(stream);
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<BitmapImage?> LoadKritaPreviewAsync(string path, uint requestedSize)
    {
        try
        {
            var bytes = await Task.Run(() =>
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
                var entry = archive.GetEntry("preview.png") ?? archive.GetEntry("mergedimage.png");
                if (entry is null || entry.Length <= 0 || entry.Length > MaximumEmbeddedPreviewBytes) return null;
                using var source = entry.Open();
                using var destination = new MemoryStream((int)entry.Length);
                source.CopyTo(destination);
                return destination.ToArray();
            });
            return bytes is null ? null : await LoadEncodedAsync(bytes, requestedSize);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? RenderPhotoshopPreview(string path, uint requestedSize)
    {
        try
        {
            var settings = new MagickReadSettings { FrameIndex = 0, FrameCount = 1 };
            using var image = new MagickImage(path, settings);
            image.AutoOrient();
            image.Thumbnail(requestedSize, requestedSize);
            image.Format = MagickFormat.Png32;
            return image.ToByteArray();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<BitmapImage?> LoadShellThumbnailAsync(string path, uint requestedSize)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(path);
            using var thumbnail = await storageFile.GetThumbnailAsync(
                ThumbnailMode.PicturesView, requestedSize, ThumbnailOptions.UseCurrentScale);
            if (thumbnail is null || thumbnail.Size == 0) return null;
            var image = new BitmapImage { DecodePixelWidth = (int)requestedSize };
            await image.SetSourceAsync(thumbnail);
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<BitmapImage?> LoadEncodedAsync(byte[] bytes, uint requestedSize)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
            }
            stream.Seek(0);
            var image = new BitmapImage { DecodePixelWidth = (int)requestedSize };
            await image.SetSourceAsync(stream);
            return image;
        }
        catch
        {
            return null;
        }
    }
}
