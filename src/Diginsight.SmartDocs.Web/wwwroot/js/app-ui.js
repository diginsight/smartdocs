// Draggable sidebar resizer. Drag the handle to resize; double-click to reset.
// Width is stored in the CSS variable --sidebar-width and persisted to localStorage.
window.appUi = {
    initResizer: function () {
        try {
            var saved = localStorage.getItem('lh-sidebar-width');
            if (saved) {
                document.documentElement.style.setProperty('--sidebar-width', saved);
            }
        } catch (e) { /* ignore */ }

        var resizer = document.querySelector('.sidebar-resizer');
        if (!resizer || resizer.dataset.init === '1') {
            return;
        }
        resizer.dataset.init = '1';

        var dragging = false;
        var minWidth = 180;
        var maxWidth = 640;

        function clientX(e) {
            return e.touches && e.touches.length ? e.touches[0].clientX : e.clientX;
        }

        function onMove(e) {
            if (!dragging) { return; }
            var width = Math.max(minWidth, Math.min(maxWidth, clientX(e)));
            document.documentElement.style.setProperty('--sidebar-width', width + 'px');
        }

        function stop() {
            if (!dragging) { return; }
            dragging = false;
            document.body.style.userSelect = '';
            document.body.style.cursor = '';
            try {
                var w = getComputedStyle(document.documentElement).getPropertyValue('--sidebar-width').trim();
                localStorage.setItem('lh-sidebar-width', w);
            } catch (e) { /* ignore */ }
        }

        resizer.addEventListener('mousedown', function () {
            dragging = true;
            document.body.style.userSelect = 'none';
            document.body.style.cursor = 'col-resize';
        });
        resizer.addEventListener('touchstart', function () { dragging = true; }, { passive: true });
        window.addEventListener('mousemove', onMove);
        window.addEventListener('touchmove', onMove, { passive: true });
        window.addEventListener('mouseup', stop);
        window.addEventListener('touchend', stop);

        resizer.addEventListener('dblclick', function () {
            document.documentElement.style.setProperty('--sidebar-width', '280px');
            try { localStorage.setItem('lh-sidebar-width', '280px'); } catch (e) { /* ignore */ }
        });
    },

    // Resizer for the docked right-hand TOC pane. The handle sits on the pane's
    // LEFT edge and is (re)created by Blazor whenever the pane is shown, so we use
    // event delegation on the document instead of binding to a specific element.
    initTocResizer: function () {
        if (window.__lhTocResizerInit) { return; }
        window.__lhTocResizerInit = true;

        try {
            var saved = localStorage.getItem('lh-toc-width');
            if (saved) {
                document.documentElement.style.setProperty('--toc-width', saved);
            }
        } catch (e) { /* ignore */ }

        var dragging = false;
        var minWidth = 180;
        var maxWidth = 560;

        function clientX(e) {
            return e.touches && e.touches.length ? e.touches[0].clientX : e.clientX;
        }

        function onMove(e) {
            if (!dragging) { return; }
            // Pane is docked to the right edge; width grows as the pointer moves left.
            var width = Math.max(minWidth, Math.min(maxWidth, window.innerWidth - clientX(e)));
            document.documentElement.style.setProperty('--toc-width', width + 'px');
        }

        function stop() {
            if (!dragging) { return; }
            dragging = false;
            document.body.style.userSelect = '';
            document.body.style.cursor = '';
            try {
                var w = getComputedStyle(document.documentElement).getPropertyValue('--toc-width').trim();
                if (w) { localStorage.setItem('lh-toc-width', w); }
            } catch (e) { /* ignore */ }
        }

        document.addEventListener('mousedown', function (e) {
            if (e.target && e.target.classList && e.target.classList.contains('toc-resizer')) {
                dragging = true;
                document.body.style.userSelect = 'none';
                document.body.style.cursor = 'col-resize';
                e.preventDefault();
            }
        });
        document.addEventListener('dblclick', function (e) {
            if (e.target && e.target.classList && e.target.classList.contains('toc-resizer')) {
                document.documentElement.style.setProperty('--toc-width', '260px');
                try { localStorage.setItem('lh-toc-width', '260px'); } catch (e2) { /* ignore */ }
            }
        });
        window.addEventListener('mousemove', onMove);
        window.addEventListener('touchmove', onMove, { passive: true });
        window.addEventListener('mouseup', stop);
        window.addEventListener('touchend', stop);
    },

    // Scroll the currently-selected sidebar item into view (called after navigation).
    scrollActiveNavIntoView: function () {
        try {
            var el = document.querySelector('.sidebar .nav-link.active');
            if (el) { el.scrollIntoView({ block: 'nearest', inline: 'nearest' }); }
        } catch (e) { /* ignore */ }
    },

    // Responsive: collapse the sidebar to the icon rail on narrow viewports (usable via the hover
    // flyout), expand it on wide ones. Only notifies Blazor when crossing the breakpoint.
    initResponsive: function (dotNetRef) {
        if (window.__lhResponsive) { return; }
        window.__lhResponsive = true;

        var breakpoint = 820;
        var last = null;
        var timer;

        function report() {
            var collapsed = window.innerWidth < breakpoint;
            if (collapsed === last) { return; }
            last = collapsed;
            try { dotNetRef.invokeMethodAsync('SetSidebarCollapsed', collapsed); } catch (e) { /* ignore */ }
        }

        report();
        window.addEventListener('resize', function () {
            clearTimeout(timer);
            timer = setTimeout(report, 120);
        });
    }
};

