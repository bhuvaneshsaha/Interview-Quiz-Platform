import { Meta, StoryObj } from '@storybook/angular-vite';
import { PageStatus } from './page-status.component';

const meta: Meta<PageStatus> = {
  title: 'Shared/PageStatus',
  component: PageStatus,
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<PageStatus>;

export const Loading: Story = {
  args: {
    loading: true,
    loadingMessage: 'Loading openings.',
  },
};

export const Empty: Story = {
  args: {
    loading: false,
    empty: true,
    emptyMessage: 'No openings match this filter.',
  },
};

export const Error: Story = {
  args: {
    loading: false,
    empty: false,
    error: 'Opening does not exist.',
    correlationId: '00-6b2d4c9a9e110f8c-7c3e4d0b8f221a9d-00',
  },
};

export const Content: Story = {
  render: () => ({
    imports: [PageStatus],
    template: `
      <app-page-status [loading]="false" [empty]="false" [error]="null">
        <p>Projected page content.</p>
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Title</th>
              <th scope="col">Owner</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <th scope="row">Senior backend engineer</th>
              <td>Recruiting</td>
            </tr>
          </tbody>
        </table>
      </app-page-status>
    `,
  }),
};
