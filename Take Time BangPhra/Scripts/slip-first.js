/* ============================================================================
   สลิปก่อนยอด (CLAUDE.md ข้อ 18 — หน้าจอง: อัปโหลดสลิปก่อนเห็นช่องยอด) — ฝั่งเบราว์เซอร์ล้วน (ES5)
   ----------------------------------------------------------------------------
   SlipFirst.dropzone(cfg)  ห่อ <input type=file> เดิมให้เป็นกล่อง แตะ/ถ่ายรูป/ลากวาง + รูปตัวอย่าง + สถานะ
                            cfg = { input, texts?, accept?: 'image'|'image+pdf', maxMB?, thumb?, state?, tone?, status?,
                                    onPick?: function(file, dz) }
                            คืน dz = { el, setState(state, status, tone), setError(msg), input }
   SlipFirst.outcome(o)     ข้อความผลลัพธ์ก่อนยืนยัน — o = { amount, total, minDep, due, mode:'reserve'|'pay', customer, minRule }
                            คืน { cls:'ok'|'warn'|'err', html, blocks }
   SlipFirst.copy(text, btn) / ปุ่ม [data-sf-copy] ทำงานเอง
   ไฟล์นี้ไม่ตัดสินอะไรแทนเซิร์ฟเวอร์ — ยอดที่บันทึกบังคับตามสลิปที่เซิร์ฟเวอร์เสมอ
   ============================================================================ */
