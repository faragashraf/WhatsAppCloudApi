import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import {
  AssignableUser,
  CreateRoutingTeamRequest,
  RoutingTeam,
  UpdateRoutingTeamMembersRequest,
  UpdateRoutingTeamRequest,
} from '../../../core/models';
import { ApiService } from '../../../core/services';

type RoutingTeamDraft = RoutingTeam & {
  memberUserIds: number[];
};

@Component({
  selector: 'app-routing-teams',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule],
  templateUrl: './routing-teams.component.html',
  styleUrl: './routing-teams.component.scss',
})
export class RoutingTeamsComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly deleting = signal<number | null>(null);
  readonly errorMessage = signal('');

  readonly agents = signal<AssignableUser[]>([]);
  readonly teams = signal<RoutingTeamDraft[]>([]);

  newTeam: CreateRoutingTeamRequest = {
    name: '',
    description: '',
    isActive: true,
    autoAssignmentEnabled: true,
    manualAssignmentEnabled: true,
    memberUserIds: [],
  };

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    let pending = 2;
    const complete = () => {
      pending -= 1;
      if (pending <= 0) {
        this.loading.set(false);
      }
    };

    this.api.get<AssignableUser[]>('/users/agents').subscribe({
      next: users => {
        this.agents.set(users ?? []);
        complete();
      },
      error: error => {
        this.errorMessage.set(error?.error?.message ?? 'Failed to load users.');
        complete();
      },
    });

    this.api.get<RoutingTeam[]>('/routing/teams').subscribe({
      next: rows => {
        this.teams.set((rows ?? []).map(team => ({
          ...team,
          memberUserIds: (team.members ?? []).map(member => member.companyUserId),
        })));
        complete();
      },
      error: error => {
        this.errorMessage.set(error?.error?.message ?? 'Failed to load routing teams.');
        complete();
      },
    });
  }

  createTeam(): void {
    const payload: CreateRoutingTeamRequest = {
      ...this.newTeam,
      name: this.newTeam.name.trim(),
      description: this.newTeam.description?.trim() ?? '',
      memberUserIds: [...new Set(this.newTeam.memberUserIds.map(x => Number(x)).filter(x => x > 0))],
    };

    if (!payload.name) {
      return;
    }

    this.saving.set(true);
    this.errorMessage.set('');

    this.api.post<RoutingTeam>('/routing/teams', payload).subscribe({
      next: created => {
        this.saving.set(false);
        this.teams.update(items => [...items, {
          ...created,
          memberUserIds: (created.members ?? []).map(member => member.companyUserId),
        }].sort((a, b) => a.name.localeCompare(b.name)));

        this.newTeam = {
          name: '',
          description: '',
          isActive: true,
          autoAssignmentEnabled: true,
          manualAssignmentEnabled: true,
          memberUserIds: [],
        };
      },
      error: error => {
        this.saving.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to create team.');
      },
    });
  }

  toggleNewMember(userId: number, checked: boolean): void {
    const current = new Set(this.newTeam.memberUserIds);
    if (checked) {
      current.add(userId);
    } else {
      current.delete(userId);
    }

    this.newTeam = {
      ...this.newTeam,
      memberUserIds: Array.from(current),
    };
  }

  toggleDraftMember(teamId: number, userId: number, checked: boolean): void {
    this.teams.update(teams => teams.map(team => {
      if (team.routingTeamId !== teamId) {
        return team;
      }

      const next = new Set(team.memberUserIds);
      if (checked) {
        next.add(userId);
      } else {
        next.delete(userId);
      }

      return {
        ...team,
        memberUserIds: Array.from(next),
      };
    }));
  }

  saveTeam(team: RoutingTeamDraft): void {
    this.saving.set(true);
    this.errorMessage.set('');

    const payload: UpdateRoutingTeamRequest = {
      name: team.name.trim(),
      description: team.description?.trim() ?? '',
      isActive: team.isActive,
      autoAssignmentEnabled: team.autoAssignmentEnabled,
      manualAssignmentEnabled: team.manualAssignmentEnabled,
    };

    this.api.put<RoutingTeam>(`/routing/teams/${team.routingTeamId}`, payload).subscribe({
      next: updated => {
        const membersPayload: UpdateRoutingTeamMembersRequest = {
          memberUserIds: [...new Set(team.memberUserIds.map(x => Number(x)).filter(x => x > 0))],
        };

        this.api.put('/routing/teams/' + team.routingTeamId + '/members', membersPayload).subscribe({
          next: () => {
            this.saving.set(false);
            this.teams.update(items => items.map(item => item.routingTeamId === team.routingTeamId
              ? {
                ...updated,
                memberUserIds: membersPayload.memberUserIds,
              }
              : item));
            this.reload();
          },
          error: error => {
            this.saving.set(false);
            this.errorMessage.set(error?.error?.message ?? 'Failed to update team members.');
          },
        });
      },
      error: error => {
        this.saving.set(false);
        this.errorMessage.set(error?.error?.message ?? 'Failed to update team settings.');
      },
    });
  }

  deleteTeam(teamId: number): void {
    this.deleting.set(teamId);
    this.errorMessage.set('');

    this.api.delete<boolean>(`/routing/teams/${teamId}`).subscribe({
      next: () => {
        this.deleting.set(null);
        this.teams.update(items => items.filter(item => item.routingTeamId !== teamId));
      },
      error: error => {
        this.deleting.set(null);
        this.errorMessage.set(error?.error?.message ?? 'Failed to delete team.');
      },
    });
  }
}
