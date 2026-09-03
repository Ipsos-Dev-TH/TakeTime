/* ============================================================================
   ตารางข้อมูลบนมือถือ — เปลี่ยนเป็น "การ์ดรายรายการ" ที่มีชื่อคอลัมน์กำกับ
   ใช้คู่กับ /Content/responsive-table.css
   ----------------------------------------------------------------------------
   ทำอะไรบ้าง (ทำงานฝั่งเบราว์เซอร์ล้วน ไม่แตะโค้ดฝั่งเซิร์ฟเวอร์):
     1. หาตารางที่มี class "rt-table" แล้วครอบด้วยกล่อง .rt-wrap
     2. อ่านชื่อคอลัมน์จากแถวหัวตาราง ไปใส่เป็น data-label ของทุกช่อง
        (CSS เอาไปแสดงหน้าค่า ⇒ อ่านออกว่าเลขไหนคืออะไร)
     3. ช่องที่ไม่มีค่าถูกทำเครื่องหมายไว้ให้ CSS ซ่อน การ์ดจะได้ไม่รก
     4. ใส่ปุ่มสลับ "การ์ด ⇄ ตารางเต็ม" จำค่าที่เลือกไว้ในเครื่องผู้ใช้

   ถ้าไฟล์นี้โหลดไม่ได้ ตารางจะยังใช้งานได้ในโหมดเลื่อนแนวนอน (CSS จัดการเอง)
   ========================================================================== */
(function () {
    'use strict';

    var STORE_KEY = 'rtViewMode';           // 'cards' | 'table'
    var MOBILE_MAX = 820;

    function isMobile() {
        return window.matchMedia
            ? window.matchMedia('(max-width: ' + MOBILE_MAX + 'px)').matches
            : (document.documentElement.clientWidth <= MOBILE_MAX);
    }

    function readMode() {
        try { return localStorage.getItem(STORE_KEY) || 'cards'; }
        catch (e) { return 'cards'; }        // โหมดส่วนตัว/ปิดที่เก็บข้อมูล
    }

    function saveMode(mode) {
        try { localStorage.setItem(STORE_KEY, mode); } catch (e) { }
    }

    function textOf(el) {
        var t = el.innerText;
        if (t === undefined || t === null) t = el.textContent || '';
        return t.replace(/\s+/g, ' ').trim();
    }

    /** ครอบตารางด้วยกล่องเลื่อน ถ้ายังไม่มี */
    function wrap(table) {
        var parent = table.parentNode;
        if (parent && parent.classList && parent.classList.contains('rt-wrap')) return parent;

        var box = document.createElement('div');
        box.className = 'rt-wrap';
        parent.insertBefore(box, table);
        box.appendChild(table);
        return box;
    }

    /** ใส่ชื่อคอลัมน์ให้ทุกช่อง */
    function label(table) {
        var rows = table.rows;
        if (!rows || !rows.length) return false;

        // แถวหัวตาราง = แถวแรกที่มี th (GridView ไม่ได้ออก <thead> เสมอไป)
        var headRow = null, labels = [];
        for (var i = 0; i < rows.length && i < 5; i++) {
            if (rows[i].getElementsByTagName('th').length > 0) { headRow = rows[i]; break; }
        }
        if (!headRow) return false;

        headRow.classList.add('rt-head');
        var ths = headRow.cells;
        for (var c = 0; c < ths.length; c++) labels.push(textOf(ths[c]));

        for (var r = 0; r < rows.length; r++) {
            var row = rows[r];
            if (row === headRow) continue;

            var cells = row.cells;
            if (!cells.length) continue;

            // แถวแบ่งหน้า / แถว "ไม่มีข้อมูล" — ช่องเดียวกินทั้งแถว
            if (cells.length === 1 && cells[0].colSpan > 1) {
                cells[0].classList.add('rt-full');
                continue;
            }

            for (var k = 0; k < cells.length; k++) {
                var cell = cells[k];
                var name = labels[k] || '';
                var val = textOf(cell);

                // ช่องแรกมักเป็นปุ่ม/สถานะ ไม่ต้องมีป้าย ให้เต็มความกว้างสวยกว่า
                if (k === 0 && name === '') { cell.classList.add('rt-full'); continue; }

                if (val === '' && cell.getElementsByTagName('input').length === 0
                                && cell.getElementsByTagName('img').length === 0
                                && cell.getElementsByTagName('a').length === 0) {
                    cell.classList.add('rt-empty');
                    continue;
                }
                if (name !== '') cell.setAttribute('data-label', name);
            }
        }
        return true;
    }

    function applyMode(boxes, mode) {
        for (var i = 0; i < boxes.length; i++) {
            if (mode === 'cards') boxes[i].classList.add('rt-cards');
            else boxes[i].classList.remove('rt-cards');
        }
    }

    function addToggle(box, boxes) {
        // ปุ่มเดียวคุมทุกตารางในหน้า — วางไว้เหนือตารางแรก
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'rt-toggle';
        box.parentNode.insertBefore(btn, box);

        function paint() {
            btn.textContent = readMode() === 'cards'
                ? '🔁 สลับเป็นตารางเต็ม (เลื่อนแนวนอน)'
                : '🔁 สลับเป็นมุมมองการ์ด (อ่านง่ายบนมือถือ)';
        }

        btn.addEventListener('click', function () {
            var next = readMode() === 'cards' ? 'table' : 'cards';
            saveMode(next);
            applyMode(boxes, next);
            paint();
        });

        paint();
    }

    function init() {
        var tables = document.querySelectorAll('table.rt-table');
        if (!tables.length) return;

        var boxes = [];
        for (var i = 0; i < tables.length; i++) {
            var box = wrap(tables[i]);
            if (label(tables[i])) boxes.push(box);
        }
        if (!boxes.length) return;

        applyMode(boxes, isMobile() ? readMode() : 'table');
        addToggle(boxes[0], boxes);

        // หมุนจอ / ย่อขยายหน้าต่าง — กลับไปใช้ตารางเต็มเมื่อจอกว้างพอ
        window.addEventListener('resize', function () {
            applyMode(boxes, isMobile() ? readMode() : 'table');
        });
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', init);
    else
        init();
})();
