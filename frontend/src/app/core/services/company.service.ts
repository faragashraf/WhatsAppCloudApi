import { Injectable, inject, signal, computed } from '@angular/core';
import { Observable, tap, catchError, of, shareReplay, Subject } from 'rxjs';
import { ApiService } from './api.service';
import {
  WhatsAppConnectionStatus,
  CompanySubscription,
  WhatsAppPhoneNumber,
} from '../models';

/**
 * Shared CompanyService — single source of truth for company-level data
 * used by both Dashboard and Settings pages.
 */
@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly api = inject(ApiService);

  // ─── Signals ───
  readonly connectionStatus = signal<WhatsAppConnectionStatus | null>(null);
  readonly connectionLoading = signal(false);
  readonly subscription = signal<CompanySubscription | null>(null);
  readonly phoneNumbers = signal<WhatsAppPhoneNumber[]>([]);

  // ─── Computed helpers ───
  readonly isConnected = computed(() => this.connectionStatus()?.isConnected ?? false);
  readonly connectedNumbersCount = computed(() => this.connectionStatus()?.phoneNumberCount ?? 0);
  readonly tokenValid = computed(() => this.connectionStatus()?.tokenValid ?? false);

  // ─── Cache invalidation ───
  private readonly _refresh$ = new Subject<void>();
  private _connectionCache$: Observable<WhatsAppConnectionStatus> | null = null;

  /**
   * Fetch (or return cached) connection status.
   * Pass `forceRefresh = true` to bypass the cache.
   */
  loadConnectionStatus(forceRefresh = false): Observable<WhatsAppConnectionStatus | null> {
    if (forceRefresh) {
      this._connectionCache$ = null;
    }

    if (!this._connectionCache$) {
      this.connectionLoading.set(true);
      this._connectionCache$ = this.api
        .get<WhatsAppConnectionStatus>('/company/connection-status')
        .pipe(
          tap((status) => {
            this.connectionStatus.set(status);
            this.connectionLoading.set(false);
          }),
          catchError((err) => {
            console.error('[CompanyService] connection-status error', err);
            this.connectionLoading.set(false);
            return of(null as unknown as WhatsAppConnectionStatus);
          }),
          shareReplay(1),
        );
    }

    return this._connectionCache$;
  }

  /**
   * Load the active subscription.
   */
  loadSubscription(): Observable<CompanySubscription | null> {
    return this.api.get<CompanySubscription[]>('/subscriptions').pipe(
      tap((subs) => {
        const active = subs.find((s) => s.isActive) ?? null;
        this.subscription.set(active);
      }),
      catchError((err) => {
        console.error('[CompanyService] subscriptions error', err);
        return of(null as unknown as CompanySubscription[]);
      }),
    ) as unknown as Observable<CompanySubscription | null>;
  }

  /**
   * Load phone numbers.
   */
  loadPhoneNumbers(): Observable<WhatsAppPhoneNumber[]> {
    return this.api.get<WhatsAppPhoneNumber[]>('/phone-numbers').pipe(
      tap((phones) => this.phoneNumbers.set(phones)),
      catchError((err) => {
        console.error('[CompanyService] phone-numbers error', err);
        return of([] as WhatsAppPhoneNumber[]);
      }),
    );
  }

  /**
   * Force refresh all company data.
   */
  refreshAll(): void {
    this._connectionCache$ = null;
    this.loadConnectionStatus(true).subscribe();
    this.loadSubscription().subscribe();
    this.loadPhoneNumbers().subscribe();
  }
}
