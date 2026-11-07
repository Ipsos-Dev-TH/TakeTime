/* ═══════════════════════════════════════════════════════════════════════════
   ป้าย "สถานะ NextAcc" ฝั่ง client (CLAUDE.md ข้อ 24) — ใช้กับหน้าที่สร้างตารางจาก JSON (เช่น ReserveTable)
   รับข้อมูลแบบโครงสร้าง (ไม่รับ HTML จาก server):
     { code:'sent|pending|sending|waiting|data|config|cn|dup|failed|voided|notsent|na',
       label:'…', title:'…', icon:'…', links:{ open:'https://…', pdf:'/…', fix:'/…', queue:'/…' } }
   encode ทุกข้อความ + ตรวจ URL (http/https หรือ path ในแอปที่ขึ้นต้น "/" เดียว) ก่อนใส่ href
   ═══════════════════════════════════════════════════════════════════════════ */
(function (w) {
    'use strict';
    var CODES = ['sent', 'pending', 'sending', 'waiting', 'data', 'config', 'cn', 'dup', 'failed', 'voided', 'notsent', 'na'];

    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function safeUrl(u, allowAbsolute) {
        if (typeof u !== 'string' || !u) return '';
        if (/[<>"'\s\\]/.test(u)) return '';
        if (u.charAt(0) === '/' && u.charAt(1) !== '/') return u;
        if (allowAbsolute && /^https?:\/\/[^\/]/i.test(u)) return u;
        return '';
    }

    function render(b, opts) {
        if (!b) return '';
        opts = opts || {};
        var code = CODES.indexOf(b.code) >= 0 ? b.code : 'na';
        var html = '<span class="na-st na-st--' + code + '" data-na-state="' + code + '"'
            + (b.title ? ' title="' + esc(b.title) + '"' : '') + '>'
            + '<span class="na-st__i" aria-hidden="true">' + esc(b.icon || '') + '</span>'
            + '<span class="na-st__t">' + esc(b.label || '') + '</span></span>';
        if (opts.compact || !b.links) return html;
        var l = b.links, parts = [];
        var open = safeUrl(l.open, true), pdf = safeUrl(l.pdf, true), fix = safeUrl(l.fix, false), queue = safeUrl(l.queue, false);
        if (open && /^https?:/i.test(open)) parts.push('<a class="na-lk na-lk--open" href="' + esc(open) + '" target="_blank" rel="noopener">เปิดใน NextAcc</a>');
        if (pdf) parts.push('<a class="na-lk na-lk--pdf" href="' + esc(pdf) + '" target="_blank" rel="noopener">PDF</a>');
        if (fix) parts.push('<a class="na-lk na-lk--fix" href="' + esc(fix) + '" target="_blank">🛠 แก้ที่นี่</a>');
        if (queue) parts.push('<a class="na-lk na-lk--queue" href="' + esc(queue) + '" target="_blank">ดูในคิว</a>');
        if (parts.length) html += '<span class="na-lks">' + parts.join('') + '</span>';
        return html;
    }

    w.NaBadge = { render: render, esc: esc, safeUrl: safeUrl };
    if (typeof module !== 'undefined' && module.exports) module.exports = w.NaBadge;
})(typeof window !== 'undefined' ? window : this);
