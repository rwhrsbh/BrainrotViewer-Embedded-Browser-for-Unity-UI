using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VoltstroStudios.UnityWebBrowser.Communication;
using VoltstroStudios.UnityWebBrowser.Core;
using VoltstroStudios.UnityWebBrowser.Core.Engines;
using VoltstroStudios.UnityWebBrowser.Shared;
using VoltstroStudios.UnityWebBrowser.Shared.Events;
using BrowserResolution = VoltstroStudios.UnityWebBrowser.Shared.Resolution;

/// <summary>
/// EditorWindow для отображения TikTok через Chromium (UnityWebBrowser)
/// </summary>
public class TikTokChromiumWindow : EditorWindow
{
    // Основной клиент браузера
    private WebBrowserClient browserClient;

    // Размер окна для отслеживания изменений
    private Vector2 lastWindowSize;

    // Для визуальной отладки кликов
    private List<ClickDebugInfo> debugClicks = new List<ClickDebugInfo>();

    // Адресная строка
    private string urlInput = "https://www.youtube.com/shorts";
    private const float toolbarHeight = 30f;

    // Для отображения информации
    private bool showHelp = false;

    // Фокус для клавиатуры
    private bool browserHasFocus = false;

    // Флаг для однократной инъекции JavaScript
    private bool jsInjected = false;

    // Отслеживание состояния кнопки мыши для drag
    private bool isMouseButtonDown = false;
    private Vector2 lastMousePosition;

    // JavaScript для перехвата консоли и отправки в Unity
    private const string CONSOLE_INTERCEPT_JS = @"
        (function() {
            const originalLog = console.log;
            const originalError = console.error;
            const originalWarn = console.warn;

            console.log = function(...args) {
                originalLog.apply(console, ['[Browser]', ...args]);
            };

            console.error = function(...args) {
                originalError.apply(console, ['[Browser ERROR]', ...args]);
            };

            console.warn = function(...args) {
                originalWarn.apply(console, ['[Browser WARN]', ...args]);
            };

            // Перехват необработанных ошибок
            window.addEventListener('error', function(e) {
                console.error('Uncaught error:', e.message, 'at', e.filename, ':', e.lineno);
            });

            window.addEventListener('unhandledrejection', function(e) {
                console.error('Unhandled promise rejection:', e.reason);
            });

            console.log('Console intercept installed');
        })();
    ";

    // Продвинутый JavaScript для маскировки автоматизации
    private const string ANTI_DETECT_JS = @"
        (function() {
            console.log('[TikTok Anti-Detect] Starting initialization...');

            // 1. Скрываем navigator.webdriver
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });

            // 2. Добавляем window.chrome
            if (!window.chrome) {
                window.chrome = {
                    runtime: { id: 'fake-runtime-id' },
                    loadTimes: function() {},
                    csi: function() {},
                    app: {}
                };
            } else if (!window.chrome.runtime) {
                window.chrome.runtime = { id: 'fake-runtime-id' };
            } else if (!window.chrome.runtime.id) {
                window.chrome.runtime.id = 'fake-runtime-id';
            }

