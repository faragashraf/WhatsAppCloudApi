import { Injectable, inject, signal, OnDestroy } from '@angular/core';
import {
  Observable,
  Subject,
  Subscription,
  catchError,
  combineLatest,
  map,
  of,
  switchMap,
  timer,
} from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  ActivityEvent,
  ActivitySeverity,
  ACTIVITY_TYPES,
  StreamConnectionStatus,
  WebhookLogEntry,
  Message,
  Notification,
  PagedResult,
  ApiResponse,
} from '../models';

const POLL_INTERVAL_MS = 15_000;
const MAX_EVENTS = 50;

/**
 * ActivityStreamService — Fixed lifecycle + dedup
 *
 * Polls existing backend endpoints to build a unified activity feed.
 * Uses stable backend IDs for webhook logs to prevent duplicates.
 */
@Injectable({ providedIn: 'root' })
export class ActivityStreamService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  // ─── Public state ───
  readonly connectionStatus = signal<StreamConnectionStatus>('disconnected');
  readonly events = signal<ActivityEvent[]>([]);
  readonly lastWebhookEvent = signal<Date | null>(null);
  readonly webhookConnected = signal(false);

  // ─── Internals ───
  private _pollSub: Subscription | null = null;
  private _seenIds = new Set<string>();
  private _allEvents: ActivityEvent[] = [];
  private _consecutiveErrors = 0;

  /** Observable stream of events (for components preferring RxJS) */
  readonly events$ = new Subject<ActivityEvent[]>();

  /**
   * Start polling. Safe to call multiple times (idempotent).
   */
  startPolling(): void {
    if (this._pollSub && !this._pollSub.closed) return;
    this.connectionStatus.set('connected');
    this._consecutiveErrors = 0;

    this._pollSub = timer(0, POLL_INTERVAL_MS)
      .pipe(
        switchMap(() => this._fetchAll()),
        catchError(() => {
          this._handleError();
          return of([] as ActivityEvent[]);
        }),
      )
      .subscribe((newEvents) => {
        if (newEvents.length > 0) {
          this._mergeEvents(newEvents);
          this._consecutiveErrors = 0;
          this.connectionStatus.set('connected');
        }
      });
  }

  /**
   * Stop polling (does NOT destroy the service — safe to restart).
   */
  stopPolling(): void {
    this._pollSub?.unsubscribe();
    this._pollSub = null;
    this.connectionStatus.set('disconnected');
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  // ───────────────────────────────────────
  // Private helpers
  // ───────────────────────────────────────

  private _fetchAll(): Observable<ActivityEvent[]> {
    return combineLatest([
      this._fetchWebhookLogs(),
      this._fetchRecentMessages(),
      this._fetchNotifications(),
    ]).pipe(
      map(([webhookEvents, msgEvents, notifEvents]) => [
        ...webhookEvents,
        ...msgEvents,
        ...notifEvents,
      ]),
      catchError(() => {
        this._handleError();
        return of([] as ActivityEvent[]);
      }),
    );
  }

  /** GET /api/webhook/logs → ActivityEvent[] */
  private _fetchWebhookLogs(): Observable<ActivityEvent[]> {
    return this.http
      .get<WebhookLogEntry[]>(`${this.baseUrl}/webhook/logs`)
      .pipe(
        map((entries) => {
          if (!Array.isArray(entries)) return [];
          const events = entries.slice(0, 30).flatMap((e) => this._parseWebhookEntry(e));
          if (events.length > 0) {
            const latest = events.reduce((a, b) =>
              a.timestamp > b.timestamp ? a : b,
            );
            this.lastWebhookEvent.set(latest.timestamp);
            const fiveMinAgo = new Date(Date.now() - 5 * 60 * 1000);
            this.webhookConnected.set(latest.timestamp > fiveMinAgo);
          }
          return events;
        }),
        catchError(() => of([])),
      );
  }

  /** GET /api/messages?pageSize=10 → ActivityEvent[] */
  private _fetchRecentMessages(): Observable<ActivityEvent[]> {
    return this.http
      .get<ApiResponse<PagedResult<Message>>>(`${this.baseUrl}/messages`, {
        params: { pageSize: '10' },
      })
      .pipe(
        map((res) => {
          const items = res?.data?.items ?? [];
          return items.map((m) => this._messageToEvent(m));
        }),
        catchError(() => of([])),
      );
  }

  /** GET /api/notifications?pageSize=10 → ActivityEvent[] */
  private _fetchNotifications(): Observable<ActivityEvent[]> {
    return this.http
      .get<ApiResponse<PagedResult<Notification>>>(`${this.baseUrl}/notifications`, {
        params: { pageSize: '10' },
      })
      .pipe(
        map((res) => {
          const items = res?.data?.items ?? [];
          return items.map((n) => this._notificationToEvent(n));
        }),
        catchError(() => of([])),
      );
  }

  /**
   * Parse a webhook log entry using the backend-assigned stable ID.
   * Each log entry generates one or more ActivityEvents keyed by wamid.
   */
  private _parseWebhookEntry(entry: WebhookLogEntry): ActivityEvent[] {
    // Use the backend-assigned stable ID as the base
    const stableId = entry.id || entry.timestamp;

    if (!entry.payload) {
      return [{
        id: `wh-${stableId}`,
        type: ACTIVITY_TYPES.WEBHOOK_RECEIVED,
        message: entry.summary || 'Webhook event',
        timestamp: new Date(entry.timestamp),
        severity: 'info',
      }];
    }

    try {
      const payload = JSON.parse(entry.payload);
      const events: ActivityEvent[] = [];

      for (const ent of payload.entry ?? []) {
        for (const change of ent.changes ?? []) {
          const value = change.value;
          if (!value) continue;

          const phone = value.metadata?.display_phone_number ?? '';

          // ── Status updates ──
          for (const status of value.statuses ?? []) {
            const wamid = status.id ?? '';
            const st: string = status.status ?? 'unknown';
            const recipient = status.recipient_id ?? '';

            const typeMap: Record<string, string> = {
              sent: ACTIVITY_TYPES.MESSAGE_SENT,
              delivered: ACTIVITY_TYPES.MESSAGE_DELIVERED,
              read: ACTIVITY_TYPES.MESSAGE_READ,
              failed: ACTIVITY_TYPES.MESSAGE_FAILED,
            };
            const sevMap: Record<string, ActivitySeverity> = {
              sent: 'success',
              delivered: 'success',
              read: 'info',
              failed: 'error',
            };

            let message = `Message ${st} — to ${recipient}`;
            if (phone) message += ` (from ${phone})`;
            if (st === 'failed' && status.errors?.[0]) {
              const errDetail = status.errors[0].error_data?.details || status.errors[0].title;
              if (errDetail) message += ` — ${errDetail}`;
            }

            events.push({
              id: `wh-${wamid}-${st}`,
              type: typeMap[st] ?? ACTIVITY_TYPES.WEBHOOK_RECEIVED,
              message,
              timestamp: new Date(entry.timestamp),
              severity: sevMap[st] ?? 'info',
              metadata: { phoneNumber: phone, wamid, recipient },
            });
          }

          // ── Incoming messages ──
          const contacts: { wa_id?: string; profile?: { name?: string } }[] = value.contacts ?? [];
          for (const msg of value.messages ?? []) {
            const wamid = msg.id ?? '';
            const fromNumber: string = msg.from ?? '';
            const contact = contacts.find((c) => c.wa_id === fromNumber);
            const contactName = contact?.profile?.name ?? fromNumber;
            const body: string = msg.text?.body ?? msg.caption ?? msg.type ?? 'message';

            events.push({
              id: `wh-${wamid}`,
              type: ACTIVITY_TYPES.INCOMING_MESSAGE,
              message: `📩 ${contactName}: ${body}`,
              timestamp: new Date(entry.timestamp),
              severity: 'info',
              metadata: { phoneNumber: phone, wamid, fromNumber, contactName },
            });
          }
        }
      }

      // If we parsed actual events, use those (keyed by wamid); otherwise use the stable entry ID
      return events.length > 0 ? events : [{
        id: `wh-${stableId}`,
        type: ACTIVITY_TYPES.WEBHOOK_RECEIVED,
        message: entry.summary || 'Webhook event',
        timestamp: new Date(entry.timestamp),
        severity: 'info',
      }];
    } catch {
      return [{
        id: `wh-${stableId}`,
        type: ACTIVITY_TYPES.WEBHOOK_RECEIVED,
        message: entry.summary || 'Webhook event',
        timestamp: new Date(entry.timestamp),
        severity: 'info',
      }];
    }
  }

  private _messageToEvent(msg: Message): ActivityEvent {
    const typeMap: Record<string, { type: string; severity: ActivitySeverity }> = {
      SENT: { type: ACTIVITY_TYPES.MESSAGE_SENT, severity: 'success' },
      DELIVERED: { type: ACTIVITY_TYPES.MESSAGE_DELIVERED, severity: 'success' },
      READ: { type: ACTIVITY_TYPES.MESSAGE_READ, severity: 'info' },
      FAILED: { type: ACTIVITY_TYPES.MESSAGE_FAILED, severity: 'error' },
    };
    const mapped = typeMap[msg.status] ?? {
      type: ACTIVITY_TYPES.MESSAGE_SENT,
      severity: 'info' as ActivitySeverity,
    };
    return {
      id: `msg-${msg.messageId}`,
      type: mapped.type,
      message: `${msg.messageType} to ${msg.toNumber} — ${msg.status}`,
      timestamp: new Date(msg.createdAtUtc),
      severity: mapped.severity,
    };
  }

  private _notificationToEvent(n: Notification): ActivityEvent {
    const severityMap: Record<string, ActivitySeverity> = {
      error: 'error',
      warning: 'warning',
      info: 'info',
    };
    return {
      id: `notif-${n.notificationId}`,
      type: n.type || 'notification',
      message: n.title + (n.body ? ` — ${n.body}` : ''),
      timestamp: new Date(n.createdAtUtc),
      severity: severityMap[n.category?.toLowerCase() ?? ''] ?? 'info',
    };
  }

  /**
   * Merge new events with existing ones, using _seenIds for stable dedup.
   * Only updates the signal if the set of event IDs actually changed.
   */
  private _mergeEvents(newEvents: ActivityEvent[]): void {
    let changed = false;
    for (const evt of newEvents) {
      if (!this._seenIds.has(evt.id)) {
        this._seenIds.add(evt.id);
        this._allEvents.push(evt);
        changed = true;
      }
    }

    if (!changed) return; // Don't trigger signal update if nothing new

    this._allEvents.sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime());
    if (this._allEvents.length > MAX_EVENTS) {
      // Remove oldest events from Set and array
      const removed = this._allEvents.splice(MAX_EVENTS);
      for (const r of removed) this._seenIds.delete(r.id);
    }

    const snapshot = [...this._allEvents];
    this.events.set(snapshot);
    this.events$.next(snapshot);
  }

  private _handleError(): void {
    this._consecutiveErrors++;
    if (this._consecutiveErrors >= 3) {
      this.connectionStatus.set('disconnected');
    } else {
      this.connectionStatus.set('reconnecting');
    }
  }
}
