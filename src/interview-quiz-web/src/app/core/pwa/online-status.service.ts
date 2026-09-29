import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class OnlineStatus {
  readonly online = signal(typeof navigator === 'undefined' ? true : navigator.onLine);

  constructor() {
    if (typeof window === 'undefined') {
      return;
    }
    const update = () => this.online.set(navigator.onLine);
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
  }
}
