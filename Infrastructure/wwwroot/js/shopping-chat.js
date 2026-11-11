(() => {
    'use strict';
    const widget = document.querySelector('#xm-shopping-chat');
    if (!widget) return;
    const launch = widget.querySelector('#xm-chat-launch'), panel = widget.querySelector('#xm-chat-panel');
    const messages = widget.querySelector('#xm-chat-messages'), form = widget.querySelector('#xm-chat-form');
    const input = widget.querySelector('#xm-chat-input'), send = widget.querySelector('#xm-chat-send');
    const reset = widget.querySelector('#xm-chat-reset'), error = widget.querySelector('#xm-chat-error');
    const status = widget.querySelector('#xm-chat-status');
    let loaded = false, busy = false, available = false;
    const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value);
    const request = async (method, body) => {
        const response = await fetch('/api/xmode/chat', { method, credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': document.querySelector('#xm-antiforgery input')?.value || '' },
            ...(body ? { body: JSON.stringify(body) } : {}) });
        if (!response.ok) {
            let text = response.status === 429 ? 'Bạn đã gửi nhiều câu hỏi. Vui lòng thử lại sau một lát.' : 'Chưa thể kết nối tư vấn. Bạn vui lòng thử lại.';
            try { text = (await response.json()).message || text; } catch {}
            throw new Error(text);
        }
        return response.status === 204 ? null : response.json();
    };
    const scroll = () => { messages.scrollTop = messages.scrollHeight; };
    const showError = text => { error.textContent = text; error.hidden = false; };
    function imageUrl(raw) {
        if (!raw) return '/images/xmode/fallback.svg';
        try { const url = new URL(raw, location.origin); return url.protocol === 'https:' || url.origin === location.origin ? url.href : '/images/xmode/fallback.svg'; }
        catch { return '/images/xmode/fallback.svg'; }
    }
    function append(role, text, products = []) {
        const turn = document.createElement('div'); turn.className = 'xm-chat-turn'; turn.dataset.role = role;
        const author = document.createElement('span'); author.className = 'xm-chat-author'; author.textContent = role === 'user' ? 'BẠN' : 'XMODE';
        const paragraph = document.createElement('p'); paragraph.textContent = text; turn.append(author, paragraph);
        if (products.length) {
            const list = document.createElement('div'); list.className = 'xm-chat-products';
            for (const product of products) {
                if (!Number.isInteger(product.id) || product.id <= 0) continue;
                const card = document.createElement('article'); card.className = 'xm-chat-product';
                const img = document.createElement('img'); img.src = imageUrl(product.imageUrl); img.alt = product.name;
                img.addEventListener('error', () => { if (!img.src.endsWith('/fallback.svg')) img.src = '/images/xmode/fallback.svg'; });
                const detail = document.createElement('div'), title = document.createElement('h3');
                const price = document.createElement('p'), variants = document.createElement('small'), link = document.createElement('a');
                title.textContent = product.name; price.textContent = money(product.price);
                const inStock = product.variants.filter(v => v.stock > 0);
                variants.textContent = inStock.length ? inStock.slice(0, 5).map(v => v.color + ' / ' + v.size).join(' · ') : 'Tạm hết hàng';
                link.href = '/xmode/product/' + product.id; link.textContent = 'Xem sản phẩm ↗';
                detail.append(title, price, variants, link); card.append(img, detail); list.append(card);
            }
            turn.append(list);
        }
        messages.append(turn); scroll(); return turn;
    }
    function greeting() {
        messages.replaceChildren();
        append('assistant', 'Chào bạn, mình là trợ lý AI của Xmode. Bạn đang tìm đồ cho dịp nào, và khoảng ngân sách bao nhiêu?');
    }
    async function load() {
        if (loaded) return;
        status.textContent = 'Đang kết nối…';
        try {
            const state = await request('GET'); available = state.available; loaded = true; greeting();
            if (state.turns.length) {
                messages.replaceChildren();
                for (const turn of state.turns) append(turn.role, turn.content, turn.reply?.products || []);
            }
            status.textContent = available ? 'Trợ lý mua sắm AI' : 'Tư vấn tạm thời chưa kết nối';
        } catch (e) { greeting(); showError(e.message); status.textContent = 'Chưa kết nối'; }
    }
    async function open() { panel.hidden = false; launch.setAttribute('aria-expanded', 'true'); await load(); input.focus(); }
    function close() { panel.hidden = true; launch.setAttribute('aria-expanded', 'false'); launch.focus(); }
    launch.onclick = () => panel.hidden ? open() : close();
    widget.querySelector('#xm-chat-close').onclick = close;
    widget.addEventListener('keydown', e => { if (e.key === 'Escape' && !panel.hidden) { e.preventDefault(); close(); } });
    input.addEventListener('keydown', e => { if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); form.requestSubmit(); } });
    form.onsubmit = async e => {
        e.preventDefault(); if (busy || !input.value.trim()) return;
        if (!loaded) { await load(); if (!loaded) return; }
        const message = input.value.trim(); error.hidden = true;
        busy = true; send.disabled = true; reset.disabled = true; input.readOnly = true;
        const question = append('user', message), pending = document.createElement('div');
        pending.className = 'xm-chat-loading'; pending.setAttribute('role', 'status'); pending.textContent = 'Xmode đang chọn gợi ý cho bạn…';
        messages.append(pending); scroll();
        try {
            const match = location.pathname.match(/^\/xmode\/product\/(\d+)$/);
            const reply = await request('POST', { message, productId: match ? Number(match[1]) : null });
            append('assistant', reply.message, reply.products); input.value = '';
        } catch (e) { question.remove(); showError(e.message); }
        finally { pending.remove(); busy = false; send.disabled = false; reset.disabled = false; input.readOnly = false; input.focus(); scroll(); }
    };
    reset.onclick = async () => {
        if (busy) return; error.hidden = true;
        try { await request('DELETE'); greeting(); input.value = ''; input.focus(); } catch (e) { showError(e.message); }
    };
    widget.querySelectorAll('[data-chat-question]').forEach(button => {
        button.onclick = () => { if (busy) return; input.value = button.dataset.chatQuestion; form.requestSubmit(); };
    });
})();
