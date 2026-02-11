const Pages = window.Pages || {};
Pages.customers = {
    title: 'ลูกค้า & สมาชิก',
    async render() {
        const stats = renderStats([
            { label: 'ลูกค้าทั้งหมด', value: '1,245', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/></svg>' },
            { label: 'สมาชิก', value: '328', color: 'purple', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z"/></svg>' },
            { label: 'ลูกค้าใหม่เดือนนี้', value: '42', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"/></svg>' },
            { label: 'รีวิวเฉลี่ย', value: '4.6/5', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11.049 2.927c.3-.921 1.603-.921 1.902 0l1.519 4.674a1 1 0 00.95.69h4.915c.969 0 1.371 1.24.588 1.81l-3.976 2.888a1 1 0 00-.363 1.118l1.518 4.674c.3.922-.755 1.688-1.538 1.118l-3.976-2.888a1 1 0 00-1.176 0l-3.976 2.888c-.783.57-1.838-.197-1.538-1.118l1.518-4.674a1 1 0 00-.363-1.118l-3.976-2.888c-.784-.57-.38-1.81.588-1.81h4.914a1 1 0 00.951-.69l1.519-4.674z"/></svg>' },
        ]);
        const customers = [
            { id: 1, name: 'คุณสมชาย ใจดี', email: 'somchai@email.com', phone: '081-234-5678', tier: 'Gold', visits: 12, lastVisit: '2026-02-11', totalSpent: 156000 },
            { id: 2, name: 'John Smith', email: 'john@email.com', phone: '+1-555-1234', tier: 'Silver', visits: 3, lastVisit: '2026-02-11', totalSpent: 45000 },
            { id: 3, name: 'คุณวิภา แสงทอง', email: 'vipa@email.com', phone: '089-876-5432', tier: 'Standard', visits: 1, lastVisit: '2026-02-12', totalSpent: 1500 },
            { id: 4, name: 'Tanaka Yuki', email: 'tanaka@email.co.jp', phone: '+81-90-1234', tier: 'Platinum', visits: 25, lastVisit: '2026-02-10', totalSpent: 450000 },
            { id: 5, name: 'คุณปรีชา ศรีสุข', email: 'preecha@email.com', phone: '062-111-2222', tier: 'Gold', visits: 8, lastVisit: '2026-02-13', totalSpent: 98000 },
        ];
        const table = renderTable(
            [
                { label: 'ชื่อ', render: r => `<div><p class="font-medium">${r.name}</p><p class="text-xs text-gray-400">${r.email}</p></div>` },
                { label: 'โทร', key: 'phone' },
                { label: 'ระดับ', render: r => { const c = { Platinum:'purple', Gold:'yellow', Silver:'blue', Standard:'gray' }; return badge(r.tier, c[r.tier]); }},
                { label: 'เข้าพัก', render: r => `${r.visits} ครั้ง` },
                { label: 'ล่าสุด', render: r => formatDate(r.lastVisit) },
                { label: 'ยอดสะสม', render: r => formatMoney(r.totalSpent) },
            ],
            customers,
            { actions: (r) => `<button class="btn btn-sm btn-secondary" onclick="Pages.customers.view(${r.id})">ดู</button>` }
        );
        return stats +
            renderCard('รายชื่อลูกค้า', `
                <div class="mb-4 flex gap-2">
                    <input type="text" class="form-input" placeholder="ค้นหาชื่อ, อีเมล, เบอร์โทร..." style="max-width:300px">
                    <select class="form-input w-auto"><option>ทุกระดับ</option><option>Platinum</option><option>Gold</option><option>Silver</option><option>Standard</option></select>
                </div>
                ${table}
            `, '<button class="btn btn-sm btn-primary" onclick="Pages.customers.add()">+ เพิ่มลูกค้า</button>');
    },
    add() {
        formModal('เพิ่มลูกค้า', [
            { name: 'name', label: 'ชื่อ-นามสกุล', required: true },
            { name: 'email', label: 'อีเมล', type: 'email' },
            { name: 'phone', label: 'เบอร์โทร', required: true },
        ], async (data) => { alert('เพิ่มลูกค้าสำเร็จ (Demo)'); });
    },
    view(id) { showModal('ข้อมูลลูกค้า', '<p class="text-gray-500">ประวัติการเข้าพัก, คะแนนสะสม, ข้อมูลส่วนตัว</p>'); },
};
window.Pages = Pages;
