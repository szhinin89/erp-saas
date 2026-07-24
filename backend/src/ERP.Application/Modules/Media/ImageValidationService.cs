using ERP.Application.Common;
using ERP.Application.Common.Models;

namespace ERP.Application.Modules.Media;

/// <summary>
/// Implementación manual (sin librerías externas) de detección de formato y
/// dimensiones para PNG, JPEG y WEBP a partir de los magic bytes del archivo.
/// </summary>
public sealed class ImageValidationService : IImageValidationService
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;
    public const int MaxDimensionPx = 4000;

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public Result<ImageMetadata> Validate(MediaUploadContent content)
    {
        if (content.SizeBytes <= 0)
            return Result<ImageMetadata>.ValidationFailure("El archivo está vacío.");

        if (content.SizeBytes > MaxSizeBytes)
            return Result<ImageMetadata>.ValidationFailure("El archivo supera el tamaño máximo permitido de 5 MB.");

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            content.Content.Position = 0;
            content.Content.CopyTo(ms);
            bytes = ms.ToArray();
            content.Content.Position = 0;
        }

        if (bytes.Length > MaxSizeBytes)
            return Result<ImageMetadata>.ValidationFailure("El archivo supera el tamaño máximo permitido de 5 MB.");

        var metadata = TryReadPng(bytes) ?? TryReadJpeg(bytes) ?? TryReadWebp(bytes);

        if (metadata is null)
            return Result<ImageMetadata>.ValidationFailure("Formato de imagen no soportado. Use PNG, JPG o WEBP.");

        if (metadata.Width <= 0 || metadata.Height <= 0)
            return Result<ImageMetadata>.ValidationFailure("No se pudo determinar las dimensiones de la imagen.");

        if (metadata.Width > MaxDimensionPx || metadata.Height > MaxDimensionPx)
            return Result<ImageMetadata>.ValidationFailure(
                $"Las dimensiones de la imagen superan el máximo permitido de {MaxDimensionPx}x{MaxDimensionPx} px.");

        return Result<ImageMetadata>.Success(metadata);
    }

    private static ImageMetadata? TryReadPng(byte[] b)
    {
        if (b.Length < 24)
            return null;

        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (b[i] != PngSignature[i])
                return null;
        }

        // IHDR es siempre el primer chunk: bytes 8-11 longitud, 12-15 "IHDR", 16-19 width, 20-23 height.
        if (b[12] != (byte)'I' || b[13] != (byte)'H' || b[14] != (byte)'D' || b[15] != (byte)'R')
            return null;

        var width = ReadUInt32BigEndian(b, 16);
        var height = ReadUInt32BigEndian(b, 20);
        return new ImageMetadata("image/png", "png", width, height);
    }

    private static ImageMetadata? TryReadJpeg(byte[] b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8 || b[2] != 0xFF)
            return null;

        var pos = 2;
        while (pos + 1 < b.Length)
        {
            if (b[pos] != 0xFF)
            {
                pos++;
                continue;
            }

            var marker = b[pos + 1];

            // Marcadores sin segmento de longitud.
            if (marker == 0xD8 || marker == 0xD9 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                pos += 2;
                continue;
            }

            if (pos + 4 > b.Length)
                break;

            var segmentLength = (b[pos + 2] << 8) | b[pos + 3];

            var isSof = marker >= 0xC0 && marker <= 0xCF
                        && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;

            if (isSof)
            {
                if (pos + 9 > b.Length)
                    return null;

                var height = (b[pos + 5] << 8) | b[pos + 6];
                var width = (b[pos + 7] << 8) | b[pos + 8];
                return new ImageMetadata("image/jpeg", "jpg", width, height);
            }

            if (marker == 0xDA) // Start of Scan: no hay más segmentos de cabecera.
                break;

            pos += 2 + segmentLength;
        }

        return null;
    }

    private static ImageMetadata? TryReadWebp(byte[] b)
    {
        if (b.Length < 30)
            return null;

        if (b[0] != (byte)'R' || b[1] != (byte)'I' || b[2] != (byte)'F' || b[3] != (byte)'F')
            return null;

        if (b[8] != (byte)'W' || b[9] != (byte)'E' || b[10] != (byte)'B' || b[11] != (byte)'P')
            return null;

        var fourCc = $"{(char)b[12]}{(char)b[13]}{(char)b[14]}{(char)b[15]}";

        switch (fourCc)
        {
            case "VP8X":
            {
                var width = 1 + (b[24] | (b[25] << 8) | (b[26] << 16));
                var height = 1 + (b[27] | (b[28] << 8) | (b[29] << 16));
                return new ImageMetadata("image/webp", "webp", width, height);
            }
            case "VP8L":
            {
                if (b[20] != 0x2F)
                    return null;

                var bits = b[21] | (b[22] << 8) | (b[23] << 16) | (b[24] << 24);
                var width = (bits & 0x3FFF) + 1;
                var height = ((bits >> 14) & 0x3FFF) + 1;
                return new ImageMetadata("image/webp", "webp", width, height);
            }
            case "VP8 ":
            {
                if (b[23] != 0x9D || b[24] != 0x01 || b[25] != 0x2A)
                    return null;

                var width = (b[26] | (b[27] << 8)) & 0x3FFF;
                var height = (b[28] | (b[29] << 8)) & 0x3FFF;
                return new ImageMetadata("image/webp", "webp", width, height);
            }
            default:
                return null;
        }
    }

    private static int ReadUInt32BigEndian(byte[] b, int offset)
        => (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
}
