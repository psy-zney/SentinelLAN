using Microsoft.EntityFrameworkCore;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public static class SelfServiceModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<SelfServiceRequest>().HasIndex(x => new { x.OrganizationId, x.UserId, x.IdempotencyKey }).IsUnique();
        model.Entity<SelfServiceRequest>().HasIndex(x => new { x.OrganizationId, x.DeviceId, x.CreatedAt });
        model.Entity<SelfServiceRequest>().Property(x => x.RowVersion).IsRequired().IsConcurrencyToken().ValueGeneratedNever();
        model.Entity<SelfServiceRequest>().HasAlternateKey(x => new { x.OrganizationId, x.Id });
        model.Entity<SelfServiceMessage>().HasIndex(x => new { x.OrganizationId, x.RequestId, x.AuthorId, x.IdempotencyKey }).IsUnique().HasDatabaseName("IX_SSMessage_Dedupe");
        model.Entity<SelfServiceMessage>().HasOne<SelfServiceRequest>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RequestId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_SSMessage_Request");
        model.Entity<SelfServiceAttachment>().HasOne<SelfServiceRequest>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.RequestId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_SSAttachment_Request");
        model.Entity<SelfServiceAnnouncementReceipt>().HasIndex(x => new { x.OrganizationId, x.AnnouncementId, x.UserId }).IsUnique().HasDatabaseName("IX_SSReceipt_Unique");
        model.Entity<SelfServiceAnnouncement>().HasAlternateKey(x => new { x.OrganizationId, x.Id });
        model.Entity<SelfServiceAnnouncementReceipt>().HasOne<SelfServiceAnnouncement>().WithMany().HasForeignKey(x => new { x.OrganizationId, x.AnnouncementId }).HasPrincipalKey(x => new { x.OrganizationId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_SSReceipt_Announcement");
        model.Entity<SelfServiceNotification>().HasIndex(x => new { x.OrganizationId, x.UserId, x.DeduplicationKey }).IsUnique().HasDatabaseName("IX_SSNotification_Dedupe");
        model.Entity<SelfServicePushDevice>().HasIndex(x => x.Token).IsUnique();
        model.Entity<SelfServicePushDelivery>().HasIndex(x => new { x.NotificationId, x.PushDeviceId }).IsUnique();
        model.Entity<SelfServicePushDelivery>().HasIndex(x => new { x.NextAttemptAt, x.SentAt, x.AbandonedAt });
        model.Entity<SelfServiceRequest>().Property(x => x.IdempotencyKey).HasMaxLength(128);
        model.Entity<SelfServiceMessage>().Property(x => x.IdempotencyKey).HasMaxLength(128);
        model.Entity<SelfServicePushDevice>().Property(x => x.Token).HasMaxLength(200);
        model.Entity<SelfServiceNotification>().Property(x => x.DeduplicationKey).HasMaxLength(200);
    }
}
