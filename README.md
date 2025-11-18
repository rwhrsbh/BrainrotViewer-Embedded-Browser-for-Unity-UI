# BrainRot Watching for Unity

A Chromium-based web browser plugin for Unity Editor that lets you watch TikTok, YouTube Shorts, and other web content directly inside Unity. Built on top of [UnityWebBrowser](https://github.com/Voltstro-Studios/UnityWebBrowser).

## Features

- **Full Chromium Browser** - Embedded CEF (Chromium Embedded Framework) in Unity Editor
- **Complete Input Support** - Mouse (click, drag, scroll) and keyboard support
- **Navigation Toolbar** - Back, forward, refresh buttons and URL address bar
- **Anti-Detection** - Hides automation signals (navigator.webdriver, canvas fingerprinting, etc.)
- **Auto-Play Videos** - Automatically attempts to play video content
- **Hardware Acceleration** - GPU-accelerated video decoding
- **Console Intercept** - Browser console logs forwarded to Unity Console
- **Click Debugging** - Visual circles show where clicks are registered
- **Codec Checking** - Built-in tool to check supported media codecs

## Requirements

- **Unity** 2019.4 or higher
- **Platform** Windows, Linux, or macOS
- **UnityWebBrowser** package installed

## Installation

### Step 1: Install UnityWebBrowser

You **must** install UnityWebBrowser and its dependencies first:

1. Visit the [UnityWebBrowser repository](https://github.com/Voltstro-Studios/UnityWebBrowser)
2. Follow their installation guide
3. Install these packages via Unity Package Manager:
   - `dev.voltstro.unitywebbrowser` (core package)
   - `dev.voltstro.unitywebbrowser.engine.cef` (Chromium Embedded Framework)
   - `dev.voltstro.unitywebbrowser.communication.pipes` (Communication layer)

**Important:** Make sure these assets exist in your Resources folder:
- **Cef Engine Configuration**
- **TCP Communication Layer**

### Step 2: Add BrainRot Viewer Plugin

1. Copy the `TikTokChromiumWindow.cs` to `Assets/Plugins/BrainRotWatching/Editor` in your Unity project
2. Unity will automatically compile the scripts
3. Done!

## Usage

### Opening the Browser Window

In Unity Editor: **Window → BrainRot Viewer (Chromium)**

A new editor window will open with an embedded Chromium browser.

### Navigation

- **Address Bar** - Type any URL and press Enter or click "Go"
- **◀ Back Button** - Navigate to previous page
- **▶ Forward Button** - Navigate to next page
- **⟳ Refresh Button** - Reload current page
- **? Help Button** - Toggle help information

### Interacting with Content

- **Click** - Left/right/middle mouse buttons work
- **Drag** - Click and drag to scroll or interact
- **Scroll** - Mouse wheel scrolling
- **Type** - Click on the browser and start typing (when focused)
- **Keyboard Shortcuts** - Enter, Backspace, Arrow keys, etc.

### Default URL

Opens `https://www.youtube.com/shorts` by default.

To change: Edit [TikTokChromiumWindow.cs:27](Editor/TikTokChromiumWindow.cs#L27):
```csharp
private string urlInput = "https://www.youtube.com/shorts";
```

## Features Explained

### Anti-Detection System

The plugin injects JavaScript to hide automation signals:

- ✓ `navigator.webdriver` is hidden
- ✓ `window.chrome` object added
- ✓ Canvas fingerprinting protection (adds noise)
- ✓ WebGL fingerprinting protection
- ✓ Fake plugins list
- ✓ Battery API spoofing
- ✓ `Event.isTrusted` always returns `true`

This helps bypass detection on websites like TikTok.

### Console Intercept

All browser console output (`console.log`, `console.error`, `console.warn`) is forwarded to Unity Console with `[Browser]` prefix.

### Click Debugging

Visual circles appear where you click:
- **Green circle** - Mouse down event
- **Red circle** - Mouse up event
- **Blue circle** - Drag event

Circles fade out after 2 seconds.

### Codec Checker

The plugin automatically checks supported media codecs on page load. Check Unity Console for results.

## Configuration

### CEF Arguments

The plugin automatically configures CEF with optimized arguments:

**Anti-Detection:**
- `--disable-blink-features=AutomationControlled`
- Custom User-Agent (Chrome 120)

**Media Codecs:**
- `--enable-proprietary-codecs` (H.264, AAC support)
- `--enable-media-stream`

**Hardware Acceleration:**
- `--enable-gpu`
- `--enable-accelerated-video-decode`
- `--enable-features=D3D11VideoDecoder` (Windows)

**Auto-Play:**
- `--autoplay-policy=no-user-gesture-required`

You can modify these in the `GetCefArguments()` method at [TikTokChromiumWindow.cs:420](Editor/TikTokChromiumWindow.cs#L420).

## Known Issues

### ⚠️ TikTok Videos Don't Play

**Problem:** TikTok uses the HE-AAC v2 audio codec (`mp4a.40.29`) which standard CEF builds don't support.

**Why:** CEF must be compiled with proprietary FFmpeg codecs enabled. Most pre-built CEF packages don't include this.

**Solutions:**
1. **Use YouTube Shorts instead** - They use AAC-LC which is supported
2. **Build custom CEF** - Compile CEF with proprietary codecs (advanced)

The plugin includes `--enable-proprietary-codecs` flag, but it only works if your CEF build was compiled with FFmpeg.

### Browser Window is Black/Empty

**Solutions:**
1. Check Unity Console for errors
2. Make sure CEF Engine Configuration exists in Resources
3. Restart Unity Editor
4. Check antivirus isn't blocking CEF process

### "Could not load CEF Engine" Error

**Solutions:**
1. Install `dev.voltstro.unitywebbrowser.engine.cef` package
2. Check that "Cef Engine Configuration" exists in Resources folder
3. Reinstall UnityWebBrowser

### "Could not load TCP Communication Layer" Error

**Solutions:**
1. Install the communication package
2. Check that "TCP Communication Layer" exists in Resources folder

## File Structure

```
Assets/Plugins/BrainRotWatching/
├── Editor/
│   └── TikTokChromiumWindow.cs    # Main browser window
└── README.md                       # This file
```

## How It Works

### Initialization (OnEnable)

1. Loads CEF Engine from Resources
2. Loads TCP Communication Layer from Resources
3. Creates `WebBrowserClient` instance
4. Configures CEF arguments via reflection
5. Sets browser resolution
6. Subscribes to events (`OnUrlChanged`, `OnLoadFinish`)
7. Initializes browser

### JavaScript Injection (OnLoadFinish)

When a page finishes loading:

1. **Console Intercept** - Forwards console output to Unity
2. **Codec Check** - Tests supported media codecs
3. **Anti-Detect** - Hides automation signals
4. **Auto-Play** - Finds and plays video elements

### Input Handling (HandleInput)

- Mouse events → `SendMouseClick()` / `SendMouseMove()` / `SendMouseScroll()`
- Keyboard events → `SendKeyboardControls()`
- Events are adjusted for toolbar height
- Visual debug circles are added

### Cleanup (OnDisable)

- Unsubscribes from events
- Disposes `WebBrowserClient`
- Kills CEF browser process

## Advanced Usage

### Execute Custom JavaScript

```csharp
browserClient.ExecuteJs(@"
    console.log('Hello from Unity!');
    document.body.style.backgroundColor = 'red';
");
```

### Change Browser Resolution

```csharp
browserClient.Resolution = new BrowserResolution(1920, 1080);
```

### Navigate Programmatically

```csharp
browserClient.LoadUrl("https://example.com");
browserClient.GoBack();
browserClient.GoForward();
browserClient.Refresh();
```

## Troubleshooting

### Check Codec Support

Open the browser window and check Unity Console for codec check results. Look for:

```
✓ video/mp4; codecs="avc1.42E01E"  → probably
✓ audio/mp4; codecs="mp4a.40.2"    → probably
✗ audio/mp4; codecs="mp4a.40.29"   → NO
```

If HE-AAC v2 shows "NO", TikTok videos won't play.

### Enable Debug Logging

The plugin already enables CEF logging. Check these files:
- `debug.log` in CEF engine folder
- Unity Console for `[TikTok Viewer]` messages

### Performance Issues

- Close the window when not in use
- Reduce window size
- Disable hardware acceleration (remove GPU flags from `GetCefArguments()`)

## Security Warning

This plugin:
- Disables web security (`--disable-web-security`)
- Disables sandbox (`noSandbox = true`)
- Allows insecure content

**Only use in safe development environments!** Do not use in production or with untrusted websites.

## Credits

- Built with [UnityWebBrowser](https://github.com/Voltstro-Studios/UnityWebBrowser) by Voltstro Studios
- Uses [Chromium Embedded Framework (CEF)](https://bitbucket.org/chromiumembedded/cef)

## License

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


## Support

**For UnityWebBrowser issues:**
- See [their repository](https://github.com/Voltstro-Studios/UnityWebBrowser/issues)

**For this plugin:**
- Check Unity Console for error messages
- Enable debug logging
- Create an issue in this repository