            // 3. Переопределяем permissions
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) => (
                parameters.name === 'notifications' ?
                    Promise.resolve({ state: Notification.permission }) :
                    originalQuery(parameters)
            );

            // 4. Фейковые плагины (реалистичные)
            Object.defineProperty(navigator, 'plugins', {
                get: () => [
                    { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer', description: 'Portable Document Format' },
                    { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai', description: '' },
                    { name: 'Native Client', filename: 'internal-nacl-plugin', description: '' }
                ]
            });

            // 5. Языки
            Object.defineProperty(navigator, 'languages', {
                get: () => ['en-US', 'en', 'ru-RU', 'ru']
            });

            // 6. Battery API (если не существует)
            if (!navigator.getBattery) {
                navigator.getBattery = () => Promise.resolve({
                    charging: true,
                    chargingTime: 0,
                    dischargingTime: Infinity,
                    level: 1.0,
                    addEventListener: () => {},
                    removeEventListener: () => {}
                });
            }

            // 7. Connection API
            if (!navigator.connection) {
                Object.defineProperty(navigator, 'connection', {
                    get: () => ({
                        effectiveType: '4g',
                        downlink: 10,
                        rtt: 50,
                        saveData: false,
                        addEventListener: () => {},
                        removeEventListener: () => {}
                    })
                });
            }

            // 8. Canvas fingerprinting защита (добавляем шум)
            const originalToDataURL = HTMLCanvasElement.prototype.toDataURL;
            HTMLCanvasElement.prototype.toDataURL = function(type) {
                const context = this.getContext('2d');
                if (context) {
                    // Добавляем минимальный шум
                    const imageData = context.getImageData(0, 0, this.width, this.height);
                    for (let i = 0; i < imageData.data.length; i += 4) {
                        imageData.data[i] = imageData.data[i] + Math.floor(Math.random() * 2);
                    }
                    context.putImageData(imageData, 0, 0);
                }
                return originalToDataURL.apply(this, arguments);
            };

            // 9. WebGL fingerprinting защита
            const getParameter = WebGLRenderingContext.prototype.getParameter;
            WebGLRenderingContext.prototype.getParameter = function(parameter) {
                if (parameter === 37445) {
                    return 'Intel Inc.'; // UNMASKED_VENDOR_WEBGL
                }
                if (parameter === 37446) {
                    return 'Intel Iris OpenGL Engine'; // UNMASKED_RENDERER_WEBGL
                }
                return getParameter.apply(this, arguments);
            };

            // 10. Переопределяем Date для более реалистичного поведения
            const originalDate = Date;
            Date = class extends originalDate {
                getTimezoneOffset() {
                    return -180; // Moscow timezone
                }
            };

            // 11. Добавляем MediaDevices если нет
            if (!navigator.mediaDevices) {
                navigator.mediaDevices = {
                    getUserMedia: () => Promise.reject(new Error('Permission denied')),
                    enumerateDevices: () => Promise.resolve([
                        { deviceId: 'default', kind: 'audioinput', label: '', groupId: '' },
                        { deviceId: 'default', kind: 'videoinput', label: '', groupId: '' }
                    ])
                };
            }

            // 12. Screen resolution и color depth
            Object.defineProperty(screen, 'colorDepth', {
                get: () => 24
            });
            Object.defineProperty(screen, 'pixelDepth', {
                get: () => 24
            });

            // ПАТЧ: Глобально подделываем isTrusted для ВСЕХ событий
            const OriginalEvent = Event;
            const OriginalMouseEvent = MouseEvent;

            // Патчим Event.prototype.isTrusted
            Object.defineProperty(Event.prototype, 'isTrusted', {
                get: function() {
                    return true; // ВСЕ события теперь trusted!
                },
                configurable: true
            });

            console.log('[TikTok Anti-Detect] ✓ Event.isTrusted PATCHED - all events are now trusted');
            console.log('[TikTok Anti-Detect] ✓ navigator.webdriver:', navigator.webdriver);
            console.log('[TikTok Anti-Detect] ✓ window.chrome:', !!window.chrome);
            console.log('[TikTok Anti-Detect] ✓ plugins:', navigator.plugins.length);
            
            // ... (rest of ANTI_DETECT_JS)
            
            console.log('[TikTok Anti-Detect] ✓ Initialized successfully!');
            
        })();
    ";
    
    // ======== NEW: JAVASCRIPT FOR CODEC CHECKING ========
    private const string CODEC_CHECK_JS = @"
        (function() {
            console.clear();
            console.log('========================================');
            console.log('        STARTING CODEC CHECK');
            console.log('========================================');

            console.log('=== VIDEO CODECS ===');

            const video = document.createElement('video');
            const videoCodecs = [
                'video/mp4; codecs=""avc1.42E01E""',  // H.264 Baseline
                'video/mp4; codecs=""avc1.4D401E""',  // H.264 Main
                'video/mp4; codecs=""avc1.64001E""',  // H.264 High
                'video/webm; codecs=""vp8""',         // VP8
                'video/webm; codecs=""vp9""',         // VP9
                'video/ogg; codecs=""theora""',       // Theora
            ];

            videoCodecs.forEach(codec => {
                const support = video.canPlayType(codec);
                console.log(`${support ? '✓' : '✗'} ${codec.padEnd(50)} → ${support || 'NO'}`);
            });

            console.log('\n=== AUDIO CODECS ===');
            const audioCodecs = [
                'audio/mp4; codecs=""mp4a.40.2""',   // AAC-LC
                'audio/mp4; codecs=""mp4a.40.5""',   // HE-AAC
                'audio/mp4; codecs=""mp4a.40.29""',  // HE-AAC v2 (Critical for TikTok)
                'audio/mpeg',                       // MP3
                'audio/webm; codecs=""opus""',        // Opus
                'audio/webm; codecs=""vorbis""',      // Vorbis
                'audio/ogg; codecs=""opus""',         // Opus in Ogg
            ];

            audioCodecs.forEach(codec => {
                const support = video.canPlayType(codec);
                console.log(`${support ? '✓' : '✗'} ${codec.padEnd(50)} → ${support || 'NO'}`);
            });

            console.log('\n=== MSE (Media Source Extensions) ===');
            if (window.MediaSource) {
                console.log('MediaSource: ✓ SUPPORTED');
                
                const mseCodecs = [
                    'video/mp4; codecs=""avc1.42E01E""',
                    'video/mp4; codecs=""avc1.42E01E, mp4a.40.2""',
                    'video/mp4; codecs=""avc1.42E01E, mp4a.40.29""', // Critical for TikTok
                    'audio/mp4; codecs=""mp4a.40.2""',
                    'audio/mp4; codecs=""mp4a.40.29""', // Critical for TikTok
                    'video/webm; codecs=""vp8, opus""',
                    'video/webm; codecs=""vp9, opus""',
                ];
                
                console.log('\nMSE Support:');
                mseCodecs.forEach(codec => {
                    const support = MediaSource.isTypeSupported(codec);
                    console.log(`${support ? '✓' : '✗'} ${codec.padEnd(60)} → ${support}`);
                });
            } else {
                console.log('MediaSource: ✗ NOT SUPPORTED');
            }

            console.log('\n=== COPY THIS RESULT ===');
            console.log('========================================');
        })();
    ";


    private class ClickDebugInfo
    {
        public Vector2 position;
        public float time;
        public Color color;
    }

    [MenuItem("Window/BrainRot Viewer (Chromium)")]
    public static void ShowWindow()
    {
        TikTokChromiumWindow window = GetWindow<TikTokChromiumWindow>("BrainRot Viewer");
        window.minSize = new Vector2(400, 300);
        window.wantsMouseMove = true; // Включаем получение событий движения мыши
    }

    private void OnEnable()
    {
        // Загружаем движок CEF из Resources
        Engine cefEngine = Resources.Load<Engine>("Cef Engine Configuration");
        if (cefEngine == null)
        {
            Debug.LogError("Не удалось загрузить CEF Engine! Проверьте что пакет dev.voltstro.unitywebbrowser.engine.cef установлен.");
            return;
        }

        // Загружаем TCP Communication Layer из Resources
        CommunicationLayer commsLayer = Resources.Load<CommunicationLayer>("TCP Communication Layer");
        if (commsLayer == null)
        {
            Debug.LogError("Не удалось загрузить TCP Communication Layer!");
            return;
        }

        // Создаем клиент браузера
        browserClient = new WebBrowserClient
        {
            engine = cefEngine,
            communicationLayer = commsLayer,
            initialUrl = "https://www.youtube.com/shorts",
            javascript = true,
            localStorage = true,
            webRtc = true,
            backgroundColor = new Color32(0, 0, 0, 255), // Черный фон

            // Отключаем sandbox для лучшей совместимости с медиа
            // ВНИМАНИЕ: Используйте только в безопасном окружении!
            noSandbox = true,

            // ОТКЛЮЧАЕМ remote debugging
            remoteDebugging = false
        };

        // Настраиваем аргументы CEF для маскировки автоматизации и поддержки медиа
        ConfigureCefArguments();

        // Устанавливаем разрешение через рефлексию к приватному полю (до Init)
        var resolutionField = typeof(WebBrowserClient).GetField("resolution",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (resolutionField != null)
        {
            uint width = (uint)Mathf.Max(400, position.width);
            uint height = (uint)Mathf.Max(300, position.height);
            resolutionField.SetValue(browserClient, new BrowserResolution(width, height));
        }
        
        // Подписываемся на событие загрузки страницы для обновления URL и инъекции JS
        browserClient.OnUrlChanged += OnUrlChanged;
        browserClient.OnLoadFinish += OnLoadFinish;

        // Инициализируем браузер
        browserClient.Init();

        // Подписываемся на обновления редактора для анимации
        EditorApplication.update += OnEditorUpdate;

        lastWindowSize = new Vector2(position.width, position.height);

        // Выводим подсказку
        Debug.Log("========================================");
        Debug.Log("[TikTok Viewer] Браузер запущен!");
        Debug.Log("[TikTok Viewer] АНТИ-ДЕТЕКТ:");
        Debug.Log("[TikTok Viewer] - User-Agent: Chrome 120");
        Debug.Log("[TikTok Viewer] - navigator.webdriver скрыт");
        Debug.Log("[TikTok Viewer] - Canvas/WebGL fingerprinting защита");
        Debug.Log("[TikTok Viewer] ");
        Debug.Log("КРИТИЧЕСКАЯ ПРОБЛЕМА:");
        Debug.Log("CEF не поддерживает кодек HE-AAC v2 (mp4a.40.29)");
        Debug.Log("TikTok использует этот кодек и видео НЕ РАБОТАЕТ");
        Debug.Log("Нужна другая сборка CEF с проприетарными кодеками!");
        Debug.Log("========================================");
    }

    private void ConfigureCefArguments()
    {
        // Пытаемся получить доступ к аргументам CEF через рефлексию
        try
        {
            var engineType = browserClient.engine.GetType();
            var argsField = engineType.GetField("arguments", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            if (argsField == null)
            {
                // Пробуем найти свойство
                var argsProp = engineType.GetProperty("arguments", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (argsProp != null)
                {
                    var args = argsProp.GetValue(browserClient.engine) as string[];
                    argsProp.SetValue(browserClient.engine, GetCefArguments(args));
                }
            }
            else
            {
                var args = argsField.GetValue(browserClient.engine) as string[];
                argsField.SetValue(browserClient.engine, GetCefArguments(args));
            }

            Debug.Log("[TikTok Viewer] CEF arguments configured successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TikTok Viewer] Не удалось настроить CEF аргументы через рефлексию: {e.Message}");
            Debug.LogWarning("[TikTok Viewer] Попробуйте настроить аргументы вручную в CEF Engine Configuration");
        }
    }

    private string[] GetCefArguments(string[] existingArgs)
    {
        List<string> args = new List<string>();

        if (existingArgs != null)
            args.AddRange(existingArgs);

        // ========== АНТИ-ДЕТЕКТ ==========
        args.Add("--disable-blink-features=AutomationControlled");

        // User-Agent - Chrome 120 (старая версия, TikTok может отдавать AAC-LC вместо HE-AAC)
        args.Add("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        // ========== КРИТИЧНО: ПРОПРИЕТАРНЫЕ КОДЕКИ (H.264, AAC) ==========

        // Главный флаг - включает H.264, AAC-LC, MP3
        // ВНИМАНИЕ: Работает только если CEF собран с FFmpeg!
        args.Add("--enable-proprietary-codecs");

        // Дополнительные флаги для медиа кодеков
        args.Add("--enable-media-stream");
        args.Add("--disable-features=WebRtcHideLocalIpsWithMdns"); // Для WebRTC

        // ========== HARDWARE ACCELERATION (GPU декодирование) ==========

        args.Add("--enable-gpu");
        args.Add("--enable-accelerated-video-decode");
        args.Add("--enable-accelerated-2d-canvas");
        args.Add("--ignore-gpu-blocklist");
        args.Add("--disable-gpu-driver-bug-workarounds");

        // Платформо-специфичные декодеры
        // Раскомментируйте для вашей платформы:
        // Windows:
        args.Add("--enable-features=D3D11VideoDecoder"); // DirectX 11
        // Linux:
        // args.Add("--enable-features=VaapiVideoDecoder"); // VA-API
        // Chrome OS:
        // args.Add("--enable-features=UseChromeOSDirectVideoDecoder");

        // ========== MSE (Media Source Extensions) ==========

        // Экспериментальная поддержка MSE
        args.Add("--enable-features=MediaSourceExperimental");

        // Отключаем некоторые ограничения MSE
        args.Add("--disable-features=MediaSourceNewAbortAndDuration");

        // ========== АВТОПЛЕЙ ==========

        args.Add("--autoplay-policy=no-user-gesture-required");

        // ========== WEBGL ==========

        args.Add("--enable-webgl");
        args.Add("--enable-webgl2");

        // ========== АУДИО ==========

        args.Add("--enable-audio-service-audio-streams");
        args.Add("--mute-audio=false");

        // Платформо-специфичные:
        // Windows:
        args.Add("--force-wave-audio");
        // Linux:
        // args.Add("--enable-features=PulseaudioLoopbackForScreenShare");

        // ========== БЕЗОПАСНОСТЬ (ТОЛЬКО ДЛЯ РАЗРАБОТКИ!) ==========

        args.Add("--disable-web-security");
        args.Add("--allow-running-insecure-content");
        args.Add("--disable-features=IsolateOrigins,site-per-process");
        args.Add("--disable-site-isolation-trials");

        // ========== ПРОЧЕЕ ==========

        args.Add("--disable-dev-shm-usage");
        args.Add("--disable-setuid-sandbox");
        args.Add("--disable-infobars");
        args.Add("--window-position=0,0");
        args.Add("--ignore-certifcate-errors");
        args.Add("--ignore-certifcate-errors-spki-list");

        // Включаем логирование для отладки кодеков
        args.Add("--enable-logging");
        args.Add("--v=1"); // Verbose level 1

        Debug.Log("[TikTok Viewer] ========== CEF ARGUMENTS ==========");
        Debug.Log("[TikTok Viewer] CRITICAL: --enable-proprietary-codecs (requires FFmpeg build!)");
        Debug.Log("[TikTok Viewer] All arguments: " + string.Join(" ", args));
        Debug.Log("[TikTok Viewer] =======================================");
        return args.ToArray();
    }

    private void OnUrlChanged(string url)
    {
        // Обновляем адресную строку при изменении URL
        urlInput = url;
        jsInjected = false; // Сбрасываем флаг для новой страницы
        Debug.Log($"[TikTok Viewer] URL changed to: {url}");
    }

    private void OnLoadFinish(string url)
    {
        // Инжектим JavaScript для маскировки автоматизации
        if (!jsInjected)
        {
            jsInjected = true;
            InjectAntiDetectScript();
            Debug.Log($"[TikTok Viewer] Page loaded: {url}");
        }
    }

    private void InjectAntiDetectScript()
    {
        try
        {
            // 0. Перехват консоли (делаем первым!)
            browserClient.ExecuteJs(CONSOLE_INTERCEPT_JS);
            Debug.Log("[TikTok Viewer] Console intercept installed!");
            
            // ======== MODIFIED: ADDED CODEC CHECK ========
            // 1. Инжектим проверку кодеков
            browserClient.ExecuteJs(CODEC_CHECK_JS);
            Debug.Log("[TikTok Viewer] Codec check script injected! Check the browser console for results.");

            // 2. Инжектим анти-детект
            browserClient.ExecuteJs(ANTI_DETECT_JS);
            Debug.Log("[TikTok Viewer] Anti-detect JavaScript injected!");

            // 3. Проверка статуса
            browserClient.ExecuteJs(@"
                setTimeout(function() {
                    console.log('=== TikTok Viewer Status ===');
                    console.log('navigator.webdriver:', navigator.webdriver);
                    console.log('window.chrome:', !!window.chrome);
                    console.log('userAgent:', navigator.userAgent);
                    console.log('plugins:', navigator.plugins.length);
                }, 100);
            ");

            // 4. Попытка найти и кликнуть на видео для автоплея
            browserClient.ExecuteJs(@"
                setTimeout(function() {
                    // Ищем видео элементы на странице
                    const videos = document.querySelectorAll('video');
                    console.log('[TikTok] Found', videos.length, 'video elements');

                    videos.forEach((video, index) => {
                        console.log('[TikTok] Video', index, ':', {
                            paused: video.paused,
                            muted: video.muted,
                            readyState: video.readyState,
                            src: video.src
                        });

                        // Пытаемся запустить видео
                        if (video.paused) {
                            video.play().then(() => {
                                console.log('[TikTok] Video', index, 'started playing');
                            }).catch(err => {
                                console.log('[TikTok] Video', index, 'play failed:', err.message);
                            });
                        }
                    });
                }, 2000);
            ");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TikTok Viewer] Не удалось инжектить JavaScript: {e.Message}");
        }
    }

    private void OnDisable()
    {
        // Отписываемся от обновлений
        EditorApplication.update -= OnEditorUpdate;

        // КРИТИЧЕСКИ ВАЖНО: Убиваем браузер при закрытии окна
        if (browserClient != null)
        {
            try
            {
                // Сначала пытаемся корректно закрыть
                if (browserClient.IsConnected)
                {
                    // Отписываемся от событий
                    browserClient.OnUrlChanged -= OnUrlChanged;
                    browserClient.OnLoadFinish -= OnLoadFinish;
                }

                browserClient.Dispose();
            }
            catch (System.Exception e)
            {
                // Игнорируем ошибки при закрытии в Editor mode
                // (библиотека пытается использовать Destroy вместо DestroyImmediate)
                Debug.LogWarning($"[TikTok Viewer] Warning during cleanup: {e.Message}");
            }
            finally
            {
                browserClient = null;
            }
        }
    }

    private void OnEditorUpdate()
    {
        if (browserClient == null || !browserClient.HasInitialized)
            return;

        // Обновляем FPS (необходимо для работы браузера)
        browserClient.UpdateFps();

        // Загружаем данные текстуры
        browserClient.LoadTextureData();

        // Проверяем изменение размера окна
        CheckResize();

        // Перерисовываем окно для плавной анимации
        Repaint();
    }

    private void CheckResize()
    {
        Vector2 currentSize = new Vector2(position.width, position.height);

        // Если размер изменился, обновляем разрешение браузера
        if (lastWindowSize != currentSize && currentSize.x > 0 && currentSize.y > 0)
        {
            lastWindowSize = currentSize;

            // Изменение размера только если браузер готов и подключен
            if (browserClient.ReadySignalReceived && browserClient.IsConnected)
            {
                try
                {
                    uint width = (uint)Mathf.Max(400, currentSize.x);
                    uint height = (uint)Mathf.Max(300, currentSize.y);
                    browserClient.Resolution = new BrowserResolution(width, height);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Не удалось изменить размер браузера: {e.Message}");
                }
            }
        }
    }

    private void OnGUI()
    {
        // Если браузер еще не инициализирован, показываем заглушку
        if (browserClient == null || !browserClient.HasInitialized)
        {
            EditorGUILayout.HelpBox("Загрузка Chromium браузера...", MessageType.Info);
            return;
        }

        // Рисуем toolbar сверху
        DrawToolbar();

        float helpHeight = showHelp ? 100f : 0f;
        float browserHeight = position.height - toolbarHeight - helpHeight;

        // Если текстура браузера готова, рисуем её
        if (browserClient.BrowserTexture != null)
        {
            Rect textureRect = new Rect(0, toolbarHeight, position.width, browserHeight);

            // Инвертируем Y координаты текстуры чтобы исправить переворот
            // CEF рисует с нижнего левого угла, а Unity с верхнего левого
            Rect uvRect = new Rect(0, 1, 1, -1); // Инвертированные UV координаты

            GUI.DrawTextureWithTexCoords(textureRect, browserClient.BrowserTexture, uvRect);
        }

        // Рисуем визуальную отладку кликов
        DrawDebugClicks();

        // Рисуем помощь снизу если включена
        if (showHelp)
        {
            DrawHelp(position.height - helpHeight, helpHeight);
        }

        // Обрабатываем пользовательский ввод (мышь, клавиатура)
        HandleInput();
    }

    private void DrawToolbar()
    {
        GUILayout.BeginArea(new Rect(0, 0, position.width, toolbarHeight));
        GUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Кнопка назад
        if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            if (browserClient != null && browserClient.IsConnected)
                browserClient.GoBack();
        }

        // Кнопка вперед
        if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            if (browserClient != null && browserClient.IsConnected)
                browserClient.GoForward();
        }

        // Кнопка обновить
        if (GUILayout.Button("⟳", EditorStyles.toolbarButton, GUILayout.Width(30)))
        {
            if (browserClient != null && browserClient.IsConnected)
                browserClient.Refresh();
        }

        // Адресная строка
        GUI.SetNextControlName("URLField");
        string newUrl = GUILayout.TextField(urlInput, EditorStyles.toolbarTextField);

        // Обработка Enter в адресной строке
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
            && GUI.GetNameOfFocusedControl() == "URLField")
        {
            NavigateToUrl(urlInput);
            Event.current.Use();
        }

        if (newUrl != urlInput)
        {
            urlInput = newUrl;
        }

        // Кнопка Go
        if (GUILayout.Button("Go", EditorStyles.toolbarButton, GUILayout.Width(35)))
        {
            NavigateToUrl(urlInput);
        }

        // Кнопка помощи
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = showHelp ? Color.green : Color.white;
        if (GUILayout.Button("?", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            showHelp = !showHelp;
        }
        GUI.backgroundColor = originalColor;

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void NavigateToUrl(string url)
    {
        if (browserClient != null && browserClient.IsConnected)
        {
            // Добавляем http:// если протокол не указан
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
                urlInput = url;
            }

            browserClient.LoadUrl(url);
            Debug.Log($"[TikTok Viewer] Navigating to: {url}");
        }
    }

    private void DrawHelp(float y, float height)
    {
        GUILayout.BeginArea(new Rect(0, y, position.width, height));
        EditorGUILayout.HelpBox(
            "АНТИ-ДЕТЕКТ:\n" +
            "✓ navigator.webdriver скрыт\n" +
            "✓ Canvas/WebGL fingerprinting защита\n" +
            "✓ Event.isTrusted всегда true\n\n" +
            "КРИТИЧЕСКАЯ ПРОБЛЕМА:\n" +
            "CEF не поддерживает кодек HE-AAC v2 (mp4a.40.29)\n" +
            "TikTok использует этот кодек - ВИДЕО НЕ РАБОТАЕТ!\n\n" +
            "Решение: Нужна другая сборка CEF\n" +
            "с проприетарными кодеками (ffmpeg)",
            MessageType.Warning);
        GUILayout.EndArea();
    }

    private void DrawDebugClicks()
    {
        // Удаляем старые клики (старше 2 секунд)
        float currentTime = Time.realtimeSinceStartup;
        debugClicks.RemoveAll(click => currentTime - click.time > 2f);

        // Рисуем круги в местах кликов
        foreach (var click in debugClicks)
        {
            float age = currentTime - click.time;
            float alpha = Mathf.Lerp(1f, 0f, age / 2f); // Затухание за 2 секунды
            float radius = Mathf.Lerp(5f, 20f, age / 2f); // Расширение круга

            Color color = click.color;
            color.a = alpha;

            // Рисуем круг в точке клика (position уже содержит экранные координаты)
            DrawCircle(click.position, radius, color);
        }
    }

    private void DrawCircle(Vector2 center, float radius, Color color)
    {
        Handles.BeginGUI();
        Handles.color = color;
        Handles.DrawSolidDisc(center, Vector3.forward, radius);
        Handles.EndGUI();
    }

    private void HandleInput()
    {
        if (browserClient == null || !browserClient.HasInitialized)
            return;

        // Проверяем что браузер подключен
        if (!browserClient.IsConnected)
            return;

        Event currentEvent = Event.current;

        // Обработка мыши - только обрабатываем события, не в фазе Repaint
        if (currentEvent.isMouse && currentEvent.type != EventType.Repaint)
        {
            Vector2 mousePos = currentEvent.mousePosition;

            // Проверяем что мышь находится в области браузера (ниже toolbar)
            if (mousePos.y < toolbarHeight)
                return; // Игнорируем события на toolbar

            // Корректируем координаты мыши с учетом toolbar
            Vector2 browserMousePos = new Vector2(mousePos.x, mousePos.y - toolbarHeight);

            // Клики мыши - обрабатываем ПЕРЕД движением
            if (currentEvent.type == EventType.MouseDown)
            {
                // Устанавливаем фокус на браузере при клике
                browserHasFocus = true;
                isMouseButtonDown = true;
                lastMousePosition = browserMousePos;
                GUI.FocusControl("BrowserArea");

                MouseClickType clickType = GetMouseClickType(currentEvent.button);
                int clickCount = currentEvent.clickCount > 0 ? currentEvent.clickCount : 1;
                browserClient.SendMouseClick(browserMousePos, clickCount, clickType, MouseEventType.Down);

                // Отладочный лог
                Debug.Log($"[TikTok] MouseDown at ({(int)browserMousePos.x}, {(int)browserMousePos.y})");

                // Визуальная отладка - добавляем круг в месте клика
                debugClicks.Add(new ClickDebugInfo
                {
                    position = mousePos,
                    time = Time.realtimeSinceStartup,
                    color = Color.green
                });

                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                isMouseButtonDown = false;

                MouseClickType clickType = GetMouseClickType(currentEvent.button);
                int clickCount = currentEvent.clickCount > 0 ? currentEvent.clickCount : 1;
                browserClient.SendMouseClick(browserMousePos, clickCount, clickType, MouseEventType.Up);

                Debug.Log($"[TikTok] MouseUp at ({(int)browserMousePos.x}, {(int)browserMousePos.y})");

                debugClicks.Add(new ClickDebugInfo
                {
                    position = mousePos,
                    time = Time.realtimeSinceStartup,
                    color = Color.red
                });

                currentEvent.Use();
            }

            // Движение мыши (включая drag)
            if (currentEvent.type == EventType.MouseMove || currentEvent.type == EventType.MouseDrag)
            {
                browserClient.SendMouseMove(browserMousePos);

                // Если это drag (кнопка зажата) - логируем
                if (isMouseButtonDown || currentEvent.type == EventType.MouseDrag)
                {
                    float distance = Vector2.Distance(lastMousePosition, browserMousePos);
                    if (distance > 1f)
                    {
                        Debug.Log($"[TikTok] DRAG to ({(int)browserMousePos.x}, {(int)browserMousePos.y}), distance: {distance:F1}px");
                        lastMousePosition = browserMousePos;

                        debugClicks.Add(new ClickDebugInfo
                        {
                            position = mousePos,
                            time = Time.realtimeSinceStartup,
                            color = Color.blue
                        });
                    }
                }

                currentEvent.Use();
            }

            // Скролл мыши
            if (currentEvent.type == EventType.ScrollWheel)
            {
                // Уменьшаем масштаб скролла для более естественного поведения
                int scrollAmount = (int)(-currentEvent.delta.y * 50); // 50 пикселей за шаг
                browserClient.SendMouseScroll(browserMousePos, scrollAmount);
                Debug.Log($"[TikTok] Scroll: {scrollAmount}");
                currentEvent.Use(); // Потребляем событие
            }
        }

        // Обработка клавиатуры только если браузер в фокусе
        if (browserHasFocus && (currentEvent.isKey || currentEvent.type == EventType.KeyDown || currentEvent.type == EventType.KeyUp))
        {
            HandleKeyboardInput(currentEvent);
        }
    }

    private void HandleKeyboardInput(Event e)
    {
        if (e.type == EventType.KeyDown || e.type == EventType.KeyUp)
        {
            // Простая обработка текстового ввода
            if (e.type == EventType.KeyDown && e.character != 0 && !char.IsControl(e.character))
            {
                // Отправляем символ как текст
                char[] chars = new char[] { e.character };
                browserClient.SendKeyboardControls(new VoltstroStudios.UnityWebBrowser.Shared.WindowsKey[0],
                                                   new VoltstroStudios.UnityWebBrowser.Shared.WindowsKey[0],
                                                   chars);
                Debug.Log($"[TikTok] Key pressed: {e.character}");
                e.Use();
            }
            else if (e.keyCode != KeyCode.None)
            {
                // Отправляем специальные клавиши (Enter, Backspace, стрелки и т.д.)
                VoltstroStudios.UnityWebBrowser.Shared.WindowsKey windowsKey = ConvertToWindowsKey(e.keyCode);

                if (e.type == EventType.KeyDown)
                {
                    browserClient.SendKeyboardControls(new[] { windowsKey },
                                                       new VoltstroStudios.UnityWebBrowser.Shared.WindowsKey[0],
                                                       new char[0]);
                }
                else if (e.type == EventType.KeyUp)
                {
                    browserClient.SendKeyboardControls(new VoltstroStudios.UnityWebBrowser.Shared.WindowsKey[0],
                                                       new[] { windowsKey },
                                                       new char[0]);
                }

                Debug.Log($"[TikTok] Special key: {e.keyCode} -> {windowsKey}");
                e.Use();
            }
        }
    }

    private VoltstroStudios.UnityWebBrowser.Shared.WindowsKey ConvertToWindowsKey(KeyCode keyCode)
    {
        // Простое маппинг основных клавиш
        return keyCode switch
        {
            KeyCode.Return => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Return,
            KeyCode.Backspace => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Back,
            KeyCode.Tab => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Tab,
            KeyCode.Escape => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Escape,
            KeyCode.Space => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Space,
            KeyCode.LeftArrow => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Left,
            KeyCode.RightArrow => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Right,
            KeyCode.UpArrow => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Up,
            KeyCode.DownArrow => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Down,
            KeyCode.Delete => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.Delete,
            _ => VoltstroStudios.UnityWebBrowser.Shared.WindowsKey.None
        };
    }

    private MouseClickType GetMouseClickType(int button)
    {
        return button switch
        {
            0 => MouseClickType.Left,
            1 => MouseClickType.Right,
            2 => MouseClickType.Middle,
            _ => MouseClickType.Left
        };
    }
}