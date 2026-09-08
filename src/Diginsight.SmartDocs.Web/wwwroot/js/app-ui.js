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

/* ---------------------------------------------------------------------------
   Reading-preferences mirror.
   The Blazor PreferencesState is authoritative; localStorage is only a mirror
   read once on first interactive render. Every call is defensive because
   storage can be unavailable (private mode, disabled cookies) and a throw here
   would break the layout's OnAfterRenderAsync.
   --------------------------------------------------------------------------- */
(function () {
    window.appUi = window.appUi || {};

    window.appUi.prefsLoad = function (key) {
        try { return localStorage.getItem(key); } catch (e) { return null; }
    };

    window.appUi.prefsSave = function (key, json) {
        try { localStorage.setItem(key, json); } catch (e) { /* ignore */ }
        return true;
    };

    window.appUi.prefsClear = function (key) {
        try { localStorage.removeItem(key); } catch (e) { /* ignore */ }
        return true;
    };
})();

/* ---------------------------------------------------------------------------
   "/" focuses the library search box, matching the hint rendered beside it.
   Ignored while the caret is already in a field, so typing a slash into prose
   or into the search box itself behaves normally.
   --------------------------------------------------------------------------- */
(function () {
    document.addEventListener('keydown', function (e) {
        if (e.key !== '/' || e.metaKey || e.ctrlKey || e.altKey) { return; }

        var t = e.target;
        if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable)) {
            return;
        }

        var box = document.querySelector('.topsearch input');
        if (box) {
            e.preventDefault();
            box.focus();
            box.select();
        }
    });
})();

/* ---------------------------------------------------------------------------
   In-page anchors: scroll instead of navigating away.

   App.razor declares <base href="/">, and the HTML spec resolves a fragment-only
   href against the document base — not against the current URL. So every "#section"
   link (the table of contents, and any anchor an author writes inside an article)
   resolved to "/#section" and threw the reader out of the article onto the site
   root, without scrolling anywhere.

   Handled here rather than by rewriting hrefs, because it has to cover links that
   come from rendered Markdown as well as the ones the app renders itself.
   --------------------------------------------------------------------------- */
(function () {
    function fragmentOf(anchor) {
        var raw = anchor.getAttribute('href');
        return raw && raw.length > 1 && raw.charAt(0) === '#' ? raw.slice(1) : null;
    }

    function find(id) {
        var decoded;
        try { decoded = decodeURIComponent(id); } catch (e) { decoded = id; }
        return document.getElementById(decoded)
            || document.getElementById(id)
            || document.querySelector('[name="' + CSS.escape(decoded) + '"]');
    }

    function reveal(target, id, smooth) {
        target.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto', block: 'start' });

        // Move the caret so the heading is where keyboard and screen-reader users continue from.
        var restore = target.getAttribute('tabindex');
        if (restore === null) { target.setAttribute('tabindex', '-1'); }
        target.focus({ preventScroll: true });
        if (restore === null) { target.removeAttribute('tabindex'); }

        // replaceState, not the hash: assigning location.hash would hand the URL back to the
        // router and undo the whole point. This keeps the address copyable.
        try {
            history.replaceState(null, '', location.pathname + location.search + '#' + id);
        } catch (e) { /* ignore */ }
    }

    function scrollerOf(el) {
        var e = el.parentElement;
        while (e) {
            if (/auto|scroll/.test(getComputedStyle(e).overflowY)) { return e; }
            e = e.parentElement;
        }
        return null;
    }

    function settled(target) {
        var box = scrollerOf(target);
        if (!box) { return true; }

        // A scroller that cannot scroll yet is not "already at the end" - it is a page that has not
        // finished arriving. Conflating the two made a single early scroll look successful while
        // the reader was still sitting at the top of the article.
        var overflows = box.scrollHeight - box.clientHeight > 1;
        var atTop = Math.abs(target.getBoundingClientRect().top - box.getBoundingClientRect().top) < 4;
        var atEnd = overflows && box.scrollTop >= box.scrollHeight - box.clientHeight - 1;

        return atTop || atEnd;
    }

    // Reaching the section once is not enough. The article arrives after the runtime boots, it
    // keeps growing as images lay out, and the runtime later replaces the rendered article - which
    // puts the reader back at the top. So keep re-asserting the requested section until the budget
    // runs out, and let any real gesture from the reader end the watch immediately.
    var cancelWatch = null;

    function honour(id, budgetMs) {
        var done = false;
        var deadline = Date.now() + budgetMs;
        function stop() { done = true; }

        // Only one section can be the destination: without this, clicking a second entry while the
        // first watch is still running leaves two watches fighting over the same scroller.
        if (cancelWatch) { cancelWatch(); }
        cancelWatch = stop;

        function tick() {
            if (done) { return; }

            var target = find(id);
            if (target && !settled(target)) { reveal(target, id, false); }

            if (Date.now() > deadline) { stop(); } else { setTimeout(tick, 120); }
        }

        window.addEventListener('wheel', stop, { passive: true, once: true });
        window.addEventListener('keydown', stop, { once: true });

        tick();
    }

    // On window, not document: capture travels window -> document -> element, so a window-level
    // capture listener is guaranteed to see the click before the router's document-level one,
    // whichever script happened to load first.
    window.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) {
            return;
        }

        var anchor = e.target && e.target.closest ? e.target.closest('a[href]') : null;
        if (!anchor || anchor.target === '_blank') { return; }

        var id = fragmentOf(anchor);
        if (!id) { return; }

        // Always take the click, even when the heading cannot be found this instant: the runtime
        // swaps the rendered article in and out, and during that gap the browser's own handling
        // would resolve "#section" against the base href and throw the reader onto the site root.
        // stopPropagation is needed too, because the router listens for document clicks as well.
        e.preventDefault();
        e.stopPropagation();

        var target = find(id);
        if (target) {
            reveal(target, id, true);
            honour(id, 8000);
        } else {
            try { history.replaceState(null, '', location.pathname + location.search + '#' + id); } catch (err) { /* ignore */ }
            honour(id, 8000);
        }
    }, true);

    // A pasted or reloaded "…#section" URL has to land on the section too.
    if (location.hash && location.hash.length > 1) {
        honour(location.hash.slice(1), 25000);
    }
})();
