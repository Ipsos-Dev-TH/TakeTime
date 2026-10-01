/* ============================================================================
   หน้าตั้งค่า — พฤติกรรมส่วนกลาง (ใช้คู่กับ /Content/admin-settings.css)
   ----------------------------------------------------------------------------
   ทำงานฝั่งเบราว์เซอร์ล้วน เปิดใช้ด้วย data-attribute — หน้าไหนไม่ใส่ก็ไม่มีผลอะไร
   ไฟล์นี้โหลดไม่ได้ = หน้าทำงานตามเดิมทุกอย่าง (ฝั่งเซิร์ฟเวอร์ตรวจค่าซ้ำเสมอ)

     data-as-dirty            ครอบส่วนฟอร์ม → แก้แล้วยังไม่บันทึก เตือนก่อนออกจากหน้า
                              + ป้าย .as-dirty-flag ในแถบบันทึกขึ้น
     data-as-startdirty       (ที่ไหนก็ได้) หน้านี้มีค่าที่ยังไม่ได้บันทึกตั้งแต่โหลด
                              (เช่น บันทึกไม่ผ่าน / เติมค่าให้ทุกห้องแล้วแต่ยังไม่กดบันทึก)
     data-as-confirm="ข้อความ" ปุ่ม/ลิงก์ → ถามยืนยันก่อนทำ
     data-as-validate="selector"  ปุ่มบันทึก → ตรวจช่องในขอบเขตนั้นก่อนส่ง (ว่าง = ทั้งหน้า)
     ช่องกรอก:
       data-as-num="int|money"  data-as-min  data-as-max  data-as-req="1"
       data-as-rowreq="1"       ต้องกรอกเมื่อติ๊กช่อง (checkbox) ในแถวเดียวกัน
       data-as-rowmin="n"       ค่าต่ำสุดเมื่อติ๊กช่องในแถวเดียวกัน
       data-as-url="1"          ต้องขึ้นต้น http:// หรือ https://
       data-as-maxlen="n"       ความยาวสูงสุด
       data-as-label="ชื่อช่อง"  ใช้ในข้อความแจ้ง
       data-as-err="ข้อความ"    ข้อผิดพลาดจากฝั่งเซิร์ฟเวอร์ (แสดงใต้ช่องตอนโหลด)
     textarea[data-as-preview="id"]  ตัวอย่างข้อความแบบสด + ไฮไลต์ "[แก้ไข: …]"
       data-as-phcount="id"     กล่องนับจุดที่ยังเป็นข้อความตัวอย่าง
     [data-as-nextph="textareaId"]   ปุ่มกระโดดไปช่อง "[แก้ไข: …]" ถัดไปในกล่องข้อความ
     input[data-as-techtoggle]       ติ๊กแล้วแสดงค่าทางเทคนิค (.as-tech) — จำค่าไว้ในเครื่อง

   window.AsSettings.guard(el, fn)  ผูกเงื่อนไขก่อนกดปุ่ม (fn คืน false = ยกเลิก)
   window.AsSettings.isDirty()
   ========================================================================== */
