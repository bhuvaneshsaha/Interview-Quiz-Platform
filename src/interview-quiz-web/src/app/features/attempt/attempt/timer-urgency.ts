export type TimerUrgency = 'none' | 'warn' | 'urgent';

export interface TimerAnnouncementState {
  five: boolean;
  one: boolean;
}

/** Visible urgency. 05:00 and 01:00 themselves are not yet under those marks. */
export function timerUrgency(remainingSeconds: number): TimerUrgency {
  if (remainingSeconds <= 0 || remainingSeconds >= 300) {
    return 'none';
  }
  if (remainingSeconds < 60) {
    return 'urgent';
  }
  return 'warn';
}

/**
 * One polite announcement when remaining time first drops under 5:00, then under 1:00.
 * Later ticks at the same threshold return no message.
 */
export function nextTimerAnnouncement(
  remainingSeconds: number,
  state: TimerAnnouncementState,
): { message: string | null; state: TimerAnnouncementState } {
  if (remainingSeconds <= 0) {
    return { message: null, state };
  }
  if (remainingSeconds < 60 && !state.one) {
    return {
      message: 'Less than 1 minute remaining.',
      state: { five: true, one: true },
    };
  }
  if (remainingSeconds < 300 && !state.five) {
    return {
      message: 'Less than 5 minutes remaining.',
      state: { five: true, one: state.one },
    };
  }
  return { message: null, state };
}
