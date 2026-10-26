/* ============================================================================
   ยอดล็อกตามสลิป (PHASE19_36 / CLAUDE.md ข้อ 18) — ฝั่งเบราว์เซอร์
   ----------------------------------------------------------------------------
   ช่องยอดที่ล็อก: data-slip-locked="1" + data-slip-value="<ยอดสลิป>" (เซิร์ฟเวอร์ใส่ให้)
   → กันพิมพ์/วาง/ลาก และคืนค่ายอดสลิปทุกครั้งที่มีคนพยายามแก้
   เซิร์ฟเวอร์บังคับยอดซ้ำตอนบันทึกเสมอ — ไฟล์นี้โหลดไม่ได้ = แค่แก้ช่องได้ แต่ยอดที่บันทึกยังเป็นยอดสลิป

   SlipLock.bindAllocate(amountId, checkboxId) — ติ๊ก "แบ่งสลิป" = ปลดล็อกให้กรอกยอดส่วนของการจองนี้
   SlipLock.preview({ fileId, amountId, badgeId, url }) — เลือกไฟล์สลิปแล้วตรวจทันที (AJAX ไม่ต้องโพสต์หน้า)
   ============================================================================ */
(function (w, d) {
    'use strict';
    if (w.SlipLock) return;

    function isLocked(el) {
        return !!(el && el.getAttribute && el.getAttribute('data-slip-locked') === '1');
    }

    function restore(el) {
        var v = el.getAttribute('data-slip-value');
        if (v !== null && el.value !== v) el.value = v;
    }

    function setLocked(el, on, value) {
        if (!el) return;
        if (on) {
            if (value !== undefined && value !== null) el.setAttribute('data-slip-value', String(value));
            el.setAttribute('data-slip-locked', '1');
            el.setAttribute('readonly', 'readonly');
            if ((' ' + el.className + ' ').indexOf(' slip-locked ') < 0) el.className = (el.className ? el.className + ' ' : '') + 'slip-locked';
            restore(el);
        } else {
            el.removeAttribute('data-slip-locked');
            el.removeAttribute('readonly');
            el.className = (' ' + el.className + ' ').replace(' slip-locked ', ' ').replace(/^\s+|\s+$/g, '');
        }
    }

    function guard(e) {
        var t = e.target;
        if (!isLocked(t)) return;
        if (e.type === 'keydown') {
            var k = e.key || '';
            if (k === 'Tab' || k === 'Shift' || k.indexOf('Arrow') === 0 || k === 'Home' || k === 'End' || (e.ctrlKey && (k === 'c' || k === 'a'))) return;
        }
        e.preventDefault();
        if (e.type === 'input' || e.type === 'change') restore(t);
    }

    ['keydown', 'paste', 'drop', 'cut'].forEach(function (ev) {
        d.addEventListener(ev, guard, { capture: true, passive: false });
    });
    ['input', 'change'].forEach(function (ev) {
        d.addEventListener(ev, function (e) { if (isLocked(e.target)) restore(e.target); }, true);
    });

    function badge(el, html, cls) {
        if (!el) return;
        el.innerHTML = html || '';
        el.className = 'slip-lock-msg ' + (cls || '');
        el.style.display = html ? '' : 'none';
    }

    // แจ้งหน้าที่ใช้ (เช่น สลิปก่อนยอด) ว่ากำลังตรวจ/ได้ผล/ตรวจไม่ได้ — event บนช่องไฟล์: sliplock:start / sliplock:result / sliplock:error
    function emit(target, name, detail) {
        if (!target) return;
        var ev;
        try { ev = new CustomEvent(name, { detail: detail }); }
        catch (e0) {
            try { ev = d.createEvent('CustomEvent'); ev.initCustomEvent(name, false, false, detail); } catch (e1) { return; }
        }
        try { target.dispatchEvent(ev); } catch (e2) { }
    }

    function esc(s) {
        return String(s === undefined || s === null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    var api = {
        setLocked: setLocked,

        bindAllocate: function (amountId, checkboxId) {
            var box = d.getElementById(amountId), chk = d.getElementById(checkboxId);
            if (!box || !chk) return;
            var apply = function () {
                var v = box.getAttribute('data-slip-value');
                if (v === null) return;   // ไม่ได้ล็อก
                if (chk.checked) {
                    setLocked(box, false);
                    box.setAttribute('data-slip-value', v);
                    box.focus();
                } else {
                    setLocked(box, true, v);
                }
            };
            chk.addEventListener('change', apply);
            if (chk.checked) apply();
        },

        preview: function (cfg) {
            var file = d.getElementById(cfg.fileId), box = d.getElementById(cfg.amountId), out = d.getElementById(cfg.badgeId);
            if (!file || !box || !w.FormData || !w.fetch) return;
            // หน้าที่ย่อรูปก่อน (data-sf-compress="1" — SlipFirst.prepare) ส่ง event sliplock:go เมื่อใส่รูปที่ย่อแล้วลงช่องไฟล์
            file.addEventListener('change', function () {
                if (file.getAttribute('data-sf-compress') === '1' && file.files && file.files.length) return;
                run();
            });
            file.addEventListener('sliplock:go', function () { run(); });
            function run() {
                if (!file.files || !file.files.length) {
                    setLocked(box, false);
                    box.removeAttribute('data-slip-value');
                    badge(out, '', '');
                    emit(file, 'sliplock:error', null);
                    return;
                }
                var fd = new FormData();
                fd.append('slipPreviewFile', file.files[0]);
                badge(out, '⏳ กำลังอ่านยอดจากสลิป…', 'wait');
                emit(file, 'sliplock:start', null);
                fetch(cfg.url || w.location.href, {
                    method: 'POST', body: fd, credentials: 'same-origin', headers: { 'X-Slip-Preview': '1' }
                }).then(function (r) { return r.json(); }).then(function (j) {
                    emit(file, 'sliplock:result', j || null);
                    if (!j || !j.active) { badge(out, '', ''); return; }
                    if (j.locked) {
                        box.value = j.amountText;
                        setLocked(box, true, j.amountText);
                        badge(out, '🔒 ' + esc(j.notice) + (j.extra ? '<br/>' + esc(j.extra) : ''), 'ok');
                    } else {
                        setLocked(box, false);
                        box.removeAttribute('data-slip-value');
                        badge(out, '✏️ ' + esc(j.notice), 'warn');
                    }
                }).catch(function () {
                    badge(out, 'ตรวจสลิปไม่ได้ตอนนี้ — กรอกยอดตามสลิป (ระบบตรวจซ้ำตอนบันทึก)', 'warn');
                    emit(file, 'sliplock:error', null);
                });
            }
        }
    };

    w.SlipLock = api;
})(window, document);
