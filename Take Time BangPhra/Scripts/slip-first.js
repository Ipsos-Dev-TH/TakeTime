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
   SlipFirst.prepare(file)  ย่อรูปสลิป (ด้านยาว 1600 px JPEG 0.85, หมุนตาม EXIF) → Promise<{ file, compressed, original }>
   SlipFirst.setInputFile(input, file)  ใส่รูปที่ย่อกลับลงช่องไฟล์ (postback เดิมส่งรูปเล็ก) · SlipFirst.send(url, formData) → Promise<json|null>
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
        // rawMaxMB: หน้าที่ย่อรูปก่อนส่ง (SlipFirst.prepare) รับรูปต้นฉบับใหญ่กว่า maxMB ได้ — รูปที่ย่อแล้วต้องไม่เกิน maxMB
        var maxMB = cfg.rawMaxMB || cfg.maxMB || 8;
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
                            ? (o.topUp
                                ? 'ยอดรวมจากสลิปยังต่ำกว่ามัดจำขั้นต่ำ ฿' + money(min) + ' — โอนเพิ่มอีก ฿' + money(min - a) + ' แล้วแนบสลิปเพิ่มด้านล่าง หรือติดต่อเจ้าหน้าที่'
                                : 'ยอดนี้ต่ำกว่ามัดจำขั้นต่ำ ฿' + money(min) + ' จึงยังยืนยันการจองไม่ได้ — กรุณาติดต่อเจ้าหน้าที่')
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

    // ── ย่อรูปสลิปก่อนส่ง ──
    //   วัดกับสลิปจริง 6 ใบ (+ จำลองรูปถ่ายจากมือถือ 4032 px): ด้านยาว 1600 px · JPEG 0.85 · สมูทติ้ง 'high' → QR อ่านได้ทุกใบ
    //   และยอดจาก OCR เท่ารูปเดิม (ความมั่นใจ 92) — 1024 px/0.75 เริ่มทำ QR/ยอดหาย จึงไม่ย่อต่ำกว่านี้
    //   ไฟล์เล็ก (≤ 600 KB) / PDF / ย่อแล้วไม่เล็กลง / เบราว์เซอร์ทำไม่ได้ = ส่งไฟล์เดิม · เซิร์ฟเวอร์บอกว่า QR หาย = หน้าส่งไฟล์เดิมซ้ำ
    var CMP = { maxSide: 1600, quality: 0.85, skipBytes: 600 * 1024, keepBytes: 1500 * 1024, timeoutMs: 8000 };
    var AUTO_ORIENT = (function () {
        try { return !!(w.CSS && CSS.supports && CSS.supports('image-orientation', 'from-image')); } catch (e) { return false; }
    })();

    // EXIF Orientation ของ JPEG (1 = ปกติ) — เบราว์เซอร์รุ่นเก่าไม่หมุนรูปให้เองตอนวาดลง canvas
    function exifOrientation(buf) {
        try {
            var v = new DataView(buf);
            if (v.byteLength < 4 || v.getUint16(0) !== 0xFFD8) return 1;
            var off = 2;
            while (off + 4 <= v.byteLength) {
                var marker = v.getUint16(off);
                off += 2;
                if (marker === 0xFFE1) {
                    if (off + 10 > v.byteLength || v.getUint32(off + 2) !== 0x45786966) return 1;   // "Exif"
                    var t = off + 8, little = v.getUint16(t) === 0x4949;
                    var ifd = t + v.getUint32(t + 4, little);
                    if (ifd + 2 > v.byteLength) return 1;
                    var n = v.getUint16(ifd, little);
                    for (var i = 0; i < n; i++) {
                        var e = ifd + 2 + i * 12;
                        if (e + 10 > v.byteLength) break;
                        if (v.getUint16(e, little) === 0x0112) return v.getUint16(e + 8, little) || 1;
                    }
                    return 1;
                }
                if ((marker & 0xFF00) !== 0xFF00 || marker === 0xFFDA) break;
                off += v.getUint16(off);
            }
        } catch (x) { }
        return 1;
    }

    function canvasOf(wd, ht) {
        var c = d.createElement('canvas');
        c.width = Math.max(1, Math.round(wd));
        c.height = Math.max(1, Math.round(ht));
        return c;
    }

    function ctxOf(c) {
        var g = c.getContext('2d');
        try { g.imageSmoothingEnabled = true; g.imageSmoothingQuality = 'high'; } catch (e) { }
        return g;
    }

    function toBlob(c, q, cb) {
        try {
            if (c.toBlob) { c.toBlob(function (b) { cb(b); }, 'image/jpeg', q); return; }
            var u = c.toDataURL('image/jpeg', q), bin = atob(u.split(',')[1]), arr = new Uint8Array(bin.length);
            for (var i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
            cb(new Blob([arr], { type: 'image/jpeg' }));
        } catch (e) { cb(null); }
    }

    /**
     * ย่อรูปสลิป → Promise<{ file, compressed, original }> (ไม่ reject — ทำไม่ได้ = ไฟล์เดิม)
     */
    function prepare(file, opt) {
        var o = {}, k;
        for (k in CMP) o[k] = CMP[k];
        if (opt) for (k in opt) o[k] = opt[k];
        if (!w.Promise) return { then: function (f) { return f({ file: file, compressed: false, original: file }); } };
        return new Promise(function (resolve) {
            var done = false, url = null;
            function fin(f, c) {
                if (done) return;
                done = true;
                if (url) { try { (w.URL || w.webkitURL).revokeObjectURL(url); } catch (e0) { } }
                resolve({ file: f || file, compressed: !!c && !!f && f !== file, original: file });
            }
            try {
                if (!file || !w.FileReader || !w.Blob || !d.createElement('canvas').getContext) return fin(file, false);
                var type = (file.type || '').toLowerCase(), nm = file.name || 'slip';
                if (type === 'application/pdf' || /\.pdf$/i.test(nm)) return fin(file, false);
                if (type && type.indexOf('image/') !== 0) return fin(file, false);
                if (file.size && file.size <= o.skipBytes) return fin(file, false);
                setTimeout(function () { fin(file, false); }, o.timeoutMs);
                var rd = new FileReader();
                rd.onerror = function () { fin(file, false); };
                rd.onload = function () {
                    var orient = exifOrientation(rd.result);
                    try { url = (w.URL || w.webkitURL).createObjectURL(file); } catch (e1) { return fin(file, false); }
                    var img = new Image();
                    img.onerror = function () { fin(file, false); };
                    img.onload = function () {
                        try {
                            var sw = img.naturalWidth || img.width, sh = img.naturalHeight || img.height;
                            if (!sw || !sh) return fin(file, false);
                            var manual = orient > 1 && orient <= 8 && !AUTO_ORIENT;   // เบราว์เซอร์ไม่หมุนให้ → หมุนเอง
                            var swap = manual && orient >= 5;
                            var dw = swap ? sh : sw, dh = swap ? sw : sh;          // ขนาดตามที่ตาเห็น
                            var longSide = Math.max(dw, dh);
                            if (longSide <= o.maxSide && file.size <= o.keepBytes && !manual) return fin(file, false);
                            var s = Math.min(1, o.maxSide / longSide);
                            var tw = Math.max(1, Math.round(dw * s)), th = Math.max(1, Math.round(dh * s));
                            var tsw = swap ? th : tw, tsh = swap ? tw : th;          // ขนาดเป้าหมายในทิศของรูปต้นฉบับ
                            // ย่อทีละครึ่ง (ภาพคมกว่าย่อครั้งเดียวจากรูปใหญ่มาก)
                            var src = img, cw = sw, ch = sh;
                            while (cw / 2 >= tsw && ch / 2 >= tsh) {
                                var hc = canvasOf(cw / 2, ch / 2);
                                ctxOf(hc).drawImage(src, 0, 0, hc.width, hc.height);
                                src = hc; cw = hc.width; ch = hc.height;
                            }
                            var out = canvasOf(tw, th), g = ctxOf(out);
                            g.fillStyle = '#fff';
                            g.fillRect(0, 0, out.width, out.height);                 // PNG โปร่งใส → พื้นขาว (ไม่ใช่ดำ)
                            if (manual) {
                                switch (orient) {
                                    case 2: g.transform(-1, 0, 0, 1, tsw, 0); break;
                                    case 3: g.transform(-1, 0, 0, -1, tsw, tsh); break;
                                    case 4: g.transform(1, 0, 0, -1, 0, tsh); break;
                                    case 5: g.transform(0, 1, 1, 0, 0, 0); break;
                                    case 6: g.transform(0, 1, -1, 0, tsh, 0); break;
                                    case 7: g.transform(0, -1, -1, 0, tsh, tsw); break;
                                    case 8: g.transform(0, -1, 1, 0, 0, tsw); break;
                                }
                            }
                            g.drawImage(src, 0, 0, tsw, tsh);
                            toBlob(out, o.quality, function (b) {
                                if (!b || !b.size || (file.size && b.size >= file.size * 0.95 && !manual)) return fin(file, false);
                                var name = nm.replace(/\.[^.]*$/, '') + '.jpg', f2;
                                try { f2 = new File([b], name, { type: 'image/jpeg', lastModified: Date.now() }); }
                                catch (e2) { f2 = b; try { f2.name = name; } catch (e3) { } }
                                // Chrome/Android มีตัวอ่าน QR ในเครื่อง: รูปเดิมมี QR แต่รูปย่อไม่มี = ใช้รูปเดิม
                                if (w.BarcodeDetector) {
                                    try {
                                        var bd = new w.BarcodeDetector({ formats: ['qr_code'] });
                                        Promise.all([bd.detect(img), bd.detect(out)]).then(function (r) {
                                            if (r[0] && r[0].length && !(r[1] && r[1].length)) fin(file, false); else fin(f2, true);
                                        }, function () { fin(f2, true); });
                                        return;
                                    } catch (e4) { }
                                }
                                fin(f2, true);
                            });
                        } catch (e5) { fin(file, false); }
                    };
                    img.src = url;
                };
                rd.readAsArrayBuffer(file.slice ? file.slice(0, 256 * 1024) : file);
            } catch (e6) { fin(file, false); }
        });
    }

    // ใส่ไฟล์ (เช่น รูปที่ย่อแล้ว) กลับลง <input type=file> — postback/ฟอร์มเดิมส่งไฟล์นี้แทน · ทำไม่ได้ = false
    function setInputFile(input, f) {
        try {
            if (!input || !f || !w.DataTransfer) return false;
            var dt = new DataTransfer();
            dt.items.add(f instanceof w.File ? f : new File([f], f.name || 'slip.jpg', { type: f.type || 'image/jpeg' }));
            input.files = dt.files;
            return !!(input.files && input.files.length);
        } catch (e) { return false; }
    }

    // POST แบบ multipart ไปตัวตรวจสลิป (API/SlipCheck.ashx) → Promise<json|null> (ไม่ reject — เน็ต/เซิร์ฟเวอร์ล้ม = null)
    function send(url, fd, timeoutMs) {
        if (!w.fetch || !w.Promise) return { then: function (f) { return f(null); } };
        var ctl = null, timer = null;
        try { ctl = new AbortController(); timer = setTimeout(function () { try { ctl.abort(); } catch (e) { } }, timeoutMs || 60000); } catch (e0) { ctl = null; }
        return fetch(url, {
            method: 'POST', body: fd, credentials: 'same-origin', headers: { 'X-Slip-Check': '1' }, signal: ctl ? ctl.signal : undefined
        }).then(function (r) {
            if (timer) clearTimeout(timer);
            return r.json().then(function (j) { return j; }, function () { return null; });
        }, function () { if (timer) clearTimeout(timer); return null; });
    }

    w.SlipFirst = {
        dropzone: dropzone, outcome: outcome, copy: copy, money: money,
        prepare: prepare, setInputFile: setInputFile, send: send, compressOptions: CMP, exifOrientation: exifOrientation
    };
})(window, document);
