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

  // Pick count seen by the progress bar; null until the page's first render.
  var lastPickCount = null;

  // ---------- Share card (1080×1350, Instagram portrait) ----------
  var CARD = { w: 1080, h: 1350, pad: 72, bg: '#07090d', tile: '#0d1117', border: '#263142', over: '#f97316', under: '#0ea5e9', muted: '#a1a1aa' };

  function font(weight, size) {
    return weight + ' ' + size + 'px Inter, ui-sans-serif, system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
  }

  function roundRect(ctx, x, y, w, h, r) {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  // Largest font size (down to 60%) at which the text fits the width.
  function fitFont(ctx, text, weight, size, maxWidth) {
    var s = size;
    ctx.font = font(weight, s);
    while (s > size * 0.6 && ctx.measureText(text).width > maxWidth) {
      s -= 1;
      ctx.font = font(weight, s);
    }
  }

  function drawBall(ctx, cx, cy, r) {
    ctx.save();
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fillStyle = CARD.over;
    ctx.fill();
    ctx.clip();
    ctx.strokeStyle = '#1c0d03';
    ctx.lineWidth = r * 0.12;
    ctx.beginPath();
    ctx.moveTo(cx - r, cy); ctx.lineTo(cx + r, cy);
    ctx.moveTo(cx, cy - r); ctx.lineTo(cx, cy + r);
    ctx.stroke();
    ctx.beginPath(); ctx.arc(cx - r * 1.55, cy, r * 1.25, 0, Math.PI * 2); ctx.stroke();
    ctx.beginPath(); ctx.arc(cx + r * 1.55, cy, r * 1.25, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.strokeStyle = '#1c0d03';
    ctx.lineWidth = r * 0.12;
    ctx.stroke();
  }

  function drawRuns(ctx, runs, x, y) {
    runs.forEach(function (run) {
      ctx.fillStyle = run[1];
      ctx.fillText(run[0], x, y);
      x += ctx.measureText(run[0]).width;
    });
    return x;
  }

  function drawPill(ctx, x, y, w, h, count, label, color) {
    roundRect(ctx, x, y, w, h, 24);
    ctx.fillStyle = color + '26';
    ctx.fill();
    ctx.lineWidth = 3;
    ctx.strokeStyle = color;
    ctx.stroke();
    ctx.textBaseline = 'middle';
    ctx.font = font(900, 60);
    var numW = ctx.measureText(String(count)).width;
    ctx.font = font(800, 32);
    var labelW = ctx.measureText(label).width;
    var start = x + (w - numW - 16 - labelW) / 2;
    ctx.font = font(900, 60);
    ctx.fillStyle = color;
    ctx.fillText(String(count), start, y + h / 2 + 2);
    ctx.font = font(800, 32);
    ctx.fillStyle = '#ffffff';
    ctx.fillText(label, start + numW + 16, y + h / 2 + 2);
    ctx.textBaseline = 'alphabetic';
  }

  function drawTile(ctx, team, x, y, w, h) {
    roundRect(ctx, x, y, w, h, 18);
    ctx.fillStyle = CARD.tile;
    ctx.fill();
    ctx.lineWidth = 2;
    ctx.strokeStyle = team.over === null ? CARD.border : (team.over ? CARD.over : CARD.under) + '80';
    ctx.stroke();

    // Team badge: primary fill, secondary bottom stripe (same as the site's badges).
    var b = 56, bx = x + 14, by = y + (h - b) / 2;
    ctx.save();
    roundRect(ctx, bx, by, b, b, 14);
    ctx.clip();
    ctx.fillStyle = team.primary;
    ctx.fillRect(bx, by, b, b);
    ctx.fillStyle = team.secondary;
    ctx.fillRect(bx, by + b - 7, b, 7);
    ctx.restore();
    ctx.fillStyle = team.fg;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    fitFont(ctx, team.abbr, 900, 20, b - 8);
    ctx.fillText(team.abbr, bx + b / 2, by + b / 2 - 2);
    ctx.textAlign = 'left';
    ctx.textBaseline = 'alphabetic';

    var tx = bx + b + 12, tw = x + w - tx - 10;
    ctx.fillStyle = team.over === null ? CARD.muted : (team.over ? CARD.over : CARD.under);
    fitFont(ctx, team.side || '—', 900, 28, tw);
    ctx.fillText(team.side || '—', tx, y + h / 2 + 4);
    ctx.fillStyle = CARD.muted;
    ctx.font = font(600, 19);
    ctx.fillText(team.line, tx, y + h / 2 + 30);
  }

  function drawShareCard(d) {
    var fonts = document.fonts && document.fonts.load
      ? Promise.all([document.fonts.load(font(900, 40)), document.fonts.load(font(800, 40)), document.fonts.load(font(600, 40))]).catch(function () { })
      : Promise.resolve();
    return fonts.then(function () {
      var canvas = document.createElement('canvas');
      canvas.width = CARD.w;
      canvas.height = CARD.h;
      var ctx = canvas.getContext('2d');
      var pad = CARD.pad, inner = CARD.w - pad * 2;

      ctx.fillStyle = CARD.bg;
      ctx.fillRect(0, 0, CARD.w, CARD.h);
      var glow = ctx.createRadialGradient(CARD.w, 0, 0, CARD.w, 0, 760);
      glow.addColorStop(0, 'rgba(249,115,22,0.30)');
      glow.addColorStop(1, 'rgba(249,115,22,0)');
      ctx.fillStyle = glow;
      ctx.fillRect(0, 0, CARD.w, CARD.h);

      // Header: logo, heading, player name.
      drawBall(ctx, pad + 30, 104, 30);
      ctx.font = font(900, 52);
      drawRuns(ctx, [['Alt mı ', '#ffffff'], ['Üst', CARD.over], [' mü?', '#ffffff']], pad + 78, 122);
      ctx.fillStyle = CARD.over;
      ctx.font = font(800, 28);
      ctx.fillText(d.heading.toLocaleUpperCase(document.documentElement.lang), pad, 206);
      ctx.fillStyle = '#ffffff';
      fitFont(ctx, d.name, 900, 52, inner);
      ctx.fillText(d.name, pad, 266);

      var pillW = (inner - 20) / 2;
      drawPill(ctx, pad, 296, pillW, 88, d.over, d.overLabel, CARD.over);
      drawPill(ctx, pad + pillW + 20, 296, pillW, 88, d.under, d.underLabel, CARD.under);

      // Picks: one block per conference, 5 columns × 3 rows each.
      var cols = 5, gap = 14, tileW = (inner - gap * (cols - 1)) / cols, tileH = 92, y = 444;
      d.conferences.forEach(function (conf) {
        ctx.fillStyle = CARD.muted;
        ctx.font = font(800, 24);
        ctx.fillText(conf.label.toLocaleUpperCase(document.documentElement.lang), pad, y);
        y += 18;
        conf.teams.forEach(function (team, i) {
          drawTile(ctx, team, pad + (i % cols) * (tileW + gap), y + Math.floor(i / cols) * (tileH + gap), tileW, tileH);
        });
        y += Math.ceil(conf.teams.length / cols) * (tileH + gap) + 38;
      });

      // Footer: the invitation and the site address (links aren't clickable on Instagram/TikTok, so it's printed).
      var fy = CARD.h - 150;
      ctx.fillStyle = CARD.border;
      ctx.fillRect(pad, fy, inner, 2);
      ctx.fillStyle = '#ffffff';
      fitFont(ctx, d.tagline, 800, 36, inner);
      ctx.fillText(d.tagline, pad, fy + 56);
      ctx.font = font(600, 30);
      var x = drawRuns(ctx, [[d.cta + ' ', CARD.muted]], pad, fy + 106);
      ctx.fillStyle = CARD.over;
      fitFont(ctx, d.host, 900, 40, CARD.w - pad - x);
      ctx.fillText(d.host, x, fy + 108);

      return new Promise(function (resolve, reject) {
        canvas.toBlob(function (blob) { return blob ? resolve(blob) : reject(); }, 'image/png');
      });
    });
  }

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

    // Opens the share sheet once, right after the last missing pick is made (not on page load).
    Alpine.data('pickProgress', function (count, total) {
      return {
        init: function () {
          if (lastPickCount !== null && lastPickCount < total && count >= total) {
            setTimeout(function () { window.dispatchEvent(new CustomEvent('open-share')); }, 500);
          }
          lastPickCount = count;
        }
      };
    });

    // Share sheet: fetches the user's picks, draws the share image and builds per-network links.
    // The image is prepared before any button is pressed because file sharing must run inside the click.
    Alpine.data('shareSheet', function (endpoint) {
      return {
        open: false, data: null, file: null, imageUrl: null, canShareFile: false, copied: false,
        show: function () {
          var self = this;
          self.open = true;
          self.$nextTick(function () { self.$refs.closeBtn.focus(); });
          fetch(endpoint, { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (d) { self.data = d; return drawShareCard(d); })
            .then(function (blob) {
              if (self.imageUrl) { URL.revokeObjectURL(self.imageUrl); }
              self.file = new File([blob], self.data.fileName, { type: 'image/png' });
              self.imageUrl = URL.createObjectURL(blob);
              self.canShareFile = !!(navigator.canShare && navigator.canShare({ files: [self.file] }));
            })
            .catch(function () { toast(t('Paylaşım kartı hazırlanamadı.', 'Couldn\'t prepare the share card.'), 'error'); });
        },
        close: function () { this.open = false; },
        message: function () { return this.data ? this.data.text + ' ' + this.data.url : ''; },
        link: function (network) {
          if (!this.data) { return '#'; }
          var e = encodeURIComponent, text = this.data.text, url = this.data.url, both = e(this.message());
          switch (network) {
            case 'x': return 'https://x.com/intent/post?text=' + e(text) + '&url=' + e(url) + '&hashtags=NBA,AltMiUstMu';
            case 'facebook': return 'https://www.facebook.com/sharer/sharer.php?u=' + e(url);
            case 'whatsapp': return 'https://wa.me/?text=' + both;
            case 'telegram': return 'https://t.me/share/url?url=' + e(url) + '&text=' + e(text);
            case 'threads': return 'https://www.threads.net/intent/post?text=' + both;
            case 'bluesky': return 'https://bsky.app/intent/compose?text=' + both;
            case 'reddit': return 'https://www.reddit.com/submit?url=' + e(url) + '&title=' + e(text);
            case 'linkedin': return 'https://www.linkedin.com/sharing/share-offsite/?url=' + e(url);
            default: return url;
          }
        },
        // Native share sheet when the device supports files (mobile: Instagram, TikTok, Snapchat…); otherwise download.
        shareImage: function (network) {
          if (!this.file) { return; }
          if (this.canShareFile) {
            navigator.share({ files: [this.file], text: this.message() }).catch(function () { });
            return;
          }
          this.download();
          if (network) {
            toast(t('Görsel indirildi. ' + network + '\'ta paylaşabilirsin.', 'Image saved. You can now post it on ' + network + '.'));
          }
        },
        download: function () {
          if (!this.imageUrl) { return; }
          var a = document.createElement('a');
          a.href = this.imageUrl;
          a.download = this.data.fileName;
          document.body.appendChild(a);
          a.click();
          a.remove();
        },
        copy: function () {
          var self = this, text = self.message();
          var done = function () {
            self.copied = true;
            toast(t('Metin ve bağlantı kopyalandı', 'Text and link copied'));
            setTimeout(function () { self.copied = false; }, 2000);
          };
          if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(done, function () { fallback(text); done(); });
          } else {
            fallback(text);
            done();
          }
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
