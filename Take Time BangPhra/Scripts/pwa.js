/*
 * Take Time Admin — PWA client
 *  1) ลงทะเบียน /sw.js ทุกหน้า (เฉพาะ https หรือ localhost)
 *  2) แผ่นชวนติดตั้งแอพสำหรับผู้ดูแลที่ล็อกอิน (#ttPwaSheet ถูก render จาก Site.Master เฉพาะผู้ดูแล)
 *     - Chrome/Edge/Android: ใช้ beforeinstallprompt → ปุ่ม "ติดตั้งแอพ"
 *     - iOS: ไม่มี prompt → แสดงขั้นตอน แชร์ → เพิ่มไปยังหน้าจอโฮม
 *     - "ไม่ต้องแจ้งเตือนอีก" = ปิดถาวร (localStorage แยกตามผู้ใช้) · "ภายหลัง"/× = ซ่อน 24 ชม.
 *     - ซ่อนเองเมื่อเปิดเป็นแอพอยู่แล้ว (display-mode: standalone / navigator.standalone / appinstalled)
 *     - เปิดเองได้ทุกเมื่อจากลิงก์ [data-tt-pwa-open] (เมนู พนักงาน → ติดตั้งแอพบนเครื่องนี้) หรือ window.ttPwaOpen()
 */
