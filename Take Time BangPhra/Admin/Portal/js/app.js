// TakeTime Admin Portal - Main App
function adminApp() {
    return {
        currentPage: 'dashboard',
        pageTitle: 'แดชบอร์ด',
        sidebarOpen: false,
        user: null,
        tenantId: '',
        currentTime: '',

        init() {
            // Check auth
            const token = localStorage.getItem('token');
            if (!token) {
                window.location.href = 'login.html';
                return;
            }

            this.user = JSON.parse(localStorage.getItem('user') || '{}');
            this.tenantId = localStorage.getItem('tenantId') || '';

            // Clock
            this.updateTime();
            setInterval(() => this.updateTime(), 60000);

            // Load page from hash
            const hash = window.location.hash.replace('#', '');
            if (hash && Pages[hash]) {
                this.currentPage = hash;
            }

            this.loadPage();

            // Handle back/forward
            window.addEventListener('hashchange', () => {
                const h = window.location.hash.replace('#', '');
                if (h && Pages[h]) {
                    this.currentPage = h;
                    this.loadPage();
                }
            });

            // Make accessible globally
            window.adminApp = this;
        },

        updateTime() {
            const now = new Date();
            this.currentTime = now.toLocaleString('th-TH', {
                weekday: 'short', day: 'numeric', month: 'short',
                hour: '2-digit', minute: '2-digit'
            });
        },

        navigate(page) {
            this.currentPage = page;
            this.sidebarOpen = false;
            window.location.hash = page;
            this.loadPage();
        },

        async loadPage() {
            const page = Pages[this.currentPage];
            if (!page) return;

            this.pageTitle = page.title;
            const content = document.getElementById('page-content');
            content.innerHTML = loadingSkeleton(6);

            try {
                const html = await page.render();
                content.innerHTML = html;
            } catch (e) {
                console.error('Page load error:', e);
                content.innerHTML = `<div class="text-center py-12">
                    <svg class="w-16 h-16 mx-auto text-gray-300 mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>
                    <p class="text-gray-500">ไม่สามารถโหลดหน้านี้ได้</p>
                    <p class="text-sm text-gray-400 mt-1">${e.message}</p>
                </div>`;
            }
        },

        logout() {
            localStorage.clear();
            window.location.href = '/login.html';
        },
    };
}
