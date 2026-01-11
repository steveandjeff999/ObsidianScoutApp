using Microsoft.Maui.Controls;

namespace ObsidianScout.Models;

public class GraphHtmlInfo
{
    public string GraphType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    
    public WebViewSource WebViewSource
    {
        get
        {
            if (string.IsNullOrEmpty(HtmlContent))
                return new HtmlWebViewSource { Html = string.Empty };
            
            // If it looks like a URI (file:// or http://), use UrlWebViewSource
            if (HtmlContent.StartsWith("file://") || HtmlContent.StartsWith("http://") || HtmlContent.StartsWith("https://"))
            {
                return new UrlWebViewSource { Url = HtmlContent };
            }
            
            // Otherwise treat as HTML
            return new HtmlWebViewSource { Html = HtmlContent };
        }
    }
}

