import { Injectable, signal } from '@angular/core';

export interface NotificationPreferences {
  desktopEnabled: boolean;
  soundEnabled: boolean;
  soundVolume: number;
}

const DEFAULT_PREFS: NotificationPreferences = {
  desktopEnabled: true,
  soundEnabled: true,
  soundVolume: 0.5,
};

const STORAGE_KEY = 'wa_notification_prefs';

@Injectable({ providedIn: 'root' })
export class NotificationManagerService {
  readonly preferences = signal<NotificationPreferences>(this.loadPrefs());
  readonly permissionGranted = signal(
    typeof Notification !== 'undefined' ? Notification.permission === 'granted' : false
  );

  private loadPrefs(): NotificationPreferences {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return stored ? { ...DEFAULT_PREFS, ...JSON.parse(stored) } : { ...DEFAULT_PREFS };
    } catch {
      return { ...DEFAULT_PREFS };
    }
  }

  savePreferences(prefs: Partial<NotificationPreferences>): void {
    const updated = { ...this.preferences(), ...prefs };
    this.preferences.set(updated);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
  }

  async requestPermission(): Promise<boolean> {
    if (typeof Notification === 'undefined') return false;
    if (Notification.permission === 'granted') {
      this.permissionGranted.set(true);
      return true;
    }
    if (Notification.permission === 'denied') return false;
    const result = await Notification.requestPermission();
    this.permissionGranted.set(result === 'granted');
    return result === 'granted';
  }

  showNotification(title: string, body: string, onClick?: () => void): void {
    const prefs = this.preferences();
    if (!prefs.desktopEnabled || typeof Notification === 'undefined' || Notification.permission !== 'granted') return;

    const notification = new Notification(title, {
      body,
      icon: '/favicon.ico',
      silent: true,
      tag: 'wa-msg-' + Date.now(),
    });

    if (prefs.soundEnabled) this.playSound();

    notification.onclick = () => {
      window.focus();
      notification.close();
      onClick?.();
    };

    setTimeout(() => notification.close(), 6000);
  }

  playSound(): void {
    const prefs = this.preferences();
    if (!prefs.soundEnabled) return;

    try {
      const ctx = new AudioContext();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain);
      gain.connect(ctx.destination);

      const vol = prefs.soundVolume * 0.3;
      const now = ctx.currentTime;

      // Two-tone notification sound
      osc.type = 'sine';
      osc.frequency.setValueAtTime(880, now);
      osc.frequency.setValueAtTime(660, now + 0.08);
      osc.frequency.setValueAtTime(880, now + 0.16);

      gain.gain.setValueAtTime(vol, now);
      gain.gain.setValueAtTime(vol * 0.7, now + 0.08);
      gain.gain.setValueAtTime(vol, now + 0.16);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.35);

      osc.start(now);
      osc.stop(now + 0.35);

      // Cleanup
      osc.onended = () => { gain.disconnect(); osc.disconnect(); ctx.close(); };
    } catch { /* Audio not available */ }
  }
}
