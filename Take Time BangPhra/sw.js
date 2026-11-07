/*
 * Take Time Admin — Service Worker (scope "/")
 *
 * ระบบหลังบ้านที่ต้องล็อกอิน ⇒ ปลอดภัยไว้ก่อน:
 *  - หน้าเว็บ (navigation) = network-first เสมอ และ "ไม่เก็บ HTML ลงแคช" (ข้อมูลลูกค้า/บัญชีต้องสดและไม่ค้างในเครื่อง)
 *    ออฟไลน์ → แสดง /offline.html แทน
 *  - ไฟล์ static เท่านั้นที่เข้าแคช:
 *      cache-first            = ไฟล์ที่มีเวอร์ชัน/ไม่เปลี่ยน (?v=, bundles, ไอคอนแอพ, ฟอนต์/ไอคอนจาก CDN)
 *      stale-while-revalidate = /Content/, /Scripts/, /fonts/, /Images/ ที่ไม่มีเวอร์ชัน (โชว์จากแคช แล้วอัปเดตเบื้องหลัง
 *                               ⇒ แก้ไฟล์บนเซิร์ฟเวอร์แล้ว รีเฟรชครั้งถัดไปได้ของใหม่ ไม่ค้างถาวร)
 *  - ไม่ยุ่งกับ POST/ไม่ใช่ GET, /API/, /Payment/, webhook, .ashx/.asmx/.axd, /Documents/, /Upload/
 *    และโฟลเดอร์รูปที่ผู้ใช้อัปโหลด/สร้างใหม่ทุกวัน (สลิป, เอกสารลา, ไฟล์แชท, Images/Reservation)
 *  - เปลี่ยน CACHE_VERSION เมื่อแก้ไฟล์นี้ → activate จะลบแคชรุ่นเก่าทิ้ง
 */
'use strict';

var CACHE_VERSION = 'v1';
var STATIC_CACHE = 'tt-static-' + CACHE_VERSION;
var OFFLINE_URL = '/offline.html';
var MAX_STATIC_ENTRIES = 250;

var PRECACHE = [
    OFFLINE_URL,
    '/Images/App/icon-192.png',
    '/Images/App/icon-512.png',
    '/Images/Logo.png'
];

// path ที่ห้ามแตะเด็ดขาด (ปล่อยให้เบราว์เซอร์ยิงตรง ไม่ผ่านแคช)
var BYPASS_RE = /^\/(api|payment|webhook|webhooks|documents|upload|admin\/login)(\/|$)|webhook|\.(ashx|asmx|axd|svc)(\/|$)/i;
var BYPASS_IMAGES_RE = /^\/images\/(reservation|paymentslips|activityslips|leavedocs|chatfiles)(\/|$)/i;

// ไฟล์ static (same-origin)
var STATIC_PATH_RE = /^\/(content|scripts|images|fonts)\//i;
var IMMUTABLE_PATH_RE = /^\/images\/(app|icon)\//i;
var STATIC_EXT_RE = /\.(css|js|png|jpe?g|gif|svg|webp|ico|woff2?|ttf|eot|otf)$/i;

// โฮสต์ภายนอกที่เป็นไฟล์ static แบบมีเวอร์ชัน (ฟอนต์/ไอคอน)
var STATIC_HOSTS = ['fonts.gstatic.com', 'fonts.googleapis.com', 'cdnjs.cloudflare.com'];

self.addEventListener('install', function (event) {
    event.waitUntil(
        caches.open(STATIC_CACHE).then(function (cache) {
            // ใส่ทีละไฟล์ — ไฟล์ใดหาย ไม่ทำให้ติดตั้ง SW ล้มทั้งชุด
            return Promise.all(PRECACHE.map(function (url) {
                return fetch(new Request(url, { cache: 'reload', credentials: 'same-origin' }))
                    .then(function (res) { if (res && res.ok) return cache.put(url, res); })
                    .catch(function () { });
            }));
        }).then(function () { return self.skipWaiting(); })
    );
});

self.addEventListener('activate', function (event) {
    event.waitUntil(
        caches.keys().then(function (keys) {
            return Promise.all(keys.map(function (key) {
                if (key.indexOf('tt-') === 0 && key !== STATIC_CACHE) return caches.delete(key);
            }));
        }).then(function () { return self.clients.claim(); })
    );
});

