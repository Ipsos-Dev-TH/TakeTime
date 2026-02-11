// TakeTime API Client
const API = {
    baseUrl: '',

    getHeaders() {
        const headers = { 'Content-Type': 'application/json' };
        const token = localStorage.getItem('token');
        const tenantId = localStorage.getItem('tenantId');
        if (token) headers['Authorization'] = `Bearer ${token}`;
        if (tenantId) headers['X-Tenant-Id'] = tenantId;
        return headers;
    },

    async request(method, url, body = null) {
        const options = { method, headers: this.getHeaders() };
        if (body) options.body = JSON.stringify(body);

        const response = await fetch(this.baseUrl + url, options);

        if (response.status === 401) {
            localStorage.clear();
            window.location.href = '/login.html';
            return;
        }

        const data = await response.json().catch(() => null);
        return { ok: response.ok, status: response.status, data };
    },

    get(url) { return this.request('GET', url); },
    post(url, body) { return this.request('POST', url, body); },
    put(url, body) { return this.request('PUT', url, body); },
    delete(url) { return this.request('DELETE', url); },

    // Service-specific endpoints
    reservations: {
        list: (q = '') => API.get(`/api/v1/reservations${q}`),
        get: (id) => API.get(`/api/v1/reservations/${id}`),
        create: (data) => API.post('/api/v1/reservations', data),
        cancel: (id) => API.put(`/api/v1/reservations/${id}/cancel`),
        checkIn: (id) => API.put(`/api/v1/reservations/${id}/check-in`),
        checkOut: (id) => API.put(`/api/v1/reservations/${id}/check-out`),
    },
    rooms: {
        list: () => API.get('/api/v1/accommodations'),
        get: (id) => API.get(`/api/v1/accommodations/${id}`),
        create: (data) => API.post('/api/v1/accommodations', data),
        update: (id, data) => API.put(`/api/v1/accommodations/${id}`, data),
        availability: (from, to) => API.get(`/api/v1/accommodations/available?from=${from}&to=${to}`),
    },
    rates: {
        list: () => API.get('/api/v1/rates'),
        create: (data) => API.post('/api/v1/rates', data),
        promotions: () => API.get('/api/v1/rates/promotions'),
    },
    payments: {
        list: (q = '') => API.get(`/api/v1/payments${q}`),
        get: (id) => API.get(`/api/v1/payments/${id}`),
        process: (data) => API.post('/api/v1/payments', data),
        refund: (id) => API.put(`/api/v1/payments/${id}/refund`),
        dailySummary: () => API.get('/api/v1/payments/daily-summary'),
    },
    inventory: {
        list: () => API.get('/api/v1/products'),
        get: (id) => API.get(`/api/v1/products/${id}`),
        create: (data) => API.post('/api/v1/products', data),
        update: (id, data) => API.put(`/api/v1/products/${id}`, data),
        categories: () => API.get('/api/v1/products/categories'),
    },
    housekeeping: {
        list: (q = '') => API.get(`/api/v1/housekeeping${q}`),
        create: (data) => API.post('/api/v1/housekeeping', data),
        assign: (id, data) => API.put(`/api/v1/housekeeping/${id}/assign`, data),
        complete: (id, data) => API.put(`/api/v1/housekeeping/${id}/complete`, data),
        today: () => API.get('/api/v1/housekeeping/today'),
    },
    roomservice: {
        list: (q = '') => API.get(`/api/v1/roomservice${q}`),
        create: (data) => API.post('/api/v1/roomservice', data),
        accept: (id) => API.put(`/api/v1/roomservice/${id}/accept`),
        deliver: (id) => API.put(`/api/v1/roomservice/${id}/deliver`),
    },
    maintenance: {
        list: (q = '') => API.get(`/api/v1/maintenance${q}`),
        report: (data) => API.post('/api/v1/maintenance', data),
        assign: (id, data) => API.put(`/api/v1/maintenance/${id}/assign`, data),
        complete: (id, data) => API.put(`/api/v1/maintenance/${id}/complete`, data),
        pending: () => API.get('/api/v1/maintenance/pending'),
    },
    customers: {
        list: (q = '') => API.get(`/api/v1/customers${q}`),
        get: (id) => API.get(`/api/v1/customers/${id}`),
        create: (data) => API.post('/api/v1/customers', data),
    },
    channels: {
        list: () => API.get('/api/v1/channels'),
        connect: (data) => API.post('/api/v1/channels', data),
    },
    affiliates: {
        list: () => API.get('/api/v1/affiliates'),
        get: (id) => API.get(`/api/v1/affiliates/${id}`),
        create: (data) => API.post('/api/v1/affiliates', data),
    },
    hr: {
        list: (q = '') => API.get(`/api/v1/employees${q}`),
        get: (id) => API.get(`/api/v1/employees/${id}`),
        create: (data) => API.post('/api/v1/employees', data),
    },
    accounting: {
        revenue: (q = '') => API.get(`/api/v1/reports/revenue${q}`),
        profitLoss: (q = '') => API.get(`/api/v1/reports/profit-loss${q}`),
        occupancy: (q = '') => API.get(`/api/v1/reports/occupancy${q}`),
        taxReport: (q = '') => API.get(`/api/v1/reports/tax-report${q}`),
    },
    notifications: {
        list: () => API.get('/api/v1/notifications'),
        send: (data) => API.post('/api/v1/notifications', data),
        templates: () => API.get('/api/v1/notifications/templates'),
    },
    admin: {
        dashboard: () => API.get('/api/v1/admin/dashboard/overview'),
        health: () => API.get('/api/v1/admin/dashboard/health'),
        tenants: () => API.get('/api/v1/admin/tenants'),
        users: () => API.get('/api/v1/admin/users'),
        features: () => API.get('/api/v1/admin/features'),
        featureMenu: (role) => API.get(`/api/v1/admin/features/menu${role ? '?role=' + role : ''}`),
    }
};