(function (w, d) {
    'use strict';
    if (w.SlipFirst) return;

    function el(x) { return typeof x === 'string' ? d.getElementById(x) : x; }
    function r2(n) { return Math.round(n * 100) / 100; }
    function groups(s) { return s.replace(/\B(?=(\d{3})+(?!\d))/g, ','); }
    function money(n) { var p = r2(n).toFixed(2).split('.'); return groups(p[0]) + '.' + p[1]; }
    function esc(s) {
        return String(s === undefined || s === null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }
    function make(tag, cls, text) {
        var e = d.createElement(tag);
        if (cls) e.className = cls;
        if (text !== undefined && text !== null) e.textContent = text;
        return e;
    }

    // ── คัดลอก ──
    function copy(text, btn) {
        var done = function () {
            if (!btn) return;
            var old = btn.getAttribute('data-sf-label') || btn.textContent;
            btn.setAttribute('data-sf-label', old);
            btn.textContent = '✓ คัดลอกแล้ว';
            btn.className = (btn.className.replace(/\s*is-copied/g, '') + ' is-copied');
            setTimeout(function () {
                btn.textContent = old;
                btn.className = btn.className.replace(/\s*is-copied/g, '');
            }, 1600);
        };
        var fallback = function () {
            var ta = make('textarea');
            ta.value = text;
            ta.setAttribute('readonly', 'readonly');
            ta.style.position = 'fixed'; ta.style.left = '-9999px'; ta.style.top = '0';
            d.body.appendChild(ta);
            ta.select();
            try { ta.setSelectionRange(0, 99999); } catch (e0) { }
            try { d.execCommand('copy'); } catch (e1) { }
            d.body.removeChild(ta);
            done();
        };
        try {
            if (w.navigator && navigator.clipboard && navigator.clipboard.writeText && w.isSecureContext !== false) {
                navigator.clipboard.writeText(text).then(done, fallback);
                return;
            }
        } catch (e2) { }
        fallback();
    }
    d.addEventListener('click', function (e) {
        var t = e.target;
        while (t && t !== d && !(t.getAttribute && t.getAttribute('data-sf-copy') !== null)) t = t.parentNode;
        if (!t || t === d) return;
        e.preventDefault();
        copy(t.getAttribute('data-sf-copy'), t);
    }, false);

    // ── กล่องแนบสลิป ──
    var DEF_TEXT = {
        title: 'แตะเพื่อแนบสลิปการโอน',
        sub: 'ถ่ายรูป หรือเลือกรูปสลิปจากเครื่อง · ลากไฟล์มาวางได้',
        reading: 'กำลังตรวจสลิป…',
        readingSub: 'อ่านยอดเงินจากสลิป ใช้เวลาไม่กี่วินาที',
        change: 'เปลี่ยนสลิป',
        badType: 'ไฟล์นี้ไม่ใช่รูปภาพ — กรุณาแนบรูปสลิป (JPG / PNG)',
        tooBig: 'ไฟล์ใหญ่เกินไป (เกิน {mb} MB) — ลองแคปหน้าจอสลิปแทน'
    };

    function dropzone(cfg) {
        var input = el(cfg.input);
        if (!input || input.getAttribute('data-sf-dz') === '1') return null;
        input.setAttribute('data-sf-dz', '1');
        var t = {}, k;
        for (k in DEF_TEXT) t[k] = DEF_TEXT[k];
        if (cfg.texts) for (k in cfg.texts) t[k] = cfg.texts[k];
        var maxMB = cfg.maxMB || 8;
        var allowPdf = cfg.accept === 'image+pdf';

        var root = make('div', 'sf-drop');
        root.setAttribute('data-state', 'empty');

        var main = make('label', 'sf-drop-main');
        if (!input.id) input.id = 'sfFile' + Math.floor(Math.random() * 1e6);
        main.setAttribute('for', input.id);
        var icon = make('span', 'sf-drop-icon', '📎');
        icon.setAttribute('aria-hidden', 'true');
        main.appendChild(icon);
        main.appendChild(make('span', 'sf-drop-t', t.title));
        main.appendChild(make('span', 'sf-drop-s', t.sub));

        var prev = make('div', 'sf-drop-prev');
        var thumbBox = make('div');
        var img = make('img', 'sf-thumb');
        img.alt = 'รูปสลิปที่แนบ';
        thumbBox.appendChild(img);
        var info = make('div', 'sf-prev-info');
        var status = make('div', 'sf-prev-status');
        status.setAttribute('aria-live', 'polite');
        var name = make('div', 'sf-prev-name');
        var change = make('button', 'sf-change', t.change);
        change.type = 'button';
        info.appendChild(status);
        info.appendChild(name);
        info.appendChild(change);
        prev.appendChild(thumbBox);
        prev.appendChild(info);

        var busy = make('div', 'sf-drop-busy');
        busy.setAttribute('role', 'status');
        busy.appendChild(make('span', 'sf-spin'));
        busy.appendChild(make('b', null, t.reading));
        busy.appendChild(make('small', null, t.readingSub));

        var err = make('div', 'sf-drop-err');
        err.setAttribute('role', 'alert');

        input.parentNode.insertBefore(root, input);
        root.appendChild(main);
        root.appendChild(input);
        root.appendChild(prev);
        root.appendChild(busy);
        root.appendChild(err);

        var dz = { el: root, input: input };
        dz.setState = function (state, statusText, tone) {
            root.setAttribute('data-state', state || 'empty');
            if (tone) root.setAttribute('data-tone', tone); else root.removeAttribute('data-tone');
            if (statusText !== undefined) status.textContent = statusText || '';
        };
        dz.setError = function (msg) {
            err.textContent = msg || '';
            err.className = 'sf-drop-err' + (msg ? ' is-on' : '');
        };
        dz.setThumb = function (src, fileName, isPdf) {
            if (isPdf) {
                img.removeAttribute('src');
                img.className = 'sf-thumb sf-thumb-pdf';
                img.alt = 'PDF';
            } else if (src) {
                img.className = 'sf-thumb';
                img.src = src;
            }
            name.textContent = fileName || '';
        };

        if (cfg.thumb) dz.setThumb(cfg.thumb, cfg.fileName || '');
        if (cfg.state) dz.setState(cfg.state, cfg.status || '', cfg.tone);

        change.onclick = function () { try { input.click(); } catch (e) { } };

        function accept(f) {
            if (!f) return false;
            var type = (f.type || '').toLowerCase(), nm = (f.name || '').toLowerCase();
            var isImg = type.indexOf('image/') === 0 || /\.(jpe?g|png|gif|webp|heic|heif|bmp)$/.test(nm);
            var isPdf = type === 'application/pdf' || /\.pdf$/.test(nm);
            if (!isImg && !(allowPdf && isPdf)) { dz.setError(t.badType); return false; }
            if (f.size && f.size > maxMB * 1024 * 1024) { dz.setError(t.tooBig.replace('{mb}', maxMB)); return false; }
            dz.setError('');
            return true;
        }

        function handle() {
            var f = input.files && input.files.length ? input.files[0] : null;
            if (!f) return;
            if (!accept(f)) { try { input.value = ''; } catch (e0) { } return; }
            var isPdf = (f.type || '').toLowerCase() === 'application/pdf' || /\.pdf$/i.test(f.name || '');
            var src = null;
            if (!isPdf) { try { src = (w.URL || w.webkitURL).createObjectURL(f); } catch (e1) { src = null; } }
            dz.setThumb(src, f.name, isPdf);
            dz.setState('picked', '', null);
            if (typeof cfg.onPick === 'function') cfg.onPick(f, dz);
        }
        input.addEventListener('change', handle, false);

        // ลากวาง (คอมพิวเตอร์) — ใส่ไฟล์ลง input เดิม แล้วทำงานเหมือนเลือกเอง
        var over = function (e) { e.preventDefault(); root.className = 'sf-drop is-over'; };
        var leave = function () { root.className = 'sf-drop'; };
        root.addEventListener('dragover', over, false);
        root.addEventListener('dragenter', over, false);
        root.addEventListener('dragleave', leave, false);
        root.addEventListener('drop', function (e) {
            e.preventDefault();
            leave();
            var files = e.dataTransfer && e.dataTransfer.files;
            if (!files || !files.length) return;
            try { input.files = files; } catch (e2) { dz.setError('เบราว์เซอร์นี้ลากวางไม่ได้ — แตะกล่องเพื่อเลือกไฟล์'); return; }
            handle();
        }, false);
        return dz;
    }

    // ── ผลลัพธ์ก่อนยืนยัน: รับมัดจำ / ชำระครบ / รับเกิน / ต่ำกว่ามัดจำขั้นต่ำ ──
    function outcome(o) {
        var a = r2(+o.amount || 0);
        if (!(a > 0)) return { cls: '', html: '', blocks: false };
        var rows = [], note = '', cls = 'ok', blocks = false;
        if (o.mode === 'reserve') {
            var total = r2(+o.total || 0), min = +o.minDep || 0, rule = o.minRule || 0.8;
            if (total > 0 && a >= total - 0.009) {
                rows.push(['✅ ชำระครบ', '฿' + money(total)]);
                if (a - total > 0.009) {
                    rows.push(['รับเกิน (บันทึกเป็นมัดจำ)', '฿' + money(a - total)]);
                    cls = 'warn';
                }
            } else {
                rows.push(['รับมัดจำ', '฿' + money(a)]);
                if (total > 0) rows.push(['คงเหลือชำระตอนเช็คอิน', '฿' + money(total - a)]);
                if (min > 0 && a < min - 0.009) {
                    if (a < min * rule - 0.009) {
                        cls = o.customer ? 'err' : 'warn';
                        blocks = !!o.customer;
                        note = o.customer
                            ? 'ยอดนี้ต่ำกว่ามัดจำขั้นต่ำ ฿' + money(min) + ' จึงยังยืนยันการจองไม่ได้ — กรุณาติดต่อเจ้าหน้าที่'
                            : '⚠ ต่ำกว่ามัดจำขั้นต่ำ ฿' + money(min) + ' — พนักงานบันทึกต่อได้ (ตรวจกับลูกค้าแล้ว)';
                    } else {
                        cls = 'warn';
                        note = 'ต่ำกว่ามัดจำขั้นต่ำ ฿' + money(min) + ' เล็กน้อย — ยังยืนยันการจองได้';
                    }
                }
            }
        } else {
            var due = +o.due;
            if (!(due >= 0) || isNaN(due)) {
                rows.push(['รับชำระ', '฿' + money(a)]);
            } else if (a >= due - 0.009) {
                rows.push(['✅ ชำระครบ', '฿' + money(due)]);
                if (a - due > 0.009) {
                    rows.push(['รับเกิน', '฿' + money(a - due)]);
                    cls = 'warn';
                    note = 'บันทึกเต็มยอดสลิป (ก่อนเข้าพัก = มัดจำ · หลังเข้าพัก ระบบจะแจ้งตอนบันทึก)';
                }
            } else {
                rows.push(['รับชำระ', '฿' + money(a)]);
                rows.push(['คงค้างหลังรับ', '฿' + money(due - a)]);
            }
        }
        var html = '';
        for (var i = 0; i < rows.length; i++)
            html += '<div class="sf-o-row"><span>' + esc(rows[i][0]) + '</span><b>' + esc(rows[i][1]) + '</b></div>';
        if (note) html += '<div class="sf-o-note">' + esc(note) + '</div>';
        return { cls: cls, html: html, blocks: blocks };
    }

    w.SlipFirst = { dropzone: dropzone, outcome: outcome, copy: copy, money: money };
})(window, document);