// คืน 'immutable' | 'static' | null
function classify(url) {
    if (url.origin === self.location.origin) {
        var p = url.pathname;
        if (BYPASS_IMAGES_RE.test(p)) return null;
        // ASP.NET bundles (/bundles/x?v=hash, /Content/css?v=hash) และไฟล์ที่ใส่ ?v= — เปลี่ยนเวอร์ชัน = URL ใหม่
        if (/[?&]v=/.test(url.search) && (/^\/(bundles|content|scripts)\//i.test(p) || STATIC_EXT_RE.test(p))) return 'immutable';
        if (IMMUTABLE_PATH_RE.test(p) && STATIC_EXT_RE.test(p)) return 'immutable';
        if (STATIC_PATH_RE.test(p) && STATIC_EXT_RE.test(p)) return 'static';
        if (/^\/(favicon\.ico|manifest\.webmanifest)$/i.test(p)) return 'static';
        return null;
    }
    return STATIC_HOSTS.indexOf(url.hostname) !== -1 ? 'immutable' : null;
}

function cacheable(res) {
    // เก็บเฉพาะ 200 ที่อ่านได้ (basic/cors) — ไม่เก็บ opaque (กินโควต้า + ไม่รู้ว่าสำเร็จจริง)
    return res && res.ok && (res.type === 'basic' || res.type === 'cors');
}

function putInCache(event, cache, request, res) {
    event.waitUntil(cache.put(request, res).then(function () {
        return trimCache(STATIC_CACHE, MAX_STATIC_ENTRIES);
    }).catch(function () { }));
}

function trimCache(cacheName, max) {
    return caches.open(cacheName).then(function (cache) {
        return cache.keys().then(function (keys) {
            if (keys.length <= max) return;
            return Promise.all(keys.slice(0, keys.length - max).map(function (k) { return cache.delete(k); }));
        });
    });
}

function cacheFirst(event, request) {
    return caches.open(STATIC_CACHE).then(function (cache) {
        return cache.match(request).then(function (cached) {
            if (cached) return cached;
            return fetch(request).then(function (res) {
                if (cacheable(res)) putInCache(event, cache, request, res.clone());
                return res;
            });
        });
    });
}

function staleWhileRevalidate(event, request) {
    return caches.open(STATIC_CACHE).then(function (cache) {
        return cache.match(request).then(function (cached) {
            var network = fetch(request).then(function (res) {
                if (cacheable(res)) putInCache(event, cache, request, res.clone());
                return res;
            });
            if (cached) {
                event.waitUntil(network.catch(function () { }));
                return cached;
            }
            return network;
        });
    });
}

function networkFirstNavigation(request) {
    return fetch(request).catch(function () {
        return caches.match(OFFLINE_URL).then(function (offline) {
            return offline || new Response(
                '<!doctype html><meta charset="utf-8"><title>ออฟไลน์</title><p style="font-family:sans-serif;padding:24px">ไม่มีการเชื่อมต่ออินเทอร์เน็ต กรุณาลองใหม่อีกครั้ง</p>',
                { status: 503, headers: { 'Content-Type': 'text/html; charset=utf-8' } });
        });
    });
}

self.addEventListener('fetch', function (event) {
    var request = event.request;
    if (request.method !== 'GET') return;                 // POST/postback ไม่ผ่าน SW เลย
    if (request.headers.has('range')) return;              // วิดีโอ/ไฟล์ใหญ่ — ให้เบราว์เซอร์จัดการ
    if (request.cache === 'only-if-cached' && request.mode !== 'same-origin') return;

    var url;
    try { url = new URL(request.url); } catch (e) { return; }
    if (url.protocol !== 'https:' && url.protocol !== 'http:') return;

    if (url.origin === self.location.origin && BYPASS_RE.test(url.pathname)) return;

    if (request.mode === 'navigate') {
        if (url.origin !== self.location.origin) return;
        event.respondWith(networkFirstNavigation(request));
        return;
    }

    var kind = classify(url);
    if (kind === 'immutable') event.respondWith(cacheFirst(event, request));
    else if (kind === 'static') event.respondWith(staleWhileRevalidate(event, request));
    // อื่น ๆ (AJAX/JSON/PageMethods ฯลฯ) → ไม่แตะ ปล่อยผ่านเครือข่ายตามปกติ
});

self.addEventListener('message', function (event) {
    if (event.data === 'SKIP_WAITING') self.skipWaiting();
});
