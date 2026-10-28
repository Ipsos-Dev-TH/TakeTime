/* หน้าใบสำคัญจ่าย (Account/PaymentVoucher) — CLAUDE.md ข้อ 3b
 * • สรุปยอดสด + ตรวจสมการ ยอดก่อน VAT + VAT − หัก ณ ที่จ่าย = สุทธิ
 * • "จ่ายจาก" รวมแหล่งเงิน + บัญชี NextAcc (บัญชีที่ไม่มีแหล่งเงินผูก → ถาม "บันทึกในระบบเป็น")
 * • ตรวจใบซ้ำ (เลขที่ใบของผู้ขาย) + ชิปคำแนะนำจาก AI ของ NextAcc ผ่าน API/PvAssist.ashx — ผู้ใช้กด "ใช้" เอง ไม่เติมค่าเงียบ ๆ
 * • ตรวจช่องบังคับก่อนส่ง (ข้อความในหน้า ไม่ใช้ alert) — เซิร์ฟเวอร์ตรวจซ้ำเสมอ
 * ไม่มี window.PV_CFG (สคริปต์ตั้งค่าไม่ออก) = ฟอร์มทำงานแบบ postback ปกติ */
(function () {
    'use strict';
    var C = window.PV_CFG;
    if (!C || !C.ids) return;
    var ids = C.ids;

    function $(key) { return ids[key] ? document.getElementById(ids[key]) : null; }
    function num(el) {
        if (!el) return 0;
        var v = parseFloat(String(el.value || el.textContent || '0').replace(/,/g, ''));
        return isNaN(v) ? 0 : v;
    }
    function fmt(n) {
        return (Math.round(n * 100) / 100).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }
    function debounce(fn, ms) {
        var t = null;
        return function () { var a = arguments, s = this; clearTimeout(t); t = setTimeout(function () { fn.apply(s, a); }, ms); };
    }
    function selectedText(sel) { return sel && sel.selectedIndex >= 0 ? sel.options[sel.selectedIndex].text : ''; }
    function isPicked(v) { return !!v && v.indexOf('---') !== 0 && v !== '%'; }

    // ── สรุปยอด ────────────────────────────────────────────────────
    function updateTotals() {
        var sub = num($('sub')), vat = num($('vatAmt')), wht = num($('whtAmt')), net = num($('net'));
        var w = document.getElementById('pvTotWht');
        if (w) w.textContent = fmt(wht);
        var bar = document.getElementById('pvNetBar');
        if (bar) bar.textContent = fmt(net);
        var warn = document.getElementById('voucherValidationWarn');
        if (!warn) return;
        var computed = sub + vat - wht, diff = Math.abs(computed - net);
        if (diff > 0.01) {
            warn.style.display = 'block';
            warn.className = 'pv-calc is-bad';
            warn.textContent = 'ยอดไม่ตรง: ' + fmt(sub) + ' + ' + fmt(vat) + ' − ' + fmt(wht) + ' = ' + fmt(computed)
                + ' แต่ยอดสุทธิ = ' + fmt(net) + ' (ต่างกัน ' + fmt(diff) + ' บาท) — แก้ก่อนบันทึก';
        } else if (sub > 0) {
            warn.style.display = 'block';
            warn.className = 'pv-calc is-ok';
            warn.textContent = '✓ ยอดถูกต้อง: ยอดก่อน VAT + VAT − หัก ณ ที่จ่าย = ยอดจ่ายสุทธิ';
        } else {
            warn.style.display = 'none';
        }
    }

    // ── จ่ายจาก ──────────────────────────────────────────────────
    function groupAccountOptions(sel) {
        if (!sel || sel.querySelector('optgroup')) return;
        var acc = [];
        for (var i = 0; i < sel.options.length; i++) if (sel.options[i].value.indexOf('NA:') === 0) acc.push(sel.options[i]);
        if (!acc.length) return;
        var g = document.createElement('optgroup');
        g.label = sel.getAttribute('data-na-group') || 'บัญชีอื่นในผัง NextAcc';
        acc.forEach(function (o) { g.appendChild(o); });
        sel.appendChild(g);
    }
    function onPayFrom() {
        var pf = $('payFrom'), box = document.getElementById('pvPaidHowAs'), hint = document.getElementById('pvPayFromHint');
        if (!pf) return;
        var v = pf.value || '';
        var accountOnly = v.indexOf('NA:') === 0;
        if (box) box.hidden = !accountOnly;
        if (hint) {
            if (!v) hint.textContent = 'เลือกแหล่งเงินที่จ่ายออก — แสดงบัญชี NextAcc ที่ผูกไว้ให้ดูก่อนบันทึก';
            else if (accountOnly) hint.textContent = 'ใบนี้จะลง Cr บัญชี ' + selectedText(pf).replace(/^บัญชี NextAcc:\s*/, '') + ' ตรง ๆ';
            else {
                var t = selectedText(pf), k = t.indexOf(' — ');
                hint.textContent = k > 0 && t.indexOf('ยังไม่ผูก') < 0
                    ? 'ใบนี้จะลง Cr ' + t.substring(k + 3) + ' (บัญชีที่ผูกกับ "' + t.substring(0, k) + '")'
                    : 'แหล่งเงินนี้ยังไม่ผูกบัญชี NextAcc — ผูกได้ที่ ตั้งค่า › Accounting Integration › แหล่งเงิน (ไม่ผูก = NextAcc เลือกตามวิธีจ่าย)';
            }
        }
    }

    // ── ภาษี / หมวดหลัก ──────────────────────────────────────────
    function onVatType() {
        var vt = $('vat'), lbl = document.getElementById('pvAmtLabel');
        if (!vt || !lbl) return;
        var inclusive = vt.value === String(C.vatInclusiveId || '2');
        lbl.firstChild.nodeValue = inclusive ? 'จำนวนเงิน (รวม VAT แล้ว) ' : 'จำนวนเงิน (ไม่รวม VAT) ';
    }
    function headCat() {
        var hc = $('headCat'), t = document.getElementById('pvHeadCatText'), d = document.getElementById('pvHeadCat');
        if (!hc || !t) return;
        var picked = hc.selectedIndex > 0;
        t.textContent = picked ? selectedText(hc) : (C.lineCount > 0 ? 'ยังไม่ได้เลือก — กรุณาเลือก' : 'เลือกให้อัตโนมัติจากรายการแรก');
        if (d && !picked && C.lineCount > 0) d.open = true;
    }

    // ── API ─────────────────────────────────────────────────────
    function api(data, keepalive) {
        data.nonce = C.nonce;
        var body = Object.keys(data).map(function (k) {
            return encodeURIComponent(k) + '=' + encodeURIComponent(data[k] == null ? '' : String(data[k]));
        }).join('&');
        return fetch(C.api || '/API/PvAssist.ashx', {
            method: 'POST', credentials: 'same-origin', keepalive: !!keepalive,
            headers: { 'X-PV-Assist': '1', 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
            body: body
        }).then(function (r) { return r.ok ? r.json() : null; }).catch(function () { return null; });
    }

    // ── ตรวจใบซ้ำ ─────────────────────────────────────────────────
    var dupSeq = 0;
    function checkDup() {
        var inv = $('inv'), box = document.getElementById('pvDupBox'), vendor = $('vendor');
        if (!inv || !box) return;
        var v = (inv.value || '').trim();
        if (v.length < 2 || !vendor || !/^\d+$/.test(vendor.value || '')) { box.textContent = ''; return; }
        var seq = ++dupSeq;
        api({ action: 'dup', vendorId: vendor.value, inv: v, amount: num($('net')), date: ($('date') || {}).value || '', exclude: C.exclude || '' })
            .then(function (r) {
                if (seq !== dupSeq) return;
                box.textContent = '';
                if (!r || !r.ok || !r.hits || !r.hits.length) return;
                var m = el('div', 'pv-msg pv-msg-warn');
                m.setAttribute('role', 'alert');
                m.appendChild(el('b', null, 'อาจเป็นใบซ้ำ — เลขที่ใบนี้ของผู้ขายรายนี้เคยบันทึกแล้ว'));
                var ul = el('ul');
                r.hits.forEach(function (h) {
                    var li = el('li');
                    li.appendChild(document.createTextNode(h.sourceLabel + ' '));
                    var label = h.number || '(ไม่มีเลข)';
                    if (h.url) {
                        var a = el('a', null, label);
                        a.href = h.url; a.target = '_blank'; a.rel = 'noopener';
                        li.appendChild(a);
                    } else li.appendChild(el('b', null, label));
                    var extra = [];
                    if (h.date) extra.push(h.date);
                    if (h.amount) extra.push(fmt(h.amount) + ' บาท');
                    if (h.status) extra.push(h.status);
                    if (extra.length) li.appendChild(document.createTextNode(' · ' + extra.join(' · ')));
                    ul.appendChild(li);
                });
                m.appendChild(ul);
                m.appendChild(el('div', 'pv-hint', 'ถ้าเป็นการจ่ายงวดถัดไปของใบเดิม กด "บันทึก" แล้วยืนยันอีกครั้งได้'));
                box.appendChild(m);
            });
    }

    // ── ชิปคำแนะนำ AI ────────────────────────────────────────────
    var targets = { line: 'line', vat: 'vat', wht: 'wht', credit: 'credit' };
    var pending = {};   // kind → {fid, value, answer}

    // ค่าที่ผู้ใช้เลือกจริง ในรูปเดียวกับคำตอบของ NextAcc (รหัสบัญชี / "7"|"0" / "None" / "Cash"|"Credit") — null = ไม่ส่ง
    function currentAnswer(kind) {
        if (kind === 'account') {
            var t = selectedText($('line')), k = t.indexOf(' — ');
            var m = (k >= 0 ? t.substring(k + 3) : t).match(/^\s*(\d{3,})/);
            return m ? m[1] : null;
        }
        if (kind === 'vat') { var vt = $('vat'); return vt && C.vatPct && C.vatPct[vt.value] != null ? String(C.vatPct[vt.value]) : null; }
        if (kind === 'wht') return (($('wht') || {}).value || '0') === '0' ? 'None' : null;   // อัตรา ≠ รหัสประเภทเงินได้ — ไม่สอนค่าที่เดา
        if (kind === 'paytype') return ($('credit') || {}).checked ? 'Credit' : 'Cash';
        return 'rejected';
    }
    function feedback(fid, chosen, accepted) {
        if (!fid || !chosen) return;
        api({ action: 'feedback', fid: fid, chosen: chosen || 'none', accepted: accepted ? '1' : '0' }, true);
    }
    function applySuggestion(s, chip) {
        var key = targets[s.target], ctl = key ? $(key) : null;
        if (s.kind === 'contact') { feedback(s.fid, s.answer, true); chip.classList.add('is-done'); chip.querySelector('.pv-chip-act').remove(); return; }
        if (!ctl) return;
        if (s.kind === 'paytype') {
            if (ctl.disabled) return;
            ctl.checked = s.value === '1';
        } else {
            var found = false;
            for (var i = 0; i < ctl.options.length; i++) if (ctl.options[i].value === s.value) { found = true; break; }
            if (!found) {
                chip.querySelector('.pv-chip-why').textContent = 'ใช้ไม่ได้ — ตัวเลือกนี้ไม่มีในรายการของหน้านี้ (ลองกด "ดึง Chart of Accounts" ในหน้าตั้งค่า)';
                return;
            }
            ctl.value = s.value;
        }
        delete pending[s.kind];
        feedback(s.fid, s.answer, true);
        chip.classList.add('is-done');
        var act = chip.querySelector('.pv-chip-act');
        if (act) act.replaceWith(el('span', 'pv-chip-k', '✓ ใช้แล้ว'));
        // VAT / หัก ณ ที่จ่าย คำนวณยอดใหม่ฝั่งเซิร์ฟเวอร์ (AutoPostBack เดิมของ control)
        if (s.kind === 'vat' || s.kind === 'wht') {
            setTimeout(function () { ctl.dispatchEvent(new Event('change', { bubbles: true })); }, 150);
        } else {
            ctl.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }
    function chipFor(s) {
        var chip = el('div', 'pv-chip' + (s.same ? ' is-same' : '') + (s.kind === 'contact' ? ' is-info' : ''));
        var main = el('div', 'pv-chip-main');
        main.appendChild(el('span', 'pv-chip-k', s.same ? '✓ ตรงกับที่เลือก' : (s.kind === 'contact' ? 'NextAcc' : 'แนะนำ')));
        main.appendChild(el('span', 'pv-chip-l', s.label));
        if (s.confidence > 0) main.appendChild(el('span', 'pv-chip-c' + (s.confidence < 60 ? ' is-low' : ''), s.confidence + '%'));
        chip.appendChild(main);
        if (!s.same) {
            var act = el('div', 'pv-chip-act');
            var use = el('button', 'pv-use', s.kind === 'contact' ? 'รับทราบ' : 'ใช้');
            use.type = 'button';
            use.setAttribute('aria-label', (s.kind === 'contact' ? 'รับทราบ: ' : 'ใช้คำแนะนำ: ') + s.label);
            use.addEventListener('click', function () { applySuggestion(s, chip); });
            var no = el('button', null, '✕');
            no.type = 'button';
            no.setAttribute('aria-label', 'ไม่ใช้คำแนะนำนี้');
            no.addEventListener('click', function () {
                delete pending[s.kind];
                feedback(s.fid, currentAnswer(s.kind), false);
                chip.remove();
            });
            act.appendChild(use); act.appendChild(no);
            chip.appendChild(act);
            if (s.fid) pending[s.kind] = { fid: s.fid, value: s.value, answer: s.answer };
        }
        chip.appendChild(el('div', 'pv-chip-why', s.reasoning || ''));
        return chip;
    }
    function render(boxId, list, kinds) {
        var box = document.getElementById(boxId);
        if (!box) return;
        box.textContent = '';
        var shown = list.filter(function (s) { return kinds.indexOf(s.kind) >= 0; });
        if (!shown.length) return;
        box.appendChild(el('div', 'pv-ai-h', 'คำแนะนำจาก NextAcc — ตรวจก่อนกด "ใช้"'));
        shown.forEach(function (s) { box.appendChild(chipFor(s)); });
    }
    var lineSeq = 0, vendorSeq = 0;
    function suggestLine() {
        if (!C.ai) return;
        var d = $('desc');
        var desc = d ? (d.value || '').trim() : '';
        if (desc.length < 2) return;
        var vt = $('vat'), seq = ++lineSeq;
        api({
            action: 'suggest', want: 'line', vendorId: ($('vendor') || {}).value || '', desc: desc, amount: num($('amt')),
            line: ($('line') || {}).value || '', vatPct: vt && C.vatPct && C.vatPct[vt.value] != null ? C.vatPct[vt.value] : '',
            wht: ($('wht') || {}).value || '0', credit: ($('credit') || {}).checked ? '1' : '0'
        }).then(function (r) {
            if (seq !== lineSeq || !r || !r.ok || !r.enabled) return;
            render('pvAiLine', r.suggestions || [], ['account', 'vat', 'wht']);
        });
    }
    function suggestVendor() {
        if (!C.ai) return;
        var v = $('vendor');
        if (!v || !/^\d+$/.test(v.value || '')) { render('pvAiVendor', [], []); render('pvAiPay', [], []); return; }
        var seq = ++vendorSeq;
        api({ action: 'suggest', want: 'vendor', vendorId: v.value, credit: ($('credit') || {}).checked ? '1' : '0' })
            .then(function (r) {
                if (seq !== vendorSeq || !r || !r.ok || !r.enabled) return;
                render('pvAiVendor', r.suggestions || [], ['contact']);
                render('pvAiPay', r.suggestions || [], ['paytype']);
            });
    }
    // ผู้ใช้เปลี่ยนค่าเองระหว่างที่ชิปยังค้าง = ไม่ใช้คำแนะนำ (สอน NextAcc ด้วยค่าที่เลือกจริง)
    function watchOverride(key, kind) {
        var ctl = $(key);
        if (!ctl) return;
        ctl.addEventListener('change', function () {
            var p = pending[kind];
            if (!p) return;
            var now = kind === 'paytype' ? (ctl.checked ? '1' : '0') : ctl.value;
            if (now === p.value) return;
            delete pending[kind];
            feedback(p.fid, currentAnswer(kind), false);
        });
    }

    // ── ตรวจช่องบังคับก่อนส่ง ─────────────────────────────────────
    function fieldOf(ctl) { var n = ctl; while (n && !(n.classList && n.classList.contains('pv-field'))) n = n.parentNode; return n; }
    function mark(ctl, msg) {
        if (!ctl) return;
        var f = fieldOf(ctl) || ctl.parentNode, old = f.querySelector('.pv-field-err');
        if (old) old.remove();
        ctl.classList.toggle('pv-invalid', !!msg);
        if (msg) { ctl.setAttribute('aria-invalid', 'true'); f.appendChild(el('div', 'pv-field-err', msg)); }
        else ctl.removeAttribute('aria-invalid');
    }
    function validateForSave() {
        var bad = [];
        function need(key, ok, msg) { var c = $(key); if (!c) return; mark(c, ok ? null : msg); if (!ok) bad.push(c); }
        need('vendor', /^\d+$/.test(($('vendor') || {}).value || ''), 'เลือกผู้ขาย / ผู้รับเงิน');
        var pf = ($('payFrom') || {}).value || '';
        need('payFrom', !!pf, 'เลือกว่าจ่ายจากแหล่งเงินไหน');
        if (pf.indexOf('NA:') === 0) need('paidHowAs', isPicked(($('paidHowAs') || {}).value), 'เลือกวิธีจ่ายที่จะบันทึกในระบบ');
        need('vat', isPicked(($('vat') || {}).value), 'เลือกประเภทภาษีมูลค่าเพิ่ม');
        var cil = $('cil');
        if (cil && cil.checked) {
            Array.prototype.forEach.call(document.querySelectorAll('[data-pv-required="1"]'), function (c) {
                var ok = (c.value || '').trim().length > 0;
                mark(c, ok ? null : 'กรอกช่องนี้ (ใบรับรองแทนใบเสร็จ)');
                if (!ok) bad.push(c);
            });
        }
        if (bad.length) {
            var m = document.getElementById('pvFormMsg');
            if (m) {
                m.textContent = '';
                var box = el('div', 'pv-msg pv-msg-err');
                box.setAttribute('role', 'alert');
                box.appendChild(el('b', null, 'ยังบันทึกไม่ได้ — กรอกช่องที่ทำเครื่องหมายสีแดง (' + bad.length + ' ช่อง)'));
                m.appendChild(box);
            }
            try { bad[0].focus({ preventScroll: true }); bad[0].scrollIntoView({ block: 'center', behavior: 'smooth' }); } catch (e) { }
            return false;
        }
        return true;
    }

    function init() {
        ['sub', 'vatAmt', 'whtAmt', 'net'].forEach(function (k) {
            var c = $(k);
            if (c) { c.addEventListener('input', updateTotals); c.addEventListener('change', updateTotals); }
        });
        updateTotals();

        var pf = $('payFrom');
        if (pf) { groupAccountOptions(pf); pf.addEventListener('change', function () { onPayFrom(); mark(pf, null); }); onPayFrom(); }
        groupAccountOptions($('line'));
        var vt = $('vat');
        if (vt) { vt.addEventListener('change', onVatType); onVatType(); }
        headCat();

        var inv = $('inv');
        if (inv) { inv.addEventListener('input', debounce(checkDup, 700)); inv.addEventListener('change', checkDup); }
        var vendor = $('vendor');
        if (vendor) vendor.addEventListener('change', function () { mark(vendor, null); checkDup(); suggestVendor(); });
        var ds = debounce(suggestLine, 700);
        var d = $('desc'), a = $('amt'), ln = $('line');
        if (d) { d.addEventListener('change', ds); d.addEventListener('blur', ds); }
        if (a) a.addEventListener('change', ds);
        watchOverride('line', 'account');
        watchOverride('vat', 'vat');
        watchOverride('wht', 'wht');
        watchOverride('credit', 'paytype');

        var save = $('save');
        if (save) save.addEventListener('click', function (e) { if (!validateForSave()) e.preventDefault(); });
        Array.prototype.forEach.call(document.querySelectorAll('#pvWrap select, #pvWrap input'), function (c) {
            c.addEventListener('change', function () { if (c.classList.contains('pv-invalid') && (c.value || '').trim() && isPicked(c.value)) mark(c, null); });
        });

        if (inv && (inv.value || '').trim()) checkDup();
        if (vendor && /^\d+$/.test(vendor.value || '')) suggestVendor();
        if (C.scrollToMsg) {
            var m = document.getElementById('pvFormMsg');
            if (m && m.firstElementChild) try { m.scrollIntoView({ block: 'center' }); } catch (e) { }
        }
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
