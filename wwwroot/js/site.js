(function () {
  "use strict";

  // ----- Theme toggle -----
  var THEME_KEY = "dkh-theme";
  function applyTheme(theme) {
    if (theme === "light" || theme === "dark") {
      document.documentElement.setAttribute("data-theme", theme);
    } else {
      document.documentElement.removeAttribute("data-theme");
    }
  }
  function currentEffectiveTheme() {
    var explicit = document.documentElement.getAttribute("data-theme");
    if (explicit) return explicit;
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  }
  try {
    var saved = localStorage.getItem(THEME_KEY);
    if (saved) applyTheme(saved);
  } catch (e) { /* storage unavailable: ignore, default theme stands */ }

  document.addEventListener("DOMContentLoaded", function () {
    var toggle = document.querySelector(".theme-toggle");
    if (toggle) {
      toggle.addEventListener("click", function () {
        var next = currentEffectiveTheme() === "dark" ? "light" : "dark";
        applyTheme(next);
        try { localStorage.setItem(THEME_KEY, next); } catch (e) { /* ignore */ }
      });
    }

    // ----- Mobile sidebar -----
    var menuToggle = document.querySelector(".menu-toggle");
    var sidebar = document.querySelector(".sidebar");
    if (menuToggle && sidebar) {
      menuToggle.addEventListener("click", function () {
        sidebar.classList.toggle("open");
      });
      document.addEventListener("click", function (e) {
        if (sidebar.classList.contains("open") && !sidebar.contains(e.target) && !menuToggle.contains(e.target)) {
          sidebar.classList.remove("open");
        }
      });
    }

    // ----- Tabs -----
    document.querySelectorAll(".tabs").forEach(function (tabList) {
      var buttons = tabList.querySelectorAll(".tab-btn");
      var container = tabList.closest(".examples-block");
      if (!container) return;
      var panels = container.querySelectorAll(".tab-panel");
      buttons.forEach(function (btn, idx) {
        btn.addEventListener("click", function () {
          buttons.forEach(function (b) { b.classList.remove("active"); });
          panels.forEach(function (p) { p.classList.remove("active"); });
          btn.classList.add("active");
          if (panels[idx]) panels[idx].classList.add("active");
        });
      });
    });

    // ----- Copy buttons on code blocks -----
    document.querySelectorAll(".case-section-body pre, .tab-panel pre, .ref-body pre").forEach(function (pre) {
      if (pre.querySelector(".copy-btn")) return;
      var btn = document.createElement("button");
      btn.type = "button";
      btn.className = "copy-btn";
      btn.textContent = "Copy";
      btn.setAttribute("aria-label", "Copy code to clipboard");
      btn.addEventListener("click", function () {
        var code = pre.querySelector("code");
        var text = code ? code.textContent : pre.textContent;
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(text).then(function () {
            btn.textContent = "Copied";
            btn.classList.add("copied");
            setTimeout(function () { btn.textContent = "Copy"; btn.classList.remove("copied"); }, 1500);
          });
        }
      });
      pre.appendChild(btn);
    });

    // ----- Search -----
    var input = document.querySelector("#site-search-input");
    var resultsBox = document.querySelector("#site-search-results");
    if (input && resultsBox) {
      var debounceTimer = null;
      input.addEventListener("input", function () {
        clearTimeout(debounceTimer);
        var query = input.value.trim();
        if (query.length < 2) {
          resultsBox.classList.remove("open");
          resultsBox.innerHTML = "";
          return;
        }
        debounceTimer = setTimeout(function () { runSearch(query); }, 180);
      });

      input.addEventListener("focus", function () {
        if (resultsBox.innerHTML.trim() !== "") resultsBox.classList.add("open");
      });

      document.addEventListener("click", function (e) {
        if (!resultsBox.contains(e.target) && e.target !== input) {
          resultsBox.classList.remove("open");
        }
      });

      document.addEventListener("keydown", function (e) {
        if ((e.key === "/" && document.activeElement !== input) ) {
          e.preventDefault();
          input.focus();
        }
        if (e.key === "Escape") {
          resultsBox.classList.remove("open");
          input.blur();
        }
      });

      function runSearch(query) {
        fetch("/api/search?q=" + encodeURIComponent(query))
          .then(function (r) { return r.json(); })
          .then(function (items) {
            if (!items.length) {
              resultsBox.innerHTML = '<div class="search-empty">No matches for "' + escapeHtml(query) + '"</div>';
              resultsBox.classList.add("open");
              return;
            }
            resultsBox.innerHTML = items.map(function (item) {
              return '<a class="search-result-item" href="' + item.url + '">' +
                '<div class="search-result-title">' + escapeHtml(item.title) + '</div>' +
                '<div class="search-result-meta">' + escapeHtml(item.kind) + ' · ' + escapeHtml(item.topicTitle) + '</div>' +
                '</a>';
            }).join("");
            resultsBox.classList.add("open");
          })
          .catch(function () { /* network hiccup: leave existing results in place */ });
      }

      function escapeHtml(s) {
        return s.replace(/[&<>"']/g, function (c) {
          return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
        });
      }
    }
  });
})();