// Space activates a focused link (anchors respond only to Enter by default), so Tabbing to a
// menu/article link and pressing Space selects it — matching button-like keyboard behaviour.
(function () {
    if (window.__lhSpaceActivate) { return; }
    window.__lhSpaceActivate = true;

    document.addEventListener('keydown', function (e) {
        if (e.key !== ' ' && e.key !== 'Spacebar') { return; }

        var el = document.activeElement;
        if (!el) { return; }

        if (el.tagName === 'A' && (
            el.classList.contains('nav-link') ||
            el.classList.contains('topmenu-link') ||
            el.classList.contains('breadcrumb-link') ||
            el.classList.contains('crumb-navbtn'))) {
            e.preventDefault();
            el.click();
            return;
        }

        // Folder summary: Space toggles expand/collapse (via the twisty, so it never navigates).
        if (el.tagName === 'SUMMARY' && el.closest('.dynnav')) {
            var tw = el.querySelector('.nav-twisty');
            if (tw) { e.preventDefault(); tw.click(); }
        }
    });
})();

// Sidebar keyboard: Arrow Up/Down move focus between menu items (like Tab / Shift+Tab); the menu
// no longer scrolls on plain arrows. Ctrl+Arrow Up/Down scrolls the menu instead.
(function () {
    if (window.__lhArrowNav) { return; }
    window.__lhArrowNav = true;

    function menuItems() {
        return Array.prototype.slice.call(
            document.querySelectorAll('.dynnav .nav-list a.nav-link, .dynnav .nav-list summary'));
    }

    function scrollParent(el) {
        var n = el;
        while (n && n !== document.body) {
            var s = getComputedStyle(n);
            if (/(auto|scroll)/.test(s.overflowY) && n.scrollHeight > n.clientHeight) { return n; }
            n = n.parentElement;
        }
        return document.scrollingElement || document.documentElement;
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') { return; }

        var el = document.activeElement;
        var isItem = el && ((el.tagName === 'A' && el.classList.contains('nav-link')) || el.tagName === 'SUMMARY');
        if (!isItem || !el.closest('.dynnav')) { return; }

        // Ctrl+Arrow → scroll the menu (the behaviour plain arrows used to have).
        if (e.ctrlKey) {
            e.preventDefault();
            scrollParent(el).scrollBy({ top: e.key === 'ArrowDown' ? 64 : -64 });
            return;
        }

        // Plain Arrow → move focus to the previous/next visible menu item.
        e.preventDefault();
        var items = menuItems();
        var i = items.indexOf(el);
        if (i < 0) { return; }
        var next = e.key === 'ArrowDown' ? i + 1 : i - 1;
        if (next >= 0 && next < items.length) {
            items[next].focus();
        }
    });
})();

