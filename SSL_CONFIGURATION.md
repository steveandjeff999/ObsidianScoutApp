# SSL Certificate & HTTP Configuration Guide

## Overview

The OBSIDIAN Scout Mobile App is configured to work with:
- ? **HTTP connections** (for local development)
- ? **HTTPS with valid certificates** (for production)
- ? **HTTPS with self-signed certificates** (for development/testing)

## Configuration Summary

### All Platforms

The app uses a custom `HttpClientHandler` configured in `MauiProgram.cs` that:

**Debug Mode (Development):**
- Accepts ALL SSL certificates (including self-signed)
- Allows HTTP connections
- No certificate validation

**Release Mode (Production):**
- Accepts ALL SSL certificates by default (can be customized)
- You can add specific certificate validation if needed

## Platform-Specific Settings

### Windows
- No additional configuration needed
- HttpClientHandler settings apply automatically
- Works with HTTP and self-signed HTTPS out of the box

### Android

**Files Modified:**
1. `Platforms/Android/AndroidManifest.xml`
   - `android:usesCleartextTraffic="true"` - Allows HTTP connections
   - `android:networkSecurityConfig="@xml/network_security_config"` - References security config

2. `Platforms/Android/Resources/xml/network_security_config.xml`
   - Allows cleartext (HTTP) traffic
   - Trusts system certificates
   - Trusts user-added certificates (for self-signed certs)
   - Debug overrides for development builds

**What This Enables:**
- ? HTTP connections (e.g., `http://192.168.1.100:8080`)
- ? HTTPS with self-signed certificates
- ? User can install custom certificates in Android settings

### iOS

**File Modified:**
- `Platforms/iOS/Info.plist`
  - `NSAppTransportSecurity` - App Transport Security settings
  - `NSAllowsArbitraryLoads` - Allows HTTP and self-signed certs globally
  - `NSExceptionDomains` - Specific domain exceptions (localhost configured)

**What This Enables:**
- ? HTTP connections to any domain
- ? HTTPS with self-signed certificates
- ? Bypass ATS (App Transport Security) restrictions

### macOS (Catalyst)

**File Modified:**
- `Platforms/MacCatalyst/Info.plist`
  - Same `NSAppTransportSecurity` settings as iOS

**What This Enables:**
- ? HTTP connections
- ? HTTPS with self-signed certificates
- ? Bypass ATS restrictions

## Usage Scenarios

### Scenario 1: Local Development with HTTP

```
Server Configuration:
- Protocol: http
- Address: 192.168.1.100
- Port: 8080
- Full URL: http://192.168.1.100:8080

Status: ? Works on all platforms
```

### Scenario 2: Self-Signed Certificate (Development)

```
Server Configuration:
- Protocol: https
- Address: dev.scout.local
- Port: 443
- Full URL: https://dev.scout.local:443

Certificate: Self-signed

Status: ? Works on all platforms (no certificate warnings)
```

### Scenario 3: Valid SSL Certificate (Production)

```
Server Configuration:
- Protocol: https
- Address: scout.team5454.com
- Port: 443
- Full URL: https://scout.team5454.com:443

Certificate: Valid (Let's Encrypt, DigiCert, etc.)

Status: ? Works on all platforms
```

### Scenario 4: Localhost Development

```
Server Configuration:
- Protocol: http
- Address: localhost
- Port: 3000
- Full URL: http://localhost:3000

Status: ? Works on all platforms
```

## Security Considerations

### Development vs Production

**Current Configuration:**
- ?? The app currently accepts ALL certificates in both debug and release modes
- This is convenient for development but may not be suitable for production

**Recommended for Production:**

Edit `MauiProgram.cs` to add proper certificate validation in release mode:

```csharp
#if DEBUG
    // Accept all certificates in debug
    handler.ServerCertificateCustomValidationCallback = 
        (sender, cert, chain, sslPolicyErrors) => true;
#else
    // In production, validate certificates properly
    handler.ServerCertificateCustomValidationCallback = 
        (sender, cert, chain, sslPolicyErrors) =>
        {
            // Accept valid certificates
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            
            // Option 1: Accept specific certificate thumbprint
            if (cert?.Thumbprint == "YOUR_PRODUCTION_CERT_THUMBPRINT")
                return true;
            
            // Option 2: Accept certificates from specific CA
            // Add your validation logic here
            
            // Reject all others
            return false;
        };
#endif
```

### Android Certificate Installation

For production apps with self-signed certificates, users can install the certificate:

