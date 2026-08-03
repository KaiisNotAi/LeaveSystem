/* ===================================================================
   學生請假系統 — 專題報告 投影片互動
   =================================================================== */
(function () {
    'use strict';

    const slides = Array.from(document.querySelectorAll('section.slide'));
    if (slides.length === 0) return;

    // 1. 自動編號 + 給 id
    slides.forEach((el, i) => {
        const num = i + 1;
        if (!el.id) el.id = 'slide-' + num;
        el.setAttribute('data-slide-num', num + ' / ' + slides.length);
    });

    // 2. 頁碼指示器
    const indicator = document.querySelector('.page-indicator');
    function currentIndex() {
        // 以視窗中央位置最接近的 slide 為目前頁
        const mid = window.scrollY + window.innerHeight / 2;
        let best = 0, bestDist = Infinity;
        slides.forEach((el, i) => {
            const rect = el.getBoundingClientRect();
            const top = rect.top + window.scrollY;
            const center = top + rect.height / 2;
            const d = Math.abs(center - mid);
            if (d < bestDist) { bestDist = d; best = i; }
        });
        return best;
    }
    function updateIndicator() {
        if (!indicator) return;
        indicator.textContent = (currentIndex() + 1) + ' / ' + slides.length;
    }
    window.addEventListener('scroll', updateIndicator, { passive: true });
    updateIndicator();

    // 3. 跳頁函式
    function goTo(idx) {
        if (idx < 0) idx = 0;
        if (idx >= slides.length) idx = slides.length - 1;
        slides[idx].scrollIntoView({ behavior: 'smooth', block: 'start' });
        history.replaceState(null, '', '#' + slides[idx].id);
    }

    // 4. 鍵盤導覽
    document.addEventListener('keydown', (e) => {
        // 若焦點在輸入元件則忽略
        const tag = (document.activeElement && document.activeElement.tagName) || '';
        if (/^(INPUT|TEXTAREA|SELECT)$/.test(tag)) return;

        switch (e.key) {
            case 'ArrowRight':
            case 'PageDown':
            case ' ':
                e.preventDefault();
                goTo(currentIndex() + 1);
                break;
            case 'ArrowLeft':
            case 'PageUp':
                e.preventDefault();
                goTo(currentIndex() - 1);
                break;
            case 'Home':
                e.preventDefault();
                if (window.location.pathname.endsWith('index.html') ||
                    /\/report\/?$/.test(window.location.pathname)) {
                    goTo(0);
                } else {
                    window.location.href = 'index.html';
                }
                break;
            case 'End':
                e.preventDefault();
                goTo(slides.length - 1);
                break;
            case 'f':
            case 'F':
                e.preventDefault();
                toggleFullscreen();
                break;
            case 'Escape':
                if (document.fullscreenElement) document.exitFullscreen();
                break;
        }
    });

    // 5. 全螢幕切換
    function toggleFullscreen() {
        if (!document.fullscreenElement) {
            (document.documentElement.requestFullscreen || function () { })
                .call(document.documentElement);
        } else {
            document.exitFullscreen();
        }
    }
    const fsBtn = document.querySelector('[data-action="fullscreen"]');
    if (fsBtn) fsBtn.addEventListener('click', toggleFullscreen);

    // 6. 上/下一頁按鈕
    const prevBtn = document.querySelector('[data-action="prev"]');
    const nextBtn = document.querySelector('[data-action="next"]');
    if (prevBtn) prevBtn.addEventListener('click', () => goTo(currentIndex() - 1));
    if (nextBtn) nextBtn.addEventListener('click', () => goTo(currentIndex() + 1));

    // 7. URL hash 定位
    if (window.location.hash) {
        const target = document.querySelector(window.location.hash);
        if (target) setTimeout(() => target.scrollIntoView({ block: 'start' }), 100);
    }
})();