(function () {
    'use strict';

    var LATER_MS = 24 * 60 * 60 * 1000;
    var isLocalhost = /^(localhost|127\.0\.0\.1|\[::1\])$/i.test(location.hostname);
    var secure = location.protocol === 'https:' || isLocalhost;

    // ---------- 1) Service worker ----------
    if (secure && 'serviceWorker' in navigator) {
        var registerSw = function () {
            try {
                navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(function () { });
            } catch (e) { /* เบราว์เซอร์บล็อก SW (เช่นโหมดส่วนตัว) — ไม่กระทบการใช้งาน */ }
        };
        if (document.readyState === 'complete') registerSw();
        else window.addEventListener('load', registerSw);
    }

    // ---------- 2) Install prompt ----------
    var deferredPrompt = null;
    var sheet = null;
    var userKey = 'admin';

    function store(get, key, value) {
        try {
            if (get) return window.localStorage.getItem(key);
            if (value === null) window.localStorage.removeItem(key);
            else window.localStorage.setItem(key, value);
        } catch (e) { }
        return null;
    }
    function kNever() { return 'ttPwa.never.' + userKey; }
    function kLater() { return 'ttPwa.later.' + userKey; }
    var K_INSTALLED = 'ttPwa.installed';

    function mq(q) {
        try { return !!(window.matchMedia && window.matchMedia(q).matches); } catch (e) { return false; }
    }
    function isStandalone() {
        return mq('(display-mode: standalone)') || mq('(display-mode: fullscreen)') ||
            mq('(display-mode: minimal-ui)') || mq('(display-mode: window-controls-overlay)') ||
            window.navigator.standalone === true ||
            (document.referrer || '').indexOf('android-app://') === 0;
    }

    var ua = navigator.userAgent || '';
    var isIOS = /iphone|ipad|ipod/i.test(ua) ||
        (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1); // iPadOS แจ้งตัวเป็น Mac
    var isIOSOtherBrowser = isIOS && /CriOS|FxiOS|EdgiOS|OPiOS|GSA\//i.test(ua);
    var isAndroid = /android/i.test(ua);
    var supportsPrompt = 'onbeforeinstallprompt' in window;
    var isMacSafari = !isIOS && /Macintosh/i.test(ua) && /Safari\//i.test(ua) &&
        !/Chrome|Chromium|Edg\/|OPR\/|Firefox/i.test(ua);

    function neverRemind() { return store(true, kNever()) === '1'; }
    function snoozed() {
        var t = parseInt(store(true, kLater()) || '0', 10);
        return t > 0 && (Date.now() - t) < LATER_MS;
    }

    function currentMode() {
        if (isStandalone()) return 'installed';
        if (!secure) return 'insecure';
        if (deferredPrompt) return 'prompt';
        if (isIOS) return 'ios';
        if (store(true, K_INSTALLED) === '1') return 'installed';
        return 'manual';
    }

    function q(sel) { return sheet ? sheet.querySelector(sel) : null; }

    function render(mode) {
        if (!sheet) return;
        var blocks = sheet.querySelectorAll('[data-pwa-mode]');
        for (var i = 0; i < blocks.length; i++) {
            blocks[i].hidden = blocks[i].getAttribute('data-pwa-mode') !== mode;
        }
        var other = q('[data-pwa-ios-other]');
        if (other) other.hidden = !isIOSOtherBrowser;
        var installBtn = q('[data-pwa="install"]');
        if (installBtn) installBtn.hidden = mode !== 'prompt';
        var later = q('[data-pwa="later"]');
        if (later) later.textContent = mode === 'prompt' || mode === 'ios' || mode === 'manual' ? 'ภายหลัง' : 'ปิด';
        var never = q('[data-pwa="never"]');
        var on = neverRemind();
        if (never) never.checked = on;
        var saved = q('[data-pwa="saved"]');
        if (saved) saved.style.display = on ? 'block' : 'none';
    }

    function show(mode) {
        if (!sheet) return;
        render(mode || currentMode());
        sheet.hidden = false;
    }
    function hide() { if (sheet) sheet.hidden = true; }

    function snoozeAndHide() {
        if (!neverRemind()) store(false, kLater(), String(Date.now()));
        hide();
    }

    function hideMenuIfStandalone() {
        if (!isStandalone()) return;
        var items = document.querySelectorAll('[data-tt-pwa-menu]');
        for (var i = 0; i < items.length; i++) items[i].style.display = 'none';
    }

    function canAutoShow() {
        return !!sheet && secure && !isStandalone() && !neverRemind() && !snoozed() &&
            store(true, K_INSTALLED) !== '1';
    }

    function doInstall() {
        if (!deferredPrompt) { render(currentMode()); return; }
        var p = deferredPrompt;
        deferredPrompt = null; // ใช้ได้ครั้งเดียว
        try {
            p.prompt();
            p.userChoice.then(function (choice) {
                if (choice && choice.outcome === 'accepted') {
                    store(false, K_INSTALLED, '1');
                    hide();
                } else {
                    snoozeAndHide();
                }
            }).catch(function () { hide(); });
        } catch (e) {
            render(currentMode());
        }
    }

    // ต้องผูกทันที (ก่อน DOM พร้อม) เพื่อไม่พลาด event — และกัน mini-infobar ของ Chrome โผล่ให้ลูกค้า
    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredPrompt = e;
        store(false, K_INSTALLED, null); // event นี้มาเฉพาะเมื่อยังไม่ได้ติดตั้ง (เช่น ถอนแอพไปแล้ว)
        if (!sheet) return;
        if (!sheet.hidden) render('prompt');
        else if (canAutoShow()) show('prompt');
    });

    window.addEventListener('appinstalled', function () {
        deferredPrompt = null;
        store(false, K_INSTALLED, '1');
        hide();
    });

    function init() {
        sheet = document.getElementById('ttPwaSheet');
        hideMenuIfStandalone();

        // ลิงก์เปิดเอง (ทำงานแม้ติ๊กไม่ต้องแจ้งเตือนแล้ว)
        document.addEventListener('click', function (e) {
            var t = e.target;
            while (t && t !== document) {
                if (t.getAttribute && t.getAttribute('data-tt-pwa-open') !== null) {
                    e.preventDefault();
                    window.ttPwaOpen();
                    return;
                }
                t = t.parentNode;
            }
        });

        if (!sheet) return;
        userKey = (sheet.getAttribute('data-user') || 'admin').toLowerCase();

        sheet.addEventListener('click', function (e) {
            var t = e.target;
            while (t && t !== sheet) {
                var act = t.getAttribute && t.getAttribute('data-pwa');
                if (act === 'close' || act === 'later') { e.preventDefault(); snoozeAndHide(); return; }
                if (act === 'install') { e.preventDefault(); doInstall(); return; }
                t = t.parentNode;
            }
        });

        var never = q('[data-pwa="never"]');
        if (never) {
            never.addEventListener('change', function () {
                store(false, kNever(), never.checked ? '1' : null);
                if (never.checked) store(false, kLater(), null);
                render(currentMode());
            });
        }

        document.addEventListener('keydown', function (e) {
            if ((e.key === 'Escape' || e.keyCode === 27) && !sheet.hidden) snoozeAndHide();
        });

        if (isStandalone()) { store(false, K_INSTALLED, '1'); return; }

        if (canAutoShow()) {
            if (deferredPrompt) {
                show('prompt');
            } else if (isIOS || (isAndroid && !supportsPrompt) || isMacSafari) {
                // ไม่มี beforeinstallprompt → แสดงขั้นตอนเอง (หน่วงเล็กน้อยไม่ให้บังตอนหน้าเพิ่งโหลด)
                setTimeout(function () { if (canAutoShow() && sheet.hidden) show(); }, 1500);
            }
            // Chromium อื่น ๆ: รอ beforeinstallprompt — ถ้าไม่มา (ติดตั้งแล้ว/ติดตั้งไม่ได้) ก็ไม่รบกวน
        }
    }

    window.ttPwaOpen = function () {
        if (!sheet) sheet = document.getElementById('ttPwaSheet');
        if (!sheet) return false;
        show();
        return false;
    };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
