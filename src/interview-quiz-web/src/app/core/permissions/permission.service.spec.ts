import { TestBed } from '@angular/core/testing';
import { PermissionCodes } from './permission-codes';
import { PermissionService } from './permission.service';

describe('PermissionService', () => {
  let service: PermissionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(PermissionService);
  });

  it('denies by default', () => {
    expect(service.hasPermission(PermissionCodes.OpeningsRead)).toBe(false);
    expect(service.hasAny([PermissionCodes.OpeningsRead, PermissionCodes.UsersManage])).toBe(false);
  });

  it('hasPermission returns true only for granted codes', () => {
    service.set([PermissionCodes.OpeningsRead, PermissionCodes.OpeningsWrite]);
    expect(service.hasPermission(PermissionCodes.OpeningsRead)).toBe(true);
    expect(service.hasPermission(PermissionCodes.OpeningsWrite)).toBe(true);
    expect(service.hasPermission(PermissionCodes.RolesManage)).toBe(false);
  });

  it('hasAny is true when any listed code is present', () => {
    service.set([PermissionCodes.UsersManage]);
    expect(service.hasAny([PermissionCodes.RolesManage, PermissionCodes.UsersManage])).toBe(true);
    expect(service.hasAny([PermissionCodes.RolesManage, PermissionCodes.OpeningsRead])).toBe(false);
  });

  it('clear removes all codes', () => {
    service.set([PermissionCodes.OpeningsRead]);
    service.clear();
    expect(service.hasPermission(PermissionCodes.OpeningsRead)).toBe(false);
    expect(service.permissions()).toEqual([]);
  });

  it('does not treat role names as permissions', () => {
    service.set([PermissionCodes.OpeningsRead]);
    expect(service.hasPermission('Admin')).toBe(false);
    expect(service.hasPermission('Dev Admin')).toBe(false);
  });
});
