(() => {
    'use strict';
    const app = document.querySelector('#xmode-app');
    const adminTopbar = document.querySelector('.xm-admin .xm-topbar');
    if (adminTopbar) {
        const measure = () => document.documentElement.style.setProperty('--xm-admin-header-height', Math.ceil(adminTopbar.getBoundingClientRect().height) + 'px');
        new ResizeObserver(measure).observe(adminTopbar); measure();
    }
    const dialog = document.querySelector('#xm-dialog');
    const content = document.querySelector('#xm-dialog-content');
    const token = document.querySelector('#xm-antiforgery input')?.value;
    const esc = v => String(v ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const money = v => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(v || 0);
    const safeImage = url => { try { if (!url) return '/images/xmode/fallback.svg'; const u = new URL(url, location.origin); return u.protocol === 'https:' || u.origin === location.origin ? esc(u.href) : '/images/xmode/fallback.svg'; } catch { return '/images/xmode/fallback.svg'; } };
    const localLink = url => typeof url === 'string' && url.startsWith('/') && !url.startsWith('//') ? esc(url) : '/xmode/catalog';
    const api = async (path, options = {}) => {
        const headers = { 'X-CSRF-TOKEN': token, ...options.headers };
        if (options.body && !(options.body instanceof FormData)) { headers['Content-Type'] = 'application/json'; options.body = JSON.stringify(options.body); }
        const response = await fetch('/api/xmode' + path, { ...options, headers, credentials: 'same-origin' });
        if (!response.ok) { let msg = `Yêu cầu thất bại (${response.status}).`; try { const body = await response.json(); msg = body.message || body.title || msg; } catch {} throw new Error(msg); }
        return response.status === 204 ? null : response.json();
    };
    const notice = message => { const box = document.querySelector('#xm-notice'); if (box) { box.textContent = message; box.hidden = false; } };
    const fail = error => notice(error.message || 'Không thể xử lý yêu cầu.');
    const modal = html => { content.innerHTML = html; dialog.showModal(); };
    const close = () => dialog.close();
    const input = (name, label, value = '', type = 'text', attrs = '') => `<label>${esc(label)}<input name="${name}" type="${type}" value="${esc(value)}" ${attrs}></label>`;
    const area = (name, label, value = '') => `<label>${esc(label)}<textarea name="${name}">${esc(value)}</textarea></label>`;
    const check = (name, label, checked) => `<label><input type="checkbox" name="${name}" ${checked ? 'checked' : ''}> ${esc(label)}</label>`;
    const data = form => Object.fromEntries(new FormData(form).entries());
    const empty = (title, message = '') => `<div class="xm-empty"><h2>${esc(title)}</h2>${message ? `<p>${esc(message)}</p>` : ''}</div>`;
    const chartHtml = chart => chart ? `<h3>${esc(chart.name)}</h3><p>${esc(chart.instructions)}</p><div class="xm-table-wrap"><table><thead><tr><th>Size</th><th>Ngực (cm)</th><th>Eo (cm)</th><th>Hông (cm)</th></tr></thead><tbody>${chart.rows.sort((a,b) => a.sortOrder-b.sortOrder).map(r => `<tr><td>${esc(r.size)}</td><td>${esc(r.chest)}</td><td>${esc(r.waist)}</td><td>${esc(r.hip)}</td></tr>`).join('')}</tbody></table></div>` : '<p>Chưa có hướng dẫn chọn size.</p>';
    const productImage = p => safeImage(p.images?.[0]?.url);
    const card = p => `<article class="xm-card"><a class="xm-card-image" href="/xmode/product/${p.id}"><img src="${productImage(p)}" alt="${esc(p.name)}" loading="lazy"><span class="xm-tag">${!p.variants.some(v => v.isActive && v.stock > 0) ? 'Hết hàng' : p.isNew ? 'Mới' : p.oldPrice ? 'Ưu đãi' : ''}</span><span class="xm-card-arrow">↗</span></a><div class="xm-card-meta">${esc(p.audience)}</div><a href="/xmode/product/${p.id}">${esc(p.name)}</a><div class="xm-price">${money(p.price)} ${p.oldPrice ? `<del>${money(p.oldPrice)}</del>` : ''}</div><div class="xm-swatches">${[...new Map(p.variants.filter(v=>v.isActive).map(v=>[v.color,v])).values()].map(v=>`<span style="background:${/^#[\da-f]{6}$/i.test(v.hex) ? v.hex : '#aaa'}" title="${esc(v.color)}"></span>`).join('')}</div></article>`;
    const grid = list => `<div class="xm-grid">${list.map(card).join('')}</div>`;
    const updateCount = async () => { try { const cart = await api('/cart'); document.querySelectorAll('[data-cart-count]').forEach(x => x.textContent = cart.items.reduce((n, i) => n + i.quantity, 0)); } catch { /* Other pages remain usable when cart is unavailable. */ } };
    const sectionHtml = s => s ? `<section class="${s.slot === 'hero' ? 'xm-hero' : 'xm-editorial'}">${s.imageUrl ? `<img src="${safeImage(s.imageUrl)}" alt="${esc(s.title)}">` : ''}<div class="${s.slot === 'hero' ? 'xm-hero-copy' : ''}"><p class="xm-eyebrow">${esc(s.subtitle)}</p><h2>${esc(s.title)}</h2><p>${esc(s.body)}</p>${s.linkText ? `<a class="xm-button" href="${localLink(s.linkUrl)}">${esc(s.linkText)} ↗</a>` : ''}</div></section>` : '';
    async function home() {
        const h = await api('/home');
        const displayed = h.newProducts.length ? h.newProducts : h.recentProducts;
        app.innerHTML = `${sectionHtml(h.sections.find(s => s.slot === 'hero'))}<section class="xm-section"><div class="xm-section-heading"><h1>Khám phá Xmode.</h1><a href="/xmode/catalog">Tất cả sản phẩm ↗</a></div>${h.categories.length ? `<div class="xm-category-grid">${h.categories.map(c => `<a href="/xmode/catalog?category=${c.id}">${c.imageUrl ? `<img src="${safeImage(c.imageUrl)}" alt="${esc(c.name)}">` : ''}<span>${esc(c.name)} ↗</span></a>`).join('')}</div>` : ''}</section><section class="xm-section"><h2>${h.newProducts.length ? 'Mới trong tủ đồ.' : 'Sản phẩm.'}</h2>${displayed.length ? grid(displayed) : empty('Chưa có sản phẩm nào')}</section>${sectionHtml(h.sections.find(s => s.slot === 'editorial'))}${h.featured.length ? `<section class="xm-section"><h2>Thiết kế nổi bật.</h2>${grid(h.featured)}</section>` : ''}`;
    }
    async function catalog() {
        const q = new URLSearchParams(location.search); const [result, cats, filters] = await Promise.all([api('/products?' + q), api('/categories'), api('/filters')]);
        const select = (name, label, items) => `<label>${label}<select name="${name}"><option value="">Tất cả</option>${items.map(([v,t]) => `<option value="${esc(v)}" ${q.get(name) === String(v) ? 'selected' : ''}>${esc(t)}</option>`).join('')}</select></label>`;
        const active = [...q.keys()].some(k => !['page','search'].includes(k) && q.get(k));
        app.innerHTML = `<section class="xm-section"><p class="xm-eyebrow">XMODE / COLLECTION</p><h1>Bộ sưu tập.</h1><form id="xm-filters" class="xm-filters" method="get" action="/xmode/catalog"><label>Tìm sản phẩm<input type="search" name="q" value="${esc(q.get('q'))}" placeholder="Tên sản phẩm, chất liệu…"></label><div id="xm-filter-fields">${select('audience','Dành cho',['Nam','Nữ','Unisex'].map(v=>[v,v]))}${select('category','Danh mục',cats.map(c=>[c.id,c.name]))}${select('size','Kích cỡ',filters.sizes.map(v=>[v,v]))}${select('color','Màu sắc',filters.colors.map(v=>[v,v]))}${input('min','Giá từ',q.get('min'),'number','min="0"')}${input('max','Đến',q.get('max'),'number','min="0"')}${select('sort','Sắp xếp',[['new','Hàng mới'],['asc','Giá tăng dần'],['desc','Giá giảm dần']])}${q.get('new') === '1' ? '<input type="hidden" name="new" value="1">' : ''}<button class="xm-button">Áp dụng</button><a href="/xmode/catalog">Xóa bộ lọc</a></div></form><div class="xm-result-count">${result.total} sản phẩm</div>${result.items.length ? grid(result.items) : empty(active ? 'Chưa tìm thấy sản phẩm phù hợp.' : 'Chưa có sản phẩm nào')}${result.total > result.pageSize ? `<nav class="xm-pagination" aria-label="Phân trang">${Array.from({length:Math.ceil(result.total/result.pageSize)},(_,i)=>{const p=new URLSearchParams(q);p.set('page',i+1);return `<a ${result.page===i+1?'aria-current="page"':''} href="?${esc(p)}">${i+1}</a>`}).join('')}</nav>` : ''}</section>`;
    }
    async function detail(id) {
        let p; try { p = await api('/products/' + encodeURIComponent(id)); } catch (e) { if (e.message.includes('404')) { app.innerHTML=empty('Không tìm thấy sản phẩm.'); return; } throw e; }
        const variants = p.variants.filter(v => v.isActive); const colors = [...new Set(variants.map(v => v.color))];
        app.innerHTML = `<section class="xm-section"><div class="xm-breadcrumb"><a href="/xmode/catalog">Bộ sưu tập</a></div><div class="xm-detail"><div class="xm-gallery">${p.images.length ? p.images.map((img,i)=>`<button class="xm-photo" type="button" data-photo="${safeImage(img.url)}" aria-label="Phóng to ảnh ${i+1}"><img src="${safeImage(img.url)}" alt="${esc(img.alt || p.name)}"></button>`).join('') : `<img src="/images/xmode/fallback.svg" alt="${esc(p.name)}">`}</div><div class="xm-product-info"><p class="xm-eyebrow">${esc(p.audience)}</p><h1>${esc(p.name)}</h1><p class="xm-price">${money(p.price)} ${p.oldPrice ? `<del>${money(p.oldPrice)}</del>` : ''}</p><p>${esc(p.description)}</p><form id="xm-add"><label>Màu sắc<select name="color" required><option value="">Chọn màu</option>${colors.map(c=>`<option>${esc(c)}</option>`).join('')}</select></label><label>Kích cỡ<select name="size" required><option value="">Chọn size</option>${[...new Set(variants.map(v=>v.size))].map(s=>`<option>${esc(s)}</option>`).join('')}</select></label>${input('quantity','Số lượng',1,'number','min="1" required')}<p id="xm-stock">Chọn màu và kích cỡ để xem tình trạng.</p><button class="xm-button xm-wide" ${variants.some(v=>v.stock>0)?'':'disabled'}>Thêm vào giỏ hàng ↗</button></form>${p.sizeChartId ? `<button class="xm-text-button" data-size="${p.sizeChartId}">Hướng dẫn chọn size ↗</button>` : ''}<details open><summary>Chất liệu & phom dáng</summary><p>${esc(p.material)} ${esc(p.fit)}</p></details><details><summary>Hướng dẫn bảo quản</summary><p>${esc(p.care)}</p></details></div></div></section>`;
        const form = document.querySelector('#xm-add');
        form.onchange = () => { const v = variants.find(x=>x.color===form.elements.color.value && x.size===form.elements.size.value); document.querySelector('#xm-stock').textContent = v ? (v.stock ? `Còn ${v.stock} sản phẩm` : 'Biến thể hết hàng') : 'Chọn màu và kích cỡ còn hàng.'; };
        form.onsubmit = async e => { e.preventDefault(); const v=variants.find(x=>x.color===form.elements.color.value && x.size===form.elements.size.value); if(!v || v.stock<=0) return notice('Biến thể không còn hàng.'); try { await api('/cart/items',{method:'POST',body:{variantId:v.id,quantity:Number(form.elements.quantity.value)}}); await updateCount(); notice('Đã thêm vào giỏ hàng.'); } catch(err){fail(err);} };
    }
    const totals = cart => `<div class="xm-totals"><h2>Tóm tắt giỏ hàng</h2><p><span>Tạm tính</span><strong>${money(cart.subtotal)}</strong></p>${cart.shippingFee == null ? '' : `<p><span>Giao hàng tiêu chuẩn</span><span>${cart.shippingFee === 0 ? 'Miễn phí' : money(cart.shippingFee)}</span></p>`}<hr><p><span>Tổng thanh toán</span><strong>${money(cart.subtotal + (cart.shippingFee ?? 0))}</strong></p></div>`;
    async function cartPage() {
        const cart = await api('/cart');
        app.innerHTML = `<section class="xm-section"><h1>Giỏ hàng của bạn.</h1>${cart.items.length ? `<div class="xm-checkout"><div>${cart.items.map(i=>`<article class="xm-cart-line"><a href="/xmode/product/${i.productId}"><img src="${safeImage(i.imageUrl)}" alt="${esc(i.productName)}"></a><div><a href="/xmode/product/${i.productId}"><strong>${esc(i.productName)}</strong></a><p>${esc(i.color)} / ${esc(i.size)}</p><p>${money(i.price)} · ${i.available ? `Còn ${i.availableStock}` : 'Ngừng bán'}</p>${input('quantity','Số lượng',i.quantity,'number',`min="1" data-cart-quantity="${i.id}"`)}<button class="xm-text-button" data-cart-remove="${i.id}">Xóa</button>${i.quantity>i.availableStock?'<p>Vui lòng điều chỉnh số lượng.</p>':''}</div><strong>${money(i.price*i.quantity)}</strong></article>`).join('')}</div><aside>${totals(cart)}<a class="xm-button xm-wide" href="/xmode/checkout">Tiếp tục đặt hàng ↗</a></aside></div>` : empty('Giỏ hàng của bạn đang trống.')}</section>`;
    }
    async function checkout() {
        const [cart,methods] = await Promise.all([api('/cart'),api('/payment-methods')]); if (!cart.items.length) return cartPage();
        const valid = cart.items.every(i=>i.available && i.quantity<=i.availableStock);
        app.innerHTML = `<section class="xm-section"><h1>Thông tin nhận hàng.</h1><div class="xm-checkout"><form id="xm-checkout"><h2>Thông tin của bạn</h2>${input('recipientName','Họ và tên','','text','required maxlength="100"')}${input('email','Email','','email','required maxlength="100"')}${input('phone','Số điện thoại','','tel','required maxlength="25"')}${input('address','Địa chỉ nhận hàng','','text','required maxlength="300"')}${input('city','Tỉnh / Thành phố','','text','required maxlength="100"')}${area('note','Ghi chú')}<fieldset><legend>Phương thức thanh toán</legend><div class="xm-payment-options">${methods.filter(m=>m.available).map(m=>`<label class="xm-payment-option"><input type="radio" name="paymentMethod" value="${esc(m.code)}" ${m.code==='COD'?'checked':''} ${m.available?'':'disabled'}><span>${esc(m.name)}<small>${esc(m.message || (m.code==='COD'?'Thanh toán cho đơn vị giao hàng khi nhận hàng.':'Chuyển đến cổng thanh toán để xác nhận. Giữ hàng trong 15 phút.'))}</small></span></label>`).join('')}</div></fieldset>${!valid ? '<p>Vui lòng kiểm tra tồn kho trước khi đặt hàng.</p>' : ''}<button class="xm-button xm-wide" ${valid?'':'disabled'}>Đặt hàng ↗</button></form><aside>${totals(cart)}${cart.items.map(i=>`<p>${esc(i.productName)} · ${esc(i.color)} / ${esc(i.size)} × ${i.quantity}</p>`).join('')}</aside></div></section>`;
        const requestId=crypto.randomUUID(); const form=document.querySelector('#xm-checkout'); let busy=false;
        form.onsubmit=async e=>{e.preventDefault(); if(busy)return; busy=true; form.querySelector('button').disabled=true; try { const result=await api('/orders',{method:'POST',body:{...data(form),requestId,shippingFee:cart.shippingFee ?? 0,lines:cart.items.map(i=>({variantId:i.variantId,quantity:i.quantity,unitPrice:i.price}))}}); await updateCount(); location.href='/xmode/orders/'+result.id+(result.paymentMethod==='COD'?'':'?pay=1'); } catch(err){fail(err); busy=false; form.querySelector('button').disabled=false;} };
    }
    const statusName = s => ({Pending:'Chờ xác nhận',Confirmed:'Đã xác nhận',Shipping:'Đang giao',Completed:'Hoàn tất',Cancelled:'Đã hủy'}[s] || s);
    const paymentName = s => ({Unpaid:'Chưa thanh toán',Pending:'Chờ thanh toán',Paid:'Đã thanh toán',Failed:'Thanh toán không thành công',Expired:'Hết hạn thanh toán',Cancelled:'Đã hủy thanh toán',RefundRequired:'Cần hoàn tiền',Refunded:'Đã hoàn tiền'}[s] || s);
    const canPay = o => o.paymentMethod!=='COD' && o.status==='Pending' && o.paymentStatus==='Pending' && new Date(o.paymentExpiresAt)>new Date();
    async function payOrder(id, button) {
        if(button) button.disabled=true;
        try {
            const result=await api('/orders/'+id+'/payment',{method:'POST'});
            if(result.url) {
                const url=new URL(result.url);
                if(url.protocol!=='https:' || url.username || !['sandbox.vnpayment.vn','pay.vnpay.vn','test-payment.momo.vn','payment.momo.vn'].includes(url.hostname)) throw Error('Liên kết thanh toán không hợp lệ.');
                location.assign(url.href);
            } else { await ordersPage(id); notice(result.message || paymentName(result.status)); }
        } catch(err) { fail(err); if(button)button.disabled=false; }
    }
    function orderDetail(o, admin = false) { return `<h2>Đơn ${esc(o.code)}</h2><p>${esc(statusName(o.status))} · ${money(o.total)}</p><div class="xm-payment-state"><strong>${esc(o.paymentMethod)} · ${esc(paymentName(o.paymentStatus))}</strong>${o.paymentMethod!=='COD' && o.paymentStatus==='Pending'?`<p>Giữ hàng đến ${esc(new Date(o.paymentExpiresAt).toLocaleString('vi-VN'))}. Trạng thái được cập nhật khi nhận xác nhận từ cổng thanh toán.</p>`:''}${o.paymentStatus==='RefundRequired'?'<p>Đơn đã hủy nhưng đã nhận tiền. Cửa hàng cần đối soát và hoàn tiền.</p>':''}${!admin && canPay(o)?`<button class="xm-button" data-pay-order="${o.id}">Thanh toán qua ${esc(o.paymentMethod)}</button>`:''}${!admin && o.paymentMethod!=='COD'?'<button class="xm-text-button" data-refresh-order>Kiểm tra thanh toán</button>':''}</div><p>${esc(o.recipientName)} · ${esc(o.phone)} · ${esc(o.email)}</p><p>${esc(o.address)}, ${esc(o.city)}</p><p>${esc(o.note)}</p><div class="xm-table-wrap"><table><thead><tr><th>Sản phẩm</th><th>Màu / size</th><th>Số lượng</th><th>Đơn giá</th></tr></thead><tbody>${o.items.map(i=>`<tr><td>${esc(i.productName)}</td><td>${esc(i.color)} / ${esc(i.size)}</td><td>${i.quantity}</td><td>${money(i.unitPrice)}</td></tr>`).join('')}</tbody></table></div><p>Giao hàng tiêu chuẩn: ${o.shippingFee === 0 ? 'Miễn phí' : money(o.shippingFee)}</p><h3>Lịch sử trạng thái</h3>${o.history.map(h=>`<p>${esc(statusName(h.status))} · ${esc(new Date(h.createdAt).toLocaleString('vi-VN'))} ${esc(h.reason)}</p>`).join('')}${!admin && o.status==='Pending'?`<button class="xm-button xm-outline" data-cancel-order="${o.id}">Hủy đơn</button>`:''}`; }
    async function ordersPage(id) {
        if(id) {
            const o=await api('/orders/'+id); app.innerHTML=`<section class="xm-section">${orderDetail(o)}<a href="/xmode/orders">Các đơn hàng</a></section>`;
            const payButton=app.querySelector('[data-pay-order]'); if(payButton)payButton.onclick=()=>payOrder(id,payButton);
            const refresh=app.querySelector('[data-refresh-order]'); if(refresh)refresh.onclick=()=>ordersPage(id).catch(fail);
            if(new URLSearchParams(location.search).get('pay')==='1') {
                history.replaceState(null,'',location.pathname);
                if(payButton) await payOrder(id,payButton);
            } else if(o.paymentStatus==='Pending' && location.search.includes('payment=return')) {
                notice('Đang chờ xác nhận từ cổng thanh toán. Bạn có thể kiểm tra lại trạng thái tại đây.');
            }
            return;
        }
        const list=await api('/orders'); app.innerHTML=`<section class="xm-section"><h1>Đơn hàng của bạn.</h1>${list.length?`<div class="xm-table-wrap"><table><thead><tr><th>Mã đơn</th><th>Trạng thái</th><th>Tổng</th><th>Ngày đặt</th></tr></thead><tbody>${list.map(o=>`<tr><td><a href="/xmode/orders/${o.id}">${esc(o.code)}</a></td><td>${esc(statusName(o.status))}<br><small>${esc(o.paymentMethod)} · ${esc(paymentName(o.paymentStatus))}</small></td><td>${money(o.total)}</td><td>${esc(new Date(o.createdAt).toLocaleString('vi-VN'))}</td></tr>`).join('')}</tbody></table></div>`:empty('Chưa có đơn hàng nào.')}</section>`;
    }
    async function guide() { const charts=await api('/size-charts'); app.innerHTML=`<section class="xm-section xm-reading"><h1>Hướng dẫn chọn size.</h1>${charts.length?charts.map(chartHtml).join(''):empty('Chưa có hướng dẫn chọn size.')}</section>`; }

    const adminTitle = (title, add = '') => `<p class="xm-eyebrow">XMODE STUDIO</p><div class="xm-section-heading"><h1>${esc(title)}</h1>${add ? `<button class="xm-button" data-admin-new="${add}">+ Thêm</button>` : ''}</div>`;
    const mediaSelect = (media, selected) => `<option value="">Không chọn ảnh</option>${media.map(m=>`<option value="${m.id}" ${m.id===selected?'selected':''}>${esc(m.alt || 'Ảnh '+m.id)}</option>`).join('')}`;
    async function categoryAdmin() {
        const cats=await api('/admin/categories'); app.innerHTML=`<section class="xm-section">${adminTitle('Danh mục','category')}${cats.length?`<div class="xm-table-wrap"><table><thead><tr><th>Danh mục</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>${cats.map(c=>`<tr><td>${esc(c.name)}</td><td>${c.isVisible?'Hiển thị':'Ẩn'}</td><td><button data-category-edit="${c.id}">Sửa</button> <button data-category-delete="${c.id}">Xóa</button></td></tr>`).join('')}</tbody></table></div>`:empty('Chưa có danh mục nào.')}</section>`;
    }
    async function categoryForm(id) {
        const [cats,media]=await Promise.all([api('/admin/categories'),api('/admin/media')]); const c=cats.find(x=>x.id===id);
        modal(`<h2 id="xm-dialog-title">${c?'Sửa':'Thêm'} danh mục</h2><form id="xm-category-form">${input('name','Tên danh mục',c?.name,'text','required maxlength="100"')}${area('description','Mô tả',c?.description)}<label>Ảnh đại diện<select name="imageId">${mediaSelect(media,c?.imageId)}</select></label>${input('sortOrder','Thứ tự',c?.sortOrder || 0,'number')}${check('isVisible','Hiển thị',c?.isVisible ?? true)}<button class="xm-button">Lưu</button></form><p>Ảnh được tải lên trong mục sản phẩm hoặc nội dung trang chủ.</p>`);
        const form=document.querySelector('#xm-category-form'); form.onsubmit=async e=>{e.preventDefault();const d=data(form);try{await api('/admin/categories'+(id?'/'+id:''),{method:id?'PUT':'POST',body:{name:d.name,description:d.description,imageId:d.imageId?Number(d.imageId):null,sortOrder:Number(d.sortOrder),isVisible:form.elements.isVisible.checked}});close();await categoryAdmin();notice('Đã lưu danh mục.');}catch(err){fail(err);}};
    }
    async function productAdmin() {
        const q=new URLSearchParams(location.search); const list=await api('/admin/products?'+q); app.innerHTML=`<section class="xm-section">${adminTitle('Sản phẩm','product')}<form method="get"><label>Tìm kiếm<input name="q" type="search" value="${esc(q.get('q'))}"></label><button class="xm-button xm-outline">Tìm</button></form>${list.items.length?`<div class="xm-table-wrap"><table><thead><tr><th>Sản phẩm</th><th>Giá</th><th>Tồn kho</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>${list.items.map(p=>`<tr><td><div class="xm-table-product"><img src="${productImage(p)}" alt=""><div>${esc(p.name)}<small>${esc(p.code)}</small></div></div></td><td>${money(p.price)}</td><td>${p.variants.filter(v=>v.isActive).reduce((n,v)=>n+v.stock,0)}</td><td>${p.isVisible?'Hiển thị':'Ẩn'}</td><td><a href="/xmode/product/${p.id}">Xem</a> <button data-product-edit="${p.id}">Sửa</button> <button data-product-delete="${p.id}" data-version="${esc(p.version)}">Xóa</button></td></tr>`).join('')}</tbody></table></div>`:empty('Chưa có sản phẩm nào.')}${list.total>list.pageSize?`<nav class="xm-pagination">${Array.from({length:Math.ceil(list.total/list.pageSize)},(_,i)=>{const p=new URLSearchParams(q);p.set('page',i+1);return `<a href="?${esc(p)}">${i+1}</a>`}).join('')}</nav>`:''}</section>`;
    }
    async function productForm(id) {
        const [cats,charts,initialMedia,p] = await Promise.all([api('/admin/categories'),api('/admin/size-charts'),api('/admin/media'),id?api('/admin/products/'+id):null]);
        if(!cats.length) return notice('Hãy tạo danh mục trước khi thêm sản phẩm.');
        let media=initialMedia; const selected=new Map((p?.images||[]).map(i=>[i.mediaAssetId,{primary:i.isPrimary,sort:i.sortOrder}]));
        modal(`<h2 id="xm-dialog-title">${p?'Sửa':'Thêm'} sản phẩm</h2><form id="xm-product-form">${input('code','Mã sản phẩm',p?.code,'text','required maxlength="50"')}${input('name','Tên sản phẩm',p?.name,'text','required maxlength="120"')}${input('brand','Thương hiệu',p?.brand,'text','maxlength="100"')}<div class="xm-form-grid"><label>Danh mục<select name="categoryId">${cats.map(c=>`<option value="${c.id}" ${p?.categoryId===c.id?'selected':''}>${esc(c.name)}</option>`).join('')}</select></label><label>Dành cho<select name="audience">${['Nam','Nữ','Unisex'].map(v=>`<option ${p?.audience===v?'selected':''}>${v}</option>`).join('')}</select></label>${input('price','Giá bán VND',p?.price,'number','required min="1" step="0.01"')}${input('oldPrice','Giá trước giảm (để trống nếu không có)',p?.oldPrice,'number','min="0" step="0.01"')}</div>${input('material','Chất liệu',p?.material)}${input('fit','Phom dáng',p?.fit)}${area('description','Mô tả',p?.description)}${area('care','Bảo quản',p?.care)}<label>Bảng size<select name="sizeChartId"><option value="">Chưa chọn</option>${charts.map(c=>`<option value="${c.id}" ${p?.sizeChartId===c.id?'selected':''}>${esc(c.name)}</option>`).join('')}</select></label>${check('isNew','Hàng mới',p?.isNew)}${check('featured','Nổi bật',p?.featured)}${check('isVisible','Hiển thị',p?.isVisible ?? true)}<h3>Biến thể</h3><div id="xm-variants"></div><button type="button" class="xm-text-button" id="xm-add-variant">+ Thêm biến thể</button><h3>Ảnh sản phẩm</h3><label>Tải ảnh lên Cloudinary<input id="xm-upload" type="file" accept="image/jpeg,image/png,image/webp" multiple></label><div id="xm-product-media"></div><button class="xm-button xm-wide">Lưu sản phẩm</button></form>`);
        const form=document.querySelector('#xm-product-form'), rows=document.querySelector('#xm-variants');
        const addRow=v=>{const row=document.createElement('div');row.className='xm-variant-row';row.dataset.id=v.id||'';row.dataset.version=v.version||'';row.innerHTML=`${input('sku','SKU',v.sku,'text','required maxlength="60"')}${input('color','Màu',v.color,'text','required maxlength="30"')}${input('hex','Mã màu',v.hex||'#222222','color')}${input('size','Size',v.size,'text','required maxlength="20"')}${input('stock','Tồn kho',v.stock??0,'number','required min="0"')}${check('active','Đang bán',v.isActive??true)}<button type="button" aria-label="Xóa biến thể">×</button>`;row.querySelector('button').onclick=()=>row.remove();rows.append(row);};
        (p?.variants||[{}]).forEach(addRow); document.querySelector('#xm-add-variant').onclick=()=>addRow({});
        const syncMedia=()=>{ document.querySelectorAll('[data-media-check]').forEach(el=>{const id=Number(el.dataset.mediaCheck);if(el.checked)selected.set(id,{primary:document.querySelector(`[data-media-primary="${id}"]`)?.checked||false,sort:Number(document.querySelector(`[data-media-sort="${id}"]`)?.value||0)});else selected.delete(id);}); };
        const paintMedia=()=>{document.querySelector('#xm-product-media').innerHTML=media.map(m=>`<div class="xm-table-product"><img src="${safeImage(m.url)}" alt="${esc(m.alt)}"><label><input type="checkbox" data-media-check="${m.id}" ${selected.has(m.id)?'checked':''}> Chọn ${esc(m.alt||'ảnh')}</label><label><input type="radio" name="primaryMedia" data-media-primary="${m.id}" ${selected.get(m.id)?.primary?'checked':''}> Ảnh chính</label><label>Thứ tự<input type="number" data-media-sort="${m.id}" value="${selected.get(m.id)?.sort??0}"></label></div>`).join('');}; paintMedia();
        document.querySelector('#xm-upload').onchange=async e=>{syncMedia();try{for(const file of e.target.files){const body=new FormData();body.append('file',file);const uploaded=await api('/admin/media',{method:'POST',body});media.unshift(uploaded);selected.set(uploaded.id,{primary:selected.size===0,sort:selected.size});}paintMedia();notice('Đã tải ảnh lên.');}catch(err){fail(err);}e.target.value='';};
        form.onsubmit=async e=>{e.preventDefault();syncMedia();const d=data(form);const variants=[...rows.children].map(row=>({id:row.dataset.id?Number(row.dataset.id):null,version:row.dataset.version||null,sku:row.querySelector('[name=sku]').value,color:row.querySelector('[name=color]').value,hex:row.querySelector('[name=hex]').value,size:row.querySelector('[name=size]').value,stock:Number(row.querySelector('[name=stock]').value),isActive:row.querySelector('[name=active]').checked}));const images=[...selected].map(([mediaAssetId,x])=>({mediaAssetId,sortOrder:x.sort,isPrimary:x.primary}));if(images.length && images.filter(i=>i.isPrimary).length!==1)return notice('Hãy chọn một ảnh chính trong các ảnh đã chọn.');try{await api('/admin/products'+(id?'/'+id:''),{method:id?'PUT':'POST',body:{code:d.code,name:d.name,brand:d.brand,categoryId:Number(d.categoryId),audience:d.audience,price:Number(d.price),oldPrice:d.oldPrice?Number(d.oldPrice):null,description:d.description,material:d.material,fit:d.fit,care:d.care,isNew:form.elements.isNew.checked,featured:form.elements.featured.checked,isVisible:form.elements.isVisible.checked,sizeChartId:d.sizeChartId?Number(d.sizeChartId):null,version:p?.version||null,variants,images}});close();await productAdmin();notice('Đã lưu sản phẩm.');}catch(err){fail(err);}};
    }
    async function contentAdmin() {
        const [sections,media]=await Promise.all([api('/admin/home-sections'),api('/admin/media')]);
        app.innerHTML=`<section class="xm-section">${adminTitle('Nội dung trang chủ','section')}${sections.length?`<div class="xm-table-wrap"><table><thead><tr><th>Vị trí</th><th>Tiêu đề</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>${sections.map(s=>`<tr><td>${esc(s.slot)}</td><td>${esc(s.title)}</td><td>${s.isVisible?'Hiển thị':'Ẩn'}</td><td><button data-section-edit="${s.id}">Sửa</button> <button data-section-delete="${s.id}">Xóa</button></td></tr>`).join('')}</tbody></table></div>`:empty('Chưa có nội dung trang chủ.')}<h2>Thư viện ảnh</h2><label>Tải ảnh JPEG, PNG hoặc WebP<input id="xm-admin-upload" type="file" accept="image/jpeg,image/png,image/webp" multiple></label><div class="xm-image-preview">${media.map(m=>`<div><img src="${safeImage(m.url)}" alt="${esc(m.alt)}"><p>${esc(m.alt)}</p><button data-media-delete="${m.id}">Xóa ảnh</button></div>`).join('')}</div></section>`;
        document.querySelector('#xm-admin-upload').onchange=async e=>{try{for(const file of e.target.files){const body=new FormData();body.append('file',file);await api('/admin/media',{method:'POST',body});}await contentAdmin();notice('Đã tải ảnh lên.');}catch(err){fail(err);}};
    }
    async function sectionForm(id) {
        const [sections,media]=await Promise.all([api('/admin/home-sections'),api('/admin/media')]);const s=sections.find(x=>x.id===id);
        modal(`<h2 id="xm-dialog-title">${s?'Sửa':'Thêm'} nội dung</h2><form id="xm-section-form"><label>Vị trí<select name="slot"><option value="hero" ${s?.slot==='hero'?'selected':''}>Banner đầu trang</option><option value="editorial" ${s?.slot==='editorial'?'selected':''}>Khối nội dung</option></select></label>${input('title','Tiêu đề',s?.title,'text','required maxlength="200"')}${input('subtitle','Nhãn phụ',s?.subtitle)}${area('body','Nội dung',s?.body)}<label>Ảnh<select name="imageId">${mediaSelect(media,s?.imageId)}</select></label>${input('linkText','Nhãn nút',s?.linkText)}${input('linkUrl','Đường dẫn nội bộ',s?.linkUrl,'text','placeholder="/xmode/catalog"')}${input('sortOrder','Thứ tự',s?.sortOrder||0,'number')}${check('isVisible','Hiển thị',s?.isVisible??true)}<button class="xm-button">Lưu</button></form>`);
        const form=document.querySelector('#xm-section-form');form.onsubmit=async e=>{e.preventDefault();const d=data(form);try{await api('/admin/home-sections'+(id?'/'+id:''),{method:id?'PUT':'POST',body:{...d,imageId:d.imageId?Number(d.imageId):null,sortOrder:Number(d.sortOrder),isVisible:form.elements.isVisible.checked}});close();await contentAdmin();notice('Đã lưu nội dung.');}catch(err){fail(err);}};
    }
    async function chartAdmin() {
        const charts=await api('/admin/size-charts');app.innerHTML=`<section class="xm-section">${adminTitle('Bảng size','chart')}${charts.length?`<div class="xm-table-wrap"><table><thead><tr><th>Bảng size</th><th>Thao tác</th></tr></thead><tbody>${charts.map(c=>`<tr><td>${esc(c.name)}</td><td><button data-chart-edit="${c.id}">Sửa</button> <button data-chart-delete="${c.id}">Xóa</button></td></tr>`).join('')}</tbody></table></div>`:empty('Chưa có bảng size nào.')}</section>`;
    }
    async function chartForm(id) {
        const charts=await api('/admin/size-charts');const c=charts.find(x=>x.id===id);
        modal(`<h2 id="xm-dialog-title">${c?'Sửa':'Thêm'} bảng size</h2><form id="xm-chart-form">${input('name','Tên bảng',c?.name,'text','required maxlength="100"')}${area('instructions','Hướng dẫn đo',c?.instructions)}<div id="xm-chart-rows"></div><button type="button" id="xm-chart-add">+ Thêm size</button><button class="xm-button">Lưu</button></form>`);
        const rows=document.querySelector('#xm-chart-rows');const add=r=>{const row=document.createElement('div');row.className='xm-variant-row';row.innerHTML=`${input('size','Size',r.size,'text','required maxlength="20"')}${input('chest','Ngực (cm)',r.chest)}${input('waist','Eo (cm)',r.waist)}${input('hip','Hông (cm)',r.hip)}<button type="button" aria-label="Xóa size">×</button>`;row.querySelector('button').onclick=()=>row.remove();rows.append(row);};(c?.rows||[{}]).forEach(add);document.querySelector('#xm-chart-add').onclick=()=>add({});
        const form=document.querySelector('#xm-chart-form');form.onsubmit=async e=>{e.preventDefault();const d=data(form);const list=[...rows.children].map((r,i)=>({size:r.querySelector('[name=size]').value,chest:r.querySelector('[name=chest]').value,waist:r.querySelector('[name=waist]').value,hip:r.querySelector('[name=hip]').value,sortOrder:i}));try{await api('/admin/size-charts'+(id?'/'+id:''),{method:id?'PUT':'POST',body:{name:d.name,instructions:d.instructions,rows:list}});close();await chartAdmin();notice('Đã lưu bảng size.');}catch(err){fail(err);}};
    }
    async function settingsAdmin() {
        const s=await api('/admin/store-settings');app.innerHTML=`<section class="xm-section">${adminTitle('Thiết lập cửa hàng')}<form id="xm-settings-form">${input('name','Tên cửa hàng',s.name)}${input('tagline','Giới thiệu ngắn',s.tagline)}${input('email','Email',s.email,'email')}${input('phone','Điện thoại',s.phone,'tel')}<h2>Phí vận chuyển</h2>${input('shippingFee','Phí giao hàng tiêu chuẩn mỗi đơn (VND)',s.shippingFee,'number','required min="0" max="1000000" step="1"')}<p>Áp dụng cùng một mức phí trên toàn quốc cho mỗi đơn, không phụ thuộc số lượng sản phẩm. Mặc định 30.000đ/đơn; nhập 0 để miễn phí giao hàng.</p><button class="xm-button">Lưu thiết lập</button></form></section>`;
        const form=document.querySelector('#xm-settings-form');form.onsubmit=async e=>{e.preventDefault();const d=data(form);try{await api('/admin/store-settings',{method:'PUT',body:{...d,shippingFee:d.shippingFee===''?null:Number(d.shippingFee)}});notice('Đã lưu thiết lập.');}catch(err){fail(err);}};
    }
    async function orderAdmin() {
        const q=new URLSearchParams(location.search);const list=await api('/admin/orders?'+q);app.innerHTML=`<section class="xm-section">${adminTitle('Đơn hàng')}<form method="get"><label>Tìm đơn<input name="q" value="${esc(q.get('q'))}" placeholder="Mã đơn, tên, điện thoại"></label><label>Trạng thái<select name="status"><option value="">Tất cả</option>${['Pending','Confirmed','Shipping','Completed','Cancelled'].map(s=>`<option value="${s}" ${q.get('status')===s?'selected':''}>${statusName(s)}</option>`).join('')}</select></label><label><input type="checkbox" name="archived" value="true" ${q.get('archived')==='true'?'checked':''}> Đã lưu trữ</label><button class="xm-button xm-outline">Tìm</button></form><p>${list.total} đơn hàng phù hợp</p>${list.items.length?`<div class="xm-table-wrap"><table><thead><tr><th>Mã đơn</th><th>Người nhận</th><th>Trạng thái</th><th>Tổng</th><th>Thao tác</th></tr></thead><tbody>${list.items.map(o=>`<tr><td>${esc(o.code)}</td><td>${esc(o.recipientName)}<br>${esc(o.phone)}</td><td>${esc(statusName(o.status))}<br><small>${esc(o.paymentMethod)} · ${esc(paymentName(o.paymentStatus))}</small></td><td>${money(o.total)}</td><td><button data-order-view="${o.id}">Chi tiết</button></td></tr>`).join('')}</tbody></table></div>`:empty('Chưa có đơn hàng nào.')}${list.total>list.pageSize?`<nav class="xm-pagination" aria-label="Phân trang">${Array.from({length:Math.ceil(list.total/list.pageSize)},(_,i)=>{const p=new URLSearchParams(q);p.set('page',i+1);return `<a ${list.page===i+1?'aria-current="page"':''} href="?${esc(p)}">${i+1}</a>`}).join('')}</nav>`:''}</section>`;
    }
    async function orderAdminDetail(id) {
        const o=await api('/admin/orders/'+id);let actions='';
        if(o.status==='Pending') actions+=`<button data-admin-order-edit="${o.id}">Sửa thông tin nhận</button> <button data-order-status="Confirmed" data-order-id="${o.id}" data-version="${esc(o.version)}" ${o.paymentMethod!=='COD'&&o.paymentStatus!=='Paid'?'disabled title="Chưa xác nhận thanh toán"':''}>Xác nhận</button> `;
        if(o.status==='Confirmed') actions+=`<button data-order-status="Shipping" data-order-id="${o.id}" data-version="${esc(o.version)}">Bắt đầu giao</button> `;
        if(o.status==='Shipping') actions+=`<button data-order-status="Completed" data-order-id="${o.id}" data-version="${esc(o.version)}">Hoàn tất</button> `;
        if(['Pending','Confirmed','Shipping'].includes(o.status)) actions+=`<button data-order-status="Cancelled" data-order-id="${o.id}" data-version="${esc(o.version)}" data-shipping="${o.status==='Shipping'}">Hủy đơn</button> `;
        if(['Completed','Cancelled'].includes(o.status)&&!o.isArchived&&o.paymentStatus!=='RefundRequired') actions+=`<button data-order-archive="${o.id}" data-version="${esc(o.version)}">Lưu trữ</button>`;
        if(o.paymentStatus==='RefundRequired') actions+=`<button data-refund-confirm="${o.id}" data-version="${esc(o.version)}">Ghi nhận đã hoàn tiền</button>`;
        modal(`<div id="xm-dialog-title">${orderDetail(o,true)}</div><div class="xm-dialog-actions">${actions}</div>`);
        const refund=content.querySelector('[data-refund-confirm]');
        if(refund)refund.onclick=async()=>{
            const reference=prompt('Chỉ ghi nhận sau khi đã hoàn tiền qua cổng/ngân hàng. Nhập mã tham chiếu hoàn tiền:');
            if(!reference?.trim())return;
            refund.disabled=true;
            try{await api('/admin/orders/'+id+'/refund-confirmation',{method:'POST',body:{version:o.version,reference}});close();await orderAdmin();notice('Đã ghi nhận hoàn tiền.');}catch(err){fail(err);refund.disabled=false;}
        };
    }
    async function orderAdminEdit(id) {
        const o=await api('/admin/orders/'+id);close();modal(`<h2 id="xm-dialog-title">Sửa thông tin nhận hàng</h2><form id="xm-order-edit">${input('recipientName','Họ tên',o.recipientName,'text','required maxlength="100"')}${input('email','Email',o.email,'email','required maxlength="100"')}${input('phone','Điện thoại',o.phone,'tel','required maxlength="25"')}${input('address','Địa chỉ',o.address,'text','required maxlength="300"')}${input('city','Tỉnh / Thành phố',o.city,'text','required maxlength="100"')}${area('note','Ghi chú',o.note)}<button class="xm-button">Lưu</button></form>`);
        const form=document.querySelector('#xm-order-edit');form.onsubmit=async e=>{e.preventDefault();try{await api('/admin/orders/'+id,{method:'PUT',body:{...data(form),version:o.version}});close();await orderAdmin();notice('Đã cập nhật đơn hàng.');}catch(err){fail(err);}};
    }
    async function render() {
        if (!app) return;
        const path=location.pathname;
        if(path==='/xmode/admin/categories') return categoryAdmin();
        if(path==='/xmode/admin/content') return contentAdmin();
        if(path==='/xmode/admin/size-charts') return chartAdmin();
        if(path==='/xmode/admin/settings') return settingsAdmin();
        if(path==='/xmode/admin/orders') return orderAdmin();
        if(path.startsWith('/xmode/admin')) return productAdmin();
        if(path==='/xmode/catalog') return catalog();
        if(path.startsWith('/xmode/product/')) return detail(path.split('/').pop());
        if(path==='/xmode/cart') return cartPage();
        if(path==='/xmode/checkout') return checkout();
        if(path==='/xmode/guide') return guide();
        if(path==='/xmode/orders') return ordersPage();
        if(path.startsWith('/xmode/orders/')) return ordersPage(path.split('/').pop());
        return home();
    }
    document.addEventListener('error',e=>{if(e.target instanceof HTMLImageElement && !e.target.src.endsWith('/fallback.svg'))e.target.src='/images/xmode/fallback.svg';},true);
    document.addEventListener('change',async e=>{
        const id=e.target.dataset.cartQuantity;if(!id)return;
        try{await api('/cart/items/'+id,{method:'PATCH',body:{quantity:Number(e.target.value)}});await cartPage();await updateCount();}catch(err){fail(err);await cartPage();}
    });
    document.addEventListener('click',async e=>{
        const b=e.target.closest('button');if(!b)return;
        try {
            if(b.hasAttribute('data-close')) { close();return; }
            if(b.hasAttribute('data-menu')) { modal('<h2 id="xm-dialog-title">Khám phá Xmode</h2><nav class="xm-mobile-nav">'+document.querySelector('.xm-nav').innerHTML+'</nav>');return; }
            if(b.dataset.photo) { modal(`<h2 id="xm-dialog-title">Chi tiết sản phẩm</h2><img class="xm-zoom" src="${safeImage(b.dataset.photo)}" alt="Ảnh sản phẩm phóng to">`);return; }
            if(b.dataset.size) { const chart=await api('/size-charts/'+b.dataset.size);modal(`<h2 id="xm-dialog-title">Hướng dẫn chọn size</h2>${chartHtml(chart)}`);return; }
            if(b.dataset.cartRemove) {await api('/cart/items/'+b.dataset.cartRemove,{method:'DELETE'});await cartPage();await updateCount();return;}
            if(b.dataset.cancelOrder) {if(!confirm('Bạn muốn hủy đơn hàng này?'))return;await api('/orders/'+b.dataset.cancelOrder+'/cancel',{method:'POST'});await ordersPage(b.dataset.cancelOrder);notice('Đã hủy đơn hàng.');return;}
            if(b.dataset.adminNew==='category')return await categoryForm();
            if(b.dataset.adminNew==='product')return await productForm();
            if(b.dataset.adminNew==='section')return await sectionForm();
            if(b.dataset.adminNew==='chart')return await chartForm();
            if(b.dataset.categoryEdit)return await categoryForm(Number(b.dataset.categoryEdit));
            if(b.dataset.productEdit)return await productForm(Number(b.dataset.productEdit));
            if(b.dataset.sectionEdit)return await sectionForm(Number(b.dataset.sectionEdit));
            if(b.dataset.chartEdit)return await chartForm(Number(b.dataset.chartEdit));
            if(b.dataset.orderView)return await orderAdminDetail(b.dataset.orderView);
            if(b.dataset.adminOrderEdit)return await orderAdminEdit(b.dataset.adminOrderEdit);
            if(b.dataset.categoryDelete && confirm('Xóa danh mục này?')){await api('/admin/categories/'+b.dataset.categoryDelete,{method:'DELETE'});await categoryAdmin();notice('Đã xóa danh mục.');return;}
            if(b.dataset.productDelete && confirm('Ngừng bán và xóa sản phẩm khỏi danh sách?')){await api('/admin/products/'+b.dataset.productDelete+'/delete',{method:'POST',body:{version:b.dataset.version}});await productAdmin();notice('Đã xóa sản phẩm.');return;}
            if(b.dataset.sectionDelete && confirm('Xóa nội dung này?')){await api('/admin/home-sections/'+b.dataset.sectionDelete,{method:'DELETE'});await contentAdmin();notice('Đã xóa nội dung.');return;}
            if(b.dataset.chartDelete && confirm('Xóa bảng size này?')){await api('/admin/size-charts/'+b.dataset.chartDelete,{method:'DELETE'});await chartAdmin();notice('Đã xóa bảng size.');return;}
            if(b.dataset.mediaDelete && confirm('Xóa ảnh khỏi Cloudinary?')){await api('/admin/media/'+b.dataset.mediaDelete,{method:'DELETE'});await contentAdmin();notice('Đã xóa ảnh.');return;}
            if(b.dataset.orderStatus) {const shipping=b.dataset.shipping==='true';if(!confirm(shipping?'Xác nhận cửa hàng đã nhận lại hàng và hủy đơn?':`Chuyển đơn sang ${statusName(b.dataset.orderStatus)}?`))return;await api('/admin/orders/'+b.dataset.orderId+'/status',{method:'POST',body:{version:b.dataset.version,status:b.dataset.orderStatus,reason:null,goodsReturned:shipping}});close();await orderAdmin();notice('Đã cập nhật trạng thái đơn.');return;}
            if(b.dataset.orderArchive && confirm('Lưu trữ đơn hàng này?')){await api('/admin/orders/'+b.dataset.orderArchive+'/archive',{method:'POST',body:{version:b.dataset.version}});close();await orderAdmin();notice('Đã lưu trữ đơn.');return;}
        } catch(err){fail(err);}
    });
    updateCount();
    if(app) render().catch(err=>{app.innerHTML=empty('Không thể tải dữ liệu.',err.message);});
})();