1. Download the `.crt` or `.pem` certificate file
2. Open Android Settings
3. Security ? Encryption & credentials ? Install a certificate
4. Select "CA certificate"
5. Navigate to and select the certificate file
6. Give it a name and install

Once installed, the app will trust this certificate.

### iOS Certificate Trust

For iOS devices with self-signed certificates:

1. Email or AirDrop the certificate to the device
2. Install the certificate profile
3. Go to Settings ? General ? About ? Certificate Trust Settings
4. Enable full trust for the certificate

## Testing SSL/Certificate Configuration

### Test with HTTP
```
1. Configure server: http://192.168.1.100:8080
2. Tap "Test Connection"
3. Should see: "? Connection successful"
```

### Test with Self-Signed HTTPS
```
1. Configure server: https://192.168.1.100:8443
2. Tap "Test Connection"
3. Should see: "? Connection successful" (no certificate errors)
```

### Test with Invalid Certificate
```
1. Configure server with intentionally wrong hostname
2. Example: https://192.168.1.100 with cert for "example.com"
3. In current config: Still succeeds (accepts all certs)
4. In strict production config: Would fail with certificate error
```

## Troubleshooting

### "Connection failed" with HTTP

**Problem:** Android won't connect to HTTP endpoints

**Solution:**
- Verify `android:usesCleartextTraffic="true"` is in AndroidManifest.xml
- Check network_security_config.xml exists and is properly referenced
- Rebuild the app completely

### "SSL connection error" on iOS

**Problem:** iOS won't connect to self-signed HTTPS

**Solution:**
- Verify NSAppTransportSecurity is in Info.plist
- Ensure NSAllowsArbitraryLoads is set to true
- Clean and rebuild the app

### "Certificate validation failed"

**Problem:** Release build rejects self-signed certificates

**Solution:**
- Check if you're in release mode with strict validation
- Either use a valid certificate or adjust validation logic
- For development, use debug builds

### Works on Windows but not Android/iOS

**Problem:** Platform-specific restrictions

**Solution:**
- Windows has most permissive settings by default
- Android/iOS require explicit permission in manifest/plist files
- Verify platform-specific files were properly modified
- Clean and rebuild for target platform

## Production Deployment Recommendations

### For Public Release

1. **Use Valid SSL Certificates**
   - Obtain from trusted CA (Let's Encrypt, DigiCert, etc.)
   - No app modifications needed
   - Users won't see certificate warnings

2. **Enable Certificate Pinning** (Advanced)
   - Pin specific certificate or public key
   - Prevents man-in-the-middle attacks
   - Requires app update if certificate changes

3. **Remove Arbitrary Loads** (iOS/macOS)
   - Set `NSAllowsArbitraryLoads` to `false`
   - Only allow specific domains in NSExceptionDomains
   - Improves security

### For Internal/Enterprise Use

1. **Current Configuration is OK**
   - Convenient for development
   - Works with internal servers
   - Self-signed certificates accepted

2. **Install Root Certificates**
   - Create internal CA
   - Install CA cert on all devices
   - Sign server certificates with internal CA
   - More secure than accepting all certificates

3. **Use HTTP for Local Network Only**
   - Only if server is on trusted local network
   - Behind firewall
   - No sensitive data transmission

## Code Reference

### Main SSL Configuration
**File:** `ObsidianScout/MauiProgram.cs`
```csharp
// Line ~30-50
handler.ServerCertificateCustomValidationCallback = 
    (sender, cert, chain, sslPolicyErrors) =>
    {
        return true; // Accept all certificates
    };
```

### Android Configuration
**Files:**
- `Platforms/Android/AndroidManifest.xml` - Manifest settings
- `Platforms/Android/Resources/xml/network_security_config.xml` - Network security

### iOS/macOS Configuration
**Files:**
- `Platforms/iOS/Info.plist` - iOS settings
- `Platforms/MacCatalyst/Info.plist` - macOS settings

## Summary

? **What Works Now:**
- HTTP connections on all platforms
- HTTPS with self-signed certificates on all platforms
- HTTPS with valid certificates on all platforms
- No certificate warnings or errors

?? **Security Consideration:**
- App accepts ALL SSL certificates in current configuration
- Fine for development and internal use
- Consider stricter validation for public/production release

?? **Customization:**
- Modify `MauiProgram.cs` for different certificate validation rules
- Add specific certificate thumbprints or CA validation
- Use conditional compilation (#if DEBUG) for different behaviors

---

**Last Updated:** 2025  
**Configuration Status:** ? Fully Configured for HTTP and Self-Signed HTTPS
