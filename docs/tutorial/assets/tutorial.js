/* ===================================================================
   學生請假系統 — 互動循序圖教學站

   一頁裡有兩份對應同一件事的東西：
     1. SVG 循序圖裡的 <g class="msg" data-step="n">
     2. 下方程式碼對照卡片 <article class="step-card" data-step="n">

   本檔負責讓兩者雙向連動，並提供逐步播放。
   =================================================================== */
(function () {
    'use strict';

    const svg = document.querySelector('svg.seq');
    const cards = Array.from(document.querySelectorAll('.step-card[data-step]'));
    if (!svg && cards.length === 0) return;

    const msgs = svg ? Array.from(svg.querySelectorAll('.msg[data-step]')) : [];
    const readout = document.querySelector('.step-readout');
    const total = Math.max(
        msgs.length,
        cards.reduce((max, c) => Math.max(max, Number(c.dataset.step) || 0), 0)
    );

    let current = 0;        // 0 = 沒有任何高亮
    let playTimer = null;

    // ── 高亮 ────────────────────────────────────────────────
    function highlight(step, opts) {
        const scroll = opts && opts.scroll;
        current = step;

        msgs.forEach((m) => m.classList.toggle('active', Number(m.dataset.step) === step));
        cards.forEach((c) => c.classList.toggle('active', Number(c.dataset.step) === step));

        if (svg) svg.classList.toggle('has-active', step > 0);

        if (readout) {
            readout.innerHTML = step > 0
                ? '目前步驟 <b>' + step + '</b> / ' + total
                : '共 <b>' + total + '</b> 個步驟';
        }

        if (scroll && step > 0) {
            const card = cards.find((c) => Number(c.dataset.step) === step);
            if (card) card.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    function clear() {
        stopPlaying();
        highlight(0);
    }

    // ── 點擊互動（雙向）──────────────────────────────────────
    msgs.forEach((m) => {
        const step = Number(m.dataset.step);
        m.addEventListener('click', () => { stopPlaying(); highlight(step, { scroll: true }); });
        m.addEventListener('mouseenter', () => { if (!playTimer) highlight(step); });
    });

    cards.forEach((c) => {
        const step = Number(c.dataset.step);
        c.addEventListener('click', () => { stopPlaying(); highlight(step); });
        c.addEventListener('mouseenter', () => { if (!playTimer) highlight(step); });
    });

    // ── 逐步播放 ────────────────────────────────────────────
    const playBtn = document.querySelector('[data-action="play"]');
    const stepBtn = document.querySelector('[data-action="next-step"]');
    const resetBtn = document.querySelector('[data-action="reset"]');

    function stopPlaying() {
        if (playTimer) {
            clearInterval(playTimer);
            playTimer = null;
        }
        if (playBtn) playBtn.textContent = '▶ 逐步播放';
    }

    function startPlaying() {
        if (total === 0) return;
        highlight(1, { scroll: true });
        if (playBtn) playBtn.textContent = '⏸ 暫停';

        playTimer = setInterval(() => {
            if (current >= total) {
                stopPlaying();
                return;
            }
            highlight(current + 1, { scroll: true });
        }, 1800);
    }

    if (playBtn) {
        playBtn.addEventListener('click', () => {
            if (playTimer) stopPlaying();
            else startPlaying();
        });
    }

    if (stepBtn) {
        stepBtn.addEventListener('click', () => {
            stopPlaying();
            highlight(current >= total ? 1 : current + 1, { scroll: true });
        });
    }

    if (resetBtn) resetBtn.addEventListener('click', clear);

    // ── 鍵盤導覽（與報告投影片的操作習慣一致）─────────────────
    document.addEventListener('keydown', (e) => {
        const tag = (document.activeElement && document.activeElement.tagName) || '';
        if (/^(INPUT|TEXTAREA|SELECT)$/.test(tag)) return;

        switch (e.key) {
            case 'ArrowRight':
                e.preventDefault();
                stopPlaying();
                highlight(current >= total ? 1 : current + 1, { scroll: true });
                break;
            case 'ArrowLeft':
                e.preventDefault();
                stopPlaying();
                highlight(current <= 1 ? total : current - 1, { scroll: true });
                break;
            case ' ':
                e.preventDefault();
                if (playTimer) stopPlaying(); else startPlaying();
                break;
            case 'Escape':
                if (document.fullscreenElement) document.exitFullscreen();
                else clear();
                break;
            case 'f':
            case 'F':
                e.preventDefault();
                if (!document.fullscreenElement) document.documentElement.requestFullscreen();
                else document.exitFullscreen();
                break;
            case 'Home':
                e.preventDefault();
                window.location.href = 'index.html';
                break;
        }
    });

    highlight(0);
})();
