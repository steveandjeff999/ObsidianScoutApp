using QRCoder;
using System.Text.Json;

namespace ObsidianScout.Services;

public interface IQRCodeService
{
    ImageSource GenerateQRCode(string data);
    string SerializeScoutingData(Dictionary<string, object?> data);
}

public class QRCodeService : IQRCodeService
{
    public ImageSource GenerateQRCode(string data)
    {
        // Try progressively lower ECC levels to accommodate larger payloads
        QRCodeGenerator.ECCLevel[] eccLevels =
        [
            QRCodeGenerator.ECCLevel.Q,  // ~1663 bytes
            QRCodeGenerator.ECCLevel.M,  // ~2331 bytes
            QRCodeGenerator.ECCLevel.L   // ~2953 bytes
        ];

        Exception? lastException = null;
        foreach (var ecc in eccLevels)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(data, ecc);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20);
                return ImageSource.FromStream(() => new MemoryStream(qrCodeImage));
            }
            catch (Exception ex) when (ex.Message.Contains("maximum size", StringComparison.OrdinalIgnoreCase) ||
                                        ex.Message.Contains("exceeds", StringComparison.OrdinalIgnoreCase) ||
                                        ex.Message.Contains("too long", StringComparison.OrdinalIgnoreCase))
            {
                lastException = ex;
                System.Diagnostics.Debug.WriteLine($"[QRCode] ECC level {ecc} capacity exceeded, trying lower level...");
            }
        }

        throw new InvalidOperationException(
            $"Data too large for QR code ({data.Length} bytes). Try scouting fewer teams at once.",
            lastException);
    }

    public string SerializeScoutingData(Dictionary<string, object?> data)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(data, options);
    }
}