// Left/Right arrows on a focused folder (section) collapse/expand it (standard tree behaviour).
// Sections are Blazor-controlled <details>, so we click the <summary> to keep Blazor's open state
// and lazy child-loading in sync rather than toggling the DOM attribute directly.
(function () {
    if (window.__lhTreeArrows) { return; }
    window.__lhTreeArrows = true;

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') { return; }

        var el = document.activeElement;
        if (!el || !el.closest('.dynnav')) { return; }

        // Article link: Left arrow selects (focuses) the containing folder.
        if (el.tagName === 'A' && el.classList.contains('nav-link')) {
            if (e.key === 'ArrowLeft') {
                var pd = el.closest('details');
                var psum = pd && pd.querySelector(':scope > summary');
                if (psum) { e.preventDefault(); psum.focus(); }
            }
            return;
        }

        // Folder summary: Right opens (or steps into first child); Left collapses (or steps to parent).
        // Expand/collapse goes through the twisty so it never navigates — only structural movement.
        if (el.tagName !== 'SUMMARY') { return; }

        var details = el.parentElement;
        if (!details || details.tagName !== 'DETAILS') { return; }
        var twisty = el.querySelector('.nav-twisty');

        if (e.key === 'ArrowRight') {
            e.preventDefault();
            if (!details.open) {
                if (twisty) { twisty.click(); } // open (no navigation)
            } else {
                var child = details.querySelector(':scope > ul.nav-list a.nav-link, :scope > ul.nav-list summary');
                if (child) { child.focus(); } // already open → step into first child
            }
        } else { // ArrowLeft
            e.preventDefault();
            if (details.open) {
                if (twisty) { twisty.click(); } // collapse (no navigation)
            } else {
                var parent = details.parentElement && details.parentElement.closest('details');
                var ps = parent && parent.querySelector(':scope > summary');
                if (ps) { ps.focus(); } // already closed → go to parent folder
            }
        }
    });
})();

// ---------------------------------------------------------------------------
// Mermaid diagrams. The Markdown renderer emits ```mermaid fences as
// <pre class="mermaid">…source…</pre>; here we lazily import Mermaid from the CDN
// (same approach as the bootstrap-icons CDN) and turn those into SVG after each
// content render. Diagrams re-render on theme change so they match light/dark.
(function () {
    var loadPromise = null;

    function isDark() {
        try {
            var bg = getComputedStyle(document.body).backgroundColor || '';
            var m = bg.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
            if (!m) { return false; }
            var lum = (0.2126 * +m[1] + 0.7152 * +m[2] + 0.0722 * +m[3]) / 255;
            return lum < 0.5;
        } catch (e) { return false; }
    }

    function load() {
        if (loadPromise) { return loadPromise; }
        loadPromise = import('https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs')
            .then(function (mod) {
                var mermaid = mod.default;
                mermaid.initialize({ startOnLoad: false, theme: isDark() ? 'dark' : 'default' });
                return mermaid;
            });
        return loadPromise;
    }

    function run(nodes) {
        if (!nodes.length) { return; }
        load().then(function (mermaid) {
            nodes.forEach(function (n) {
                if (!n.hasAttribute('data-src')) { n.setAttribute('data-src', n.textContent); }
            });
            try { mermaid.run({ nodes: nodes, suppressErrors: true }); } catch (e) { /* ignore */ }
        }).catch(function () { /* ignore: leave the source visible if the CDN is unreachable */ });
    }

    window.appUi = window.appUi || {};

    // Render any not-yet-processed diagrams (called after each content render).
    window.appUi.renderMermaid = function () {
        run(Array.prototype.slice.call(document.querySelectorAll('pre.mermaid:not([data-processed])')));
    };

    // Restore original source and re-render all diagrams with the current theme (on light/dark switch).
    window.appUi.rerenderMermaid = function () {
        if (!loadPromise) { window.appUi.renderMermaid(); return; }
        var all = Array.prototype.slice.call(document.querySelectorAll('pre.mermaid'));
        all.forEach(function (n) {
            var src = n.getAttribute('data-src');
            if (src !== null) { n.textContent = src; n.removeAttribute('data-processed'); }
        });
        load().then(function (mermaid) {
            try { mermaid.initialize({ startOnLoad: false, theme: isDark() ? 'dark' : 'default' }); } catch (e) { /* ignore */ }
            run(all);
        });
    };
})();