(function () {
    'use strict';

    var DIRTY_MSG = 'มีการแก้ไขที่ยังไม่ได้บันทึก — ออกจากหน้านี้แล้วค่าที่แก้จะหายไป';
    var PH_RE_SRC = '\\[แก้ไข[^\\]]*\\]|⚠?\\s*ตัวอย่าง —[^\\n<]*';

    var dirty = false;
    var submitAt = 0;
    var guards = [];

    function all(sel, root) { return (root || document).querySelectorAll(sel); }

    function matches(el, sel) {
        if (!el || el.nodeType !== 1) return false;
        var f = el.matches || el.msMatchesSelector || el.webkitMatchesSelector;
        return f ? f.call(el, sel) : false;
    }

    function closest(el, sel) {
        while (el && el.nodeType === 1) {
            if (matches(el, sel)) return el;
            el = el.parentNode;
        }
        return null;
    }

    function enc(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // ── 1) แก้แล้วยังไม่บันทึก ────────────────────────────────────────────────

    function markDirty() {
        if (dirty) return;
        dirty = true;
        try { document.body.classList.add('as-is-dirty'); } catch (e) { }
    }

    function isFormField(t) {
        if (!t || !t.tagName) return false;
        var n = t.tagName.toUpperCase();
        if (n !== 'INPUT' && n !== 'SELECT' && n !== 'TEXTAREA') return false;
        if (t.type === 'hidden' || t.type === 'submit' || t.type === 'button') return false;
        if (t.hasAttribute('data-as-nodirty')) return false;
        return true;
    }

    function initDirty() {
        var roots = all('[data-as-dirty]');
        if (!roots.length) return;

        function onEdit(e) { if (isFormField(e.target)) markDirty(); }
        for (var i = 0; i < roots.length; i++) {
            roots[i].addEventListener('input', onEdit);
            roots[i].addEventListener('change', onEdit);
        }
        if (all('[data-as-startdirty]').length) markDirty();

        // ส่งฟอร์ม (กดปุ่ม/postback) ไม่ใช่การ "ทิ้ง" ค่า — ไม่ต้องเตือน
        var f = document.forms && document.forms[0];
        if (f) f.addEventListener('submit', function () { submitAt = new Date().getTime(); });
        if (typeof window.__doPostBack === 'function') {
            var orig = window.__doPostBack;
            window.__doPostBack = function () {
                submitAt = new Date().getTime();
                return orig.apply(this, arguments);
            };
        }

        window.addEventListener('beforeunload', function (e) {
            if (!dirty) return undefined;
            if (new Date().getTime() - submitAt < 4000) return undefined;
            e.returnValue = DIRTY_MSG;
            return DIRTY_MSG;
        });
    }

    // ── 2) ตรวจค่าที่กรอก ──────────────────────────────────────────────────────

    function labelOf(el) {
        return el.getAttribute('data-as-label') || 'ช่องนี้';
    }

    function rowChecked(el) {
        var tr = closest(el, 'tr') || closest(el, '.as-row');
        if (!tr) return false;
        var cb = tr.querySelector('input[type=checkbox]');
        return !!(cb && cb.checked);
    }

    /** ซ่อนด้วย style="display:none" (เช่น การ์ดของเกตเวย์ที่ไม่ได้เลือก) = ไม่เกี่ยว ไม่ต้องตรวจ
     *  (ช่องในกล่องพับ details / การ์ดพับ ยังตรวจตามปกติ แล้วกางให้เห็นเมื่อผิด) */
    function hiddenInline(el) {
        var p = el;
        while (p && p.nodeType === 1) {
            if (p.style && p.style.display === 'none') return true;
            p = p.parentNode;
        }
        return false;
    }

    function validateOne(el) {
        if (el.disabled || hiddenInline(el)) return '';
        var v = (el.value || '').replace(/^\s+|\s+$/g, '');
        var kind = el.getAttribute('data-as-num');
        var req = el.getAttribute('data-as-req') === '1'
               || (el.getAttribute('data-as-rowreq') === '1' && rowChecked(el));
        var minA = el.getAttribute('data-as-min');
        var maxA = el.getAttribute('data-as-max');
        var maxLen = parseInt(el.getAttribute('data-as-maxlen') || '0', 10);

        if (v.length === 0) return req ? ('กรุณากรอก' + (el.getAttribute('data-as-label') ? ' ' + labelOf(el) : '')) : '';
        if (maxLen > 0 && v.length > maxLen)
            return 'ยาวเกินไป (' + v.length.toLocaleString() + ' / สูงสุด ' + maxLen.toLocaleString() + ' ตัวอักษร)';
        if (el.getAttribute('data-as-url') === '1' && !/^https?:\/\//i.test(v))
            return 'ต้องขึ้นต้นด้วย http:// หรือ https://';

        if (kind) {
            var raw = v.replace(/,/g, '');
            var ok = kind === 'int' ? /^-?\d+$/.test(raw) : /^-?\d+(\.\d+)?$/.test(raw);
            if (!ok) return kind === 'int' ? 'ใส่เป็นจำนวนเต็ม (ตัวเลขล้วน)' : 'ใส่เป็นตัวเลข เช่น 500 หรือ 1,500.50';
            var n = parseFloat(raw);
            var rmin = minA !== null && minA !== '' ? parseFloat(minA) : null;
            // ติ๊กในแถวแล้ว ต้องอย่างน้อย data-as-rowmin (เช่น รับสัตว์เลี้ยง → อย่างน้อย 1 ตัว)
            var rowMinA = el.getAttribute('data-as-rowmin');
            if (rowMinA !== null && rowMinA !== '' && rowChecked(el)) {
                var rm = parseFloat(rowMinA);
                if (rmin === null || rm > rmin) rmin = rm;
            }
            if (rmin !== null && n < rmin) return 'ต้องไม่น้อยกว่า ' + rmin.toLocaleString();
            if (maxA !== null && maxA !== '' && n > parseFloat(maxA))
                return 'ต้องไม่เกิน ' + parseFloat(maxA).toLocaleString();
        }
        return '';
    }

    function msgHost(el) {
        // ข้อความแจ้งวางต่อท้ายช่อง (ตัวเดียวต่อช่อง)
        var next = el.nextSibling;
        while (next && next.nodeType === 3) next = next.nextSibling;
        if (next && next.nodeType === 1 && next.className === 'as-fielderr') return next;
        var span = document.createElement('span');
        span.className = 'as-fielderr';
        el.parentNode.insertBefore(span, el.nextSibling);
        return span;
    }

    function showErr(el, msg) {
        if (msg) {
            el.classList.add('as-invalid');
            msgHost(el).textContent = msg;
            el.setAttribute('aria-invalid', 'true');
        } else {
            el.classList.remove('as-invalid');
            el.removeAttribute('aria-invalid');
            var next = el.nextSibling;
            while (next && next.nodeType === 3) next = next.nextSibling;
            if (next && next.nodeType === 1 && next.className === 'as-fielderr') next.parentNode.removeChild(next);
        }
    }

    function revealField(el) {
        // ช่องที่อยู่ในกล่องพับ (details) ต้องกางออกก่อนถึงจะเห็น
        var d = closest(el, 'details');
        while (d) { d.open = true; d = closest(d.parentNode, 'details'); }
        var card = closest(el, '[data-pg-adv]');
        if (card) card.classList.remove('pg-collapsed');
    }

    var FIELD_SEL = '[data-as-num],[data-as-req],[data-as-rowreq],[data-as-url],[data-as-maxlen]';

    function initValidation() {
        var fields = all(FIELD_SEL);
        for (var i = 0; i < fields.length; i++) {
            (function (el) {
                var handler = function () { showErr(el, validateOne(el)); refreshCounter(); };
                el.addEventListener('blur', handler);
                el.addEventListener('change', handler);
                el.addEventListener('input', function () {
                    if (el.classList.contains('as-invalid')) handler();
                });
            })(fields[i]);
        }

        // ติ๊ก/ไม่ติ๊กช่องในแถว → ตรวจช่องที่ขึ้นกับมันใหม่
        document.addEventListener('change', function (e) {
            var t = e.target;
            if (!t || t.type !== 'checkbox') return;
            var tr = closest(t, 'tr') || closest(t, '.as-row');
            if (!tr) return;
            var deps = tr.querySelectorAll('[data-as-rowreq]');
            for (var k = 0; k < deps.length; k++) showErr(deps[k], validateOne(deps[k]));
            refreshCounter();
        });

        // ข้อผิดพลาดจากฝั่งเซิร์ฟเวอร์
        var srv = all('[data-as-err]');
        for (var s = 0; s < srv.length; s++) {
            var m = srv[s].getAttribute('data-as-err');
            if (!m) continue;
            if (srv[s].tagName === 'DIV' || srv[s].tagName === 'SPAN' || srv[s].tagName === 'TABLE') {
                srv[s].classList.add('as-invalid');
                var box = document.createElement('span');
                box.className = 'as-fielderr';
                box.textContent = m;
                srv[s].appendChild(box);
            } else {
                showErr(srv[s], m);
            }
            revealField(srv[s]);
        }
        refreshCounter();
    }

    function refreshCounter() {
        var bad = all('.as-invalid').length;
        var flags = all('.as-clienterr');
        for (var i = 0; i < flags.length; i++) {
            if (bad > 0) {
                flags[i].textContent = 'มีช่องที่ต้องแก้ ' + bad + ' ช่อง (ไฮไลต์สีแดง)';
                flags[i].classList.add('show');
            } else {
                flags[i].classList.remove('show');
            }
        }
    }

    function validateScope(sel) {
        var root = sel ? document.querySelector(sel) : document;
        if (!root) return true;
        var fields = root.querySelectorAll(FIELD_SEL);
        var first = null;
        for (var i = 0; i < fields.length; i++) {
            var msg = validateOne(fields[i]);
            showErr(fields[i], msg);
            if (msg && !first) first = fields[i];
        }
        refreshCounter();
        if (first) {
            revealField(first);
            try { first.focus(); } catch (e) { }
            try { first.scrollIntoView({ block: 'center' }); } catch (e2) { first.scrollIntoView(); }
            return false;
        }
        return true;
    }

    // ── 3) ปุ่ม: ตรวจค่า → เงื่อนไขเฉพาะหน้า → ถามยืนยัน (ก่อน onclick เดิมของปุ่ม) ──

    function cancel(e) {
        e.preventDefault();
        if (e.stopImmediatePropagation) e.stopImmediatePropagation();
        e.stopPropagation();
    }

    function initButtons() {
        // capture phase บน document = ทำงานก่อน onclick ของปุ่ม (รวม confirm เดิม/__doPostBack)
        document.addEventListener('click', function (e) {
            var btn = closest(e.target, 'input[type=submit],input[type=button],button,a');
            if (!btn) return;

            if (btn.hasAttribute('data-as-validate')) {
                if (!validateScope(btn.getAttribute('data-as-validate'))) { cancel(e); return; }
            }
            for (var g = 0; g < guards.length; g++) {
                if (guards[g].el === btn) {
                    var ok = true;
                    try { ok = guards[g].fn() !== false; } catch (ex) { ok = true; }
                    if (!ok) { cancel(e); return; }
                }
            }
            var msg = btn.getAttribute('data-as-confirm');
            if (msg && !window.confirm(msg)) { cancel(e); return; }

            if (matches(btn, 'input[type=submit],button[type=submit],button:not([type])'))
                submitAt = new Date().getTime();
        }, true);
    }

    // ── 4) ตัวอย่างข้อความ + ไฮไลต์ "[แก้ไข: …]" ─────────────────────────────

    /** แปลงเหมือน BookingPolicy.ToHtml ฝั่งเซิร์ฟเวอร์ (encode ก่อน → ตัวหนา → ขึ้นบรรทัด) + ไฮไลต์ */
    function policyHtml(text) {
        var s = enc(String(text || '').replace(/\r\n/g, '\n').replace(/\r/g, '\n').replace(/^\s+|\s+$/g, ''));
        s = s.replace(new RegExp(PH_RE_SRC, 'g'), function (m) { return '<mark class="as-ph">' + m + '</mark>'; });
        s = s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
        return s.replace(/\n/g, '<br/>');
    }

    function countPh(text) {
        var m = String(text || '').match(new RegExp(PH_RE_SRC, 'g'));
        return m ? m.length : 0;
    }

    function initPreview() {
        var tas = all('textarea[data-as-preview]');
        for (var i = 0; i < tas.length; i++) {
            (function (ta) {
                var pv = document.getElementById(ta.getAttribute('data-as-preview'));
                var cnt = ta.getAttribute('data-as-phcount') ? document.getElementById(ta.getAttribute('data-as-phcount')) : null;
                var timer = null;
                function paint() {
                    if (pv) pv.innerHTML = policyHtml(ta.value);
                    if (cnt) {
                        var n = countPh(ta.value);
                        cnt.className = 'as-phcount ' + (n > 0 ? 'warn' : 'ok');
                        cnt.textContent = n > 0
                            ? '⚠ ยังมีข้อความตัวอย่าง/ช่อง [แก้ไข: …] ' + n + ' จุด (ไฮไลต์สีเหลือง)'
                            : '✓ ไม่มีข้อความตัวอย่างค้าง';
                    }
                    var nb = all('[data-as-nextph="' + ta.id + '"]');
                    for (var k = 0; k < nb.length; k++) nb[k].disabled = countPh(ta.value) === 0;
                }
                ta.addEventListener('input', function () {
                    if (timer) clearTimeout(timer);
                    timer = setTimeout(paint, 150);
                });
                paint();
            })(tas[i]);
        }

        var nexts = all('[data-as-nextph]');
        for (var j = 0; j < nexts.length; j++) {
            nexts[j].addEventListener('click', function (e) {
                e.preventDefault();
                var ta = document.getElementById(this.getAttribute('data-as-nextph'));
                if (!ta) return;
                var re = new RegExp(PH_RE_SRC, 'g');
                var from = ta.selectionEnd || 0;
                var hit = null, m;
                while ((m = re.exec(ta.value)) !== null) {
                    if (m.index >= from) { hit = m; break; }
                    if (!hit) hit = m;   // วนกลับไปจุดแรก
                    if (re.lastIndex === m.index) re.lastIndex++;
                }
                if (!hit) {
                    re.lastIndex = 0;
                    hit = re.exec(ta.value);
                }
                if (!hit) return;
                ta.focus();
                try { ta.setSelectionRange(hit.index, hit.index + hit[0].length); } catch (ex) { }
                // เลื่อนกล่องข้อความไปบรรทัดที่เลือก (เบราว์เซอร์บางตัวไม่เลื่อนให้เอง)
                var before = ta.value.substring(0, hit.index).split('\n').length;
                var total = ta.value.split('\n').length || 1;
                ta.scrollTop = Math.max(0, (before / total) * ta.scrollHeight - ta.clientHeight / 2);
            });
        }
    }

    // ── 5) แสดง/ซ่อนค่าทางเทคนิค ─────────────────────────────────────────────

    function initTech() {
        var boxes = all('input[data-as-techtoggle]');
        if (!boxes.length) return;
        var on = false;
        try { on = localStorage.getItem('asShowTech') === '1'; } catch (e) { }
        function apply(v) {
            if (v) document.body.classList.add('as-show-tech');
            else document.body.classList.remove('as-show-tech');
            for (var i = 0; i < boxes.length; i++) boxes[i].checked = v;
        }
        for (var i = 0; i < boxes.length; i++) {
            boxes[i].addEventListener('change', function () {
                apply(this.checked);
                try { localStorage.setItem('asShowTech', this.checked ? '1' : '0'); } catch (e) { }
            });
        }
        apply(on);
    }

    // ── เริ่มทำงาน ───────────────────────────────────────────────────────────

    window.AsSettings = {
        guard: function (el, fn) {
            if (typeof el === 'string') el = document.getElementById(el);
            if (el && typeof fn === 'function') guards.push({ el: el, fn: fn });
        },
        isDirty: function () { return dirty; },
        markDirty: markDirty,
        countPlaceholders: countPh
    };

    function init() {
        try { initButtons(); } catch (e) { }
        try { initDirty(); } catch (e) { }
        try { initValidation(); } catch (e) { }
        try { initPreview(); } catch (e) { }
        try { initTech(); } catch (e) { }
        try {
            // ข้อความแจ้งผลจากเซิร์ฟเวอร์ (บันทึกสำเร็จ/ไม่สำเร็จ) — เลื่อนให้เห็นทันที
            var err = document.querySelector('[data-as-banner="err"]');
            if (err && err.scrollIntoView) err.scrollIntoView({ block: 'center' });
        } catch (e) { }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
