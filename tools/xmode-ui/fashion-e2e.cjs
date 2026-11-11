const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const base = process.env.XMODE_TEST_BASE;
if (!/^http:\/\/127\.0\.0\.1:\d+$/.test(base || '')) throw new Error('Test host must be loopback.');

(async () => {
    const browser = await chromium.launch({ channel: 'msedge', headless: true });
    const adminContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const guestContext = await browser.newContext({ viewport: { width: 375, height: 850 } });
    const admin = await adminContext.newPage(), guest = await guestContext.newPage();
    const errors = []; for (const page of [admin, guest]) page.on('pageerror', e => errors.push(e.message));
    const go = async (page, path) => { const response=await page.goto(base+path); assert.equal(response.status(),200,path); };
    const pass = name => console.log('PASS '+name);
    try {
        await go(guest, '/'); await guest.getByText('Chưa có sản phẩm nào', { exact: true }).waitFor();
        assert.equal(await guest.evaluate(() => localStorage.getItem('xmode.mock.v1')), null);
        assert.equal(await guest.evaluate(async () => (await fetch('/api/xmode/admin/categories')).status), 401);
        pass('empty storefront and admin access control');

        await guest.locator('#xm-chat-launch').click();
        await guest.locator('#xm-chat-input').fill('Tôi muốn tìm áo thun');
        await guest.locator('#xm-chat-send').click();
        await guest.getByText('Cửa hàng hiện chưa có sản phẩm công khai.',{exact:false}).waitFor();
        await guest.reload(); await guest.locator('#xm-chat-launch').click();
        await guest.getByText('Cửa hàng hiện chưa có sản phẩm công khai.',{exact:false}).waitFor();
        await guest.locator('#xm-chat-reset').click();
        await guest.getByText('Chào bạn, mình là trợ lý AI của Xmode.',{exact:false}).waitFor();
        await guest.keyboard.press('Escape'); assert.equal(await guest.locator('#xm-chat-launch').getAttribute('aria-expanded'),'false');
        pass('chat session history, empty catalog and keyboard close');

        await go(admin, '/Login'); await admin.locator('[name=Username]').fill('e2e_admin'); await admin.locator('[name=Password]').fill('E2e-Only-Password-127');
        await Promise.all([admin.waitForURL(base+'/'), admin.locator('form').first().getByRole('button',{name:'Đăng Nhập'}).click()]);
        assert.equal(await admin.evaluate(async () => (await fetch('/api/xmode/admin/categories',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'})).status),400);
        pass('admin login and antiforgery');

        await go(admin,'/xmode/admin/categories'); await admin.locator('[data-admin-new=category]').click();
        await admin.locator('#xm-category-form [name=name]').fill('Áo kiểm thử');
        await admin.locator('#xm-category-form button').click(); await admin.getByRole('cell',{name:'Áo kiểm thử'}).waitFor();
        await admin.reload(); await admin.getByRole('cell',{name:'Áo kiểm thử'}).waitFor();
        pass('category creation persists');

        await go(admin,'/xmode/admin/size-charts'); await admin.locator('[data-admin-new=chart]').click();
        await admin.locator('#xm-chart-form [name=name]').fill('Bảng áo'); await admin.locator('#xm-chart-form [name=size]').fill('M');
        await admin.locator('#xm-chart-form [name=chest]').fill('90–95'); await admin.locator('#xm-chart-form button.xm-button').click();
        await admin.getByRole('cell',{name:'Bảng áo'}).waitFor();
        await admin.locator('[data-chart-edit]').click(); await admin.locator('#xm-chart-form [name=chest]').fill('91–96'); await admin.locator('#xm-chart-form button.xm-button').click();
        await admin.getByRole('cell',{name:'Bảng áo'}).waitFor();
        pass('size chart creation and editing');

        await go(admin,'/xmode/admin/settings'); await admin.locator('#xm-settings-form [name=shippingFee]').fill('30000'); await admin.locator('#xm-settings-form button').click();
        await admin.getByText('Đã lưu thiết lập.').waitFor();
        await go(admin,'/xmode/admin/content'); await admin.locator('[data-admin-new=section]').click();
        await admin.locator('#xm-section-form [name=title]').fill('Bộ sưu tập mới'); await admin.locator('#xm-section-form [name=body]').fill('Phong cách mỗi ngày');
        await admin.locator('#xm-section-form button').click(); await admin.getByRole('cell',{name:'Bộ sưu tập mới'}).waitFor();
        pass('store settings and homepage content');

        if (process.env.XMODE_TEST_CLOUDINARY === '1') {
            const png=Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/aH0AAAAASUVORK5CYII=','base64');
            const responsePromise=admin.waitForResponse(r=>r.url().endsWith('/api/xmode/admin/media') && r.request().method()==='POST');
            await admin.locator('#xm-admin-upload').setInputFiles({name:'xmode-e2e.png',mimeType:'image/png',buffer:png});
            const response=await responsePromise; const body=await response.json(); assert.equal(response.status(),200,body.message || 'Cloudinary upload failed');
            try {
                await admin.locator(`[data-media-delete="${body.id}"]`).waitFor();
                const checked=await admin.evaluate(async imageId=>{
                    const token=document.querySelector('#xm-antiforgery input').value;
                    const headers={'X-CSRF-TOKEN':token,'Content-Type':'application/json'};
                    const categories=await (await fetch('/api/xmode/admin/categories')).json(); const category=categories[0];
                    const put=async id=>fetch('/api/xmode/admin/categories/'+category.id,{method:'PUT',headers,body:JSON.stringify({name:category.name,description:category.description,imageId:id,sortOrder:category.sortOrder,isVisible:category.isVisible})});
                    if(!(await put(imageId)).ok) throw Error('Failed to attach image');
                    const blocked=await fetch('/api/xmode/admin/media/'+imageId,{method:'DELETE',headers});
                    if(!(await put(null)).ok) throw Error('Failed to detach image');
                    return blocked.status;
                },body.id);
                assert.equal(checked,400,'referenced image cannot be deleted');
            }
            finally {
                const removed=await admin.evaluate(async id=>{const token=document.querySelector('#xm-antiforgery input').value;return fetch('/api/xmode/admin/media/'+id,{method:'DELETE',headers:{'X-CSRF-TOKEN':token}}).then(r=>r.status)},body.id);
                assert.equal(removed,204,'Cloudinary test image cleanup');
            }
            pass('real Cloudinary upload and cleanup');
        }

        await go(admin,'/xmode/admin/products'); await admin.locator('[data-admin-new=product]').click();
        await admin.locator('#xm-product-form [name=code]').fill('AO-TEST-01'); await admin.locator('#xm-product-form [name=name]').fill('Áo cà phê');
        await admin.locator('#xm-product-form [name=price]').fill('200000'); await admin.locator('#xm-product-form [name=description]').fill('Áo cotton');
        await admin.locator('#xm-product-form [name=sku]').fill('AO-TEST-DEN-M'); await admin.locator('#xm-product-form [name=color]').fill('Đen');
        await admin.locator('#xm-product-form [name=size]').fill('M'); await admin.locator('#xm-product-form [name=stock]').fill('3');
        await admin.locator('#xm-product-form [name=isNew]').check(); await admin.locator('#xm-product-form [name=sizeChartId]').selectOption({label:'Bảng áo'});
        await admin.locator('#xm-product-form button.xm-wide').click(); await admin.getByRole('cell',{name:/Áo cà phê/}).waitFor();
        await admin.reload(); const row=admin.locator('tr').filter({hasText:'Áo cà phê'}); await row.waitFor();
        const productHref=await row.locator('a').getAttribute('href'); assert.match(productHref,/^\/xmode\/product\/\d+$/);
        await row.locator('[data-product-edit]').click(); await admin.locator('#xm-product-form [name=name]').fill('Áo cà phê mới');
        await admin.locator('#xm-product-form [name=isNew]').uncheck();
        await admin.locator('#xm-product-form button.xm-wide').click(); await admin.getByRole('cell',{name:/Áo cà phê mới/}).waitFor();
        pass('product and variant create, edit and persistence');

        await go(guest,productHref); await guest.locator('#xm-chat-launch').click();
        await guest.locator('#xm-chat-input').fill('Gợi ý cho tôi áo cotton');
        await guest.locator('#xm-chat-send').click();
        await guest.locator('#xm-chat-error').waitFor();
        assert.equal(await guest.locator('#xm-chat-input').inputValue(),'Gợi ý cho tôi áo cotton');
        assert.equal(await guest.locator('#xm-chat-send').isDisabled(),false);
        await guest.locator('#xm-chat-close').click();
        pass('chat missing-key error preserves question for retry');

        const productId=Number(productHref.split('/').pop());
        await guest.route('**/api/xmode/chat',async route=>{
            if(route.request().method()==='GET') return route.fulfill({json:{available:true,turns:[]}});
            if(route.request().method()==='DELETE') return route.fulfill({status:204});
            const body=route.request().postDataJSON(); assert.equal(body.productId,productId);
            return route.fulfill({json:{message:body.message==='Tư vấn áo này'?'Gợi ý áo cotton. <img src=x onerror=alert(1)>':'Áo cotton này phù hợp đi học hoặc dạo phố. Bạn thích phối cùng quần jeans hay sơ mi khoác ngoài?',products:[{id:productId,name:'Áo cà phê mới',price:200000,oldPrice:null,imageUrl:null,url:productHref,variants:[{color:'Đen',size:'M',stock:3}]}]}});
        });
        await go(guest,productHref); await guest.locator('#xm-chat-launch').click();
        await guest.locator('#xm-chat-input').fill('Tư vấn áo này'); await guest.locator('#xm-chat-input').press('Enter');
        await guest.locator('.xm-chat-product').waitFor();
        assert.equal(await guest.locator('.xm-chat-product a').getAttribute('href'),productHref);
        assert.equal(await guest.locator('.xm-chat-turn p img').count(),0);
        assert(await guest.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
        await guest.locator('#xm-chat-reset').click();await guest.getByText('Chào bạn, mình là trợ lý AI của Xmode.',{exact:false}).waitFor();
        await guest.locator('#xm-chat-input').fill('Gợi ý phối đồ');await guest.locator('#xm-chat-send').click();
        await guest.locator('.xm-chat-product').waitFor();await guest.waitForTimeout(350);
        const screenshots=path.join(__dirname,'../../docs/xmode/screenshots');fs.mkdirSync(screenshots,{recursive:true});
        await guest.screenshot({path:path.join(screenshots,'shopping-chat-mobile.png'),fullPage:true});
        await guest.setViewportSize({width:1440,height:1000});
        await guest.screenshot({path:path.join(screenshots,'shopping-chat-desktop.png'),fullPage:true});
        await guest.setViewportSize({width:375,height:850});await guest.locator('#xm-chat-close').click();
        await guest.unroute('**/api/xmode/chat');
        pass('chat UI contract, product cards, XSS safety and responsive screenshots (stubbed reply)');

        await go(guest,'/'); await guest.getByText('Bộ sưu tập mới').first().waitFor(); await guest.getByText('Áo cà phê mới').first().waitFor();
        await go(guest,'/xmode/catalog?q=ao%20ca%20phe&size=M&color=%C4%90en'); await guest.getByText('Áo cà phê mới').first().waitFor();
        assert(await guest.evaluate(()=>document.documentElement.scrollWidth <= innerWidth));
        assert(await guest.locator('#xm-filter-fields select[name=size]').isVisible(), 'mobile filters are visible');
        await go(guest,productHref); await guest.locator('#xm-add [name=color]').selectOption({label:'Đen'}); await guest.locator('#xm-add [name=size]').selectOption({label:'M'});
        await guest.locator('#xm-add button').click(); await guest.getByText('Đã thêm vào giỏ hàng.').waitFor();
        await go(guest,'/xmode/cart'); await guest.getByText('Áo cà phê mới').first().waitFor(); await guest.locator('[data-cart-quantity]').fill('2'); await guest.locator('[data-cart-quantity]').dispatchEvent('change');
        await guest.getByText('430.000 ₫',{exact:false}).first().waitFor();
        pass('mobile catalog, filters and persisted cart');

        await go(guest,'/xmode/checkout'); await guest.locator('[name=recipientName]').fill('Khách kiểm thử'); await guest.locator('[name=email]').fill('guest@example.invalid');
        await guest.locator('[name=phone]').fill('0901234567'); await guest.locator('[name=address]').fill('1 Đường thử'); await guest.locator('[name=city]').fill('Hà Nội');
        await Promise.all([guest.waitForURL(/\/xmode\/orders\/[0-9a-f-]+$/),guest.locator('#xm-checkout button').click()]);
        await guest.getByText('Chờ xác nhận').first().waitFor(); const orderPath=new URL(guest.url()).pathname;
        const stranger=await browser.newContext();const strangerPage=await stranger.newPage();await go(strangerPage,orderPath);await strangerPage.getByText('Không thể tải dữ liệu.').waitFor();await stranger.close();
        await go(admin,'/xmode/admin/orders');await admin.getByText('Khách kiểm thử').waitFor();await admin.locator('[data-order-view]').first().click();
        admin.once('dialog',d=>d.accept());await admin.locator('[data-order-status=Confirmed]').click();await admin.locator('#xm-dialog').waitFor({state:'hidden'});
        await admin.locator('[data-order-view]').first().click();await admin.getByText('Đã xác nhận').last().waitFor();
        pass('guest COD checkout and order ownership');

        admin.once('dialog',d=>d.accept()); await admin.locator('[data-order-status=Shipping]').click(); await admin.locator('#xm-dialog').waitFor({state:'hidden'});
        await admin.locator('[data-order-view]').first().click();
        admin.once('dialog',d=>d.accept()); await admin.locator('[data-order-status=Completed]').click(); await admin.locator('#xm-dialog').waitFor({state:'hidden'});
        await admin.locator('[data-order-view]').first().click();
        admin.once('dialog',d=>d.accept()); await admin.locator('[data-order-archive]').click(); await admin.locator('#xm-dialog').waitFor({state:'hidden'});
        await admin.getByText('Chưa có đơn hàng nào.').waitFor();
        pass('admin order transitions and archival');

        await go(guest,productHref); await guest.locator('#xm-add [name=color]').selectOption({label:'Đen'}); await guest.locator('#xm-add [name=size]').selectOption({label:'M'});
        await guest.locator('#xm-add button').click(); await guest.getByText('Đã thêm vào giỏ hàng.').waitFor();
        await go(guest,'/Signup'); await guest.locator('[name=Username]').fill('buyer123'); await guest.locator('[name=DisplayName]').fill('Người mua');
        await guest.locator('[name=Email]').fill('buyer@example.invalid'); await guest.locator('[name=Password]').fill('E2e-buyer-Password');
        await guest.locator('[name=ConfirmPassword]').fill('E2e-buyer-Password');
        await Promise.all([guest.waitForURL(base+'/Login'),guest.locator('.signupbtn').click()]);
        await guest.locator('[name=Username]').fill('buyer123');await guest.locator('[name=Password]').fill('E2e-buyer-Password');
        await Promise.all([guest.waitForURL(base+'/'),guest.locator('form').first().getByRole('button',{name:'Đăng Nhập'}).click()]);
        assert.equal(await guest.evaluate(async () => (await fetch('/api/xmode/admin/categories')).status),403);
        await go(guest,'/xmode/cart');await guest.getByText('Áo cà phê mới').first().waitFor();
        pass('signup, direct login and guest cart merge');

        await Promise.all([guest.waitForURL(base+'/'), guest.locator('form[action="/Logout"] button').click()]);
        await go(guest,productHref); await guest.locator('#xm-add [name=color]').selectOption({label:'Đen'}); await guest.locator('#xm-add [name=size]').selectOption({label:'M'});
        await guest.locator('#xm-add button').click();await guest.getByText('Đã thêm vào giỏ hàng.').waitFor();
        await go(guest,'/Login');await guest.locator('[name=Username]').fill('buyer123');await guest.locator('[name=Password]').fill('E2e-buyer-Password');
        await Promise.all([guest.waitForURL(base+'/'),guest.locator('form').first().getByRole('button',{name:'Đăng Nhập'}).click()]);
        await go(guest,'/xmode/cart');assert.equal(await guest.locator('[data-cart-quantity]').inputValue(),'2');
        await guest.getByText('Vui lòng điều chỉnh số lượng.').waitFor();
        pass('merge of existing carts preserves quantity and warns when stock is exceeded');

        await go(admin,'/Article');await admin.locator('[name=title]').fill('Bài viết kiểm thử');await admin.locator('[name=content]').fill('Nội dung thực tế.');
        await Promise.all([admin.waitForURL(base+'/BlogList'),admin.locator('form').filter({has:admin.locator('[name=title]')}).getByRole('button',{name:'Lưu bài viết'}).click()]);
        await admin.getByText('Bài viết kiểm thử').waitFor(); await go(guest,'/Posts'); await guest.getByText('Bài viết kiểm thử').waitFor();
        pass('journal create and public display');
        await go(admin,'/xmode/admin/products'); admin.once('dialog',d=>d.accept()); await admin.locator('[data-product-delete]').click();
        await admin.getByText('Chưa có sản phẩm nào.').waitFor();
        await go(admin,'/xmode/admin/categories');admin.once('dialog',d=>d.accept());await admin.locator('[data-category-delete]').click();
        await admin.getByText('Chưa có danh mục nào.').waitFor();
        await go(guest,'/xmode/catalog');await guest.getByText('Chưa có sản phẩm nào').waitFor();
        pass('product and category deletion retain historical orders');
        assert.deepEqual(errors,[]);
    } finally { await browser.close(); }
})().catch(e=>{console.error(e);process.exit(1);});
