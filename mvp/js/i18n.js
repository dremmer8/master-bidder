// Localization: UI strings (ui.*.js) + English content overrides (content.en.js).
// Game logic keeps Russian canonical values (periodRu, genreRu, etc.) for matching.

const I18n = {
  locale: 'ru',
  ui: { ru: {}, en: {} },
  content: null,

  SUPPORTED: ['ru', 'en'],
  STORAGE_KEY: 'master-bidder-lang',

  registerUI(locale, strings) {
    this.ui[locale] = { ...this.ui[locale], ...strings };
  },

  init() {
    if (typeof CONTENT_EN !== 'undefined') this.content = CONTENT_EN;
    this.locale = this.detectLocale();
    document.documentElement.lang = this.locale;
  },

  detectLocale() {
    const params = new URLSearchParams(window.location.search);
    const fromUrl = params.get('lang');
    if (fromUrl && this.SUPPORTED.includes(fromUrl)) return fromUrl;

    try {
      const stored = localStorage.getItem(this.STORAGE_KEY);
      if (stored && this.SUPPORTED.includes(stored)) return stored;
    } catch (_) {}

    const nav = (navigator.language || '').slice(0, 2).toLowerCase();
    if (nav === 'en') return 'en';
    return 'ru';
  },

  getLocale() {
    return this.locale;
  },

  setLocale(locale) {
    if (!this.SUPPORTED.includes(locale) || locale === this.locale) return;
    this.locale = locale;
    try {
      localStorage.setItem(this.STORAGE_KEY, locale);
    } catch (_) {}
    document.documentElement.lang = locale;
    this.applyStaticTexts();
    window.dispatchEvent(new CustomEvent('localechange'));
  },

  t(key, params = {}) {
    let text = this.ui[this.locale]?.[key] ?? this.ui.ru[key] ?? key;
    Object.entries(params).forEach(([k, v]) => {
      text = text.replace(new RegExp(`\\{${k}\\}`, 'g'), String(v));
    });
    return text;
  },

  /** Translate a canonical Russian vocabulary term (period, genre, artist). */
  vocab(ruValue) {
    if (!ruValue || this.locale === 'ru') return ruValue;
    return this.content?.vocab?.[ruValue] ?? ruValue;
  },

  /** Localized artwork field (title, artist, fact). Period/genre use vocab(). */
  artwork(artwork, field) {
    if (!artwork) return '';
    if (this.locale === 'en') {
      const tr = this.content?.artworks?.[artwork.id];
      if (tr?.[field]) return tr[field];
    }
    return artwork[field + 'Ru'] ?? '';
  },

  /** Localized named entity from campaign data (collector, venue, upgrade, booster). */
  entity(type, id, field, fallbackRu) {
    if (this.locale === 'en') {
      const tr = this.content?.[type]?.[id];
      if (tr?.[field]) return tr[field];
    }
    return fallbackRu;
  },

  currencySymbol() {
    return this.locale === 'en' ? '$' : '₽';
  },

  formatMoney(n) {
    const tag = this.locale === 'en' ? 'en-US' : 'ru-RU';
    return Math.round(n).toLocaleString(tag);
  },

  formatMoneyWithCurrency(n) {
    return `${this.formatMoney(n)} ${this.currencySymbol()}`;
  },

  /** Apply data-i18n / data-i18n-html attributes in index.html. */
  applyStaticTexts() {
    document.querySelectorAll('[data-i18n]').forEach((el) => {
      el.textContent = this.t(el.dataset.i18n);
    });
    document.querySelectorAll('option[data-i18n]').forEach((el) => {
      el.textContent = this.t(el.dataset.i18n);
    });
    document.querySelectorAll('[data-i18n-html]').forEach((el) => {
      el.innerHTML = this.t(el.dataset.i18nHtml);
    });
    document.querySelectorAll('[data-i18n-title]').forEach((el) => {
      el.title = this.t(el.dataset.i18nTitle);
    });
    document.querySelectorAll('[data-i18n-placeholder]').forEach((el) => {
      el.placeholder = this.t(el.dataset.i18nPlaceholder);
    });
    const titleEl = document.querySelector('title[data-i18n]');
    if (titleEl) document.title = this.t(titleEl.dataset.i18n);
  },

  /** Plural form for "orders remaining" (Russian has 3 forms). */
  ordersRemaining(n) {
    if (this.locale === 'en') {
      return n === 1 ? this.t('collector.ordersRemaining.one', { n }) : this.t('collector.ordersRemaining.many', { n });
    }
    if (n === 1) return this.t('collector.ordersRemaining.one');
    const mod10 = n % 10;
    const mod100 = n % 100;
    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) {
      return this.t('collector.ordersRemaining.few', { n });
    }
    return this.t('collector.ordersRemaining.many', { n });
  },
};

function formatMoney(n) {
  return I18n.formatMoney(n);
}
