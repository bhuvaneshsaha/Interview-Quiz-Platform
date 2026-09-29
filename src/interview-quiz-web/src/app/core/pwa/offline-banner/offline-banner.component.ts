import { Component, inject } from '@angular/core';
import { OnlineStatus } from '../online-status.service';

@Component({
  selector: 'app-offline-banner',
  imports: [],
  templateUrl: './offline-banner.component.html',
  styleUrl: './offline-banner.component.css',
})
export class OfflineBanner {
  readonly online = inject(OnlineStatus).online;
}
