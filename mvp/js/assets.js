// Image preload cache: decodes the day's lot stack before the auction floor opens.

const ImageCache = {
  _images: new Map(),
  _sessionId: 0,
  _loaded: 0,
  _total: 0,
  _ready: true,

  isReady() {
    return this._ready;
  },

  progress() {
    return { loaded: this._loaded, total: this._total, ready: this._ready };
  },

  get(url) {
    return this._images.get(url) || null;
  },

  preloadUrls(urls, onProgress) {
    this._sessionId += 1;
    const sessionId = this._sessionId;
    const unique = [...new Set(urls.filter(Boolean))];
    this._loaded = 0;
    this._total = unique.length;
    this._ready = unique.length === 0;

    if (this._ready) {
      if (onProgress) onProgress(this.progress());
      return Promise.resolve();
    }

    const report = () => {
      if (sessionId !== this._sessionId) return;
      if (onProgress) onProgress(this.progress());
    };

    return Promise.all(
      unique.map(
        (url) =>
          new Promise((resolve) => {
            if (this._images.has(url)) {
              this._loaded += 1;
              if (this._loaded >= this._total) this._ready = true;
              report();
              resolve();
              return;
            }

            const img = new Image();
            img.onload = () => {
              if (sessionId !== this._sessionId) {
                resolve();
                return;
              }
              this._images.set(url, img);
              this._loaded += 1;
              if (this._loaded >= this._total) this._ready = true;
              report();
              resolve();
            };
            img.onerror = () => {
              if (sessionId !== this._sessionId) {
                resolve();
                return;
              }
              this._loaded += 1;
              if (this._loaded >= this._total) this._ready = true;
              report();
              resolve();
            };
            img.src = url;
          })
      )
    );
  },
};
