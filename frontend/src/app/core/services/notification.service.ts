import { Injectable, signal } from '@angular/core';
import { Subject, interval, takeUntil } from 'rxjs';

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
  /** Global unread notification count polled from backend */
  readonly unreadCount = signal(0);
  private pollingDestroy$?: Subject<void>;
  private lastUnreadCount: number | null = null;
  private apiService: any = null; // Lazy-injected to avoid circular deps

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

  /** Start polling for unread notification count. Call once from dashboard layout. */
  startUnreadPolling(apiService: any): void {
    if (this.pollingDestroy$) return; // Already polling
    this.apiService = apiService;
    this.pollingDestroy$ = new Subject<void>();

    // Initial fetch
    this.fetchUnreadCount();

    // Poll every 15 seconds
    interval(15000).pipe(
      takeUntil(this.pollingDestroy$),
    ).subscribe(() => this.fetchUnreadCount());
  }

  stopUnreadPolling(): void {
    this.pollingDestroy$?.next();
    this.pollingDestroy$?.complete();
    this.pollingDestroy$ = undefined;
    this.lastUnreadCount = null;
  }

  refreshUnreadCount(): void {
    this.fetchUnreadCount();
  }

  syncUnreadCount(count: number): void {
    const normalized = Math.max(0, count);
    this.unreadCount.set(normalized);
    this.lastUnreadCount = normalized;
  }

  private fetchUnreadCount(): void {
    if (!this.apiService) return;
    this.apiService.get('/notifications', { isRead: 'false', pageSize: '1' }).subscribe({
      next: (r: any) => {
        const nextUnreadCount = Math.max(0, r?.totalCount ?? r?.items?.length ?? 0);
        if (this.lastUnreadCount !== null && nextUnreadCount > this.lastUnreadCount && !this.isInboxRoute()) {
          this.notifyUnreadIncrease(r?.items?.[0]);
        }

        this.unreadCount.set(nextUnreadCount);
        this.lastUnreadCount = nextUnreadCount;
      },
      error: () => { /* silently ignore */ },
    });
  }

  /** Manually decrement or reset the unread count */
  markAllRead(): void {
    this.unreadCount.set(0);
    this.lastUnreadCount = 0;
  }

  private notifyUnreadIncrease(latestNotification: any): void {
    if (this.canShowDesktopNotifications() && latestNotification?.title) {
      this.showNotification(latestNotification.title, latestNotification.body || 'New notification');
      return;
    }

    this.playSound();
  }

  private canShowDesktopNotifications(): boolean {
    const prefs = this.preferences();
    return prefs.desktopEnabled && typeof Notification !== 'undefined' && Notification.permission === 'granted';
  }

  private isInboxRoute(): boolean {
    if (typeof window === 'undefined') return false;
    return /\/dashboard\/inbox(\/|$)/.test(window.location.pathname);
  }
}
