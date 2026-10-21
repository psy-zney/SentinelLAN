using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed class DepartmentStore(SentinelDbContext db) : IDepartmentStore, IDepartmentPolicyReader
{
    public async Task<IReadOnlyList<Department>> GetDepartmentsAsync(Guid org, CancellationToken ct) => await db.Departments.Where(x => x.OrganizationId == org).ToListAsync(ct);
    public async Task<IReadOnlyList<DepartmentMemberDto>> GetUsersAsync(Guid org, CancellationToken ct) => await db.Users.AsNoTracking().Where(x => x.OrganizationId == org && x.Status == UserStatuses.Active).Select(x => new DepartmentMemberDto(x.Id, x.DisplayName, x.DepartmentId)).ToListAsync(ct);
    public async Task<IReadOnlyList<Device>> GetDevicesAsync(Guid org, CancellationToken ct) => await db.Devices.AsNoTracking().Where(x => x.OrganizationId == org && !x.IsRevoked).ToListAsync(ct);
    public Task<User?> FindUserAsync(Guid org, Guid id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct);
    public Task<Device?> FindDeviceAsync(Guid org, Guid id, CancellationToken ct) => db.Devices.SingleOrDefaultAsync(x => x.OrganizationId == org && x.Id == id, ct);
    public void Add(Department value) => db.Add(value);
    public void Add(DeviceCommand value) => db.Add(value);
    public async Task<Guid?> GetPolicyIdAsync(Guid org, Guid device, CancellationToken ct) => await (from d in db.Devices.AsNoTracking()
                                                                                                     join department in db.Departments.AsNoTracking() on d.DepartmentId equals department.Id
                                                                                                     where d.OrganizationId == org && department.OrganizationId == org && d.Id == device && !d.IsRevoked
                                                                                                     select department.PolicyId).SingleOrDefaultAsync(ct);
}
