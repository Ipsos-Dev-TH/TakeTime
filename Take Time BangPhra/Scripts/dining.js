/* ============================================================================
   QR สั่งอาหาร — หน้าลูกค้า (ES5, ไม่พึ่งไลบรารี)
   DN.menuPage()   Order/Default: หมวด/ค้นหา/การ์ดเมนู/ตัวเลือก/ตะกร้า/ยืนยันสั่ง → API place (idempotency key ต่อตะกร้า) → หน้าสถานะ
   DN.statusPage() Order/Status : ทุกรอบ + timeline + ยอดค้าง + การชำระ · poll · เรียกพนักงาน · เช็คบิล · เงินสด/โอน+สลิป/ลงบิลห้อง
   ยอดเงินที่หน้าคำนวณ = แสดงผลเท่านั้น — เซิร์ฟเวอร์คิดใหม่เสมอ (quote/place) · ทุกข้อความลง DOM ผ่าน textContent/esc()
   ข้อมูลเริ่มต้นอยู่ใน <script id="dnBoot" type="application/json"> (nonce, api, session, menu/summary …)
   ============================================================================ */
(function (w, d) {
    'use strict';
    if (w.DN) return;

    // ── utils ──
    function $(id) { return d.getElementById(id); }
    function esc(s) {
        return String(s === undefined || s === null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }
    function r2(n) { return Math.round((+n || 0) * 100) / 100; }
    function fmt(n) {
        var p = Math.abs(r2(n)).toFixed(2).split('.');
        return (n < 0 ? '-' : '') + p[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',') + '.' + p[1];
    }
    function money(n) { return '฿' + fmt(n); }
    function make(tag, cls, text) {
        var e = d.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined && text !== null) e.textContent = text;
        return e;
    }
    function boot() {
        try { return JSON.parse($('dnBoot').textContent); } catch (e) { return null; }
    }
    function store(k, v) { try { if (v === null) w.sessionStorage.removeItem(k); else w.sessionStorage.setItem(k, JSON.stringify(v)); } catch (e) { } }
    function load(k) { try { var v = w.sessionStorage.getItem(k); return v ? JSON.parse(v) : null; } catch (e) { return null; } }
    function uuid() {
        var s = '', c = 'abcdefghijklmnopqrstuvwxyz0123456789';
        try {
            if (w.crypto && w.crypto.getRandomValues) {
                var a = new Uint8Array(24); w.crypto.getRandomValues(a);
                for (var i = 0; i < a.length; i++) s += c[a[i] % c.length];
                return s;
            }
        } catch (e) { }
        for (var j = 0; j < 24; j++) s += c[Math.floor(Math.random() * c.length)];
        return s;
    }
    function hm(iso) {
        if (!iso) return '';
        var m = /T(\d{2}):(\d{2})/.exec(iso);
        return m ? m[1] + ':' + m[2] : '';
    }
    var toastTimer = null;
    function toast(msg, tone) {
        var t = $('dnToast');
        if (!t) return;
        t.textContent = msg;
        t.className = 'dn-toast' + (tone ? ' is-' + tone : '');
        t.hidden = false;
        if (toastTimer) clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { t.hidden = true; }, tone === 'err' ? 4500 : 2600);
    }
    function busy(btn, on) {
        if (!btn) return;
        btn.disabled = !!on;
        btn.className = btn.className.replace(/\s*is-busy/g, '') + (on ? ' is-busy' : '');
    }
    function openSheet(id) {
        var s = $(id);
        if (!s) return;
        s.hidden = false;
        d.body.style.overflow = 'hidden';
    }
    function closeSheet(id) {
        var s = $(id);
        if (!s) return;
        s.hidden = true;
        var any = d.querySelector('.dn-sheet:not([hidden])');
        if (!any) d.body.style.overflow = '';
    }
    d.addEventListener('click', function (e) {
        var t = e.target;
        while (t && t !== d && !(t.getAttribute && t.getAttribute('data-close'))) t = t.parentNode;
        if (!t || t === d) return;
        closeSheet(t.getAttribute('data-close'));
    }, false);
    d.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var open = d.querySelectorAll('.dn-sheet:not([hidden])');
        for (var i = 0; i < open.length; i++) closeSheet(open[i].id);
    }, false);

    /** POST ไป API (multipart) → Promise<json> ไม่ reject: เน็ตล้ม = {ok:false, code:'network'} · reload=true = โหลดหน้าใหม่ (nonce หมดอายุ) */
    function api(cfg, action, fields, file, timeoutMs) {
        var fd = new FormData();
        fd.append('a', action);
        fd.append('nonce', cfg.nonce || '');
        if (fields) for (var k in fields) if (fields.hasOwnProperty(k) && fields[k] !== undefined && fields[k] !== null) fd.append(k, fields[k]);
        if (file) fd.append('file', file, file.name || 'slip.jpg');
        var ctl = null, timer = null;
        try { ctl = new AbortController(); timer = setTimeout(function () { try { ctl.abort(); } catch (e) { } }, timeoutMs || 30000); } catch (e0) { ctl = null; }
        if (!w.fetch) return Promise.resolve({ ok: false, code: 'network', message: 'เบราว์เซอร์นี้ไม่รองรับ — กรุณาใช้เบราว์เซอร์รุ่นใหม่' });
        return fetch(cfg.api, { method: 'POST', body: fd, credentials: 'same-origin', headers: { 'X-Dining': '1' }, signal: ctl ? ctl.signal : undefined })
            .then(function (r) {
                if (timer) clearTimeout(timer);
                return r.json().then(function (j) { return j || { ok: false, code: 'bad', message: 'เซิร์ฟเวอร์ตอบกลับผิดรูปแบบ' }; },
                    function () { return { ok: false, code: 'bad', message: r.status === 429 ? 'ส่งคำขอถี่เกินไป — รอสักครู่' : 'เซิร์ฟเวอร์ตอบกลับผิดรูปแบบ' }; });
            }, function () {
                if (timer) clearTimeout(timer);
                return { ok: false, code: 'network', message: 'เชื่อมต่อไม่ได้ — ตรวจสัญญาณอินเทอร์เน็ตแล้วลองใหม่' };
            })
            .then(function (j) {
                if (j && j.reload) { setTimeout(function () { w.location.reload(); }, 900); }
                return j;
            });
    }

    // ═════════════════════════════════════════════════════════════════════
    // หน้าเมนู
    // ═════════════════════════════════════════════════════════════════════
    function menuPage() {
        var cfg = boot();
        if (!cfg || !cfg.menu) return;
        var items = cfg.menu.items || [], cats = cfg.menu.categories || [], byId = {};
        var cartKey = 'dnCart:' + cfg.session.id, idemKey = 'dnIdem:' + cfg.session.id;
        var cart = load(cartKey) || [];
        var catFilter = 'all', query = '';
        var maxQty = +cfg.maxQty || 99, maxLines = +cfg.maxLines || 50, minOrder = +cfg.minOrder || 0;
        var i;
        for (i = 0; i < items.length; i++) byId[items[i].id] = items[i];
        if (cfg.resumed) { var rs = $('dnResume'); if (rs) rs.hidden = false; }

        // ── หมวด ──
        var catList = [{ id: 'all', name: 'ทั้งหมด' }];
        var hasRec = false, j;
        for (j = 0; j < items.length; j++) if (items[j].rec) { hasRec = true; break; }
        if (hasRec) catList.push({ id: 'rec', name: '⭐ แนะนำ' });
        var countBy = {};
        for (j = 0; j < items.length; j++) countBy[items[j].cat || 0] = (countBy[items[j].cat || 0] || 0) + 1;
        for (j = 0; j < cats.length; j++) if (countBy[cats[j].id]) catList.push({ id: String(cats[j].id), name: (cats[j].icon ? cats[j].icon + ' ' : '') + cats[j].name });
        if (countBy[0] && !cats.some(function (c) { return c.id === 0; })) catList.push({ id: '0', name: 'อื่น ๆ' });

        function renderChips() {
            var box = $('dnChips');
            box.innerHTML = '';
            for (var k = 0; k < catList.length; k++) {
                var b = make('button', 'dn-chip' + (catFilter === catList[k].id ? ' is-on' : ''), catList[k].name);
                b.type = 'button';
                b.setAttribute('data-cat', catList[k].id);
                b.setAttribute('role', 'tab');
                box.appendChild(b);
            }
        }
        $('dnChips').addEventListener('click', function (e) {
            var t = e.target;
            while (t && t !== this && !(t.getAttribute && t.getAttribute('data-cat'))) t = t.parentNode;
            if (!t || t === this) return;
            catFilter = t.getAttribute('data-cat');
            renderChips();
            renderMenu();
            var sec = d.querySelector('.dn-cat[data-cat="' + catFilter + '"]');
            if (sec && catFilter !== 'all') { try { sec.scrollIntoView({ block: 'start', behavior: 'smooth' }); } catch (e1) { } }
        }, false);

        // ── ค้นหา ──
        var searchTimer = null;
        $('dnSearch').addEventListener('input', function () {
            var v = this.value;
            if (searchTimer) clearTimeout(searchTimer);
            searchTimer = setTimeout(function () { query = (v || '').trim().toLowerCase(); renderMenu(); }, 150);
        }, false);

        function matches(it) {
            if (query) {
                var hay = ((it.name || '') + ' ' + (it.nameEn || '') + ' ' + (it.desc || '')).toLowerCase();
                if (hay.indexOf(query) < 0) return false;
            }
            if (catFilter === 'all' || query) return true;
            if (catFilter === 'rec') return !!it.rec;
            return String(it.cat || 0) === catFilter;
        }
        function qtyInCart(pid) {
            var n = 0;
            for (var k = 0; k < cart.length; k++) if (cart[k].pid === pid) n += cart[k].qty;
            return n;
        }
        function itemCard(it) {
            var card = make('button', 'dn-item' + (it.soldOut ? ' is-out' : ''));
            card.type = 'button';
            card.setAttribute('data-pid', it.id);
            var main = make('div', 'dn-item-main');
            main.appendChild(make('div', 'dn-item-t', it.name));
            if (it.nameEn) main.appendChild(make('div', 'dn-item-sub', it.nameEn));
            if (it.desc) main.appendChild(make('div', 'dn-item-d', it.desc));
            if (it.prep) main.appendChild(make('div', 'dn-item-sub', '⏱ ประมาณ ' + it.prep + ' นาที'));
            if (it.allergens && it.allergens.length) {
                var tags = make('div', 'dn-tags');
                for (var a = 0; a < it.allergens.length; a++) tags.appendChild(make('span', 'dn-tag', '⚠ ' + it.allergens[a]));
                main.appendChild(tags);
            }
            var pr = make('div', 'dn-item-p');
            pr.appendChild(make('span', 'dn-price', money(it.price)));
            if (it.soldOut) pr.appendChild(make('span', 'dn-out', it.soldOutUntil ? 'หมด ถึง ' + it.soldOutUntil : 'หมดชั่วคราว'));
            else {
                var n = qtyInCart(it.id);
                pr.appendChild(make('span', 'dn-add' + (n ? ' has-qty' : ''), n ? n + ' ในตะกร้า' : (it.groups && it.groups.length ? 'เลือก' : '+ เพิ่ม')));
            }
            main.appendChild(pr);
            card.appendChild(main);
            if (it.thumb) {
                var img = make('img', 'dn-item-th');
                img.alt = ''; img.loading = 'lazy'; img.src = it.thumb;
                img.onerror = function () { var ph = make('div', 'dn-item-th is-ph', '🍽'); img.parentNode.replaceChild(ph, img); };
                card.appendChild(img);
            } else card.appendChild(make('div', 'dn-item-th is-ph', '🍽'));
            if (it.rec && !it.soldOut) card.appendChild(make('span', 'dn-badge', 'แนะนำ'));
            return card;
        }
        function renderMenu() {
            var box = $('dnMenu');
            box.innerHTML = '';
            var any = false;
            var groupsOrder = [];
            if (catFilter === 'rec' && !query) groupsOrder.push({ id: 'rec', name: '⭐ แนะนำ', items: items.filter(function (it) { return it.rec && matches(it); }) });
            else {
                for (var c = 0; c < cats.length; c++) groupsOrder.push({ id: String(cats[c].id), name: cats[c].name, items: items.filter(function (it) { return (it.cat || 0) === cats[c].id && matches(it); }) });
                if (!cats.some(function (cc) { return cc.id === 0; })) groupsOrder.push({ id: '0', name: 'อื่น ๆ', items: items.filter(function (it) { return !(it.cat || 0) && matches(it); }) });
            }
            for (var g = 0; g < groupsOrder.length; g++) {
                var grp = groupsOrder[g];
                if (!grp.items.length) continue;
                any = true;
                var h = make('div', 'dn-cat');
                h.setAttribute('data-cat', grp.id);
                h.appendChild(make('h2', null, grp.name));
                h.appendChild(make('small', null, grp.items.length + ' รายการ'));
                box.appendChild(h);
                var grid = make('div', 'dn-items');
                for (var k = 0; k < grp.items.length; k++) grid.appendChild(itemCard(grp.items[k]));
                box.appendChild(grid);
            }
            if (!any) box.appendChild(make('div', 'dn-empty', query ? 'ไม่พบเมนูที่ค้นหา "' + query + '"' : 'ยังไม่มีเมนูในหมวดนี้'));
        }
        $('dnMenu').addEventListener('click', function (e) {
            var t = e.target;
            while (t && t !== this && !(t.getAttribute && t.getAttribute('data-pid'))) t = t.parentNode;
            if (!t || t === this) return;
            var it = byId[+t.getAttribute('data-pid')];
            if (!it) return;
            if (it.soldOut) { toast(it.name + ' หมดชั่วคราว', 'err'); return; }
            openItem(it);
        }, false);

        // ── รายละเอียด + ตัวเลือก ──
        var cur = null, qty = 1;
        function openItem(it) {
            cur = it; qty = 1;
            var img = $('dnItemImg');
            if (it.img) { img.src = it.img; img.hidden = false; } else { img.hidden = true; img.removeAttribute('src'); }
            $('dnItemName').textContent = it.name;
            $('dnItemEn').textContent = it.nameEn || '';
            $('dnItemDesc').textContent = it.desc || '';
            $('dnItemDesc').hidden = !it.desc;
            var tags = $('dnItemTags');
            tags.innerHTML = '';
            if (it.allergens) for (var a = 0; a < it.allergens.length; a++) tags.appendChild(make('span', 'dn-tag', '⚠ ' + it.allergens[a]));
            if (it.prep) tags.appendChild(make('span', 'dn-tag', '⏱ ประมาณ ' + it.prep + ' นาที'));
            if (it.trackStock && it.stock !== null && it.stock !== undefined && it.stock < 10) tags.appendChild(make('span', 'dn-tag', 'เหลือ ' + it.stock));
            var gbox = $('dnItemGroups');
            gbox.innerHTML = '';
            var groups = it.groups || [];
            for (var g = 0; g < groups.length; g++) {
                var grp = groups[g];
                var single = grp.max <= 1;
                var box = make('div', 'dn-group');
                box.setAttribute('data-gid', grp.id);
                var h = make('div', 'dn-group-h');
                h.appendChild(make('b', null, grp.name));
                var rule = single ? 'เลือก 1' : 'เลือกได้ ' + (grp.min > 0 ? grp.min + '–' : 'ไม่เกิน ') + grp.max;
                h.appendChild(make('small', null, (grp.req || grp.min > 0 ? 'จำเป็น · ' : 'ไม่บังคับ · ') + rule));
                box.appendChild(h);
                for (var o = 0; o < grp.options.length; o++) {
                    var opt = grp.options[o];
                    var lab = make('label', 'dn-opt');
                    var inp = make('input');
                    inp.type = single ? 'radio' : 'checkbox';
                    inp.name = 'g' + grp.id;
                    inp.value = opt.id;
                    inp.setAttribute('data-delta', opt.delta || 0);
                    lab.appendChild(inp);
                    lab.appendChild(make('span', null, opt.name));
                    lab.appendChild(make('small', null, opt.delta ? (opt.delta > 0 ? '+' : '') + fmt(opt.delta) : ''));
                    box.appendChild(lab);
                }
                gbox.appendChild(box);
            }
            $('dnItemNote').value = '';
            $('dnQty').textContent = '1';
            calcItem();
            openSheet('dnItemSheet');
            try { $('dnItemSheet').querySelector('.dn-sheet-body').scrollTop = 0; } catch (e) { }
        }
        function selected() {
            var ids = [], delta = 0, groups = {};
            var inputs = $('dnItemGroups').querySelectorAll('input');
            for (var k = 0; k < inputs.length; k++) {
                var gid = inputs[k].name;
                if (!groups[gid]) groups[gid] = 0;
                if (inputs[k].checked) { ids.push(+inputs[k].value); delta += +inputs[k].getAttribute('data-delta') || 0; groups[gid]++; }
            }
            return { ids: ids, delta: delta, groups: groups };
        }
        function calcItem() {
            if (!cur) return;
            var s = selected();
            $('dnAddTotal').textContent = money((cur.price + s.delta) * qty);
        }
        $('dnItemGroups').addEventListener('change', calcItem, false);
        $('dnQtyMinus').onclick = function () { if (qty > 1) { qty--; $('dnQty').textContent = qty; calcItem(); } };
        $('dnQtyPlus').onclick = function () { if (qty < maxQty) { qty++; $('dnQty').textContent = qty; calcItem(); } else toast('สั่งได้ไม่เกิน ' + maxQty + ' ต่อรายการ'); };
        $('dnAddBtn').onclick = function () {
            if (!cur) return;
            var s = selected(), groups = cur.groups || [], bad = null;
            for (var g = 0; g < groups.length; g++) {
                var n = s.groups['g' + groups[g].id] || 0, grp = groups[g];
                var min = grp.req ? Math.max(1, grp.min) : grp.min;
                var box = $('dnItemGroups').querySelector('[data-gid="' + grp.id + '"]');
                var ok = n >= min && n <= Math.max(1, grp.max);
                if (box) box.className = 'dn-group' + (ok ? '' : ' is-bad');
                if (!ok && !bad) bad = n < min ? 'กรุณาเลือก "' + grp.name + '"' + (min > 1 ? ' อย่างน้อย ' + min : '') : '"' + grp.name + '" เลือกได้ไม่เกิน ' + grp.max;
            }
            if (bad) { toast(bad, 'err'); return; }
            var note = ($('dnItemNote').value || '').trim().slice(0, 300);
            var ids = s.ids.slice().sort(function (a, b) { return a - b; });
            var key = cur.id + '|' + ids.join(',') + '|' + note;
            var line = null;
            for (var k = 0; k < cart.length; k++) if (cart[k].key === key) { line = cart[k]; break; }
            if (line) line.qty = Math.min(maxQty, line.qty + qty);
            else {
                if (cart.length >= maxLines) { toast('ตะกร้ารับได้สูงสุด ' + maxLines + ' รายการ — สั่งรอบนี้ก่อนแล้วค่อยสั่งเพิ่ม', 'err'); return; }
                cart.push({ key: key, pid: cur.id, qty: qty, opt: ids, note: note });
            }
            saveCart();
            closeSheet('dnItemSheet');
            toast('ใส่ตะกร้าแล้ว: ' + cur.name + ' × ' + qty, 'ok');
            renderMenu();
        };

        // ── ตะกร้า ──
        function lineInfo(l) {
            var it = byId[l.pid];
            if (!it) return null;
            var names = [], delta = 0;
            var groups = it.groups || [];
            for (var g = 0; g < groups.length; g++)
                for (var o = 0; o < groups[g].options.length; o++)
                    if (l.opt.indexOf(groups[g].options[o].id) >= 0) { names.push(groups[g].options[o].name); delta += groups[g].options[o].delta || 0; }
            return { item: it, names: names, unit: it.price + delta, total: (it.price + delta) * l.qty };
        }
        function cartTotals() {
            var n = 0, t = 0;
            for (var k = 0; k < cart.length; k++) { var li = lineInfo(cart[k]); if (!li) continue; n += cart[k].qty; t += li.total; }
            return { count: n, total: r2(t) };
        }
        function saveCart() {
            cart = cart.filter(function (l) { return l.qty > 0 && byId[l.pid]; });
            store(cartKey, cart);
            if (cart.length && !load(idemKey)) store(idemKey, uuid());
            if (!cart.length) store(idemKey, null);
            updateBar();
        }
        function updateBar() {
            var t = cartTotals(), bar = $('dnBar');
            bar.hidden = t.count === 0;
            $('dnBarCount').textContent = t.count;
            $('dnBarTotal').textContent = money(t.total);
        }
        function renderCart() {
            var box = $('dnCartLines');
            box.innerHTML = '';
            if (!cart.length) box.appendChild(make('div', 'dn-empty', 'ตะกร้าว่าง — เลือกเมนูก่อนนะคะ'));
            for (var k = 0; k < cart.length; k++) {
                var l = cart[k], li = lineInfo(l);
                if (!li) continue;
                var row = make('div', 'dn-cl');
                var left = make('div');
                left.appendChild(make('div', 'dn-cl-n', li.item.name));
                var sub = [];
                if (li.names.length) sub.push(li.names.join(', '));
                if (l.note) sub.push('📝 ' + l.note);
                sub.push(money(li.unit) + ' × ' + l.qty);
                left.appendChild(make('div', 'dn-cl-s', sub.join(' · ')));
                row.appendChild(left);
                var right = make('div', 'dn-cl-r');
                right.appendChild(make('b', 'dn-mono', money(li.total)));
                var q = make('div', 'dn-qty');
                var minus = make('button', null, '−'); minus.type = 'button'; minus.setAttribute('data-k', k); minus.setAttribute('data-d', '-1'); minus.setAttribute('aria-label', 'ลด');
                var plus = make('button', null, '+'); plus.type = 'button'; plus.setAttribute('data-k', k); plus.setAttribute('data-d', '1'); plus.setAttribute('aria-label', 'เพิ่ม');
                q.appendChild(minus); q.appendChild(make('span', null, l.qty)); q.appendChild(plus);
                right.appendChild(q);
                var rm = make('button', 'dn-cl-rm', 'นำออก'); rm.type = 'button'; rm.setAttribute('data-k', k); rm.setAttribute('data-d', 'rm');
                right.appendChild(rm);
                row.appendChild(right);
                box.appendChild(row);
            }
            var t = cartTotals(), sum = $('dnCartSum');
            sum.innerHTML = '';
            var r1 = make('div', 'dn-sum-row'); r1.appendChild(make('span', null, 'จำนวน')); r1.appendChild(make('span', null, t.count + ' รายการ')); sum.appendChild(r1);
            var r2x = make('div', 'dn-sum-row is-total'); r2x.appendChild(make('span', null, 'รวม (รวม VAT)')); r2x.appendChild(make('span', 'dn-mono', money(t.total))); sum.appendChild(r2x);
            var mo = $('dnMinOrder');
            if (minOrder > 0 && t.total < minOrder && t.count > 0) { mo.hidden = false; mo.textContent = 'ยอดสั่งขั้นต่ำ ' + money(minOrder) + ' — ขาดอีก ' + money(minOrder - t.total); }
            else mo.hidden = true;
            $('dnCheckoutBtn').disabled = t.count === 0 || (minOrder > 0 && t.total < minOrder);
        }
        $('dnCartLines').addEventListener('click', function (e) {
            var t = e.target;
            while (t && t !== this && !(t.getAttribute && t.getAttribute('data-k'))) t = t.parentNode;
            if (!t || t === this) return;
            var k = +t.getAttribute('data-k'), dd = t.getAttribute('data-d');
            if (!cart[k]) return;
            if (dd === 'rm') cart.splice(k, 1);
            else {
                var nq = cart[k].qty + (+dd);
                if (nq > maxQty) { toast('สั่งได้ไม่เกิน ' + maxQty + ' ต่อรายการ'); return; }
                if (nq <= 0) cart.splice(k, 1); else cart[k].qty = nq;
            }
            saveCart(); renderCart(); renderMenu();
        }, false);
        $('dnBarBtn').onclick = function () { renderCart(); openSheet('dnCartSheet'); };

        // ── ยืนยันสั่ง ──
        function cartPayload() {
            return JSON.stringify(cart.map(function (l) { return { pid: l.pid, qty: l.qty, opt: l.opt, note: l.note || null }; }));
        }
        function renderQuote(q) {
            var box = $('dnQuote');
            box.innerHTML = '';
            if (!q || !q.lines) return;
            for (var k = 0; k < q.lines.length; k++) {
                var l = q.lines[k], row = make('div', 'dn-cl');
                var left = make('div');
                left.appendChild(make('div', 'dn-cl-n', l.name + ' × ' + (+l.qty)));
                var sub = [];
                if (l.opts && l.opts.length) sub.push(l.opts.map(function (o) { return o.name; }).join(', '));
                if (l.note) sub.push('📝 ' + l.note);
                if (sub.length) left.appendChild(make('div', 'dn-cl-s', sub.join(' · ')));
                row.appendChild(left);
                row.appendChild(make('b', 'dn-mono', money(l.total)));
                box.appendChild(row);
            }
            var tot = make('div', 'dn-sum');
            if (q.vat > 0) {
                var rv = make('div', 'dn-sum-row'); rv.appendChild(make('small', null, 'ในนั้นเป็น VAT 7%')); rv.appendChild(make('small', 'dn-mono', money(q.vat))); tot.appendChild(rv);
            }
            var rt = make('div', 'dn-sum-row is-total'); rt.appendChild(make('span', null, 'ยอดรวมรอบนี้')); rt.appendChild(make('span', 'dn-mono', money(q.total))); tot.appendChild(rt);
            box.appendChild(tot);
        }
        function showCoErr(msg) { var e = $('dnCoErr'); e.hidden = !msg; e.textContent = msg || ''; }
        var quoteOk = false;
        $('dnCheckoutBtn').onclick = function () {
            if (!cart.length) return;
            closeSheet('dnCartSheet');
            openSheet('dnCheckoutSheet');
            showCoErr('');
            quoteOk = false;
            $('dnQuote').innerHTML = '<div class="dn-skel-line"></div><div class="dn-skel-line"></div>';
            $('dnMethodTransfer').hidden = !cfg.session.allowTransfer;
            if (!cfg.session.allowTransfer) { var r = d.querySelector('input[name="dnMethod"][value="CASH"]'); if (r) r.checked = true; }
            if (!cfg.session.allowCash && cfg.session.allowTransfer) { var r2b = d.querySelector('input[name="dnMethod"][value="TRANSFER"]'); if (r2b) r2b.checked = true; }
            syncMethod();
            api(cfg, 'quote', { cart: cartPayload() }).then(function (j) {
                if (j.closed) { onClosed(); return; }
                if (!j.ok) { renderQuote(j); showCoErr(j.message || (j.errors && j.errors.join('\n')) || 'คิดราคาไม่ได้'); return; }
                quoteOk = true;
                renderQuote(j);
            });
        };
        function method() { var r = d.querySelector('input[name="dnMethod"]:checked'); return r ? r.value : 'CASH'; }
        function syncMethod() {
            var m = method();
            var radios = d.querySelectorAll('.dn-radio');
            for (var k = 0; k < radios.length; k++) {
                var inp = radios[k].querySelector('input');
                radios[k].className = 'dn-radio' + (inp && inp.checked ? ' is-on' : '');
            }
            $('dnPlaceBtn').textContent = m === 'TRANSFER' ? 'สั่งเลย แล้วไปโอนชำระ' : 'สั่งเลย (จ่ายตอนเช็คบิล)';
            $('dnCoPhoneHint').textContent = m === 'TRANSFER' ? '(จำเป็นเมื่อโอนเงิน — ใช้ติดต่อกรณีสลิปมีปัญหา)' : '(ไม่บังคับ)';
        }
        $('dnPayPick').addEventListener('change', syncMethod, false);
        var placing = false;
        $('dnPlaceBtn').onclick = function () {
            if (placing || !cart.length) return;
            var m = method();
            var phone = ($('dnCoPhone').value || '').trim();
            if (m === 'TRANSFER' && phone.replace(/\D/g, '').length < 9) { showCoErr('กรุณากรอกเบอร์โทรศัพท์เมื่อเลือกโอนเงิน'); $('dnCoPhone').focus(); return; }
            showCoErr('');
            var idem = load(idemKey);
            if (!idem) { idem = uuid(); store(idemKey, idem); }
            placing = true;
            busy($('dnPlaceBtn'), true);
            api(cfg, 'place', {
                cart: cartPayload(), note: ($('dnCoNote').value || '').trim(), name: ($('dnCoName').value || '').trim(), phone: phone, method: m, idem: idem
            }, null, 45000).then(function (j) {
                placing = false;
                busy($('dnPlaceBtn'), false);
                if (j.closed) { onClosed(); return; }
                if (!j.ok) {
                    if (j.quote) renderQuote(j.quote);
                    showCoErr(j.message || 'สั่งไม่สำเร็จ');
                    if (j.code === 'SOLD_OUT' || j.code === 'OUT_OF_STOCK' || j.code === 'VALIDATION') { store(idemKey, uuid()); }
                    if (j.field === 'phone') $('dnCoPhone').focus();
                    return;
                }
                cart = [];
                store(cartKey, null); store(idemKey, null);
                updateBar();
                toast(j.duplicate ? 'รับออเดอร์นี้ไว้แล้ว' : 'ส่งออเดอร์เข้าครัวแล้ว ✓', 'ok');
                store('dnJustOrdered', { no: j.orderNo, total: j.total, method: m });
                w.location.href = j.statusUrl || cfg.statusUrl;
            });
        };
        function onClosed() {
            toast('โต๊ะนี้ปิดบิลแล้ว — สแกน QR ใหม่เพื่อสั่งอีกครั้ง', 'err');
            store(cartKey, null); store(idemKey, null);
            setTimeout(function () { w.location.href = cfg.menuUrl; }, 1500);
        }

        renderChips();
        renderMenu();
        updateBar();
    }

    // ═════════════════════════════════════════════════════════════════════
    // หน้าสถานะ
    // ═════════════════════════════════════════════════════════════════════
    var STEPS = [['NEW', 'รับออเดอร์'], ['ACCEPTED', 'รับแล้ว'], ['PREPARING', 'กำลังทำ'], ['SERVED', 'เสิร์ฟแล้ว']];

    function statusPage() {
        var cfg = boot();
        if (!cfg || !cfg.summary) return;
        var S = cfg.summary;
        var pay = { open: false, tab: null, slipDone: false, room: null, rooms: null };
        var pollMs = Math.max(2, +cfg.pollSec || 5) * 1000, pollTimer = null, polling = false, failStreak = 0;

        // ── render ──
        function pill(o) {
            if (o.cancelled) return ['err', 'ยกเลิก'];
            if (o.pay === 'PAID') return ['ok', 'ชำระแล้ว'];
            if (o.pay === 'CHARGED') return ['info', 'ลงบิลห้องพัก'];
            if (o.pendingSlip) return ['warn', 'สลิปรอตรวจ'];
            return ['muted', 'ยังไม่ชำระ'];
        }
        function renderOrders() {
            var box = $('dnOrders');
            box.innerHTML = '';
            if (!S.orders.length) { box.appendChild(make('div', 'dn-empty', 'ยังไม่มีออเดอร์ — กด "สั่งเพิ่ม" เพื่อเริ่มสั่ง')); return; }
            for (var k = S.orders.length - 1; k >= 0; k--) {
                var o = S.orders[k];
                var el = make('div', 'dn-order' + (o.cancelled ? ' is-cancelled' : ''));
                var h = make('div', 'dn-order-h');
                var left = make('div');
                left.appendChild(make('b', null, 'รอบที่ ' + o.round + ' · ' + o.no));
                left.appendChild(make('span', 'dn-muted', '  ' + hm(o.createdAt)));
                h.appendChild(left);
                var p = pill(o);
                h.appendChild(make('span', 'dn-pill dn-pill-' + p[0], p[1]));
                el.appendChild(h);
                var tl = make('div', 'dn-tl' + (o.cancelled ? ' is-cancelled' : ''));
                var idx = -1;
                for (var s = 0; s < STEPS.length; s++) if (STEPS[s][0] === o.status) idx = s;
                for (s = 0; s < STEPS.length; s++) {
                    var st = make('div', 'dn-tl-s' + (!o.cancelled && s < idx ? ' is-done' : '') + (!o.cancelled && s === idx ? (s === STEPS.length - 1 ? ' is-done is-now' : ' is-now') : ''), STEPS[s][1]);
                    tl.appendChild(st);
                }
                el.appendChild(tl);
                if (o.cancelled) el.appendChild(make('div', 'dn-alert dn-alert-err', 'ยกเลิกแล้ว' + (o.cancelReason ? ' — ' + o.cancelReason : '')));
                for (var i = 0; i < o.items.length; i++) {
                    var it = o.items[i], row = make('div', 'dn-oi');
                    row.appendChild(make('span', 'dn-oi-q', (+it.qty) + '×'));
                    var n = make('span', 'dn-oi-n', it.name);
                    var sub = [];
                    if (it.opts) sub.push(it.opts);
                    if (it.note) sub.push('📝 ' + it.note);
                    if (sub.length) n.appendChild(make('small', null, sub.join(' · ')));
                    row.appendChild(n);
                    row.appendChild(make('span', 'dn-oi-p dn-mono', money(it.total)));
                    el.appendChild(row);
                }
                if (o.note) el.appendChild(make('div', 'dn-order-note', 'หมายเหตุ: ' + o.note));
                var f = make('div', 'dn-order-f');
                f.appendChild(make('span', 'dn-muted', o.servedAt ? 'เสิร์ฟ ' + hm(o.servedAt) : o.acceptedAt ? 'รับออเดอร์ ' + hm(o.acceptedAt) : 'ส่งเข้าครัวแล้ว'));
                f.appendChild(make('b', 'dn-mono', money(o.total)));
                el.appendChild(f);
                box.appendChild(el);
            }
        }
        function renderPays() {
            var card = $('dnPaysCard'), box = $('dnPays');
            box.innerHTML = '';
            card.hidden = !S.payments.length;
            for (var k = 0; k < S.payments.length; k++) {
                var p = S.payments[k], row = make('div', 'dn-pay-row');
                var l = make('div');
                var tone = p.confirmed ? 'ok' : p.pending ? 'warn' : 'err';
                var lab = p.confirmed ? (p.method === 'ROOM_CHARGE' ? 'ลงบิลห้องแล้ว' : 'ชำระแล้ว') : p.pending ? 'รอพนักงานตรวจสลิป' : 'สลิปไม่ผ่าน';
                l.appendChild(make('b', null, p.methodTh + (p.paidHow ? ' · ' + p.paidHow : '') + (p.roomRef ? ' · ' + p.roomRef : '')));
                var sm = make('small');
                sm.appendChild(make('span', 'dn-pill dn-pill-' + tone, lab));
                sm.appendChild(d.createTextNode(' ' + hm(p.paidAt) + (p.note && !p.confirmed ? ' · ' + p.note : '')));
                l.appendChild(sm);
                row.appendChild(l);
                row.appendChild(make('b', 'dn-mono', money(p.amount)));
                box.appendChild(row);
            }
        }
        function renderDue() {
            var o = S.outstanding, due = $('dnDue');
            $('dnDueAmt').textContent = money(o.amount);
            due.className = 'dn-card dn-due' + (o.amount <= 0 && S.totalOrdered > 0 ? ' is-paid' : '');
            $('dnDueSide').textContent = 'สั่งรวม ' + money(S.totalOrdered) + (S.totalPaid > 0 ? ' · ชำระแล้ว ' + money(S.totalPaid) : '');
            var sub = [];
            if (o.pendingSlip > 0) sub.push('มีสลิปรอพนักงานตรวจ ' + money(o.pendingSlip) + ' — ไม่ต้องโอนซ้ำ');
            if (o.amount <= 0 && S.totalOrdered > 0) sub.push('ชำระครบแล้ว ขอบคุณค่ะ 🙏');
            else if (S.billRequested) sub.push('แจ้งเช็คบิลแล้ว — เลือกวิธีชำระด้านล่าง หรือรอพนักงานมาเก็บเงิน');
            else if (o.payable > 0) sub.push('สั่งเพิ่มได้จนกว่าจะเช็คบิล');
            if (!S.zoneOpen && S.zoneMsg) { var zm = $('dnZoneMsg'); zm.hidden = false; zm.textContent = S.zoneMsg + ' — ออเดอร์ที่สั่งไว้แล้วยังดำเนินการตามปกติ'; }
            else $('dnZoneMsg').hidden = true;
            $('dnDueSub').textContent = sub.join('\n');
            var bill = $('dnBillBtn');
            bill.hidden = o.payable <= 0;
            bill.textContent = S.billRequested ? 'เลือกวิธีชำระ · Pay' : 'เช็คบิล · Bill';
            if (o.payable <= 0 && pay.open && !pay.slipDone && pay.tab !== 'room') { $('dnPay').hidden = true; pay.open = false; }
        }
        function renderAll() { renderDue(); renderOrders(); renderPays(); if (pay.open) renderPayAmount(); }

        // ── ปิดโต๊ะ ──
        function onClosed() {
            stopPoll();
            $('dnClosed').hidden = false;
            d.body.style.overflow = 'hidden';
        }
        function applyResult(j) {
            if (j && j.closed) { onClosed(); return false; }
            if (j && j.summary) { S = j.summary; renderAll(); }
            return true;
        }

        // ── poll ──
        function poll() {
            if (polling || d.hidden) return;
            polling = true;
            api(cfg, 'status', { v: S.v }, null, 15000).then(function (j) {
                polling = false;
                var live = $('dnLive');
                if (j.closed) { onClosed(); return; }
                if (!j.ok) {
                    failStreak++;
                    if (failStreak >= 2) { live.className = 'dn-live is-off'; $('dnLiveTxt').textContent = j.code === 'network' ? 'ออฟไลน์ — กำลังลองเชื่อมต่อใหม่' : 'อัปเดตไม่ได้ชั่วคราว'; }
                    return;
                }
                failStreak = 0;
                live.className = 'dn-live';
                $('dnLiveTxt').textContent = 'อัปเดตล่าสุด ' + new Date().toTimeString().slice(0, 5);
                if (j.same) return;
                var prevPaid = S.totalPaid;
                S = j; renderAll();
                if (S.totalPaid > prevPaid) toast('บันทึกการชำระแล้ว ขอบคุณค่ะ', 'ok');
            });
        }
        function startPoll() { stopPoll(); pollTimer = setInterval(poll, pollMs); }
        function stopPoll() { if (pollTimer) clearInterval(pollTimer); pollTimer = null; }
        d.addEventListener('visibilitychange', function () { if (!d.hidden) poll(); }, false);

        // ── เรียกพนักงาน ──
        $('dnCallBtn').onclick = function () { $('dnCallMsg').value = ''; openSheet('dnCallSheet'); };
        $('dnCallQuick').addEventListener('click', function (e) {
            var t = e.target;
            if (t && t.getAttribute && t.getAttribute('data-q')) $('dnCallMsg').value = t.getAttribute('data-q');
        }, false);
        $('dnCallSend').onclick = function () {
            var btn = this;
            busy(btn, true);
            api(cfg, 'call', { msg: ($('dnCallMsg').value || '').trim().slice(0, 200) }).then(function (j) {
                busy(btn, false);
                if (!applyResult(j)) return;
                closeSheet('dnCallSheet');
                toast(j.ok ? '🙋 ' + (j.message || 'แจ้งพนักงานแล้ว') : (j.code === 'RATE_LIMITED' ? 'ส่งแล้ว รอสักครู่ — พนักงานกำลังมา' : (j.message || 'ส่งไม่สำเร็จ')), j.ok ? 'ok' : (j.code === 'RATE_LIMITED' ? '' : 'err'));
            });
        };

        // ── เช็คบิล + เลือกวิธีจ่าย ──
        function tabsAvail() {
            var s = S.session;
            return { cash: !!s.allowCash, transfer: !!s.allowTransfer, room: !!s.allowRoomCharge };
        }
        function renderPayAmount() {
            var amt = S.outstanding.payable;
            $('dnPayAmt').textContent = money(amt);
            $('dnXferAmt').textContent = money(amt);
            $('dnXferCopy').setAttribute('data-sf-copy', (r2(amt) === Math.floor(r2(amt)) ? Math.floor(amt) : r2(amt).toFixed(2)) + '');
        }
        function openPay(tab) {
            var av = tabsAvail();
            $('dnTabCash').hidden = !av.cash; $('dnTabTransfer').hidden = !av.transfer; $('dnTabRoom').hidden = !av.room;
            if (!tab || !av[tab]) tab = av.cash ? 'cash' : av.transfer ? 'transfer' : av.room ? 'room' : null;
            pay.open = true; pay.slipDone = false;
            $('dnPay').hidden = false;
            renderPayAmount();
            selectTab(tab);
            try { $('dnPay').scrollIntoView({ block: 'start', behavior: 'smooth' }); } catch (e) { }
        }
        function selectTab(tab) {
            pay.tab = tab;
            var btns = $('dnPayTabs').querySelectorAll('button');
            for (var k = 0; k < btns.length; k++) btns[k].className = btns[k].getAttribute('data-pay') === tab ? 'is-on' : '';
            $('dnPayCash').hidden = tab !== 'cash';
            $('dnPayTransfer').hidden = tab !== 'transfer';
            $('dnPayRoom').hidden = tab !== 'room';
            if (tab === 'transfer') initSlip();
            if (tab === 'room') loadRooms();
        }
        $('dnPayTabs').addEventListener('click', function (e) {
            var t = e.target;
            while (t && t !== this && !(t.getAttribute && t.getAttribute('data-pay'))) t = t.parentNode;
            if (!t || t === this) return;
            selectTab(t.getAttribute('data-pay'));
        }, false);
        $('dnBillBtn').onclick = function () {
            var btn = this;
            if (S.billRequested) { openPay(null); return; }
            busy(btn, true);
            api(cfg, 'bill').then(function (j) {
                busy(btn, false);
                if (!applyResult(j)) return;
                if (!j.ok) { toast(j.message || 'แจ้งเช็คบิลไม่สำเร็จ', 'err'); return; }
                S.billRequested = true;
                toast('🧾 ' + (j.message || 'แจ้งพนักงานแล้ว'), 'ok');
                renderDue();
                if (S.outstanding.payable > 0) openPay(null);
            });
        };

        // ── โอน + สลิป ──
        var dz = null, slipBusy = false;
        function initSlip() {
            if (dz || !w.SlipFirst) return;
            dz = w.SlipFirst.dropzone({
                input: 'dnSlipFile', accept: 'image', maxMB: 8, rawMaxMB: 25,
                texts: { title: 'แตะเพื่อแนบสลิปการโอน', sub: 'ถ่ายรูป หรือเลือกรูปสลิปจากเครื่อง', reading: 'กำลังตรวจสลิป…', readingSub: 'อ่านยอดเงินจากสลิป ใช้เวลาไม่กี่วินาที' },
                onPick: function (file, z) { sendSlip(file, z); }
            });
        }
        function slipResult(html, tone) {
            var box = $('dnSlipResult');
            box.hidden = false;
            box.innerHTML = '';
            var a = make('div', 'dn-alert dn-alert-' + tone);
            a.innerHTML = html;
            box.appendChild(a);
            return box;
        }
        function contactHtml(fromServer) {
            if (fromServer) return fromServer;
            var t = $('dnContactTpl');
            return t ? t.innerHTML : '';
        }
        function sendSlip(file, z) {
            if (slipBusy) return;
            if (S.outstanding.payable <= 0) { toast('ไม่มียอดค้างให้ชำระ', 'err'); return; }
            slipBusy = true;
            z.setState('reading', '', null);
            $('dnSlipResult').hidden = true;
            var paidHow = (cfg.channels && cfg.channels.length) ? cfg.channels[0].id : 0;
            var prep = w.SlipFirst && w.SlipFirst.prepare ? w.SlipFirst.prepare(file) : Promise.resolve({ file: file });
            prep.then(function (p) {
                return api(cfg, 'slip', { paidHow: paidHow }, p.file || file, 90000);
            }).then(function (j) {
                slipBusy = false;
                if (j.closed) { onClosed(); return; }
                if (j.summary) { S = j.summary; renderAll(); }
                if (j.ok && j.verified) {
                    z.setState('done', '✅ ตรวจสลิปผ่าน', 'ok');
                    pay.slipDone = true;
                    var box = slipResult('', 'ok');
                    box.innerHTML = '';
                    var rc = make('div', 'dn-receipt');
                    rc.appendChild(make('div', null, 'ชำระเงินเรียบร้อย ขอบคุณค่ะ 🙏'));
                    rc.appendChild(make('b', 'dn-mono', money(j.amount || j.slipAmount || 0)));
                    rc.appendChild(make('div', 'dn-muted', (j.paidHow || 'โอนเข้าบัญชีร้าน') + ' · ' + new Date().toTimeString().slice(0, 5) + (j.covered && j.covered.length ? ' · ปิด ' + j.covered.length + ' ออเดอร์' : '')));
                    if (j.slipAmount && j.amount && j.slipAmount - j.amount > 0.009) rc.appendChild(make('div', 'dn-muted', 'ยอดสลิปเกิน ' + money(j.slipAmount - j.amount) + ' — แจ้งพนักงานเพื่อรับเงินทอน'));
                    box.appendChild(rc);
                    toast('ชำระเงินเรียบร้อย', 'ok');
                    return;
                }
                if (j.ok && j.pending) {
                    z.setState('picked', '⏳ รอพนักงานตรวจ', 'warn');
                    pay.slipDone = true;
                    slipResult('<b>⏳ รับสลิปแล้ว — รอพนักงานตรวจสอบ</b><br>' + esc(j.message || 'พนักงานจะยืนยันให้เร็ว ๆ นี้') + (j.reason ? '<br><small>' + esc(j.reason) + '</small>' : ''), 'warn');
                    return;
                }
                // ไม่รับ / ผิดพลาด
                z.setState('picked', j.duplicate ? '⛔ สลิปซ้ำ' : '⛔ ใช้ไม่ได้', 'err');
                var msg = '<b>' + (j.duplicate ? '⛔ สลิปนี้ถูกใช้ไปแล้ว' : '⛔ สลิปใช้ไม่ได้') + '</b><br>' + esc(j.message || 'ตรวจสลิปไม่ผ่าน')
                    + (j.reason && j.reason !== j.message ? '<br><small>' + esc(j.reason) + '</small>' : '')
                    + (j.rejected ? contactHtml(j.contactHtml) : '');
                var box2 = slipResult(msg, 'err');
                var again = make('button', 'dn-btn dn-btn-ghost dn-wide', 'แนบสลิปใหม่');
                again.type = 'button';
                again.onclick = function () { try { $('dnSlipFile').value = ''; } catch (e) { } $('dnSlipResult').hidden = true; z.setState('empty', '', null); try { $('dnSlipFile').click(); } catch (e2) { } };
                box2.appendChild(again);
            });
        }

        // ── ลงบิลห้องพัก ──
        function loadRooms() {
            var box = $('dnRooms');
            pay.room = null;
            $('dnRoomBtn').disabled = true;
            $('dnRoomErr').hidden = true;
            box.innerHTML = '<div class="dn-skel-line"></div>';
            api(cfg, 'roomcandidates').then(function (j) {
                if (!applyResult(j)) return;
                box.innerHTML = '';
                if (!j.ok) { box.appendChild(make('div', 'dn-muted', j.message || 'โหลดรายการห้องไม่ได้')); return; }
                if (j.locked) { showRoomErr('การลงบิลห้องถูกล็อกชั่วคราว (ยืนยันเบอร์ผิดหลายครั้ง) — ลองใหม่หลัง ' + j.locked + ' น. หรือติดต่อพนักงาน'); }
                if (!j.rooms.length) { box.appendChild(make('div', 'dn-muted', 'ไม่มีห้องที่เช็คอินอยู่ขณะนี้ — กรุณาเลือกวิธีชำระอื่น')); return; }
                for (var k = 0; k < j.rooms.length; k++) {
                    var b = make('button', 'dn-room', j.rooms[k].room);
                    b.type = 'button';
                    b.setAttribute('data-rid', j.rooms[k].id);
                    box.appendChild(b);
                }
            });
        }
        function showRoomErr(msg) { var e = $('dnRoomErr'); e.hidden = !msg; e.textContent = msg || ''; }
        $('dnRooms').addEventListener('click', function (e) {
            var t = e.target;
            if (!t || !t.getAttribute || !t.getAttribute('data-rid')) return;
            var all = this.querySelectorAll('.dn-room');
            for (var k = 0; k < all.length; k++) all[k].className = 'dn-room';
            t.className = 'dn-room is-on';
            pay.room = { id: +t.getAttribute('data-rid'), label: t.textContent };
            $('dnRoomBtn').disabled = false;
            $('dnRoomBtn').textContent = 'ลงบิลห้อง ' + pay.room.label + ' · ' + money(S.outstanding.payable);
            showRoomErr('');
        }, false);
        $('dnRoomBtn').onclick = function () {
            var btn = this;
            if (!pay.room) return;
            var phone = ($('dnRoomPhone').value || '').trim();
            if (phone.replace(/\D/g, '').length < 9) { showRoomErr('กรุณากรอกเบอร์โทรที่ใช้จองให้ครบ'); $('dnRoomPhone').focus(); return; }
            busy(btn, true);
            api(cfg, 'roomcharge', { rid: pay.room.id, phone: phone }, null, 45000).then(function (j) {
                busy(btn, false);
                if (!applyResult(j)) return;
                if (j.ok) {
                    pay.slipDone = true;
                    var panel = $('dnPayRoom');
                    panel.innerHTML = '';
                    var rc = make('div', 'dn-receipt');
                    rc.appendChild(make('div', null, '🛏 ลงบิลห้อง ' + (j.room || pay.room.label) + ' แล้ว'));
                    rc.appendChild(make('b', 'dn-mono', money(j.amount || 0)));
                    rc.appendChild(make('div', 'dn-muted', 'ยอดนี้จะรวมในบิลตอนเช็คเอาท์ · ' + new Date().toTimeString().slice(0, 5)));
                    panel.appendChild(rc);
                    toast('ลงบิลห้อง ' + (j.room || '') + ' แล้ว', 'ok');
                    return;
                }
                if (j.code === 'PHONE_MISMATCH') showRoomErr('เบอร์โทรไม่ตรงกับการจองของห้องนี้' + (j.attemptsLeft > 0 ? ' — ลองได้อีก ' + j.attemptsLeft + ' ครั้ง' : ''));
                else if (j.code === 'LOCKED') showRoomErr(j.message || ('ถูกล็อกชั่วคราว' + (j.lockedUntil ? ' จนถึง ' + j.lockedUntil + ' น.' : '')));
                else showRoomErr(j.message || 'ลงบิลห้องไม่สำเร็จ');
            });
        };

        // ── เริ่ม ──
        renderAll();
        startPoll();
        var jo = load('dnJustOrdered');
        if (jo) { store('dnJustOrdered', null); toast('ส่งออเดอร์ ' + (jo.no || '') + ' เข้าครัวแล้ว ✓', 'ok'); }
        var m = /pay=(\w+)/.exec(w.location.hash || '');
        if (m && S.outstanding.payable > 0) { openPay(m[1]); try { w.history.replaceState(null, '', w.location.pathname + w.location.search); } catch (e) { } }
        else if (S.billRequested && S.outstanding.payable > 0) openPay(null);
    }

    w.DN = { menuPage: menuPage, statusPage: statusPage, money: money, esc: esc };
})(window, document);
