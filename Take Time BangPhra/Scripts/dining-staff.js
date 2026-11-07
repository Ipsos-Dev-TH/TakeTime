/* dining-staff.js — กระดานออเดอร์ + แผนผังโต๊ะ QR สั่งอาหาร (ฝั่งพนักงาน, ทีม B)
 * ใช้กับ Admin/Dining/Orders.aspx (mode=board) และ Admin/Dining/Tables.aspx (mode=tables)
 * ข้อมูลทุกอย่างมาจาก WebMethod ของ Orders.aspx (JSON) — ทุกค่าที่ลง DOM ผ่าน esc() เสมอ (ห้ามต่อ string ดิบ)
 * ES5 เท่านั้น (มือถือพนักงาน/เบราว์เซอร์เก่า) */
(function (w, d) {
    'use strict';
    var B = null;            // boot
    var S = {                // state
        filter: {}, orders: [], tables: {}, slips: [], counts: {}, lastTicks: 0,
        seenNew: {}, pollTimer: null, pollN: 0, loading: false, soundOn: true, muted: false,
        prevCall: -1, prevBill: -1, focusDone: false, tilesZone: 0, panelSession: 0, alertOpen: false, alertTimer: null
    };

    // ── utils ────────────────────────────────────────────────────────────────
    function esc(s) {
        if (s === null || s === undefined) return '';
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function $(id) { return d.getElementById(id); }
    function money(v) { var n = Number(v || 0); return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
    function slipUrl(p) { if (!p) return ''; return (B.root || '/') + String(p).replace(/^~\//, '').replace(/^\//, ''); }
    function toast(msg, kind) {
        var t = $('dnToast'); if (!t) return;
        t.textContent = msg || ''; t.className = 'dn-toast show ' + (kind || '');
        clearTimeout(t._tm); t._tm = setTimeout(function () { t.className = 'dn-toast'; }, 3500);
    }
    function api(method, params) {
        return fetch(B.api + '/' + method, {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json; charset=utf-8', 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify(params || {})
        }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        }).then(function (j) {
            var res = j && j.d !== undefined ? j.d : j;
            if (!res) throw new Error('empty');
            return res;
        });
    }
    function fail(res) { toast((res && res.message) ? res.message : 'ทำรายการไม่สำเร็จ', 'err'); }
    function tableOf(o) { return (S.tables && S.tables[o.tableId]) ? S.tables[o.tableId] : null; }

    // ── เสียง (WebAudio — เสียงต่างกันตามเหตุการณ์; fallback <audio>) ───────
    var AC = null;
    function ac() {
        if (AC) return AC;
        try { AC = new (w.AudioContext || w.webkitAudioContext)(); } catch (e) { AC = null; }
        return AC;
    }
    function tone(freq, t0, dur, vol) {
        var c = ac(); if (!c) return;
        var o = c.createOscillator(), g = c.createGain();
        o.type = 'sine'; o.frequency.value = freq;
        g.gain.setValueAtTime(0.0001, c.currentTime + t0);
        g.gain.exponentialRampToValueAtTime(vol || 0.5, c.currentTime + t0 + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, c.currentTime + t0 + dur);
        o.connect(g); g.connect(c.destination);
        o.start(c.currentTime + t0); o.stop(c.currentTime + t0 + dur + 0.05);
    }
    function beep(kind) {
        if (!S.soundOn) return;
        var c = ac();
        if (c && c.state === 'suspended') { try { c.resume(); } catch (e) { } }
        if (c) {
            if (kind === 'call') { tone(1320, 0, 0.12, 0.6); tone(1320, 0.18, 0.12, 0.6); tone(1320, 0.36, 0.12, 0.6); tone(1760, 0.6, 0.25, 0.6); }
            else if (kind === 'bill') { tone(523, 0, 0.25, 0.5); tone(392, 0.3, 0.4, 0.5); }
            else { tone(880, 0, 0.18, 0.5); tone(1175, 0.2, 0.3, 0.5); }
            return;
        }
        try { var a = $('dnSound'); if (a) { a.currentTime = 0; a.play().catch(function () { }); } } catch (e) { }
    }
    function unlockAudio() { var c = ac(); if (c && c.state === 'suspended') { try { c.resume(); } catch (e) { } } }

    // ── modal ทั่วไป ─────────────────────────────────────────────────────────
    function openModal(title, bodyHtml, buttons) {
        var m = $('dnModal'); if (!m) return;
        $('dnModalTitle').textContent = title || '';
        $('dnModalBody').innerHTML = bodyHtml || '';
        var f = $('dnModalFoot'); f.innerHTML = '';
        (buttons || []).forEach(function (b) {
            var btn = d.createElement('button'); btn.type = 'button';
            btn.className = 'dn-btn ' + (b.cls || 'dn-btn-ghost'); btn.textContent = b.text;
            btn.onclick = function () { if (b.onClick) b.onClick(btn); else closeModal(); };
            f.appendChild(btn);
        });
        m.classList.add('show');
    }
    function closeModal() { var m = $('dnModal'); if (m) m.classList.remove('show'); }
    function busy(btn, on) { if (!btn) return; btn.disabled = !!on; btn.classList.toggle('busy', !!on); }

    function askReason(title, hint, required, minLen, onOk, okText) {
        var html = '<p class="dn-hint">' + esc(hint || '') + '</p>' +
            '<textarea id="dnReason" class="dn-input" rows="3" placeholder="' + esc(required ? 'ระบุเหตุผล' : 'เหตุผล (ไม่บังคับ)') + '"></textarea>' +
            '<div class="dn-err" id="dnReasonErr"></div>';
        openModal(title, html, [
            { text: 'ยกเลิก' },
            {
                text: okText || 'ยืนยัน', cls: 'dn-btn-primary', onClick: function (btn) {
                    var v = ($('dnReason').value || '').trim();
                    if (required && v.length < (minLen || 1)) { $('dnReasonErr').textContent = 'กรุณาระบุเหตุผลอย่างน้อย ' + (minLen || 1) + ' ตัวอักษร'; return; }
                    busy(btn, true); onOk(v, btn);
                }
            }
        ]);
        setTimeout(function () { var r = $('dnReason'); if (r) r.focus(); }, 50);
    }

    // ── ป้าย ─────────────────────────────────────────────────────────────────
    function payBadge(o) {
        var map = { UNPAID: ['💵', 'pay-unpaid'], PENDING_SLIP: ['⏳', 'pay-pending'], PAID: ['✅', 'pay-paid'], CHARGED: ['🏨', 'pay-charged'], REFUNDED: ['↩', 'pay-refunded'] };
        var m = map[o.payState] || ['', 'pay-unpaid'];
        return '<span class="dn-pay ' + m[1] + '">' + m[0] + ' ' + esc(o.payStateTh) + '</span>';
    }
    function statusBadge(o) { return '<span class="dn-st st-' + esc(String(o.status).toLowerCase()) + '">' + esc(o.statusTh) + '</span>'; }
    function ageClass(min) { min = Number(min || 0); return min >= 20 ? 'age-late' : (min >= 10 ? 'age-warn' : ''); }

    // ── การ์ดออเดอร์ ─────────────────────────────────────────────────────────
    function itemsHtml(items, forTicket) {
        var h = '<ul class="dn-items' + (forTicket ? ' tk-items' : '') + '">';
        (items || []).forEach(function (it) {
            var voided = it.kitchen === 'VOID';
            h += '<li class="' + (voided ? 'void' : '') + '"><span class="qty">' + esc(it.qtyText) + '×</span> <span class="nm">' + esc(it.name) + '</span>';
            if (!forTicket) h += '<span class="amt">' + esc(it.lineTotalText) + '</span>';
            if (it.options) h += '<div class="opt">' + esc(it.options) + '</div>';
            if (it.note) h += '<div class="note">✎ ' + esc(it.note) + '</div>';
            h += '</li>';
        });
        return h + '</ul>';
    }
    function tableActionsHtml(t, o) {
        if (!t) return '';
        var h = '';
        if (t.needsStaff) h += '<button type="button" class="dn-btn dn-btn-call" data-act="ack" data-table="' + t.tableId + '">🙋 รับทราบการเรียก</button>';
        if (t.billRequested) h += '<span class="dn-flag flag-bill">🧾 ลูกค้าขอบิล</span>';
        if (t.sessionId) {
            if (Number(t.payable) > 0) h += '<button type="button" class="dn-btn dn-btn-cash" data-act="cash" data-session="' + t.sessionId + '">💵 รับเงินสด ' + esc(money(t.payable)) + '</button>';
            if (Number(t.pendingSlip) > 0) h += '<button type="button" class="dn-btn dn-btn-slip" data-act="slips" data-session="' + t.sessionId + '">⏳ ตรวจสลิป ' + esc(money(t.pendingSlip)) + '</button>';
            h += '<button type="button" class="dn-btn dn-btn-ghost" data-act="detail" data-session="' + t.sessionId + '">📋 ทั้งโต๊ะ</button>';
            h += '<button type="button" class="dn-btn dn-btn-close" data-act="close" data-session="' + t.sessionId + '" data-out="' + esc(t.outstanding) + '">🔒 ปิดโต๊ะ</button>';
        }
        return h;
    }
    function orderCard(o) {
        var t = tableOf(o);
        var cls = 'dn-card st-' + String(o.status).toLowerCase() + (o.id === B.focusOrderId ? ' focus' : '') + (t && (t.needsStaff || t.billRequested) ? ' attn' : '');
        var h = '<article class="' + cls + '" id="dnOrder' + o.id + '" data-id="' + o.id + '">';
        h += '<header class="dn-card-head"><div class="tbl"><span class="tname">' + esc(o.tableName) + '</span><span class="zone">' + esc(o.zoneName) + '</span>' +
            '<span class="round">รอบ ' + esc(o.round) + '</span>' + (o.source === 'STAFF' ? '<span class="src">พนักงานคีย์</span>' : '') + '</div>' +
            '<div class="meta"><span class="no">#' + esc(o.orderNo) + '</span><span class="time ' + ageClass(o.ageMin) + '" title="' + esc(o.createdDate) + '">' + esc(o.createdHm) + ' · ' + esc(o.ageText) + '</span></div></header>';
        h += '<div class="dn-badges">' + statusBadge(o) + payBadge(o) + (t && t.needsStaff ? '<span class="dn-flag flag-call">🙋 เรียกพนักงาน</span>' : '') + (t && t.billRequested ? '<span class="dn-flag flag-bill">🧾 ขอบิล</span>' : '') + '</div>';
        h += itemsHtml(o.items, false);
        if (o.guestNote) h += '<div class="dn-gnote">📝 ' + esc(o.guestNote) + '</div>';
        if (o.guestName || o.guestPhone) h += '<div class="dn-guest">👤 ' + esc(o.guestName) + (o.guestPhone ? ' · ' + esc(o.guestPhone) : '') + '</div>';
        if (o.status === 'CANCELLED' && o.cancelReason) h += '<div class="dn-gnote cancel">ยกเลิก: ' + esc(o.cancelReason) + '</div>';
        h += '<div class="dn-total"><span>รวม</span><b>฿' + esc(o.totalText) + '</b></div>';
        h += '<div class="dn-actions">';
        if (o.canAccept) h += '<button type="button" class="dn-btn dn-btn-accept" data-act="status" data-id="' + o.id + '" data-to="ACCEPTED">✔ รับออเดอร์</button>';
        if (o.canPrepare) h += '<button type="button" class="dn-btn dn-btn-prep" data-act="status" data-id="' + o.id + '" data-to="PREPARING">🍳 กำลังทำ</button>';
        if (o.canServe) h += '<button type="button" class="dn-btn dn-btn-serve" data-act="status" data-id="' + o.id + '" data-to="SERVED">🍽 เสิร์ฟแล้ว</button>';
        if (o.canCancel) h += '<button type="button" class="dn-btn dn-btn-cancel" data-act="cancel" data-id="' + o.id + '" data-paid="' + (o.payState === 'PAID' ? '1' : '0') + '">✖ ยกเลิก</button>';
        h += '<button type="button" class="dn-btn dn-btn-ghost" data-act="print" data-id="' + o.id + '">🖨 ครัว</button>';
        h += '</div>';
        var ta = tableActionsHtml(t, o);
        if (ta) h += '<div class="dn-actions dn-table-actions">' + ta + '</div>';
        return h + '</article>';
    }

    // ── สลิปรอตรวจ ──────────────────────────────────────────────────────────
    function slipRow(p) {
        var h = '<div class="dn-slip" data-pid="' + p.id + '">';
        h += '<a class="thumb" href="' + esc(slipUrl(p.slipPath)) + '" target="_blank" rel="noopener">' + (p.slipPath ? '<img src="' + esc(slipUrl(p.slipPath)) + '" alt="สลิป" loading="lazy">' : '<span>ไม่มีรูป</span>') + '</a>';
        h += '<div class="info"><div class="t">' + esc(p.tableLabel || ('session #' + p.sessionId)) + '</div>' +
            '<div class="a">฿' + esc(p.amountText) + ' <small>' + esc(p.createdHm) + ' · ' + esc(p.ageText) + '</small></div>' +
            (p.slipBankAmount !== null && p.slipBankAmount !== undefined ? '<div class="s">ยอดจากสลิป ฿' + esc(money(p.slipBankAmount)) + '</div>' : '') +
            (p.slipNote ? '<div class="n">' + esc(p.slipNote) + '</div>' : '') + '</div>';
        h += '<div class="act"><button type="button" class="dn-btn dn-btn-accept" data-act="approve" data-pid="' + p.id + '">✅ ยืนยันรับเงิน</button>' +
            '<button type="button" class="dn-btn dn-btn-cancel" data-act="reject" data-pid="' + p.id + '">✖ ปฏิเสธ</button></div>';
        return h + '</div>';
    }

    // ── กระดาน: ตัวกรอง + วาด ───────────────────────────────────────────────
    var STATUS_CHIPS = [['ACTIVE', 'ยังไม่เสิร์ฟ'], ['NEW', 'ใหม่'], ['ACCEPTED', 'รับแล้ว'], ['PREPARING', 'กำลังทำ'], ['SERVED', 'เสิร์ฟแล้ว'], ['CANCELLED', 'ยกเลิก'], ['ALL', 'ทั้งหมด']];
    function renderFilter() {
        var f = S.filter, h = '';
        h += '<select id="dnFZone" class="dn-select"><option value="0">ทุกโซน</option>';
        (B.zones || []).forEach(function (z) { h += '<option value="' + z.id + '"' + (Number(f.zoneId) === z.id ? ' selected' : '') + '>' + esc(z.name) + (z.open ? '' : ' (ปิด)') + '</option>'; });
        h += '</select><select id="dnFTable" class="dn-select"><option value="0">ทุกโต๊ะ</option>';
        (B.tables || []).forEach(function (t) {
            if (Number(f.zoneId) > 0 && t.zoneId !== Number(f.zoneId)) return;
            h += '<option value="' + t.id + '"' + (Number(f.tableId) === t.id ? ' selected' : '') + '>' + esc(t.name) + '</option>';
        });
        h += '</select><div class="dn-chips">';
        STATUS_CHIPS.forEach(function (c) { h += '<button type="button" class="dn-chip' + (f.status === c[0] ? ' on' : '') + '" data-status="' + c[0] + '">' + esc(c[1]) + '</button>'; });
        h += '</div><div class="dn-chips dn-chips-flags">' +
            '<button type="button" class="dn-chip flag' + (f.unpaid ? ' on' : '') + '" data-flag="unpaid">💵 ค้างจ่าย</button>' +
            '<button type="button" class="dn-chip flag' + (f.pendingSlip ? ' on' : '') + '" data-flag="pendingSlip">⏳ สลิปรอตรวจ</button>' +
            '<button type="button" class="dn-chip flag' + (f.attention ? ' on' : '') + '" data-flag="attention">🧾 ขอบิล/เรียกพนักงาน</button></div>';
        $('dnFilter').innerHTML = h;
    }
    function renderCounts() {
        var c = S.counts || {}, el = $('dnCounts'); if (!el) return;
        el.innerHTML = '<span class="cnt c-new' + (c.newCount ? ' on' : '') + '">🆕 ใหม่ ' + esc(c.newCount || 0) + '</span>' +
            '<span class="cnt c-slip' + (c.pendingSlipCount ? ' on' : '') + '">⏳ สลิป ' + esc(c.pendingSlipCount || 0) + '</span>' +
            '<span class="cnt c-call' + (c.callStaffCount ? ' on' : '') + '">🙋 เรียก ' + esc(c.callStaffCount || 0) + '</span>' +
            '<span class="cnt c-bill' + (c.billRequestedCount ? ' on' : '') + '">🧾 ขอบิล ' + esc(c.billRequestedCount || 0) + '</span>';
    }
    function renderBoard() {
        renderCounts();
        var sp = $('dnSlips');
        if (sp) {
            if (S.slips.length) { sp.style.display = ''; sp.innerHTML = '<h3>⏳ สลิปรอตรวจ (' + S.slips.length + ')</h3>' + S.slips.map(slipRow).join(''); }
            else { sp.style.display = 'none'; sp.innerHTML = ''; }
        }
        var grid = $('dnGrid');
        var list = S.orders;
        if (S.filter.pendingSlip) list = list.filter(function (o) { return o.payState === 'PENDING_SLIP'; });
        if (!list.length) {
            grid.innerHTML = '<div class="dn-empty">' + (B.ready === false ? 'ยังไม่ได้ติดตั้งตาราง (รัน migration PHASE19_42)' : 'ไม่มีออเดอร์ตามตัวกรองนี้') + '</div>';
        } else grid.innerHTML = list.map(orderCard).join('');
        var st = $('dnStamp'); if (st) st.textContent = 'อัปเดต ' + new Date().toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        if (B.focusOrderId && !S.focusDone) {
            var fe = $('dnOrder' + B.focusOrderId);
            if (fe) { S.focusDone = true; setTimeout(function () { fe.scrollIntoView({ behavior: 'smooth', block: 'center' }); }, 100); }
        }
    }
    function loadBoard() {
        if (S.loading) return Promise.resolve();
        S.loading = true;
        var f = S.filter;
        return api('LoadBoard', { zoneId: Number(f.zoneId) || 0, tableId: Number(f.tableId) || 0, status: f.status || 'ACTIVE', unpaid: !!f.unpaid, pendingSlip: !!f.pendingSlip, attention: !!f.attention, focusOrderId: Number(B.focusOrderId) || 0 })
            .then(function (res) {
                S.loading = false;
                if (!res.ok) { fail(res); return; }
                S.orders = res.orders || []; S.tables = res.tables || {}; S.slips = res.pendingSlips || []; S.counts = res.counts || {};
                if (res.ready === false) B.ready = false;
                renderBoard();
            }).catch(function (e) { S.loading = false; toast('โหลดกระดานไม่ได้: ' + e.message, 'err'); });
    }
    function setFilter(patch, pushUrl) {
        for (var k in patch) if (patch.hasOwnProperty(k)) S.filter[k] = patch[k];
        if (patch.zoneId !== undefined) S.filter.tableId = 0;
        renderFilter(); loadBoard();
        if (pushUrl && w.history && w.history.replaceState) {
            var f = S.filter, q = [];
            if (Number(f.zoneId)) q.push('zone=' + Number(f.zoneId));
            if (Number(f.tableId)) q.push('table=' + Number(f.tableId));
            if (f.status && f.status !== 'ACTIVE') q.push('status=' + encodeURIComponent(f.status));
            if (f.unpaid) q.push('unpaid=1'); if (f.pendingSlip) q.push('slip=1'); if (f.attention) q.push('attn=1');
            try { w.history.replaceState(null, '', w.location.pathname + (q.length ? '?' + q.join('&') : '')); } catch (e) { }
        }
    }

    // ── poll (ออเดอร์ใหม่ / เรียกพนักงาน / ขอบิล) ────────────────────────────
    function showNewOrderAlert(orders) {
        var h = '<p class="dn-hint">มีออเดอร์ใหม่ ' + orders.length + ' รายการ</p>';
        orders.forEach(function (o) {
            h += '<div class="dn-alert-order"><div class="t">' + esc(o.tableName) + ' <small>' + esc(o.zoneName) + ' · รอบ ' + esc(o.round) + ' · ' + esc(o.createdHm) + '</small></div>' +
                '<div class="p">' + esc(o.itemsPreview) + '</div><div class="a">฿' + esc(o.totalText) + ' ' + payBadge(o) + '</div>' +
                '<button type="button" class="dn-btn dn-btn-accept" data-act="status" data-id="' + o.id + '" data-to="ACCEPTED" data-close="1">✔ รับออเดอร์</button></div>';
        });
        openModal('🍽️ ออเดอร์ใหม่!', h, [{ text: 'ปิด (ดูบนกระดาน)', onClick: function () { closeModal(); } }]);
        S.alertOpen = true;
        clearInterval(S.alertTimer);
        S.alertTimer = setInterval(function () {
            var m = $('dnModal');
            if (!m || !m.classList.contains('show')) { clearInterval(S.alertTimer); S.alertOpen = false; return; }
            beep('order');
        }, 6000);
    }
    function banner(kind, text) {
        var b = $('dnBanner'); if (!b) return;
        b.className = 'dn-banner show ' + kind; b.textContent = text;
        clearTimeout(b._tm); b._tm = setTimeout(function () { b.className = 'dn-banner'; }, 8000);
    }
    function poll() {
        api('Poll', { sinceTicks: S.lastTicks || 0 }).then(function (res) {
            if (!res.ok) return;
            S.lastTicks = res.latestTicks || S.lastTicks;
            var fresh = [];
            (res.newOrders || []).forEach(function (o) { if (!S.seenNew[o.id]) { S.seenNew[o.id] = 1; fresh.push(o); } });
            var changed = false;
            if (fresh.length && S.pollN > 0) { beep('order'); showNewOrderAlert(fresh); changed = true; }
            if (S.prevCall >= 0 && res.callStaffCount > S.prevCall) { beep('call'); banner('call', '🙋 ลูกค้าเรียกพนักงาน (' + res.callStaffCount + ' โต๊ะ)'); changed = true; }
            if (S.prevBill >= 0 && res.billRequestedCount > S.prevBill) { beep('bill'); banner('bill', '🧾 ลูกค้าขอบิล (' + res.billRequestedCount + ' โต๊ะ)'); changed = true; }
            var c = S.counts || {};
            if (c.newCount !== res.newCount || c.pendingSlipCount !== res.pendingSlipCount || c.callStaffCount !== res.callStaffCount || c.billRequestedCount !== res.billRequestedCount) changed = true;
            S.prevCall = res.callStaffCount; S.prevBill = res.billRequestedCount;
            S.counts.newCount = res.newCount; S.counts.pendingSlipCount = res.pendingSlipCount; S.counts.callStaffCount = res.callStaffCount; S.counts.billRequestedCount = res.billRequestedCount;
            renderCounts();
            S.pollN++;
            if (changed || S.pollN % 5 === 0) { if (B.mode === 'tables') loadTiles(); else loadBoard(); }
        }).catch(function () { });
    }
    function startPoll() {
        clearInterval(S.pollTimer);
        S.pollTimer = setInterval(poll, Math.max(2, Number(B.pollSec) || 3) * 1000);
        poll();
    }

    // ── actions ──────────────────────────────────────────────────────────────
    function afterChange() { if (B.mode === 'tables') { loadTiles(); if (S.panelSession) openPanel(S.panelSession); } else loadBoard(); }
    function doStatus(id, to, reason, btn, closeAfter) {
        busy(btn, true);
        api('SetStatus', { orderId: Number(id), status: to, reason: reason || '' }).then(function (res) {
            busy(btn, false);
            if (!res.ok) { fail(res); if (res.code === 'ACCOUNTING_POSTED') openModal('ลงบัญชีแล้ว', '<p>' + esc(res.message) + '</p>', [{ text: 'ปิด' }, { text: 'ไปหน้าใบลดหนี้', cls: 'dn-btn-primary', onClick: function () { w.location.href = B.creditNotesUrl; } }]); return; }
            toast(res.message || 'บันทึกแล้ว', 'ok');
            if (closeAfter) closeModal();
            afterChange();
        }).catch(function (e) { busy(btn, false); toast(e.message, 'err'); });
    }
    function doCancel(id, paid, btn) {
        askReason('ยกเลิกออเดอร์', paid ? 'ออเดอร์นี้ชำระแล้ว — ยกเลิกได้เฉพาะ Owner/Admin และต้องระบุเหตุผล (เงินคืนลูกค้าทำนอกระบบ)' : 'ระบุเหตุผล (ไม่บังคับ) — ครัวจะหยุดทำรายการนี้', !!paid, 5,
            function (v, b) { doStatus(id, 'CANCELLED', v, b, true); }, 'ยกเลิกออเดอร์');
    }
    function doCash(sessionId, payable) {
        var chans = B.cashChannels || [];
        var h = '<p class="dn-hint">เก็บเงินสดที่โต๊ะแล้วกดยืนยัน — ยอดค้างที่จ่ายได้ทั้งหมดของโต๊ะ</p>' +
            '<div class="dn-big">฿' + esc(money(payable)) + '</div>';
        if (chans.length) {
            h += '<label class="dn-label">แหล่งเงิน (เงินสด)</label><select id="dnCashHow" class="dn-select wide">';
            chans.forEach(function (c, i) { h += '<option value="' + c.id + '"' + (i === 0 ? ' selected' : '') + '>' + esc(c.name) + '</option>'; });
            h += '</select>';
        } else h += '<p class="dn-hint">ใช้แหล่งเงิน "เงินสด" แถวแรกของระบบ</p>';
        openModal('💵 รับเงินสด', h, [{ text: 'ยกเลิก' }, {
            text: 'รับเงินสดแล้ว', cls: 'dn-btn-primary', onClick: function (btn) {
                busy(btn, true);
                var how = $('dnCashHow') ? Number($('dnCashHow').value) : 0;
                api('RecordCash', { sessionId: Number(sessionId), paidHowId: how }).then(function (res) {
                    busy(btn, false);
                    if (!res.ok) { fail(res); return; }
                    closeModal(); toast(res.message, 'ok'); afterChange();
                }).catch(function (e) { busy(btn, false); toast(e.message, 'err'); });
            }
        }]);
    }
    function doApprove(pid, btn) {
        function send(reason, b) {
            busy(b, true);
            api('ApproveSlip', { paymentId: Number(pid), reason: reason || '' }).then(function (res) {
                busy(b, false);
                if (!res.ok) {
                    if ((res.code === 'AMOUNT_MISMATCH') && res.canOverride) {
                        askReason('ตรวจสลิปไม่ผ่าน', res.message + '\nตรวจกับบัญชีธนาคารแล้ว ระบุเหตุผลเพื่อยืนยันรับเงิน (หรือกดยืนยันซ้ำภายใน 10 นาที)', true, 5, function (v, bb) { send(v, bb); }, 'ยืนยันรับเงิน');
                        return;
                    }
                    openModal('ยืนยันสลิปไม่ได้', '<p class="dn-pre">' + esc(res.message) + '</p>', [{ text: 'ปิด' }]);
                    return;
                }
                closeModal(); toast(res.message, 'ok'); afterChange();
            }).catch(function (e) { busy(b, false); toast(e.message, 'err'); });
        }
        send('', btn);
    }
    function doReject(pid) {
        askReason('ปฏิเสธสลิป', 'ออเดอร์จะกลับเป็นค้างชำระ ลูกค้าส่งสลิปใหม่หรือจ่ายเงินสดได้', true, 3, function (v, b) {
            api('RejectSlip', { paymentId: Number(pid), reason: v }).then(function (res) {
                busy(b, false); if (!res.ok) { fail(res); return; }
                closeModal(); toast(res.message, 'ok'); afterChange();
            }).catch(function (e) { busy(b, false); toast(e.message, 'err'); });
        }, 'ปฏิเสธสลิป');
    }
    function doClose(sessionId, outstanding) {
        var out = Number(outstanding || 0);
        var h = out > 0
            ? '<p class="dn-hint warn">โต๊ะนี้ยังค้างชำระ ฿' + esc(money(out)) + ' — รับเงินก่อน' + (B.canOverride ? ' หรือปิดแบบบังคับ (ออเดอร์ที่ยังไม่เสิร์ฟจะถูกยกเลิก) โดยระบุเหตุผล' : ' (Owner/Admin เท่านั้นที่ปิดแบบบังคับได้)') + '</p>'
            : '<p class="dn-hint">ปิด session ของโต๊ะ — ลิงก์บนมือถือลูกค้าจะใช้ไม่ได้ โต๊ะกลับเป็นว่าง</p>';
        h += '<textarea id="dnReason" class="dn-input" rows="2" placeholder="' + (out > 0 ? 'เหตุผล (บังคับ อย่างน้อย 5 ตัว)' : 'หมายเหตุ (ไม่บังคับ)') + '"></textarea><div class="dn-err" id="dnReasonErr"></div>';
        var btns = [{ text: 'ยกเลิก' }];
        if (out <= 0 || B.canOverride) btns.push({
            text: out > 0 ? 'ปิดโต๊ะแบบบังคับ' : 'ปิดโต๊ะ', cls: out > 0 ? 'dn-btn-cancel' : 'dn-btn-primary', onClick: function (btn) {
                var v = ($('dnReason').value || '').trim();
                if (out > 0 && v.length < 5) { $('dnReasonErr').textContent = 'กรุณาระบุเหตุผลอย่างน้อย 5 ตัวอักษร'; return; }
                busy(btn, true);
                api('CloseTable', { sessionId: Number(sessionId), reason: v, force: out > 0 }).then(function (res) {
                    busy(btn, false); if (!res.ok) { fail(res); return; }
                    closeModal(); toast(res.message, 'ok'); S.panelSession = 0; closePanel(); afterChange();
                }).catch(function (e) { busy(btn, false); toast(e.message, 'err'); });
            }
        });
        openModal('🔒 ปิดโต๊ะ', h, btns);
    }
    function doAck(tableId, btn) {
        busy(btn, true);
        api('AckCall', { tableId: Number(tableId) }).then(function (res) { busy(btn, false); if (!res.ok) { fail(res); return; } toast(res.message, 'ok'); afterChange(); })
            .catch(function (e) { busy(btn, false); toast(e.message, 'err'); });
    }
    function doOpenForGuest(tableId, btn) {
        busy(btn, true);
        api('OpenForGuest', { tableId: Number(tableId) }).then(function (res) {
            busy(btn, false); if (!res.ok) { fail(res); return; }
            var h = '<p class="dn-hint">' + esc(res.message) + '</p><p>เปิดหน้าสั่งอาหารของโต๊ะนี้บนเครื่องของคุณเพื่อคีย์ออเดอร์แทนลูกค้า (session เดียวกับ QR ที่โต๊ะ)</p>' +
                '<div class="dn-link"><a href="' + esc(res.orderUrl) + '" target="_blank" rel="noopener">' + esc(res.orderUrl) + '</a></div>';
            openModal('🧑‍🍳 เปิดโต๊ะแทนลูกค้า', h, [{ text: 'ปิด' }, { text: 'เปิดหน้าสั่งอาหาร', cls: 'dn-btn-primary', onClick: function () { w.open(res.orderUrl, '_blank'); closeModal(); } }]);
            afterChange();
        }).catch(function (e) { busy(btn, false); toast(e.message, 'err'); });
    }
    function showSlipsOfSession(sessionId) {
        var list = S.slips.filter(function (p) { return Number(p.sessionId) === Number(sessionId); });
        if (!list.length) { toast('ไม่มีสลิปรอตรวจของโต๊ะนี้ (รีเฟรช)', ''); loadBoard(); return; }
        openModal('⏳ สลิปรอตรวจ', list.map(slipRow).join(''), [{ text: 'ปิด' }]);
    }

    // ── kitchen ticket ───────────────────────────────────────────────────────
    function findOrder(id) {
        var o = null;
        S.orders.forEach(function (x) { if (x.id === Number(id)) o = x; });
        if (!o && S.panelOrders) S.panelOrders.forEach(function (x) { if (x.id === Number(id)) o = x; });
        return o;
    }
    function printTicket(id) {
        var o = findOrder(id);
        if (!o) { toast('ไม่พบออเดอร์ในหน้านี้', 'err'); return; }
        var tk = $('dnTicket'); if (!tk) return;
        var h = '<div class="tk-head"><div class="tk-title">ครัว · ' + esc(o.tableName) + '</div><div class="tk-sub">' + esc(o.zoneName) + ' · รอบ ' + esc(o.round) + '</div>' +
            '<div class="tk-sub">#' + esc(o.orderNo) + ' · ' + esc(o.createdDate) + ' ' + esc(o.createdHm) + '</div></div>';
        h += itemsHtml(o.items, true);
        if (o.guestNote) h += '<div class="tk-note">หมายเหตุ: ' + esc(o.guestNote) + '</div>';
        h += '<div class="tk-foot">พิมพ์ ' + esc(new Date().toLocaleString('th-TH')) + (B.staffName ? ' · ' + esc(B.staffName) : '') + '</div>';
        tk.innerHTML = h;
        d.body.classList.add('dn-printing');
        setTimeout(function () { w.print(); setTimeout(function () { d.body.classList.remove('dn-printing'); }, 500); }, 50);
    }

    // ── แผนผังโต๊ะ (mode=tables) ─────────────────────────────────────────────
    function tileClass(t) {
        var c = 'dn-tile tst-' + String(t.status || 'FREE').toLowerCase();
        if (!t.isActive) c = 'dn-tile tst-disabled';
        return c;
    }
    function tileHtml(t) {
        var h = '<button type="button" class="' + tileClass(t) + '" data-act="tile" data-table="' + t.tableId + '" data-session="' + (t.sessionId || 0) + '">';
        h += '<div class="tt-name">' + esc(t.name) + '</div><div class="tt-st">' + esc(t.statusTh) + '</div>';
        if (t.sessionId) {
            h += '<div class="tt-info">' + (t.openOrders ? '<span>🍳 ' + esc(t.openOrders) + '</span>' : '') + (t.ageText ? '<span>⏱ ' + esc(t.ageText) + '</span>' : '') + '</div>';
            if (Number(t.outstanding) > 0) h += '<div class="tt-out">ค้าง ฿' + esc(t.outstandingText) + (Number(t.pendingSlip) > 0 ? ' <small>(สลิป ' + esc(t.pendingSlipText) + ')</small>' : '') + '</div>';
            else h += '<div class="tt-out ok">จ่ายครบ</div>';
        } else h += '<div class="tt-info"><span>' + esc(t.seats) + ' ที่นั่ง</span></div>';
        return h + '</button>';
    }
    function renderTiles(rows) {
        var zones = {}, order = [];
        rows.forEach(function (t) { if (!zones[t.zoneId]) { zones[t.zoneId] = { name: t.zoneName, rows: [] }; order.push(t.zoneId); } zones[t.zoneId].rows.push(t); });
        var h = '';
        if (!order.length) h = '<div class="dn-empty">' + (B.ready === false ? 'ยังไม่ได้ติดตั้งตาราง (รัน migration PHASE19_42)' : 'ยังไม่มีโต๊ะ — ตั้งค่าที่ โซน & โต๊ะ') + '</div>';
        order.forEach(function (zid) {
            var z = zones[zid], occ = 0, out = 0;
            z.rows.forEach(function (t) { if (t.sessionId) occ++; out += Number(t.outstanding || 0); });
            h += '<section class="dn-zone"><h3>' + esc(z.name) + ' <small>' + occ + '/' + z.rows.length + ' โต๊ะมีลูกค้า' + (out > 0 ? ' · ค้าง ฿' + esc(money(out)) : '') + '</small></h3><div class="dn-tiles">' + z.rows.map(tileHtml).join('') + '</div></section>';
        });
        $('dnTiles').innerHTML = h;
        var st = $('dnStamp'); if (st) st.textContent = 'อัปเดต ' + new Date().toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        var sel = $('dnFZone');
        if (sel) { sel.innerHTML = '<option value="0">ทุกโซน</option>' + (B.zones || []).map(function (z) { return '<option value="' + z.id + '"' + (S.tilesZone === z.id ? ' selected' : '') + '>' + esc(z.name) + '</option>'; }).join(''); }
    }
    function loadTiles() {
        return api('LoadTables', { zoneId: S.tilesZone || 0 }).then(function (res) {
            if (!res.ok) { fail(res); return; }
            S.tileRows = res.tables || []; S.counts = res.counts || S.counts; if (res.ready === false) B.ready = false;
            S.tables = {}; S.tileRows.forEach(function (t) { S.tables[t.tableId] = t; });
            renderTiles(S.tileRows); renderCounts();
        }).catch(function (e) { toast('โหลดแผนผังไม่ได้: ' + e.message, 'err'); });
    }
    function openPanel(sessionId) {
        var p = $('dnPanel'); if (!p) return;
        S.panelSession = Number(sessionId);
        p.classList.add('show'); d.body.classList.add('dn-panel-open');
        $('dnPanelBody').innerHTML = '<div class="dn-empty">กำลังโหลด…</div>';
        api('SessionDetail', { sessionId: Number(sessionId) }).then(function (res) {
            if (!res.ok) { $('dnPanelBody').innerHTML = '<div class="dn-empty">' + esc(res.message) + '</div>'; return; }
            S.panelOrders = res.orders || [];
            var s = res.session, o = res.outstanding;
            var h = '<div class="dn-panel-head"><div class="big">' + esc(s.tableName) + '</div><div class="sub">' + esc(s.zoneName) + ' · เปิด ' + esc(s.openedHm) + ' · ' + esc(res.tableStatusTh) + (s.guestName ? ' · 👤 ' + esc(s.guestName) : '') + (s.guestPhone ? ' ' + esc(s.guestPhone) : '') + '</div></div>';
            h += '<div class="dn-panel-sum"><div><small>สั่งแล้ว</small><b>' + S.panelOrders.filter(function (x) { return x.status !== 'CANCELLED'; }).length + ' รอบ</b></div><div><small>ค้างชำระ</small><b class="' + (Number(o.amount) > 0 ? 'red' : 'green') + '">฿' + esc(o.amountText) + '</b></div><div><small>สลิปรอตรวจ</small><b>฿' + esc(o.pendingSlipText) + '</b></div></div>';
            h += '<div class="dn-actions dn-panel-actions">';
            if (res.tableStatus === 'NEEDS_STAFF') h += '<button type="button" class="dn-btn dn-btn-call" data-act="ack" data-table="' + s.tableId + '">🙋 รับทราบการเรียก</button>';
            if (s.billRequested) h += '<span class="dn-flag flag-bill">🧾 ลูกค้าขอบิล ' + esc(s.billRequestedHm) + '</span>';
            if (s.isOpen && Number(o.payable) > 0) h += '<button type="button" class="dn-btn dn-btn-cash" data-act="cash" data-session="' + s.id + '" data-payable="' + esc(o.payable) + '">💵 รับเงินสด ฿' + esc(o.payableText) + '</button>';
            if (s.isOpen && Number(o.pendingSlip) > 0) h += '<button type="button" class="dn-btn dn-btn-slip" data-act="panelslips">⏳ ตรวจสลิป</button>';
            if (s.isOpen) h += '<a class="dn-btn dn-btn-ghost" href="' + esc(res.orderUrl) + '" target="_blank" rel="noopener">🧑‍🍳 คีย์ออเดอร์แทนลูกค้า</a>';
            if (s.isOpen) h += '<a class="dn-btn dn-btn-ghost" href="' + esc(B.ordersUrl + '?table=' + s.tableId + '&status=ALL') + '">📋 ดูบนกระดาน</a>';
            if (s.isOpen) h += '<button type="button" class="dn-btn dn-btn-close" data-act="close" data-session="' + s.id + '" data-out="' + esc(o.amount) + '">🔒 ปิดโต๊ะ</button>';
            h += '</div>';
            h += '<h4>ออเดอร์</h4>';
            if (!S.panelOrders.length) h += '<div class="dn-empty">ยังไม่มีออเดอร์</div>';
            S.panelOrders.forEach(function (x) { S.tables[x.tableId] = S.tables[x.tableId] || null; h += orderCard(x); });
            h += '<h4>การชำระ</h4>';
            var pays = res.payments || [];
            if (!pays.length) h += '<div class="dn-empty">ยังไม่มีการชำระ</div>';
            pays.forEach(function (p) {
                if (p.isPending) { h += slipRow(p); return; }
                h += '<div class="dn-payrow st-' + esc(String(p.status).toLowerCase()) + '"><span>' + esc(p.methodTh) + (p.paidHowName ? ' · ' + esc(p.paidHowName) : '') + (p.roomChargeRef ? ' · ' + esc(p.roomChargeRef) : '') + '</span><span>' + esc(p.paidHm || p.createdHm) + '</span><b>฿' + esc(p.amountText) + '</b><small>' + esc(p.status) + '</small></div>';
            });
            $('dnPanelBody').innerHTML = h;
        }).catch(function (e) { $('dnPanelBody').innerHTML = '<div class="dn-empty">' + esc(e.message) + '</div>'; });
    }
    function closePanel() { var p = $('dnPanel'); if (p) p.classList.remove('show'); d.body.classList.remove('dn-panel-open'); S.panelSession = 0; }
    function tileClick(tableId, sessionId) {
        if (Number(sessionId) > 0) { openPanel(sessionId); return; }
        var t = S.tables[tableId];
        var h = '<p class="dn-hint">โต๊ะ ' + esc(t ? t.name : tableId) + ' ว่าง — เปิด session ให้ลูกค้าแล้วคีย์ออเดอร์แทนจากเครื่องของคุณ</p>';
        openModal('🪑 โต๊ะว่าง', h, [{ text: 'ปิด' }, { text: 'เปิดโต๊ะแทนลูกค้า', cls: 'dn-btn-primary', onClick: function (btn) { doOpenForGuest(tableId, btn); } }]);
    }

    // ── event delegation ─────────────────────────────────────────────────────
    function onClick(e) {
        var el = e.target.closest ? e.target.closest('[data-act],[data-status],[data-flag]') : null;
        if (!el) return;
        unlockAudio();
        if (el.hasAttribute('data-status')) { setFilter({ status: el.getAttribute('data-status') }, true); return; }
        if (el.hasAttribute('data-flag')) { var k = el.getAttribute('data-flag'), p = {}; p[k] = !S.filter[k]; setFilter(p, true); return; }
        var act = el.getAttribute('data-act');
        switch (act) {
            case 'status': doStatus(el.getAttribute('data-id'), el.getAttribute('data-to'), '', el, el.getAttribute('data-close') === '1'); break;
            case 'cancel': doCancel(el.getAttribute('data-id'), el.getAttribute('data-paid') === '1', el); break;
            case 'print': printTicket(el.getAttribute('data-id')); break;
            case 'cash': {
                var sid = el.getAttribute('data-session'), pay = el.getAttribute('data-payable');
                if (pay === null) { var tt = null; for (var k2 in S.tables) if (S.tables[k2] && Number(S.tables[k2].sessionId) === Number(sid)) tt = S.tables[k2]; pay = tt ? tt.payable : 0; }
                doCash(sid, pay); break;
            }
            case 'slips': showSlipsOfSession(el.getAttribute('data-session')); break;
            case 'panelslips': { var ps = $('dnPanelBody'); if (ps) { var first = ps.querySelector('.dn-slip'); if (first) first.scrollIntoView({ behavior: 'smooth', block: 'center' }); } break; }
            case 'approve': doApprove(el.getAttribute('data-pid'), el); break;
            case 'reject': doReject(el.getAttribute('data-pid')); break;
            case 'close': doClose(el.getAttribute('data-session'), el.getAttribute('data-out')); break;
            case 'ack': doAck(el.getAttribute('data-table'), el); break;
            case 'detail': openPanel(el.getAttribute('data-session')); break;
            case 'tile': tileClick(Number(el.getAttribute('data-table')), Number(el.getAttribute('data-session'))); break;
            case 'openguest': doOpenForGuest(el.getAttribute('data-table'), el); break;
            case 'closepanel': closePanel(); break;
            case 'closemodal': closeModal(); break;
            case 'refresh': if (B.mode === 'tables') loadTiles(); else loadBoard(); break;
            case 'sound': S.soundOn = !S.soundOn; el.textContent = S.soundOn ? '🔔 เสียงเปิด' : '🔕 เสียงปิด'; el.classList.toggle('off', !S.soundOn); if (S.soundOn) beep('order'); break;
        }
    }
    function onChange(e) {
        var el = e.target;
        if (el.id === 'dnFZone') { if (B.mode === 'tables') { S.tilesZone = Number(el.value) || 0; loadTiles(); } else setFilter({ zoneId: Number(el.value) || 0 }, true); }
        else if (el.id === 'dnFTable') setFilter({ tableId: Number(el.value) || 0 }, true);
    }

    // ── init ─────────────────────────────────────────────────────────────────
    function init(boot) {
        B = boot || {};
        d.addEventListener('click', onClick);
        d.addEventListener('change', onChange);
        d.addEventListener('keydown', function (e) { if (e.key === 'Escape') { closeModal(); closePanel(); } });
        d.addEventListener('pointerdown', unlockAudio, { once: true });
        if (B.mode === 'tables') {
            S.tilesZone = Number((B.filter || {}).zoneId) || 0;
            loadTiles().then(startPoll);
        } else {
            S.filter = B.filter || { status: 'ACTIVE' };
            renderFilter();
            loadBoard().then(function () {
                // ออเดอร์ NEW ที่มีอยู่ตอนเปิดหน้า = เห็นแล้ว (ไม่เด้ง modal ทันที) — poll รอบถัดไปจับเฉพาะที่ใหม่จริง
                S.orders.forEach(function (o) { if (o.status === 'NEW') S.seenNew[o.id] = 1; });
                startPoll();
            });
        }
        d.addEventListener('visibilitychange', function () { if (!d.hidden) { if (B.mode === 'tables') loadTiles(); else loadBoard(); } });
    }

    w.DnStaff = { init: init, esc: esc, money: money, printTicket: printTicket, openPanel: openPanel, closePanel: closePanel, closeModal: closeModal };
})(window, document);
