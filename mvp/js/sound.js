// Synthesized sound design (Web Audio API, no external audio files).
// Every effect is generated procedurally — oscillators, filtered noise and a
// small algorithmic reverb — so the game stays self-contained and the palette
// stays coherent (warm bells, soft wood/felt knocks, silk whooshes).

const Sound = {
  ctx: null,
  masterOut: null,
  dryGain: null,
  wetGain: null,
  convolver: null,
  muted: false,
  _tension: null,

  init() {
    if (this.ctx) return;
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return;
    this.ctx = new AudioCtx();

    const stored = (() => {
      try {
        return localStorage.getItem('mb-sound-muted');
      } catch (e) {
        return null;
      }
    })();
    this.muted = stored === '1';

    this.masterOut = this.ctx.createGain();
    this.masterOut.gain.value = this.muted ? 0 : 1;
    this.masterOut.connect(this.ctx.destination);

    this.dryGain = this.ctx.createGain();
    this.dryGain.gain.value = 1;
    this.dryGain.connect(this.masterOut);

    this.convolver = this.ctx.createConvolver();
    this.convolver.buffer = this._buildImpulse(1.7, 2.4);
    this.wetGain = this.ctx.createGain();
    this.wetGain.gain.value = 0.18;
    this.convolver.connect(this.wetGain);
    this.wetGain.connect(this.masterOut);
  },

  ensureUnlocked() {
    this.init();
    if (this.ctx && this.ctx.state === 'suspended') this.ctx.resume();
  },

  isMuted() {
    return this.muted;
  },

  setMuted(muted) {
    this.init();
    this.muted = muted;
    try {
      localStorage.setItem('mb-sound-muted', muted ? '1' : '0');
    } catch (e) {
      /* storage unavailable — mute state just won't persist */
    }
    if (this.masterOut) {
      const now = this.ctx.currentTime;
      this.masterOut.gain.cancelScheduledValues(now);
      this.masterOut.gain.setTargetAtTime(muted ? 0 : 1, now, 0.05);
    }
  },

  toggleMute() {
    this.setMuted(!this.muted);
    return this.muted;
  },

  // --- low-level synthesis helpers -----------------------------------------

  _buildImpulse(duration, decay) {
    const rate = this.ctx.sampleRate;
    const length = Math.floor(rate * duration);
    const impulse = this.ctx.createBuffer(2, length, rate);
    for (let ch = 0; ch < 2; ch++) {
      const data = impulse.getChannelData(ch);
      for (let i = 0; i < length; i++) {
        data[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / length, decay);
      }
    }
    return impulse;
  },

  _out(reverbSend) {
    const g = this.ctx.createGain();
    g.connect(this.dryGain);
    if (reverbSend > 0) {
      const send = this.ctx.createGain();
      send.gain.value = reverbSend;
      g.connect(send);
      send.connect(this.convolver);
    }
    return g;
  },

  _tone({
    freq,
    type = 'sine',
    start = 0,
    attack = 0.004,
    decay = 0.12,
    peak = 0.2,
    detune = 0,
    filterFreq = null,
    reverb = 0.35,
  }) {
    if (!this.ctx) return;
    const t0 = this.ctx.currentTime + start;
    const osc = this.ctx.createOscillator();
    osc.type = type;
    osc.frequency.value = freq;
    if (detune) osc.detune.value = detune;

    const env = this.ctx.createGain();
    env.gain.setValueAtTime(0, t0);
    env.gain.linearRampToValueAtTime(peak, t0 + attack);
    env.gain.exponentialRampToValueAtTime(0.0001, t0 + attack + decay);

    let node = osc;
    if (filterFreq) {
      const filter = this.ctx.createBiquadFilter();
      filter.type = 'lowpass';
      filter.frequency.value = filterFreq;
      osc.connect(filter);
      node = filter;
    }

    const out = this._out(reverb);
    node.connect(env);
    env.connect(out);
    osc.start(t0);
    osc.stop(t0 + attack + decay + 0.05);
  },

  _toneSweep({
    freqStart,
    freqEnd,
    type = 'sine',
    start = 0,
    duration = 0.3,
    attack = 0.005,
    peak = 0.2,
    filterFreq = null,
    reverb = 0.35,
  }) {
    if (!this.ctx) return;
    const t0 = this.ctx.currentTime + start;
    const osc = this.ctx.createOscillator();
    osc.type = type;
    osc.frequency.setValueAtTime(freqStart, t0);
    osc.frequency.exponentialRampToValueAtTime(Math.max(freqEnd, 1), t0 + duration);

    const env = this.ctx.createGain();
    env.gain.setValueAtTime(0, t0);
    env.gain.linearRampToValueAtTime(peak, t0 + attack);
    env.gain.exponentialRampToValueAtTime(0.0001, t0 + duration);

    let node = osc;
    if (filterFreq) {
      const filter = this.ctx.createBiquadFilter();
      filter.type = 'lowpass';
      filter.frequency.value = filterFreq;
      osc.connect(filter);
      node = filter;
    }

    const out = this._out(reverb);
    node.connect(env);
    env.connect(out);
    osc.start(t0);
    osc.stop(t0 + duration + 0.05);
  },

  _noise({
    start = 0,
    duration = 0.12,
    filterType = 'bandpass',
    filterFreq = 1000,
    freqEnd = null,
    q = 1,
    attack = 0.002,
    decay = 0.1,
    peak = 0.15,
    reverb = 0.3,
  }) {
    if (!this.ctx) return;
    const t0 = this.ctx.currentTime + start;
    const bufferSize = Math.max(1, Math.floor(this.ctx.sampleRate * duration));
    const buffer = this.ctx.createBuffer(1, bufferSize, this.ctx.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < bufferSize; i++) data[i] = Math.random() * 2 - 1;

    const src = this.ctx.createBufferSource();
    src.buffer = buffer;

    const filter = this.ctx.createBiquadFilter();
    filter.type = filterType;
    filter.frequency.setValueAtTime(filterFreq, t0);
    if (freqEnd !== null) filter.frequency.linearRampToValueAtTime(freqEnd, t0 + duration);
    filter.Q.value = q;

    const env = this.ctx.createGain();
    env.gain.setValueAtTime(0, t0);
    env.gain.linearRampToValueAtTime(peak, t0 + attack);
    env.gain.exponentialRampToValueAtTime(0.0001, t0 + attack + decay);

    src.connect(filter);
    filter.connect(env);
    const out = this._out(reverb);
    env.connect(out);
    src.start(t0);
    src.stop(t0 + duration + 0.05);
  },

  // --- UI feedback ----------------------------------------------------------

  playClick() {
    this.ensureUnlocked();
    this._tone({ freq: 1050, type: 'triangle', attack: 0.001, decay: 0.05, peak: 0.12, reverb: 0.08 });
  },

  playSelect() {
    this.ensureUnlocked();
    this._tone({ freq: 740, type: 'sine', attack: 0.001, decay: 0.06, peak: 0.1, reverb: 0.15 });
    this._tone({ freq: 1100, type: 'sine', start: 0.03, attack: 0.001, decay: 0.09, peak: 0.08, reverb: 0.15 });
  },

  playUpgrade() {
    this.ensureUnlocked();
    this._tone({ freq: 660, type: 'sine', attack: 0.002, decay: 0.1, peak: 0.13, reverb: 0.25 });
    this._tone({ freq: 880, type: 'sine', start: 0.06, attack: 0.002, decay: 0.16, peak: 0.12, reverb: 0.3 });
  },

  playError() {
    this.ensureUnlocked();
    this._tone({ freq: 220, type: 'sawtooth', attack: 0.001, decay: 0.07, peak: 0.11, filterFreq: 900, reverb: 0.1 });
    this._tone({
      freq: 180,
      type: 'sawtooth',
      start: 0.09,
      attack: 0.001,
      decay: 0.09,
      peak: 0.11,
      filterFreq: 900,
      reverb: 0.1,
    });
  },

  playSkip() {
    this.ensureUnlocked();
    this._noise({ filterType: 'bandpass', filterFreq: 1800, freqEnd: 400, q: 0.7, duration: 0.14, attack: 0.002, decay: 0.12, peak: 0.1, reverb: 0.2 });
  },

  // --- auction floor ----------------------------------------------------------

  // One tick per revealed field — pitch climbs with the reveal step, mirroring
  // the rising price. `fast` softens it for the skip-through fast-forward.
  playReveal(step, { fast = false } = {}) {
    this.ensureUnlocked();
    const freq = 540 + Math.min(step, 6) * 55;
    this._tone({
      freq,
      type: 'triangle',
      attack: 0.002,
      decay: fast ? 0.045 : 0.11,
      peak: fast ? 0.06 : 0.13,
      reverb: 0.25,
    });
  },

  // Free bonus reveal ('expert-appraiser' booster) — an airy shimmer, distinct
  // from playReveal's tick so it reads as a gift rather than a price step.
  playInsight() {
    this.ensureUnlocked();
    this._tone({ freq: 1760, type: 'sine', attack: 0.006, decay: 0.22, peak: 0.06, reverb: 0.6 });
    this._tone({ freq: 2637, type: 'sine', start: 0.03, attack: 0.006, decay: 0.28, peak: 0.04, reverb: 0.65 });
  },

  playOutcome(kind) {
    this.ensureUnlocked();
    if (kind === 'won') this._playBuy();
    else if (kind === 'lost') this._playLost();
  },

  _playBuy() {
    // Gavel knock.
    this._noise({ filterType: 'lowpass', filterFreq: 400, freqEnd: 120, q: 0.7, duration: 0.09, attack: 0.001, decay: 0.07, peak: 0.5, reverb: 0.35 });
    this._tone({ freq: 100, type: 'sine', attack: 0.001, decay: 0.09, peak: 0.35, reverb: 0.2 });
    // Bright ascending chime — the "cha-ching" of a closed sale.
    const notes = [784, 988, 1175, 1568];
    notes.forEach((f, i) => {
      const t = 0.08 + i * 0.045;
      this._tone({ freq: f, type: 'sine', start: t, attack: 0.002, decay: 0.28, peak: 0.11, reverb: 0.5 });
      this._tone({ freq: f * 2, type: 'sine', start: t, attack: 0.002, decay: 0.18, peak: 0.045, reverb: 0.5 });
    });
  },

  _playLost() {
    this._noise({ filterType: 'bandpass', filterFreq: 600, freqEnd: 350, q: 0.8, duration: 0.3, attack: 0.02, decay: 0.28, peak: 0.12, reverb: 0.4 });
    this._toneSweep({ freqStart: 300, freqEnd: 110, duration: 0.28, peak: 0.2, reverb: 0.35 });
  },

  playRivalRaise() {
    this.ensureUnlocked();
    this._noise({ filterType: 'bandpass', filterFreq: 900, freqEnd: 1400, q: 0.6, duration: 0.16, attack: 0.005, decay: 0.14, peak: 0.06, reverb: 0.2 });
  },

  // Continuous low drone under the reveal — rises with `intensity` (0..1) to
  // track the climbing price/falling commission, like a held breath.
  startTension() {
    this.ensureUnlocked();
    if (!this.ctx || this._tension) return;
    const t0 = this.ctx.currentTime;
    const oscA = this.ctx.createOscillator();
    oscA.type = 'sine';
    oscA.frequency.value = 55;
    const oscB = this.ctx.createOscillator();
    oscB.type = 'triangle';
    oscB.frequency.value = 110.5;

    const filter = this.ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = 300;

    const gainA = this.ctx.createGain();
    gainA.gain.value = 0;
    const gainB = this.ctx.createGain();
    gainB.gain.value = 0;

    oscA.connect(gainA);
    oscB.connect(gainB);
    gainA.connect(filter);
    gainB.connect(filter);
    filter.connect(this._out(0.5));

    oscA.start(t0);
    oscB.start(t0);
    this._tension = { oscA, oscB, gainA, gainB, filter };
  },

  setTensionIntensity(t) {
    if (!this._tension) return;
    const clamped = Math.max(0, Math.min(1, t));
    const now = this.ctx.currentTime;
    this._tension.gainA.gain.setTargetAtTime(0.02 + clamped * 0.05, now, 0.15);
    this._tension.gainB.gain.setTargetAtTime(clamped * 0.035, now, 0.15);
    this._tension.filter.frequency.setTargetAtTime(300 + clamped * 900, now, 0.2);
  },

  stopTension() {
    if (!this._tension) return;
    const { oscA, oscB, gainA, gainB } = this._tension;
    const now = this.ctx.currentTime;
    gainA.gain.setTargetAtTime(0, now, 0.08);
    gainB.gain.setTargetAtTime(0, now, 0.08);
    this._tension = null;
    setTimeout(() => {
      try {
        oscA.stop();
        oscB.stop();
      } catch (e) {
        /* already stopped */
      }
    }, 400);
  },

  // --- overlays / transitions -------------------------------------------------

  playCardOpen() {
    this.ensureUnlocked();
    this._noise({ filterType: 'bandpass', filterFreq: 300, freqEnd: 2200, q: 0.6, duration: 0.22, attack: 0.01, decay: 0.2, peak: 0.1, reverb: 0.3 });
    this._tone({ freq: 880, type: 'sine', start: 0.05, attack: 0.002, decay: 0.2, peak: 0.08, reverb: 0.4 });
  },

  playCardClose() {
    this.ensureUnlocked();
    this._noise({ filterType: 'bandpass', filterFreq: 2000, freqEnd: 250, q: 0.6, duration: 0.18, attack: 0.005, decay: 0.16, peak: 0.09, reverb: 0.25 });
  },

  playZoomOpen() {
    this.ensureUnlocked();
    this._noise({ filterType: 'highpass', filterFreq: 1200, freqEnd: 4000, duration: 0.12, attack: 0.002, decay: 0.1, peak: 0.06, reverb: 0.2 });
  },

  playZoomClose() {
    this.ensureUnlocked();
    this._noise({ filterType: 'highpass', filterFreq: 4000, freqEnd: 1200, duration: 0.1, attack: 0.002, decay: 0.08, peak: 0.05, reverb: 0.2 });
  },

  // --- end of day / campaign ---------------------------------------------------

  playDayPass(ordersFulfilled) {
    this.ensureUnlocked();
    const notes = ordersFulfilled ? [523, 659, 784, 1047] : [440, 523, 587];
    notes.forEach((f, i) =>
      this._tone({ freq: f, type: 'sine', start: i * 0.09, attack: 0.004, decay: 0.35, peak: 0.14, reverb: 0.55 })
    );
  },

  playDayFail() {
    this.ensureUnlocked();
    [330, 262, 196].forEach((f, i) =>
      this._tone({ freq: f, type: 'sine', start: i * 0.14, attack: 0.005, decay: 0.5, peak: 0.16, reverb: 0.5 })
    );
    this._toneSweep({ freqStart: 220, freqEnd: 80, start: 0.1, duration: 0.9, peak: 0.12, reverb: 0.5 });
  },

  playCampaignEnd() {
    this.ensureUnlocked();
    [523, 659, 784, 1047, 1319].forEach((f, i) => {
      this._tone({ freq: f, type: 'sine', start: i * 0.11, attack: 0.005, decay: 0.5, peak: 0.15, reverb: 0.6 });
      this._tone({ freq: f * 2, type: 'sine', start: i * 0.11, attack: 0.005, decay: 0.3, peak: 0.05, reverb: 0.6 });
    });
  },
};
