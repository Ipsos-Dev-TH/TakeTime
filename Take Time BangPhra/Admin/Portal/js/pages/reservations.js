const Pages = window.Pages || {};
Pages.reservations = {
    title: 'การจองห้องพัก',
    async render() {
        const header = renderSectionHeader('รายการจอง', `
            <select class="form-input w-auto text-sm" onchange="Pages.reservations.filterStatus(this.value)">
                <option value="">ทุกสถานะ</option>
                <option value="Pending">รอยืนยัน</option>
                <option value="Confirmed">ยืนยันแล้ว</option>
                <option value="CheckedIn">เช็คอินแล้ว</option>
                <option value="CheckedOut">เช็คเอาท์แล้ว</option>
                <option value="Cancelled">ยกเลิก</option>
            </select>
            <input type="date" class="form-input w-auto text-sm" value="${new Date().toISOString().split('T')[0]}">
            <button class="btn btn-primary btn-sm" onclick="Pages.reservations.create()">+ สร้างการจอง</button>
        `);

        const demoData = [
            { id: 'RES-001', guestName: 'คุณสมชาย ใจดี', phone: '081-234-5678', room: 'Deluxe 201', roomType: 'Deluxe', checkIn: '2026-02-11', checkOut: '2026-02-13', nights: 2, total: 4500, status: 'Confirmed', source: 'Direct' },
            { id: 'RES-002', guestName: 'John Smith', phone: '+1-555-1234', room: 'Suite 301', roomType: 'Suite', checkIn: '2026-02-11', checkOut: '2026-02-14', nights: 3, total: 12000, status: 'CheckedIn', source: 'Booking.com' },
            { id: 'RES-003', guestName: 'คุณวิภา แสงทอง', phone: '089-876-5432', room: 'Standard 105', roomType: 'Standard', checkIn: '2026-02-12', checkOut: '2026-02-13', nights: 1, total: 1500, status: 'Pending', source: 'Agoda' },
            { id: 'RES-004', guestName: 'Tanaka Yuki', phone: '+81-90-1234', room: 'Deluxe 205', roomType: 'Deluxe', checkIn: '2026-02-10', checkOut: '2026-02-11', nights: 1, total: 2500, status: 'CheckedOut', source: 'Direct' },
            { id: 'RES-005', guestName: 'คุณปรีชา ศรีสุข', phone: '062-111-2222', room: 'Family 401', roomType: 'Family', checkIn: '2026-02-13', checkOut: '2026-02-15', nights: 2, total: 6000, status: 'Confirmed', source: 'LINE' },
        ];

        const table = renderTable(
            [
                { label: 'รหัส', key: 'id' },
                { label: 'ชื่อแขก', render: r => `<div><p class="font-medium">${r.guestName}</p><p class="text-xs text-gray-400">${r.phone}</p></div>` },
                { label: 'ห้อง', render: r => `<div><p>${r.room}</p><p class="text-xs text-gray-400">${r.roomType}</p></div>` },
                { label: 'เช็คอิน', render: r => formatDate(r.checkIn) },
                { label: 'เช็คเอาท์', render: r => formatDate(r.checkOut) },
                { label: 'คืน', key: 'nights' },
                { label: 'ยอดรวม', render: r => formatMoney(r.total) },
                { label: 'ช่องทาง', render: r => badge(r.source, 'blue') },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
            ],
            demoData,
            { actions: (r) => {
                let btns = '';
                if (r.status === 'Confirmed') btns += `<button class="btn btn-sm btn-success" onclick="Pages.reservations.checkIn('${r.id}')">เช็คอิน</button> `;
                if (r.status === 'CheckedIn') btns += `<button class="btn btn-sm btn-secondary" onclick="Pages.reservations.checkOut('${r.id}')">เช็คเอาท์</button> `;
                if (r.status === 'Pending') btns += `<button class="btn btn-sm btn-primary" onclick="Pages.reservations.confirm('${r.id}')">ยืนยัน</button> `;
                btns += `<button class="btn btn-sm btn-secondary" onclick="Pages.reservations.view('${r.id}')">ดู</button>`;
                return btns;
            }}
        );

        return header + renderCard('รายการจอง', table);
    },
    create() {
        formModal('สร้างการจองใหม่', [
            { name: 'guestName', label: 'ชื่อแขก', required: true, placeholder: 'ชื่อ-นามสกุล' },
            { name: 'phone', label: 'เบอร์โทร', required: true, placeholder: '08x-xxx-xxxx' },
            { name: 'email', label: 'อีเมล', type: 'email', placeholder: 'email@example.com' },
            { name: 'roomType', label: 'ประเภทห้อง', type: 'select', options: [
                { value: 'Standard', label: 'Standard' }, { value: 'Deluxe', label: 'Deluxe' },
                { value: 'Suite', label: 'Suite' }, { value: 'Family', label: 'Family' },
            ]},
            { name: 'checkIn', label: 'วันเช็คอิน', type: 'date', required: true },
            { name: 'checkOut', label: 'วันเช็คเอาท์', type: 'date', required: true },
        ], async (data) => { alert('สร้างการจองสำเร็จ (Demo)'); });
    },
    view(id) { showModal('รายละเอียดการจอง ' + id, '<p class="text-gray-500">รอเชื่อมต่อ API เพื่อดึงข้อมูลจริง</p>'); },
    checkIn(id) { confirmDialog('เช็คอิน', `ยืนยันเช็คอิน ${id}?`, () => alert('เช็คอินสำเร็จ (Demo)')); },
    checkOut(id) { confirmDialog('เช็คเอาท์', `ยืนยันเช็คเอาท์ ${id}?`, () => alert('เช็คเอาท์สำเร็จ (Demo)')); },
    confirm(id) { alert('ยืนยันการจองสำเร็จ (Demo)'); },
    filterStatus(status) { /* TODO: re-render with filter */ },
};
window.Pages = Pages;
