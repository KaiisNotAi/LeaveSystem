/* ===================================================================
   學生請假系統 — 從零開始學 ASP.NET Core MVC
   本檔負責三件事：
     1. 每章結尾「檢查點」的勾選與記憶
     2. 首頁課程地圖的完成度顯示
     3. 程式碼區塊的複製按鈕、章節切換下拉、閱讀進度條
   =================================================================== */
(function () {
    'use strict';

    // 全書章節清單（首頁與每章的下拉都用這一份）
    const CHAPTERS = [
        { id: 'index', file: 'index.html',            name: '課程地圖' },
        { id: 'ch00',  file: 'ch00-before-you-start.html', name: '第 0 章 開始之前' },
        { id: 'ch01',  file: 'ch01-csharp-basics.html',    name: '第 1 章 C# 夠用就好' },
        { id: 'ch02',  file: 'ch02-first-site.html',       name: '第 2 章 第一個網站' },
        { id: 'ch03',  file: 'ch03-routing-controller.html', name: '第 3 章 路由與 Controller' },
        { id: 'ch04',  file: 'ch04-view-razor.html',       name: '第 4 章 View 與 Razor' },
        { id: 'ch05',  file: 'ch05-forms.html',            name: '第 5 章 表單、繫結與驗證' },
        { id: 'ch06',  file: 'ch06-efcore.html',           name: '第 6 章 資料庫與 EF Core' },
        { id: 'ch07',  file: 'ch07-first-crud.html',       name: '第 7 章 第一個完整 CRUD' },
        { id: 'ch08',  file: 'ch08-relations.html',        name: '第 8 章 關聯資料' },
        { id: 'ch09',  file: 'ch09-services-di-tests.html', name: '第 9 章 服務層、DI 與測試' },
        { id: 'ch10',  file: 'ch10-auth.html',             name: '第 10 章 登入與權限' },
        { id: 'ch11',  file: 'ch11-leave-approval.html',   name: '第 11 章 請假與簽核' },
        { id: 'ch12',  file: 'ch12-finish.html',           name: '第 12 章 收尾與接下來' }
    ];

    const STORE_KEY = 'leavesystem-learn-progress';

    // localStorage 在某些瀏覽器用 file:// 開啟時會被擋，
    // 所以全部包在 try/catch：存不了就只在本次瀏覽有效，功能不會壞。
    function loadProgress() {
        try {
            return JSON.parse(localStorage.getItem(STORE_KEY) || '{}');
        } catch (e) {
            return {};
        }
    }
    function saveProgress(data) {
        try {
            localStorage.setItem(STORE_KEY, JSON.stringify(data));
        } catch (e) { /* 無法保存，忽略 */ }
    }

    const progress = loadProgress();

    // ── 1. 檢查點勾選 ────────────────────────────────────────
    const checkpoint = document.querySelector('.checkpoint');
    const chapterId = document.body.getAttribute('data-chapter');

    if (checkpoint && chapterId) {
        const items = Array.from(checkpoint.querySelectorAll('li'));
        const saved = progress[chapterId] || [];

        items.forEach((li, i) => {
            const box = document.createElement('input');
            box.type = 'checkbox';
            box.checked = saved.indexOf(i) !== -1;
            li.classList.toggle('done', box.checked);
            li.insertBefore(box, li.firstChild);

            box.addEventListener('change', () => {
                li.classList.toggle('done', box.checked);
                const checked = items
                    .map((el, idx) => (el.querySelector('input').checked ? idx : -1))
                    .filter((idx) => idx !== -1);
                progress[chapterId] = checked;
                saveProgress(progress);
                paintHeaderProgress(checked.length, items.length);
            });
        });

        paintHeaderProgress(saved.length, items.length);
    }

    function paintHeaderProgress(done, total) {
        const rail = document.querySelector('.progress-rail > i');
        if (rail && total > 0) rail.style.width = Math.round((done / total) * 100) + '%';
    }

    // ── 2. 首頁課程地圖的完成度 ──────────────────────────────
    const overall = document.querySelector('.overall');
    if (overall) {
        let doneChapters = 0;
        const total = CHAPTERS.length - 1;   // 不算課程地圖本身

        CHAPTERS.slice(1).forEach((ch) => {
            const item = document.querySelector('.ch-item[data-chapter="' + ch.id + '"]');
            if (!item) return;
            // 有勾過任何一項就算「進行中」，全部勾完才算完成
            const expected = Number(item.getAttribute('data-checks') || 0);
            const got = (progress[ch.id] || []).length;
            if (expected > 0 && got >= expected) {
                doneChapters++;
                item.classList.add('done');
                const tick = document.createElement('span');
                tick.className = 'tick';
                tick.textContent = '✓ 完成';
                item.appendChild(tick);
            } else if (got > 0) {
                const tick = document.createElement('span');
                tick.className = 'tick';
                tick.style.color = 'var(--accent-dark)';
                tick.textContent = got + '/' + expected;
                item.appendChild(tick);
            }
        });

        const big = overall.querySelector('.big');
        const rail = overall.querySelector('.rail > i');
        if (big) big.textContent = doneChapters + ' / ' + total + ' 章完成';
        if (rail) rail.style.width = Math.round((doneChapters / total) * 100) + '%';

        const reset = overall.querySelector('button');
        if (reset) {
            reset.addEventListener('click', () => {
                if (!window.confirm('確定要清除所有章節的檢查點紀錄嗎？')) return;
                try { localStorage.removeItem(STORE_KEY); } catch (e) { /* ignore */ }
                window.location.reload();
            });
        }
    }

    // ── 3. 章節切換下拉 ──────────────────────────────────────
    const jump = document.querySelector('.chapter-jump');
    if (jump) {
        CHAPTERS.forEach((ch) => {
            const opt = document.createElement('option');
            opt.value = ch.file;
            opt.textContent = ch.name;
            if (ch.id === (chapterId || 'index')) opt.selected = true;
            jump.appendChild(opt);
        });
        jump.addEventListener('change', () => { window.location.href = jump.value; });
    }

    // ── 4. 程式碼複製按鈕 ────────────────────────────────────
    document.querySelectorAll('.code').forEach((block) => {
        const head = block.querySelector('.code-head');
        const pre = block.querySelector('pre');
        if (!head || !pre) return;

        const btn = document.createElement('button');
        btn.className = 'copy';
        btn.type = 'button';
        btn.textContent = '複製';
        head.appendChild(btn);

        btn.addEventListener('click', () => {
            const text = pre.innerText;
            const done = () => {
                btn.textContent = '已複製 ✓';
                setTimeout(() => { btn.textContent = '複製'; }, 1600);
            };

            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(text).then(done, fallback);
            } else {
                fallback();
            }

            // file:// 開啟時 navigator.clipboard 不可用，改用舊方法
            function fallback() {
                const ta = document.createElement('textarea');
                ta.value = text;
                ta.style.position = 'fixed';
                ta.style.opacity = '0';
                document.body.appendChild(ta);
                ta.select();
                try { document.execCommand('copy'); done(); }
                catch (e) { btn.textContent = '請手動選取'; }
                document.body.removeChild(ta);
            }
        });
    });

    // ── 5. 鍵盤導覽：← → 上下一章 ────────────────────────────
    document.addEventListener('keydown', (e) => {
        const tag = (document.activeElement && document.activeElement.tagName) || '';
        if (/^(INPUT|TEXTAREA|SELECT)$/.test(tag)) return;

        const idx = CHAPTERS.findIndex((c) => c.id === (chapterId || 'index'));
        if (idx === -1) return;

        if (e.key === 'ArrowRight' && idx < CHAPTERS.length - 1) {
            window.location.href = CHAPTERS[idx + 1].file;
        } else if (e.key === 'ArrowLeft' && idx > 0) {
            window.location.href = CHAPTERS[idx - 1].file;
        } else if (e.key === 'Home') {
            window.location.href = 'index.html';
        }
    });
})();
