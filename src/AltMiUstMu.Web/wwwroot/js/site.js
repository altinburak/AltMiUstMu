// Alt mı Üst mü? — small client-side helpers (Alpine components, htmx glue, charts).
(function () {
  'use strict';

  var THEME_KEY = 'altmiustmu-theme';

  // UI language comes from <html lang> (set server side): Turkish by default, English when chosen.
  var LANG = document.documentElement.lang === 'en' ? 'en' : 'tr';

  function t(tr, en) {
    return LANG === 'en' ? en : tr;
  }

  function isDark() {
    return document.documentElement.classList.contains('dark');
  }

  function toast(message, type) {
    window.dispatchEvent(new CustomEvent('toast', { detail: { message: message, type: type || 'info' } }));
  }

  function fallback(text) {
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.setAttribute('readonly', '');
    ta.style.position = 'fixed';
    ta.style.opacity = '0';
    document.body.appendChild(ta);
    ta.select();
    try { document.execCommand('copy'); } catch (e) { /* ignore */ }
    document.body.removeChild(ta);
  }

  window.altmiustmu = { toast: toast };

  document.addEventListener('alpine:init', function () {
    var Alpine = window.Alpine;

    Alpine.data('themeToggle', function () {
      return {
        dark: isDark(),
        toggle: function () {
          this.dark = !this.dark;
          document.documentElement.classList.toggle('dark', this.dark);
          try { localStorage.setItem(THEME_KEY, this.dark ? 'dark' : 'light'); } catch (e) { /* private mode */ }
          window.dispatchEvent(new CustomEvent('themechange'));
        }
      };
    });

    Alpine.data('toasts', function () {
      return {
        items: [],
        push: function (detail) {
          var id = Date.now() + Math.random();
          this.items.push({ id: id, message: detail.message || detail.value || '', type: detail.type || 'info' });
          var self = this;
          setTimeout(function () { self.items = self.items.filter(function (t) { return t.id !== id; }); }, 3500);
        }
      };
    });

    // Countdown to an ISO timestamp (UTC). Shows days / hours / minutes / seconds.
    Alpine.data('countdown', function (iso) {
      return {
        target: new Date(iso).getTime(),
        d: '00', h: '00', m: '00', s: '00', done: false, timer: null,
        init: function () {
          var self = this;
          self.tick();
          self.timer = setInterval(function () { self.tick(); }, 1000);
        },
        destroy: function () { clearInterval(this.timer); },
        tick: function () {
          var diff = Math.max(0, this.target - Date.now());
          this.done = diff === 0;
          var pad = function (n) { return String(n).padStart(2, '0'); };
          this.d = pad(Math.floor(diff / 86400000));
          this.h = pad(Math.floor(diff / 3600000) % 24);
          this.m = pad(Math.floor(diff / 60000) % 60);
          this.s = pad(Math.floor(diff / 1000) % 60);
        }
      };
    });

    Alpine.data('copyText', function (text) {
      return {
        copied: false,
        copy: function () {
          var self = this;
          var done = function () {
            self.copied = true;
            toast(t('Bağlantı kopyalandı', 'Link copied'));
            setTimeout(function () { self.copied = false; }, 2000);
          };
          if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(done, function () { fallback(text); done(); });
          } else {
            fallback(text);
            done();
          }
        },
        share: function (title) {
          if (navigator.share) {
            navigator.share({ title: title, url: text }).catch(function () { });
          } else {
            this.copy();
          }
        }
      };
    });

    // Remembers whether pundit picks are shown inline on the picks page.
    Alpine.data('punditToggle', function () {
      var initial = false;
      try { initial = localStorage.getItem('altmiustmu-pundits') === '1'; } catch (e) { }
      return {
        showPundits: initial,
        flip: function () {
          this.showPundits = !this.showPundits;
          try { localStorage.setItem('altmiustmu-pundits', this.showPundits ? '1' : '0'); } catch (e) { }
        }
      };
    });

    // Client-side sortable table (teams table: 30 rows, no server round trip needed).
    Alpine.data('sortableTable', function (defaultKey, defaultDir) {
      return {
        key: defaultKey,
        dir: defaultDir || 'asc',
        sort: function (key, type) {
          if (this.key === key) { this.dir = this.dir === 'asc' ? 'desc' : 'asc'; } else { this.key = key; this.dir = type === 'text' ? 'asc' : 'desc'; }
          var tbody = this.$refs.body;
          var rows = Array.prototype.slice.call(tbody.querySelectorAll('tr'));
          var dir = this.dir === 'asc' ? 1 : -1;
          rows.sort(function (a, b) {
            var av = a.dataset[key], bv = b.dataset[key];
            if (type === 'text') { return av.localeCompare(bv, LANG) * dir; }
            return ((parseFloat(av) || 0) - (parseFloat(bv) || 0)) * dir;
          });
          rows.forEach(function (r) { tbody.appendChild(r); });
        },
        ariaSort: function (key) {
          return this.key === key ? (this.dir === 'asc' ? 'ascending' : 'descending') : 'none';
        }
      };
    });
  });

  // ---------- htmx ----------
  document.addEventListener('DOMContentLoaded', function () {
    if (!window.htmx) { return; }
    window.htmx.config.defaultSwapStyle = 'outerHTML';
    window.htmx.config.scrollIntoViewOnBoost = false;
    window.htmx.config.historyCacheSize = 0;
  });

  document.addEventListener('htmx:responseError', function (e) {
    var status = e.detail.xhr ? e.detail.xhr.status : 0;
    if (status === 429) { toast(t('Çok hızlı gidiyorsun, biraz bekleyip tekrar dene.', 'You\'re going too fast, wait a moment and try again.'), 'error'); }
    else if (status === 401 || status === 403) { toast(t('Bu işlem için giriş yapmalısın.', 'You need to sign in to do this.'), 'error'); }
    else { toast(t('Bir şeyler ters gitti. Lütfen tekrar dene.', 'Something went wrong. Please try again.'), 'error'); }
  });

  document.addEventListener('htmx:sendError', function () {
    toast(t('Bağlantı hatası. İnternetini kontrol edip tekrar dene.', 'Connection error. Check your internet and try again.'), 'error');
  });

  // ---------- Charts ----------
  function chartColors() {
    var dark = isDark();
    return {
      grid: dark ? 'rgba(148,163,184,0.12)' : 'rgba(15,23,42,0.08)',
      text: dark ? '#a1a1aa' : '#52525b',
      brand: '#f97316',
      sky: '#0ea5e9'
    };
  }

  // Rank history: lower is better, so the y axis is reversed.
  window.renderRankChart = function (canvasId, data) {
    var canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) { return; }
    var chart;
    var draw = function () {
      if (chart) { chart.destroy(); }
      var c = chartColors();
      var datasets = [{
        label: t('Sıra', 'Rank'),
        data: data.ranks,
        borderColor: c.brand,
        backgroundColor: 'rgba(249,115,22,0.12)',
        fill: false,
        borderWidth: 2.5,
        tension: 0.3,
        pointRadius: data.ranks.length > 20 ? 0 : 3,
        pointHoverRadius: 5,
        yAxisID: 'y'
      }];
      if (data.projected) {
        datasets.push({
          label: t('Projeksiyon', 'Projection'),
          data: data.projected,
          borderColor: c.sky,
          borderDash: [4, 4],
          tension: 0.3,
          pointRadius: 0,
          yAxisID: 'y2'
        });
      }
      chart = new window.Chart(canvas, {
        type: 'line',
        data: { labels: data.labels, datasets: datasets },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: { mode: 'index', intersect: false },
          plugins: {
            legend: { display: !!data.projected, labels: { color: c.text, boxWidth: 12, usePointStyle: true } },
            tooltip: { callbacks: { label: function (ctx) { return ctx.dataset.label + ': ' + ctx.formattedValue; } } }
          },
          scales: {
            x: { grid: { display: false }, ticks: { color: c.text, maxTicksLimit: 6, maxRotation: 0 } },
            y: { reverse: true, min: 1, suggestedMax: data.maxRank || 10, ticks: { color: c.text, precision: 0 }, grid: { color: c.grid }, title: { display: true, text: t('Sıra', 'Rank'), color: c.text } },
            y2: { display: !!data.projected, position: 'right', min: 0, max: 30, ticks: { color: c.text, precision: 0 }, grid: { display: false } }
          }
        }
      });
      var skeleton = document.querySelector('[data-skeleton-for="' + canvasId + '"]');
      if (skeleton) { skeleton.remove(); }
    };
    draw();
    window.addEventListener('themechange', draw);
  };
})();
