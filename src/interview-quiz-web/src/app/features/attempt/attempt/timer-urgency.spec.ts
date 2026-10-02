import { nextTimerAnnouncement, timerUrgency, TimerAnnouncementState } from './timer-urgency';

const fresh: TimerAnnouncementState = { five: false, one: false };

describe('timerUrgency', () => {
  it('stays quiet at and above 5:00 and at zero', () => {
    expect(timerUrgency(300)).toBe('none');
    expect(timerUrgency(301)).toBe('none');
    expect(timerUrgency(0)).toBe('none');
  });

  it('warns under 5:00 and becomes urgent under 1:00', () => {
    expect(timerUrgency(299)).toBe('warn');
    expect(timerUrgency(60)).toBe('warn');
    expect(timerUrgency(59)).toBe('urgent');
  });
});

describe('nextTimerAnnouncement', () => {
  it('announces each threshold once', () => {
    const underFive = nextTimerAnnouncement(299, fresh);
    expect(underFive.message).toBe('Less than 5 minutes remaining.');
    expect(nextTimerAnnouncement(240, underFive.state).message).toBeNull();

    const underOne = nextTimerAnnouncement(59, underFive.state);
    expect(underOne.message).toBe('Less than 1 minute remaining.');
    expect(nextTimerAnnouncement(30, underOne.state).message).toBeNull();
    expect(nextTimerAnnouncement(0, underOne.state).message).toBeNull();
  });

  it('announces only the tighter threshold when time is already under 1:00', () => {
    const announced = nextTimerAnnouncement(45, fresh);
    expect(announced.message).toBe('Less than 1 minute remaining.');
    expect(announced.state).toEqual({ five: true, one: true });
    expect(nextTimerAnnouncement(200, announced.state).message).toBeNull();
  });

  it('does not announce at exactly 5:00 or 1:00', () => {
    expect(nextTimerAnnouncement(300, fresh).message).toBeNull();
    expect(nextTimerAnnouncement(60, fresh).message).toBe('Less than 5 minutes remaining.');
  });
});
