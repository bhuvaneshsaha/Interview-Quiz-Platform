import { Component } from '@angular/core';
import { applicationConfig, Meta, StoryObj } from '@storybook/angular-vite';
import { HasPermission } from './has-permission.directive';
import { PermissionCodes } from './permission-codes';
import { PermissionService } from './permission.service';

@Component({
  selector: 'app-has-permission-demo',
  imports: [HasPermission],
  template: `
    <p>Always visible.</p>
    <p *hasPermission="code" data-testid="gated">Visible with {{ code }}</p>
  `,
})
export class HasPermissionDemo {
  readonly code = PermissionCodes.OpeningsWrite;
}

function withPermissions(codes: readonly string[]) {
  return [
    applicationConfig({
      providers: [
        {
          provide: PermissionService,
          useFactory: () => {
            const permissions = new PermissionService();
            permissions.set(codes);
            return permissions;
          },
        },
      ],
    }),
  ];
}

const meta: Meta<HasPermissionDemo> = {
  title: 'Shared/HasPermission',
  component: HasPermissionDemo,
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<HasPermissionDemo>;

export const Granted: Story = {
  decorators: withPermissions([PermissionCodes.OpeningsWrite]),
};

export const Denied: Story = {
  decorators: withPermissions([PermissionCodes.OpeningsRead]),
};
