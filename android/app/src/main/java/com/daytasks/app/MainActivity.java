package com.daytasks.app;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.res.Configuration;
import android.graphics.Color;
import android.graphics.Insets;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowInsets;
import android.util.Log;
import android.view.WindowInsetsController;
import android.webkit.ConsoleMessage;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;

import androidx.webkit.WebSettingsCompat;
import androidx.webkit.WebViewAssetLoader;
import androidx.webkit.WebViewFeature;

/**
 * The whole app is one WebView showing the same page as the PC widget (src/TaskWidget/wwwroot,
 * packed into the APK's assets). The page keeps its data in localStorage and syncs with the
 * cloud by itself; this class only hosts it and fits it to the phone (system bars, keyboard,
 * back button).
 */
public class MainActivity extends Activity {

    // WebViewAssetLoader serves the assets under a real https origin, so the page gets
    // localStorage and can call the cloud like any website.
    private static final String HOST = "appassets.androidplatform.net";
    private static final String START_URL = "https://" + HOST + "/assets/index.html";

    private FrameLayout root;
    private WebView web;
    private Updater updater;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        root = new FrameLayout(this);
        web = new WebView(this);
        root.addView(web, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        setContentView(root);
        fitToSystemBars();

        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        followSystemDarkMode(settings);

        WebViewAssetLoader assets = new WebViewAssetLoader.Builder()
                .addPathHandler("/assets/", new WebViewAssetLoader.AssetsPathHandler(this))
                .build();

        web.setWebViewClient(new WebViewClient() {
            @Override
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                return assets.shouldInterceptRequest(request.getUrl());
            }

            // links to anything but the app itself open in the browser
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                Uri url = request.getUrl();
                if (HOST.equals(url.getHost())) return false;
                try {
                    startActivity(new Intent(Intent.ACTION_VIEW, url));
                } catch (ActivityNotFoundException ignored) {
                }
                return true;
            }
        });

        // page console → logcat (tag "DayTasks"), for debugging with `adb logcat -s DayTasks`
        web.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onConsoleMessage(ConsoleMessage message) {
                Log.println(message.messageLevel() == ConsoleMessage.MessageLevel.ERROR ? Log.ERROR : Log.DEBUG,
                        "DayTasks", message.message() + " (" + message.sourceId() + ":" + message.lineNumber() + ")");
                return true;
            }
        });

        updater = new Updater(this, status -> runOnUiThread(() -> web.evaluateJavascript(
                "window.taskWidgetUpdate && window.taskWidgetUpdate(" + status + ")", null)));
        web.addJavascriptInterface(new Bridge(), "TaskWidgetAndroid");

        if (savedInstanceState == null || web.restoreState(savedInstanceState) == null) {
            web.loadUrl(START_URL);
        }
    }

    /** Called from the page (window.TaskWidgetAndroid). */
    private final class Bridge {
        /** Status and navigation bars in the page's background colour; dark icons on a light page. */
        @JavascriptInterface
        public void setTheme(String background, boolean dark) {
            runOnUiThread(() -> applyTheme(background, dark));
        }

        /** { version, repo, asset } as JSON, for the page's update check. */
        @JavascriptInterface
        public String updateInfo() {
            return Updater.info();
        }

        /** Download the release APK and open the system installer (progress → window.taskWidgetUpdate). */
        @JavascriptInterface
        public void installUpdate(String apkUrl, String shaUrl) {
            updater.install(apkUrl, shaUrl);
        }
    }

    private void applyTheme(String background, boolean dark) {
        int color;
        try {
            color = Color.parseColor(background.trim());
        } catch (IllegalArgumentException e) {
            return;
        }
        root.setBackgroundColor(color);
        web.setBackgroundColor(color);

        Window window = getWindow();
        if (Build.VERSION.SDK_INT < 35) { // Android 15+ draws the page behind the bars instead
            window.setStatusBarColor(color);
            window.setNavigationBarColor(color);
        }
        if (Build.VERSION.SDK_INT >= 30) {
            int light = WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
                    | WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS;
            WindowInsetsController controller = window.getInsetsController();
            if (controller != null) controller.setSystemBarsAppearance(dark ? 0 : light, light);
        } else {
            View decor = window.getDecorView();
            int flags = decor.getSystemUiVisibility();
            int light = View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR | View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
            decor.setSystemUiVisibility(dark ? flags & ~light : flags | light);
        }
    }

    /**
     * Android 11+: draw edge to edge and pad the page by the system bars and the keyboard,
     * so nothing hides under the notch, the gesture bar or the keyboard.
     * Older versions keep the classic layout (adjustResize handles the keyboard).
     */
    private void fitToSystemBars() {
        if (Build.VERSION.SDK_INT < 30) return;
        getWindow().setDecorFitsSystemWindows(false);
        root.setOnApplyWindowInsetsListener((view, insets) -> {
            Insets bars = insets.getInsets(WindowInsets.Type.systemBars()
                    | WindowInsets.Type.displayCutout() | WindowInsets.Type.ime());
            view.setPadding(bars.left, bars.top, bars.right, bars.bottom);
            return WindowInsets.CONSUMED;
        });
    }

    /** Let the page's own dark styles (prefers-color-scheme) follow the phone's dark mode. */
    @SuppressWarnings("deprecation")
    private void followSystemDarkMode(WebSettings settings) {
        if (Build.VERSION.SDK_INT >= 33) return; // newer WebViews take it from the app theme
        boolean night = (getResources().getConfiguration().uiMode & Configuration.UI_MODE_NIGHT_MASK)
                == Configuration.UI_MODE_NIGHT_YES;
        if (WebViewFeature.isFeatureSupported(WebViewFeature.FORCE_DARK_STRATEGY)) {
            WebSettingsCompat.setForceDarkStrategy(settings, WebSettingsCompat.DARK_STRATEGY_WEB_THEME_DARKENING_ONLY);
        }
        if (WebViewFeature.isFeatureSupported(WebViewFeature.FORCE_DARK)) {
            WebSettingsCompat.setForceDark(settings,
                    night ? WebSettingsCompat.FORCE_DARK_ON : WebSettingsCompat.FORCE_DARK_OFF);
        }
    }

    /** Back closes what's open in the page (calendar, menus, another day) before leaving the app. */
    @Override
    @SuppressWarnings("deprecation")
    public void onBackPressed() {
        web.evaluateJavascript("window.taskWidgetBack ? window.taskWidgetBack() : false", handled -> {
            if (!"true".equals(handled)) MainActivity.super.onBackPressed();
        });
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        web.saveState(outState);
    }

    @Override
    protected void onResume() {
        super.onResume();
        web.onResume();
    }

    @Override
    protected void onPause() {
        web.onPause();
        super.onPause();
    }

    @Override
    protected void onDestroy() {
        root.removeView(web);
        web.destroy();
        super.onDestroy();
    }
}
