import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { reports } from '../report-catalog';

@Component({
  selector: 'app-report-hub',
  imports: [RouterLink],
  templateUrl: './report-hub.component.html',
  styleUrl: './report-hub.component.css',
})
export class ReportHub {
  readonly reports = reports;
}
