import { signal } from '@angular/core';
import { applicationConfig, Meta, StoryObj } from '@storybook/angular-vite';
import { OfflineBanner } from './offline-banner.component';
import { OnlineStatus } from '../online-status.service';

function onlineStatusMock(online: boolean) {
  return applicationConfig({
    providers: [
      {
        provide: OnlineStatus,
        useValue: { online: signal(online) },
      },
    ],
  });
}

const meta: Meta<OfflineBanner> = {
  title: 'Shared/OfflineBanner',
  component: OfflineBanner,
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<OfflineBanner>;

export const Online: Story = {
  decorators: [onlineStatusMock(true)],
};

export const Offline: Story = {
  decorators: [onlineStatusMock(false)],
};

export const AttemptOffline: Story = {
  args: { context: 'attempt' },
  decorators: [onlineStatusMock(false)],
};
