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
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        return ImageSource.FromStream(() => new MemoryStream(qrCodeImage));
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
